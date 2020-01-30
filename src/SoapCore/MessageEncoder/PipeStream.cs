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
	/// <summary>
	/// Wraps a <see cref="PipeReader"/> and/or <see cref="PipeWriter"/> as a <see cref="Stream"/> for
	/// easier interop with existing APIs.
	/// </summary>
	internal partial class PipeStream : Stream, IDisposableObservable
	{
		/// <summary>
		/// The <see cref="PipeWriter"/> to use when writing to this stream. May be null.
		/// </summary>
		private readonly PipeWriter? _writer;

		/// <summary>
		/// The <see cref="PipeReader"/> to use when reading from this stream. May be null.
		/// </summary>
		private readonly PipeReader? _reader;

		/// <summary>
		/// A value indicating whether the <see cref="_writer"/> and <see cref="_reader"/> should be completed when this instance is disposed.
		/// </summary>
		private readonly bool _ownsPipe;

		/// <summary>
		/// Indicates whether reading was completed.
		/// </summary>
		private bool _readingCompleted;

		/// <summary>
		/// Initializes a new instance of the <see cref="PipeStream"/> class.
		/// </summary>
		/// <param name="writer">The <see cref="PipeWriter"/> to use when writing to this stream. May be null.</param>
		/// <param name="ownsPipe"><c>true</c> to complete the underlying reader and writer when the <see cref="Stream"/> is disposed; <c>false</c> to keep them open.</param>
		internal PipeStream(PipeWriter writer, bool ownsPipe)
		{
			Requires.NotNull(writer, nameof(writer));
			_writer = writer;
			_ownsPipe = ownsPipe;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PipeStream"/> class.
		/// </summary>
		/// <param name="reader">The <see cref="PipeReader"/> to use when reading from this stream. May be null.</param>
		/// <param name="ownsPipe"><c>true</c> to complete the underlying reader and writer when the <see cref="Stream"/> is disposed; <c>false</c> to keep them open.</param>
		internal PipeStream(PipeReader reader, bool ownsPipe)
		{
			Requires.NotNull(reader, nameof(reader));
			_reader = reader;
			_ownsPipe = ownsPipe;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PipeStream"/> class.
		/// </summary>
		/// <param name="pipe">A full duplex pipe that will serve as the transport for this stream.</param>
		/// <param name="ownsPipe"><c>true</c> to complete the underlying reader and writer when the <see cref="Stream"/> is disposed; <c>false</c> to keep them open.</param>
		internal PipeStream(IDuplexPipe pipe, bool ownsPipe)
		{
			Requires.NotNull(pipe, nameof(pipe));

			_writer = pipe.Output;
			_reader = pipe.Input;
			_ownsPipe = ownsPipe;
		}

		/// <inheritdoc />
		public bool IsDisposed { get; private set; }

		/// <inheritdoc />
		public override bool CanRead => !IsDisposed && _reader != null;

		/// <inheritdoc />
		public override bool CanSeek => false;

		/// <inheritdoc />
		public override bool CanWrite => !IsDisposed && _writer != null;

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
		internal PipeReader? UnderlyingPipeReader => _reader;

		/// <summary>
		/// Gets the underlying <see cref="PipeWriter"/> (for purposes of unwrapping instead of stacking adapters).
		/// </summary>
		internal PipeWriter? UnderlyingPipeWriter => _writer;

		/// <inheritdoc />
		public override async Task FlushAsync(CancellationToken cancellationToken)
		{
			if (_writer == null)
			{
				throw new NotSupportedException();
			}

			await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public override long Seek(long offset, SeekOrigin origin) => throw ThrowDisposedOr(new NotSupportedException());

		/// <inheritdoc />
		public override void SetLength(long value) => throw ThrowDisposedOr(new NotSupportedException());

		/// <inheritdoc />
		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			Requires.NotNull(buffer, nameof(buffer));
			Requires.Range(offset + count <= buffer.Length, nameof(count));
			Requires.Range(offset >= 0, nameof(offset));
			Requires.Range(count > 0, nameof(count));
			Verify.NotDisposed(this);

			if (_reader == null)
			{
				throw new NotSupportedException();
			}

			if (_readingCompleted)
			{
				return 0;
			}

			ReadResult readResult = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
			return ReadHelper(buffer.AsSpan(offset, count), readResult);
		}

#if SPAN_BUILTIN
		/// <inheritdoc />
		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Verify.NotDisposed(this);

			if (_reader == null)
			{
				throw new NotSupportedException();
			}

			if (_readingCompleted)
			{
				return 0;
			}

			ReadResult readResult = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
			return ReadHelper(buffer.Span, readResult);
		}

		/// <inheritdoc />
		public override int Read(Span<byte> buffer)
		{
			Verify.NotDisposed(this);
			if (_reader == null)
			{
				throw new NotSupportedException();
			}

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
			ReadResult readResult = _reader.ReadAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
			return ReadHelper(buffer, readResult);
		}

		/// <inheritdoc />
		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Verify.NotDisposed(this);
			if (_writer == null)
			{
				throw new NotSupportedException();
			}

			_writer.Write(buffer.Span);
			return default;
		}

		/// <inheritdoc />
		public override void Write(ReadOnlySpan<byte> buffer)
		{
			Verify.NotDisposed(this);
			if (_writer == null)
			{
				throw new NotSupportedException();
			}

			_writer.Write(buffer);
		}

#endif

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits

		/// <inheritdoc />
		public override void Flush()
		{
			if (_writer == null)
			{
				throw new NotSupportedException();
			}

			_writer.FlushAsync().GetAwaiter().GetResult();
		}

		/// <inheritdoc />
		public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

		/// <inheritdoc />
		public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			IsDisposed = true;
			_reader?.CancelPendingRead();
			_writer?.CancelPendingFlush();
			if (_ownsPipe)
			{
				_reader?.Complete();
				_writer?.Complete();
			}

			base.Dispose(disposing);
		}

		private T ReturnIfNotDisposed<T>(T value)
		{
			Verify.NotDisposed(this);
			return value;
		}

		private Exception ThrowDisposedOr(Exception ex)
		{
			Verify.NotDisposed(this);
			throw ex;
		}

		private int ReadHelper(Span<byte> buffer, ReadResult readResult)
		{
			if (readResult.IsCanceled && IsDisposed)
			{
				return 0;
			}

			long bytesToCopyCount = Math.Min(buffer.Length, readResult.Buffer.Length);
			ReadOnlySequence<byte> slice = readResult.Buffer.Slice(0, bytesToCopyCount);
			var isCompleted = readResult.IsCompleted && slice.End.Equals(readResult.Buffer.End);
			slice.CopyTo(buffer);
			_reader!.AdvanceTo(slice.End);
			readResult.ScrubAfterAdvanceTo();
			slice = default;

			if (isCompleted)
			{
				if (_ownsPipe)
				{
					_reader.Complete();
				}
				_readingCompleted = true;
			}

			return (int)bytesToCopyCount;
		}
	}
}
