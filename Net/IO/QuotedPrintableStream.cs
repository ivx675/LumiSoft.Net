using System;
using System.IO;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace LumiSoft.Net.IO
{
    /// <summary>
    /// Implements RFC 2045 Section 6.7 Quoted-Printable stream encoding and decoding.
    /// </summary>
    public class QuotedPrintableStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly FileAccess _accessMode;
        
        // Internal buffer for raw encoded stream chunks (larger to accommodate trailing shifts)
        private readonly byte[] _rawReadBuffer = new byte[8192];
        private int _bytesInReadBuffer = 0;

        // Decoding state buffers
        private readonly byte[] _decodedBuffer = new byte[32000];
        private int _decodedOffset = 0;
        private int _decodedCount = 0;
        
        // Encoding state buffers and flags
        private readonly byte[] _encodedBuffer = new byte[78];
        private int _encodedCount = 0;
        private byte _lastByte = 0;
        private bool _isFinalized = false;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuotedPrintableStream"/> class.
        /// </summary>
        /// <param name="stream">The underlying source or destination stream.</param>
        /// <param name="access">Specifies the stream access mode (Read, Write, or ReadWrite).</param>
        /// <exception cref="ArgumentNullException">Raised when <paramref name="stream"/> is null.</exception>
        public QuotedPrintableStream(Stream stream,FileAccess access)
        {
            _innerStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _accessMode = access;
        }

        #region override method Dispose

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if(disposing && !_isFinalized){
                try{
                    Finish();
                }
                catch{
                    // Suppress or handle exceptions during disposal if necessary
                }
            }
            _disposed = true;
    
            base.Dispose(disposing);
        }

        #endregion

        #region override method DisposeAsync

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            if(!_isFinalized){
                try{
                    await FinishAsync().ConfigureAwait(false);
                }
                catch{
                    // Suppress or handle exceptions during disposal
                }
            }

            GC.SuppressFinalize(this);
        }

        #endregion


        #region method Flush

        /// <summary>
        /// Flushes the stream. Ignored as per stream lifecycle design; all final writing occurs in <see cref="FinishAsync"/>.
        /// </summary>
        public override void Flush()
        {
            // No implementation; ignored without raising errors.
        }

        #endregion

        #region method FlushAsync

        /// <summary>
        /// Asynchronously flushes the stream. Ignored as per stream lifecycle design; all final writing occurs in <see cref="FinishAsync"/>.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            // No implementation; ignored without raising errors.
            return Task.CompletedTask;
        }

        #endregion

        #region method Finish

        /// <summary>
        /// Synchronously signals the end of the input stream, processes remaining tail state, and flushes data.
        /// </summary>
        public void Finish()
        {
            FinishAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method FinishAsync

        /// <summary>
        /// Signals the end of the input stream, processes any remaining tail state 
        /// (such as a dangling trailing carriage return), flushes all data to the underlying stream, and locks further writes.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public async Task FinishAsync(CancellationToken cancellationToken = default)
        {
            if (_isFinalized) return;
            if (!CanWrite) return;

            // If the very last byte of the stream was a standalone '\r', flush it now as a hard line break.
            if(_lastByte == (byte)'\r'){
                await WriteHardLineBreakAsync(cancellationToken).ConfigureAwait(false);
                _lastByte = 0;
            }

            // Flush any remaining encoded bytes to the underlying stream
            if(_encodedCount > 0){
                await _innerStream.WriteAsync(_encodedBuffer.AsMemory(0, _encodedCount), cancellationToken).ConfigureAwait(false);
                _encodedCount = 0;
            }

            await _innerStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _isFinalized = true;
        }

        #endregion
        
        #region method Read

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!CanRead) throw new NotSupportedException();

            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method ReadAsync

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!CanRead) throw new NotSupportedException();

            return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanRead) throw new NotSupportedException();

            // 1. Drain any already-decoded bytes sitting in our output buffer first
            if(_decodedOffset < _decodedCount){
                int copyCount = Math.Min(buffer.Length, _decodedCount - _decodedOffset);
                _decodedBuffer.AsMemory(_decodedOffset, copyCount).CopyTo(buffer);
                _decodedOffset += copyCount;
                return copyCount;
            }

            _decodedOffset = 0;
            _decodedCount = 0;

            // 2. Fetch more raw data from the underlying stream if our raw buffer is running low
            // We leave space at the beginning of the buffer for any shifted incomplete bytes.
            int bytesRead = await _innerStream.ReadAsync(_rawReadBuffer.AsMemory(_bytesInReadBuffer, _rawReadBuffer.Length - _bytesInReadBuffer), cancellationToken).ConfigureAwait(false);
            _bytesInReadBuffer += bytesRead;

            if(_bytesInReadBuffer == 0){
                return 0; // End of stream
            }

            // 3. Decode as much as possible from _rawReadBuffer
            int srcIndex = 0;
            while(srcIndex < _bytesInReadBuffer && _decodedCount < _decodedBuffer.Length){
                byte b = _rawReadBuffer[srcIndex];

                // Check if we have an escape character '='
                if(b == (byte)'='){
                    // Check how many raw bytes remain in the buffer starting from this '='
                    int remaining = _bytesInReadBuffer - srcIndex;

                    // Case A: Soft line break (=CRLF or =LF) or incomplete escape at the very end of the buffer
                    if(remaining < 2){
                        // Not enough bytes to know what follows; stop decoding here and shift remainder
                        break;
                    }

                    byte next1 = _rawReadBuffer[srcIndex + 1];

                    // Soft line break check (= \r \n, = \n, or = \r)
                    if(next1 == (byte)'\r' || next1 == (byte)'\n'){
                        if(next1 == (byte)'\r'){
                            // Check if the \n is also present
                            if(remaining < 3){
                                // We have '=\r' but need the final '\n' to complete the soft break
                                break;
                            }
                            if(_rawReadBuffer[srcIndex + 2] == (byte)'\n'){
                                srcIndex += 3; // Consume '=\r\n'
                            }
                            else{
                                srcIndex += 2; // Consume '=\r'
                            }
                        }
                        else{
                            srcIndex += 2; // Consume '=\n'
                        }
                        continue;
                    }

                    // Case B: Hex encoded character (=XX)
                    if(remaining < 3){
                        // We have '=' and one hex digit, but missing the second digit; stop and shift
                        break;
                    }

                    byte hex1 = next1;
                    byte hex2 = _rawReadBuffer[srcIndex + 2];
                    byte decodedByte = (byte)((HexValue(hex1) << 4) | HexValue(hex2));

                    _decodedBuffer[_decodedCount++] = decodedByte;
                    srcIndex += 3; // Consume '=XX'
                    continue;
                }

                // Standard literal byte
                _decodedBuffer[_decodedCount++] = b;
                srcIndex++;
            }

            // 4. Shift any unprocessed/incomplete trailing bytes to the beginning of _rawReadBuffer
            int leftoverCount = _bytesInReadBuffer - srcIndex;
            if(leftoverCount > 0 && srcIndex > 0){
                Buffer.BlockCopy(_rawReadBuffer, srcIndex, _rawReadBuffer, 0, leftoverCount);
            }
            _bytesInReadBuffer = leftoverCount;

            // 5. Copy newly decoded data to the caller's target buffer
            int finalCopyCount = Math.Min(buffer.Length, _decodedCount);
            if(finalCopyCount > 0){
                _decodedBuffer.AsMemory(0, finalCopyCount).CopyTo(buffer);
                _decodedOffset = finalCopyCount;
            }

            return finalCopyCount;
        }

        #endregion

        #region method Write

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (!CanWrite) throw new NotSupportedException();

            WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method WriteAsync

        /// <inheritdoc />
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (!CanWrite) throw new NotSupportedException();

            await WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanWrite) throw new NotSupportedException();
            if (_isFinalized) throw new InvalidOperationException("Cannot write to a stream after FinishAsync() has been called.");

            int length = buffer.Length;
            int i = 0;

            // Handle boundary condition: previous chunk ended with '\r'
            if(_lastByte == (byte)'\r'){
                if(length > 0 && buffer.Span[0] == (byte)'\n'){
                    await WriteHardLineBreakAsync(cancellationToken).ConfigureAwait(false);
                    i = 1; // Consume the '\n' from this buffer
                }
                else{
                    await WriteHardLineBreakAsync(cancellationToken).ConfigureAwait(false);
                }
                _lastByte = 0;
            }

            for(; i < length; i++){
                byte b = buffer.Span[i];

                // Handle Carriage Return (\r)
                if(b == (byte)'\r'){
                    if(i + 1 < length){
                        if(buffer.Span[i + 1] == (byte)'\n'){
                            i++; // Consume matching '\n'
                        }
                        await WriteHardLineBreakAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else{
                        // \r is the very last byte of this chunk; defer handling to _lastByte
                        _lastByte = (byte)'\r';
                    }
                    continue;
                }

                // Handle standalone Line Feed (\n) - normalize to hard line break
                if(b == (byte)'\n'){
                    await WriteHardLineBreakAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Printable US-ASCII (33-60, 62-126) and safe whitespace (space, tab)
                bool isSafeChar = (b >= 33 && b <= 60) || (b >= 62 && b <= 126) || b == (byte)' ' || b == (byte)'\t';

                if(isSafeChar){
                    if(_encodedCount >= 75){
                        await FlushSoftBreakAsync(cancellationToken).ConfigureAwait(false);
                    }
                    _encodedBuffer[_encodedCount++] = b;
                }
                else{
                    if(_encodedCount >= 73){
                        await FlushSoftBreakAsync(cancellationToken).ConfigureAwait(false);
                    }

                    _encodedBuffer[_encodedCount++] = (byte)'=';
                    _encodedBuffer[_encodedCount++] = GetHexHigh(b);
                    _encodedBuffer[_encodedCount++] = GetHexLow(b);
                }
            }

            if(length > 0 && _lastByte != (byte)'\r'){
                _lastByte = 0;
            }
        }

        private async Task InternalFlushBufferAsync(CancellationToken cancellationToken)
        {
            await _innerStream.WriteAsync(_encodedBuffer.AsMemory(0, _encodedCount), cancellationToken).ConfigureAwait(false);
            _encodedCount = 0;
        }

        private async Task FlushSoftBreakAsync(CancellationToken cancellationToken)
        {
            _encodedBuffer[_encodedCount++] = (byte)'=';
            _encodedBuffer[_encodedCount++] = (byte)'\r';
            _encodedBuffer[_encodedCount++] = (byte)'\n';
            await InternalFlushBufferAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteHardLineBreakAsync(CancellationToken cancellationToken)
        {
            await EncodeTrailingWhitespaceAsync(cancellationToken);

            if(_encodedCount + 2 > 78){
                await InternalFlushBufferAsync(cancellationToken).ConfigureAwait(false);
            }
            _encodedBuffer[_encodedCount++] = (byte)'\r';
            _encodedBuffer[_encodedCount++] = (byte)'\n';
            await InternalFlushBufferAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task EncodeTrailingWhitespaceAsync(CancellationToken cancellationToken)
        {
            int wsCount = 0;
            while(_encodedCount - wsCount > 0){
                byte b = _encodedBuffer[_encodedCount - 1 - wsCount];
                if(b == (byte)' ' || b == (byte)'\t'){
                    wsCount++;
                }
                else{
                    break;
                }
            }

            if(wsCount > 0){
                byte[] wsBytes = new byte[wsCount];
                Array.Copy(_encodedBuffer, _encodedCount - wsCount, wsBytes, 0, wsCount);
                _encodedCount -= wsCount;

                foreach(byte b in wsBytes){
                    if(_encodedCount + 3 > 78){
                        await InternalFlushBufferAsync(cancellationToken).ConfigureAwait(false);
                    }

                    _encodedBuffer[_encodedCount++] = (byte)'=';
                    _encodedBuffer[_encodedCount++] = GetHexHigh(b);
                    _encodedBuffer[_encodedCount++] = GetHexLow(b);
                }
            }
        }

        #endregion

        #region Helpers & Unsupported Operations

        private static int HexValue(byte hex)
        {
            if (hex >= (byte)'0' && hex <= (byte)'9') return hex - (byte)'0';
            if (hex >= (byte)'A' && hex <= (byte)'F') return hex - (byte)'A' + 10;
            if (hex >= (byte)'a' && hex <= (byte)'f') return hex - (byte)'a' + 10;
            return 0;
        }

        private static byte GetHexHigh(byte b) => (byte)"0123456789ABCDEF"[b >> 4];
        private static byte GetHexLow(byte b) => (byte)"0123456789ABCDEF"[b & 0x0F];

        /// <summary>
        /// Sets the position within the current stream (Not supported).
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// Sets the length of the current stream (Not supported).
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException();

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead => (_accessMode & FileAccess.Read) != 0;

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite => (_accessMode & FileAccess.Write) != 0;

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking (false).
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Gets the length in bytes of the stream (not supported).
        /// </summary>
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Gets or sets the position within the current stream (not supported).
        /// </summary>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        #endregion
    }
}