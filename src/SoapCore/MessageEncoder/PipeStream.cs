// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;

namespace SoapCore.MessageEncoder
{
#pragma warning disable AvoidAsyncSuffix // Avoid Async suffix

	/// <summary>
	/// Wraps a <see cref="PipeReader"/> and/or <see cref="PipeWriter"/> as a <see cref="Stream"/> for
	/// easier interop with existing APIs.
	/// </summary>
	internal class PipeStream : Stream, IDisposableObservable
	{
		/// <summary>
		/// A value indicating whether the <see cref="UnderlyingPipeReader"/> should be completed when this instance is disposed.
		/// </summary>
		private readonly bool _ownsPipe;

		/// <summary>
		/// Indicates whether reading was completed.
		/// </summary>
		private bool _readingCompleted;

		/// <summary>
		/// Initializes a new instance of the <see cref="PipeStream"/> class.
		/// </summary>
		/// <param name="reader">The <see cref="PipeReader"/> to use when reading from this stream. May be null.</param>
		/// <param name="ownsPipe"><c>true</c> to complete the underlying reader and writer when the <see cref="Stream"/> is disposed; <c>false</c> to keep them open.</param>
		internal PipeStream(PipeReader reader, bool ownsPipe)
		{
			Requires.NotNull(reader, nameof(reader));
			UnderlyingPipeReader = reader;
			_ownsPipe = ownsPipe;
		}

		/// <inheritdoc />
		public bool IsDisposed { get; private set; }

		/// <inheritdoc />
		public override bool CanRead => !IsDisposed && UnderlyingPipeReader != null;

		/// <inheritdoc />
		public override bool CanSeek => false;

		/// <inheritdoc />
		public override bool CanWrite => false;

		/// <inheritdoc />
		public override long Length => throw ThrowDisposedOr(new NotSupportedException());

		/// <inheritdoc />
		public override long Position
		{
			get => throw ThrowDisposedOr(new NotSupportedException());
			set => throw ThrowDisposedOr(new NotSupportedException());
		}

		/// <summary>
		/// Gets the underlying <see cref="PipeReader"/> (for purposes of unwrapping instead of stacking adapters).
		/// </summary>
		internal PipeReader UnderlyingPipeReader { get; }

		/// <inheritdoc />
		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override long Seek(long offset, SeekOrigin origin) => throw ThrowDisposedOr(new NotSupportedException());

		/// <inheritdoc />
		public override void SetLength(long value) => throw ThrowDisposedOr(new NotSupportedException());

		/// <inheritdoc />
		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			Requires.NotNull(buffer, nameof(buffer));
			Requires.Range(offset + count <= buffer.Length, nameof(count));
			Requires.Range(offset >= 0, nameof(offset));
			Requires.Range(count > 0, nameof(count));
			Verify.NotDisposed(this);

			if (UnderlyingPipeReader == null)
			{
				throw new NotSupportedException();
			}

			if (_readingCompleted)
			{
				return 0;
			}

			ReadResult readResult = await UnderlyingPipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
			return ReadHelper(buffer.AsSpan(offset, count), readResult);
		}

#if SPAN_BUILTIN
        /// <inheritdoc />
		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Verify.NotDisposed(this);

            if (UnderlyingPipeReader == null)
            {
                throw new NotSupportedException();
            }

            if (_readingCompleted)
            {
                return 0;
            }

            ReadResult readResult = await UnderlyingPipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return ReadHelper(buffer.Span, readResult);
        }

		/// <inheritdoc />
		public override int Read(Span<byte> buffer)
        {
            Verify.NotDisposed(this);
            if (UnderlyingPipeReader == null)
            {
                throw new NotSupportedException();
            }

            if (_readingCompleted)
            {
	            return 0;
            }

            if (UnderlyingPipeReader.TryRead(out var readResult))
            {
	            return ReadHelper(buffer, readResult);
			}

            UnderlyingPipeReader.ReadAsync();

            return 0;
        }

        /// <inheritdoc />
		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
	        throw new NotSupportedException();
        }

        /// <inheritdoc />
		public override void Write(ReadOnlySpan<byte> buffer)
        {
	        throw new NotSupportedException();
        }

#endif

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

		/// <inheritdoc />
		public override void Flush()
		{
			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override int Read(byte[] buffer, int offset, int count)
		{
			Requires.NotNull(buffer, nameof(buffer));
			Requires.Range(offset + count <= buffer.Length, nameof(count));
			Requires.Range(offset >= 0, nameof(offset));
			Requires.Range(count > 0, nameof(count));
			Verify.NotDisposed(this);

			if (UnderlyingPipeReader == null)
			{
				throw new NotSupportedException();
			}

			if (_readingCompleted)
			{
				return 0;
			}

			return TryReadHelper(buffer.AsSpan(offset, count));
		}

		/// <inheritdoc />
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			IsDisposed = true;
			UnderlyingPipeReader?.CancelPendingRead();
			if (_ownsPipe)
			{
				UnderlyingPipeReader?.Complete();
			}

			base.Dispose(disposing);
		}

		private Exception ThrowDisposedOr(Exception ex)
		{
			Verify.NotDisposed(this);
			throw ex;
		}

		private int TryReadHelper(Span<byte> buffer)
		{
			if (UnderlyingPipeReader.TryRead(out var readResult))
			{
				var bytesRead = ReadHelper(buffer, readResult);
				UnderlyingPipeReader.ReadAsync();
				return bytesRead;
			}

			UnderlyingPipeReader.ReadAsync();

			Thread.Yield();

			if (UnderlyingPipeReader.TryRead(out readResult))
			{
				return ReadHelper(buffer, readResult);
			}

			return 0;
		}

		private int ReadHelper(Span<byte> buffer, ReadResult readResult)
		{
			if (readResult.IsCanceled && IsDisposed)
			{
				return 0;
			}

			long bytesToCopyCount = Math.Min(buffer.Length, readResult.Buffer.Length);
			ReadOnlySequence<byte> slice = readResult.Buffer.Slice(0, bytesToCopyCount);
			slice.CopyTo(buffer);
			UnderlyingPipeReader.AdvanceTo(slice.End);

			if (readResult.IsCompleted && slice.End.Equals(readResult.Buffer.End))
			{
				UnderlyingPipeReader.Complete();
				_readingCompleted = true;
			}

			return (int)bytesToCopyCount;
		}
	}
}
