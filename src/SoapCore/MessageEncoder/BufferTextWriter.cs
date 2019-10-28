// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft;

namespace SoapCore.MessageEncoder
{
	/// <summary>
	/// A <see cref="TextWriter"/> that writes to a reassignable instance of <see cref="IBufferWriter{T}"/>.
	/// </summary>
	/// <remarks>
	/// Using this is much more memory efficient than a <see cref="StreamWriter"/> when writing to many different
	/// <see cref="IBufferWriter{T}"/> because the same writer, with all its buffers, can be reused.
	/// </remarks>
	public class BufferTextWriter : TextWriter
	{
		/// <summary>
		/// A buffer of written characters that have not yet been encoded.
		/// The <see cref="_charBufferPosition"/> field tracks how many characters are represented in this buffer.
		/// </summary>
		private readonly char[] _charBuffer = new char[512];

		/// <summary>
		/// The internal buffer writer to use for writing encoded characters.
		/// </summary>
		private IBufferWriter<byte> _bufferWriter;

		/// <summary>
		/// The last buffer received from <see cref="_bufferWriter"/>.
		/// </summary>
		private Memory<byte> _memory;

		/// <summary>
		/// The number of characters written to the <see cref="_memory"/> buffer.
		/// </summary>
		private int _memoryPosition;

		/// <summary>
		/// The number of characters written to the <see cref="_charBuffer"/>.
		/// </summary>
		private int _charBufferPosition;

		/// <summary>
		/// Whether the encoding preamble has been written since the last call to <see cref="Initialize(IBufferWriter{byte}, Encoding)"/>.
		/// </summary>
		private bool _preambleWritten;

		/// <summary>
		/// The encoding currently in use.
		/// </summary>
		private Encoding _encoding;

		/// <summary>
		/// The preamble for the current <see cref="_encoding"/>.
		/// </summary>
		/// <remarks>
		/// We store this as a field to avoid calling <see cref="Encoding.GetPreamble"/> repeatedly,
		/// since the typical implementation allocates a new array for each call.
		/// </remarks>
		private ReadOnlyMemory<byte> _encodingPreamble;

		/// <summary>
		/// An encoder obtained from the current <see cref="_encoding"/> used for incrementally encoding written characters.
		/// </summary>
		private Encoder _encoder;

		/// <summary>
		/// Initializes a new instance of the <see cref="BufferTextWriter"/> class.
		/// </summary>
		/// <param name="bufferWriter">The buffer writer to write to.</param>
		/// <param name="encoding">The encoding to use.</param>
		public BufferTextWriter(IBufferWriter<byte> bufferWriter, Encoding encoding)
		{
			Initialize(bufferWriter, encoding);
		}

		/// <inheritdoc />
		public override Encoding Encoding => _encoding;

		/// <summary>
		/// Gets the number of uninitialized characters remaining in <see cref="_charBuffer"/>.
		/// </summary>
		private int CharBufferSlack => _charBuffer.Length - _charBufferPosition;

		/// <summary>
		/// Prepares for writing to the specified buffer.
		/// </summary>
		/// <param name="bufferWriter">The buffer writer to write to.</param>
		/// <param name="encoding">The encoding to use.</param>
		public void Initialize(IBufferWriter<byte> bufferWriter, Encoding encoding)
		{
			Requires.NotNull(bufferWriter, nameof(bufferWriter));
			Requires.NotNull(encoding, nameof(encoding));

			Verify.Operation(_memoryPosition == 0 && _charBufferPosition == 0, "This instance must be flushed before being reinitialized.");

			_preambleWritten = false;
			_bufferWriter = bufferWriter;
			if (encoding != _encoding)
			{
				_encoding = encoding;
				_encoder = _encoding.GetEncoder();
				_encodingPreamble = _encoding.GetPreamble();
			}
			else
			{
				// encoder != null because if it were, encoding == null too, so we would have been in the first branch above.
				_encoder.Reset();
			}
		}

		/// <summary>
		/// Clears references to the <see cref="IBufferWriter{T}"/> set by a prior call to <see cref="Initialize(IBufferWriter{byte}, Encoding)"/>.
		/// </summary>
		public void Reset()
		{
			_bufferWriter = null;
		}

		/// <inheritdoc />
		public override void Flush()
		{
			ThrowIfNotInitialized();
			EncodeCharacters(flushEncoder: true);
			CommitBytes();
		}

		/// <inheritdoc />
		public override Task FlushAsync()
		{
			try
			{
				Flush();
				return Task.CompletedTask;
			}
			catch (Exception ex)
			{
				return Task.FromException(ex);
			}
		}

		/// <inheritdoc />
		public override void Write(char value)
		{
			ThrowIfNotInitialized();
			_charBuffer[_charBufferPosition++] = value;
			EncodeCharactersIfBufferFull();
		}

