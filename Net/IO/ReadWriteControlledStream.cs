using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LumiSoft.Net.IO
{
    /// <summary>
    /// Represents a decorator stream that enforces read, write, or read-write access permissions 
    /// on an underlying stream.
    /// </summary>
    public class ReadWriteControlledStream : Stream
    {
        private readonly Stream _stream;
        private readonly FileAccess _accessMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadWriteControlledStream"/> class 
        /// wrapping the specified stream with defined access restrictions.
        /// </summary>
        /// <param name="stream">The underlying source or destination stream to wrap.</param>
        /// <param name="access">The access mode (<see cref="FileAccess.Read"/>, <see cref="FileAccess.Write"/>, or <see cref="FileAccess.ReadWrite"/>) permitted on this stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <c>null</c>.</exception>
        public ReadWriteControlledStream(Stream stream,FileAccess access)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _accessMode = access;
        }

        #region Flushing & Seeking

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush() => _stream.Flush();

        /// <summary>
        /// Asynchronously clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous flush operation.</returns>
        public override Task FlushAsync(CancellationToken cancellationToken) => _stream.FlushAsync(cancellationToken);

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
        /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream calculated by combining the initial reference point and offset.</returns>
        /// <exception cref="NotSupportedException">Thrown if the stream does not support seeking.</exception>
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream does not support both writing and seeking.</exception>
        public override void SetLength(long value) => _stream.SetLength(value);

        #endregion

        #region Reading

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> or <paramref name="count"/> is invalid.</exception>
        /// <exception cref="NotSupportedException">Thrown when reading is not supported by the access mode or underlying stream.</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!CanRead) throw new NotSupportedException("Reading is not permitted by the access mode.");

            return _stream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous read operation. The value of the task result contains the total number of bytes read into the buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> or <paramref name="count"/> is invalid.</exception>
        /// <exception cref="NotSupportedException">Thrown when reading is not supported.</exception>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!CanRead) throw new NotSupportedException("Reading is not permitted by the access mode.");

            return await _stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream into a memory buffer and advances the position within the stream.
        /// </summary>
        /// <param name="buffer">The memory region to store the read bytes.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A ValueTask representing the asynchronous read operation, containing the number of bytes read.</returns>
        /// <exception cref="NotSupportedException">Thrown when reading is not supported.</exception>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!CanRead) throw new NotSupportedException("Reading is not permitted by the access mode.");

            return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Writing

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> or <paramref name="count"/> is invalid.</exception>
        /// <exception cref="NotSupportedException">Thrown when writing is not supported by the access mode.</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!CanWrite) throw new NotSupportedException("Writing is not permitted by the access mode.");

            _stream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Asynchronously writes a sequence of bytes to the current stream and advances the current position within this stream.
        /// </summary>
        /// <param name="buffer">The byte array containing data to write.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes.</param>
        /// <param name="count">The number of bytes to be written.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> or <paramref name="count"/> is invalid.</exception>
        /// <exception cref="NotSupportedException">Thrown when writing is not supported.</exception>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!CanWrite) throw new NotSupportedException("Writing is not permitted by the access mode.");

            await _stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously writes a memory region of bytes to the current stream and advances the current position.
        /// </summary>
        /// <param name="buffer">The read-only memory region containing bytes to write.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A ValueTask representing the asynchronous write operation.</returns>
        /// <exception cref="NotSupportedException">Thrown when writing is not supported.</exception>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!CanWrite) throw new NotSupportedException("Writing is not permitted by the access mode.");

            await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead => (_accessMode & FileAccess.Read) != 0;

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek => _stream.CanSeek;

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite => (_accessMode & FileAccess.Write) != 0;

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if the underlying stream does not support seeking or length queries.</exception>
        public override long Length => _stream.Length;

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if the underlying stream does not support seeking.</exception>
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        #endregion
    }
}