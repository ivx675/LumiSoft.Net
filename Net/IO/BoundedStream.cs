using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LumiSoft.Net.IO
{
    /// <summary>
    /// Provides a high-performance, zero-copy, read-limited slice of a root stream 
    /// defined by precise absolute boundaries. Recursively unwraps parent wrappers 
    /// to maintain direct access to the original physical stream across arbitrary nesting depths.
    /// </summary>
    public sealed class BoundedStream : Stream
    {
        private readonly Stream _rootStream;
        private readonly long   _absoluteStartOffset;
        private readonly long   _length;
        private long            _relativePosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoundedStream"/> class wrapping a target stream segment.
        /// Automatically resolves and anchors to the root physical stream.
        /// </summary>
        /// <param name="stream">The underlying stream or parent <see cref="BoundedStream"/>.</param>
        /// <param name="startPosition">The starting byte offset relative to the immediate parent stream.</param>
        /// <param name="length">The exact length of the slice in bytes.</param>
        public BoundedStream(Stream stream, long startPosition, long length)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (startPosition < 0) throw new ArgumentOutOfRangeException(nameof(startPosition), "Start position must be non-negative.");
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

            _length = length;
            _relativePosition = 0;

            // Recursively unwrap parent wrappers to anchor directly to the original root stream
            if(stream is BoundedStream parentBounded){
                _rootStream = parentBounded._rootStream;
                _absoluteStartOffset = parentBounded._absoluteStartOffset + startPosition;
            }
            else{
                _rootStream = stream;
                _absoluteStartOffset = startPosition;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoundedStream"/> class starting at the stream's current position.
        /// </summary>
        public BoundedStream(Stream stream, long length) : this(stream, stream.Position, length)
        {
        }

        /// <summary>
        /// Gets the original root physical stream (e.g., FileStream) shared across all nested levels.
        /// </summary>
        internal Stream RootStream => _rootStream;

        /// <summary>
        /// Gets the absolute byte offset of this slice relative to the root physical stream.
        /// </summary>
        internal long AbsolutePosition => _absoluteStartOffset + _relativePosition;

        /// <summary>
        /// Gets the absolute starting offset where this slice begins in the root stream.
        /// </summary>
        internal long AbsoluteStartOffset => _absoluteStartOffset;

        /// <inheritdoc />
        public override bool CanRead => _rootStream.CanRead;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override bool CanSeek => _rootStream.CanSeek;

        /// <inheritdoc />
        public override long Length => _length;

        /// <inheritdoc />
        public override long Position
        {
            get => _relativePosition;
            set
            {
                if (value < 0 || value > _length)
                    throw new ArgumentOutOfRangeException(nameof(value), "Position is out of bounds for this stream slice.");
                _relativePosition = value;
            }
        }

        /// <inheritdoc />
        public override void Flush() => _rootStream.Flush();

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) => _rootStream.FlushAsync(cancellationToken);

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _relativePosition + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (target < 0 || target > _length)
                throw new IOException("Attempted to seek outside the bounds of the stream slice.");

            _relativePosition = target;
            return _relativePosition;
        }

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException("Setting length is not supported on a BoundedStream slice.");

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();

            long bytesRemaining = _length - _relativePosition;
            if (bytesRemaining <= 0) return 0;

            int maxToRead = (int)Math.Min(count, bytesRemaining);
            if (maxToRead == 0) return 0;

            lock(_rootStream){
                _rootStream.Position = _absoluteStartOffset + _relativePosition;
                int bytesRead = _rootStream.Read(buffer, offset, maxToRead);
                if(bytesRead > 0){
                    _relativePosition += bytesRead;
                }
                return bytesRead;
            }
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();

            long bytesRemaining = _length - _relativePosition;
            if (bytesRemaining <= 0) return 0;

            int maxToRead = (int)Math.Min(count, bytesRemaining);
            if (maxToRead == 0) return 0;

            lock(_rootStream){
                _rootStream.Position = _absoluteStartOffset + _relativePosition;
            }

            int bytesRead = await _rootStream.ReadAsync(buffer.AsMemory(offset, maxToRead), cancellationToken).ConfigureAwait(false);
            if(bytesRead > 0){
                Interlocked.Add(ref _relativePosition, bytesRead);
            }
            return bytesRead;
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            long bytesRemaining = _length - _relativePosition;
            if (bytesRemaining <= 0) return 0;

            int maxToRead = (int)Math.Min(buffer.Length, bytesRemaining);
            if (maxToRead == 0) return 0;

            lock(_rootStream){
                _rootStream.Position = _absoluteStartOffset + _relativePosition;
            }

            int bytesRead = await _rootStream.ReadAsync(buffer.Slice(0, maxToRead), cancellationToken).ConfigureAwait(false);
            if(bytesRead > 0){
                Interlocked.Add(ref _relativePosition, bytesRead);
            }
            return bytesRead;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc />
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken)) => throw new NotSupportedException();
    }
}