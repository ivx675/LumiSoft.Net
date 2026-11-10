using System;
using System.Buffers;
using System.Buffers.Text;

namespace LumiSoft.Net.IO
{
    /// <summary>
    /// Represents Base64 stream.
    /// </summary>
    public sealed class Base64Stream : Stream
    {
        private readonly Stream _stream;
        private readonly bool _isOwner;
        private readonly bool _addLineBreaks;
        private readonly FileAccess _accessMode;

        private readonly byte[] _readBuffer;
        private readonly byte[] _filteredBuffer;
        private int _rolloverLen;
        private bool _isEof;

        // Write-side state management
        private readonly byte[] _writeRollover = new byte[2];
        private int _writeRolloverLen;
        private bool _isFinalized;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Base64Stream"/> class with default read/write access and buffer sizing.
        /// </summary>
        public Base64Stream(Stream stream, bool owner, bool addLineBreaks) 
            : this(stream, owner, addLineBreaks, FileAccess.ReadWrite, 128000)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Base64Stream"/> class with custom file access and buffer sizing options.
        /// </summary>
        public Base64Stream(Stream stream, bool owner, bool addLineBreaks, FileAccess access, int bufferSize = 128000)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _isOwner = owner;
            _addLineBreaks = addLineBreaks;
            _accessMode = access;

            // Rent memory buffers from shared array pool to prevent heap allocations
            _readBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            _filteredBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
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
    
            if(!_isOwner){
                _stream.Flush();
            }
            else{
                _stream.Dispose();
            }
    
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

            if(_stream != null){
                if(_isOwner){
                    await _stream.DisposeAsync().ConfigureAwait(false);
                }
            }

            GC.SuppressFinalize(this);
        }

        #endregion


        #region override method Flush