		/// <inheritdoc />
		public override void Write(string value)
		{
			if (value == null)
			{
				return;
			}

			Write(value.AsSpan());
		}

		/// <inheritdoc />
		public override void Write(char[] buffer, int index, int count) => Write(Requires.NotNull(buffer, nameof(buffer)).AsSpan(index, count));

#if SPAN_BUILTIN
		/// <inheritdoc />
		public override void Write(ReadOnlySpan<char> buffer)
#else
        /// <summary>
        /// Copies a given span of characters into the writer.
        /// </summary>
        /// <param name="buffer">The characters to write.</param>
		public virtual void Write(ReadOnlySpan<char> buffer)
#endif
		{
			ThrowIfNotInitialized();

			// Try for fast path
			if (buffer.Length <= CharBufferSlack)
			{
				buffer.CopyTo(_charBuffer.AsSpan(_charBufferPosition));
				_charBufferPosition += buffer.Length;
				EncodeCharactersIfBufferFull();
			}
			else
			{
				int charsCopied = 0;
				while (charsCopied < buffer.Length)
				{
					int charsToCopy = Math.Min(buffer.Length - charsCopied, CharBufferSlack);
					buffer.Slice(charsCopied, charsToCopy).CopyTo(_charBuffer.AsSpan(_charBufferPosition));
					charsCopied += charsToCopy;
					_charBufferPosition += charsToCopy;
					EncodeCharactersIfBufferFull();
				}
			}
		}

#if SPAN_BUILTIN
		/// <inheritdoc />
		public override void WriteLine(ReadOnlySpan<char> buffer)
#else
        /// <summary>
        /// Writes a span of characters followed by a <see cref="TextWriter.NewLine"/>.
        /// </summary>
        /// <param name="buffer">The characters to write.</param>
		public virtual void WriteLine(ReadOnlySpan<char> buffer)
#endif
		{
			Write(buffer);
			WriteLine();
		}

		/// <summary>
		/// Encodes the written characters if the character buffer is full.
		/// </summary>
		private void EncodeCharactersIfBufferFull()
		{
			if (_charBufferPosition == _charBuffer.Length)
			{
				EncodeCharacters(flushEncoder: false);
			}
		}

		/// <summary>
		/// Encodes characters written so far to a buffer provided by the underyling <see cref="_bufferWriter"/>.
		/// </summary>
		/// <param name="flushEncoder"><c>true</c> to flush the characters in the encoder; useful when finalizing the output.</param>
		private void EncodeCharacters(bool flushEncoder)
		{
			if (_charBufferPosition > 0)
			{
				int maxBytesLength = Encoding.GetMaxByteCount(_charBufferPosition);
				if (!_preambleWritten)
				{
					maxBytesLength += _encodingPreamble.Length;
				}

				if (_memory.Length - _memoryPosition < maxBytesLength)
				{
					CommitBytes();
					_memory = _bufferWriter.GetMemory(maxBytesLength);
				}

				if (!_preambleWritten)
				{
					_encodingPreamble.Span.CopyTo(_memory.Span.Slice(_memoryPosition));
					_memoryPosition += _encodingPreamble.Length;
					_preambleWritten = true;
				}

				if (MemoryMarshal.TryGetArray(_memory, out ArraySegment<byte> segment))
				{
					_memoryPosition += _encoder.GetBytes(_charBuffer, 0, _charBufferPosition, segment.Array, segment.Offset + _memoryPosition, flush: flushEncoder);
				}
				else
				{
					byte[] rentedByteBuffer = ArrayPool<byte>.Shared.Rent(maxBytesLength);
					try
					{
						int bytesWritten = _encoder.GetBytes(_charBuffer, 0, _charBufferPosition, rentedByteBuffer, 0, flush: flushEncoder);
						rentedByteBuffer.CopyTo(_memory.Span.Slice(_memoryPosition));
						_memoryPosition += bytesWritten;
					}
					finally
					{
						ArrayPool<byte>.Shared.Return(rentedByteBuffer);
					}
				}

				_charBufferPosition = 0;

				if (_memoryPosition == _memory.Length)
				{
					Flush();
				}
			}
		}

		/// <summary>
		/// Commits any written bytes to the underlying <see cref="_bufferWriter"/>.
		/// </summary>
		private void CommitBytes()
		{
			if (_memoryPosition > 0)
			{
				_bufferWriter.Advance(_memoryPosition);
				_memoryPosition = 0;
				_memory = default;
			}
		}

		private void ThrowIfNotInitialized()
		{
			if (_bufferWriter == null)
			{
				throw new InvalidOperationException("Call " + nameof(Initialize) + " first.");
			}
		}
	}
}