        /// <inheritdoc />
        public override void Flush()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);

            _stream.Flush();
        }

        #endregion

        #region override method FlushAsync

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);

            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region override method Read

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);

            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        #endregion

        #region override method ReadAsync

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> targetMemory, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanRead) throw new NotSupportedException("Reading is not supported.");
            if (targetMemory.Length == 0) return 0;

            // Iterative loop prevents recursive stack growth when chunks consist solely of line breaks or whitespace fragments
            while(true){
                // If we've hit EOF and have no rollover data left, reading is complete
                if(_isEof && _rolloverLen == 0){
                    return 0;
                }

                int bytesRead = 0;
                if(!_isEof){
                    // Read next block from underlying stream into _readBuffer right after any existing rollover bytes
                    bytesRead = await _stream.ReadAsync(_readBuffer.AsMemory(_rolloverLen, _readBuffer.Length - _rolloverLen), cancellationToken).ConfigureAwait(false);
                    if(bytesRead == 0){
                        _isEof = true;
                    }
                }

                int totalLen = _rolloverLen + bytesRead;
                if(totalLen == 0){
                    return 0;
                }

                // Copy previous rollover prefix into the target filter workspace
                if(_rolloverLen > 0){
                    _readBuffer.AsSpan(0, _rolloverLen).CopyTo(_filteredBuffer);
                }

                int filteredCount = _rolloverLen;
                ReadOnlySpan<byte> rawSpan = _readBuffer.AsSpan(_rolloverLen, bytesRead);
                int sourceIdx = 0;

                // JIT-optimized vectorized scan loop stripping newlines ('\r' and '\n') directly into _filteredBuffer
                while((uint)sourceIdx < (uint)rawSpan.Length){
                    ReadOnlySpan<byte> remaining = rawSpan.Slice(sourceIdx);
                    int lineBreakIdx = remaining.IndexOfAny((byte)'\r', (byte)'\n');

                    if(lineBreakIdx < 0){
                        remaining.CopyTo(_filteredBuffer.AsSpan(filteredCount));
                        filteredCount += remaining.Length;
                        break;
                    }

                    if(lineBreakIdx > 0){
                        remaining.Slice(0, lineBreakIdx).CopyTo(_filteredBuffer.AsSpan(filteredCount));
                        filteredCount += lineBreakIdx;
                    }

                    byte hit = remaining[lineBreakIdx];
                    sourceIdx += lineBreakIdx + 1;

                    // Handle CRLF pairs atomically
                    if(hit == (byte)'\r' && (uint)sourceIdx < (uint)rawSpan.Length && rawSpan[sourceIdx] == (byte)'\n'){
                        sourceIdx++;
                    }
                }

                // Append standard Base64 padding if we reached EOF and total length requires alignment padding
                if(_isEof){
                    int remainder = filteredCount % 4;
                    if(remainder > 0){
                        while(remainder < 4){
                            _filteredBuffer[filteredCount++] = (byte)'=';
                            remainder++;
                        }
                    }
                }

                // Ensure decode length is strictly a multiple of 4 characters unless at EOF
                int decodeLength = _isEof ? filteredCount : (filteredCount & ~3);

                // If no valid 4x blocks are available yet, roll over data and loop to fetch more stream content
                if(decodeLength == 0){
                    _rolloverLen = filteredCount;
                    _filteredBuffer.AsSpan(0, filteredCount).CopyTo(_readBuffer);
                
                    if (_isEof) return 0;
                    continue; 
                }

                // Perform high-speed vectorized Base64 decode directly into caller target memory
                OperationStatus status = Base64.DecodeFromUtf8(_filteredBuffer.AsSpan(0, decodeLength), targetMemory.Span, out int bytesConsumed, out int bytesWritten);
                if(status != OperationStatus.Done && status != OperationStatus.DestinationTooSmall){
                    throw new FormatException("Invalid Base64 character sequence encountered.");
                }

                // Preserve any unconsumed filtered bytes for the next read iteration
                int unconsumedCount = filteredCount - bytesConsumed;
                _rolloverLen = unconsumedCount;

                if(unconsumedCount > 0){
                    _filteredBuffer.AsSpan(bytesConsumed, unconsumedCount).CopyTo(_readBuffer);
                }

                return bytesWritten;
            }
        }

        #endregion

        #region override method Write

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanWrite) throw new NotSupportedException("Writing is not supported.");

            Write(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanWrite) throw new NotSupportedException("Writing is not supported.");

            WriteAsync(buffer.ToArray(), default).GetAwaiter().GetResult();
        }

        #endregion

        #region override method WriteAsync

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanWrite) throw new NotSupportedException("Writing is not supported.");

            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }
        
        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> sourceMemory, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanWrite) throw new NotSupportedException("Writing is not supported.");
            if (_isFinalized) throw new InvalidOperationException("Cannot write after the stream has been finalized.");

            ReadOnlySpan<byte> source = sourceMemory.Span;
            int totalLen = _writeRolloverLen + source.Length;
            if(totalLen < 3){
                source.CopyTo(_writeRollover.AsSpan(_writeRolloverLen));
                _writeRolloverLen += source.Length;
                return;
            }

            int convertibleLen = (totalLen / 3) * 3;
            int remainderLen = totalLen - convertibleLen;

            int maxEncodedLen = Base64.GetMaxEncodedToUtf8Length(convertibleLen);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(convertibleLen + maxEncodedLen);
            try{
                Span<byte> combinedRaw = buffer.AsSpan(0, convertibleLen);
                Span<byte> encoded = buffer.AsSpan(convertibleLen, maxEncodedLen);

                int sourceBytesUsed = 0;
                if(_writeRolloverLen > 0){
                    _writeRollover.AsSpan(0, _writeRolloverLen).CopyTo(combinedRaw);
                    int takeFromSource = Math.Min(source.Length, convertibleLen - _writeRolloverLen);
                    if(takeFromSource > 0){
                        source.Slice(0, takeFromSource).CopyTo(combinedRaw.Slice(_writeRolloverLen));
                        sourceBytesUsed = takeFromSource;
                    }
                }
                else{
                    source.Slice(0, convertibleLen).CopyTo(combinedRaw);
                    sourceBytesUsed = convertibleLen;
                }

                source.Slice(sourceBytesUsed, remainderLen).CopyTo(_writeRollover);
                _writeRolloverLen = remainderLen;

                Base64.EncodeToUtf8(combinedRaw, encoded, out _, out int encodedLen);
                ReadOnlyMemory<byte> encodedMemory = buffer.AsMemory(convertibleLen, encodedLen);

                if(_addLineBreaks){
                    await WriteCrlfFormattedAsync(encodedMemory, cancellationToken).ConfigureAwait(false);
                }
                else{
                    await _stream.WriteAsync(encodedMemory, cancellationToken).ConfigureAwait(false);
                }
            }
            finally{
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }    

        private async ValueTask WriteCrlfFormattedAsync(ReadOnlyMemory<byte> encodedMemory, CancellationToken cancellationToken)
        {
            const int LineLength = 76;
            ReadOnlySpan<byte> encoded = encodedMemory.Span;
            int lineCount = (encoded.Length + LineLength - 1) / LineLength;
            int totalLength = encoded.Length + ((lineCount - 1) * 2);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(totalLength);
            try{
                Span<byte> destination = buffer.AsSpan(0, totalLength);
                int sourceOffset = 0;
                int destOffset = 0;
                ReadOnlySpan<byte> crlf = "\r\n"u8;

                while(sourceOffset < encoded.Length){
                    int chunkLen = Math.Min(LineLength, encoded.Length - sourceOffset);
                    encoded.Slice(sourceOffset, chunkLen).CopyTo(destination.Slice(destOffset));
                    sourceOffset += chunkLen;
                    destOffset += chunkLen;

                    if(sourceOffset < encoded.Length){
                        crlf.CopyTo(destination.Slice(destOffset));
                        destOffset += 2;
                    }
                }

                await _stream.WriteAsync(buffer.AsMemory(0, totalLength), cancellationToken).ConfigureAwait(false);
            }
            finally{
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        #endregion

        #region method Finish

        /// <summary>
        /// Synchronously finalizes the stream by encoding and emitting any remaining bytes 
        /// with proper Base64 padding, applying final MIME line breaks if enabled, 
        /// and flushing the underlying stream.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the stream has already been finalized.</exception>
        public void Finish()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanWrite) throw new NotSupportedException("Writing is not supported.");

            FinishAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region method FinishAsync

        /// <summary>
        /// Asynchronously finalizes the stream by encoding and emitting any remaining bytes 
        /// with proper Base64 padding, applying final MIME line breaks if enabled, 
        /// and flushing the underlying stream.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous finish operation.</returns>
        public async Task FinishAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
            if (!CanWrite) return;
            if (_isFinalized) return;
            _isFinalized = true;

            if(_writeRolloverLen > 0){
                Span<byte> finalBytes = _writeRollover.AsSpan(0, _writeRolloverLen);
                byte[] encodedPool = ArrayPool<byte>.Shared.Rent(4);
                try{
                    Base64.EncodeToUtf8(finalBytes, encodedPool, out _, out int encodedLen);
                    ReadOnlyMemory<byte> finalEncoded = encodedPool.AsMemory(0, encodedLen);

                    if(_addLineBreaks){
                        // WriteCrlfFormattedAsync can handle the underlying write
                        await WriteCrlfFormattedAsync(finalEncoded, cancellationToken).ConfigureAwait(false);
                    }
                    else{
                        await _stream.WriteAsync(finalEncoded, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally{
                    ArrayPool<byte>.Shared.Return(encodedPool);
                    _writeRolloverLen = 0;
                }
            }
        }

        #endregion    

        #region override method Seek

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        #endregion

        #region override method SetLength

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        #endregion


        #region Properties implementation

        /// <inheritdoc />
        public override bool CanRead => (_accessMode & FileAccess.Read) == FileAccess.Read && _stream.CanRead;

        /// <inheritdoc />
        public override bool CanWrite => (_accessMode & FileAccess.Write) == FileAccess.Write && _stream.CanWrite;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        #endregion    
    }
}