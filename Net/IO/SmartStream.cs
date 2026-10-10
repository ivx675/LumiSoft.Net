
using System.Text;

namespace LumiSoft.Net.IO
{
    /// <summary>
    /// This class is wrapper to normal stream, provides most needed stream methods which are missing from normal stream.
    /// </summary>
    public class SmartStream : Stream
    {  
        private bool              m_IsDisposed       = false;
        private Stream            m_pStream;
        private bool              m_IsOwner          = false;
        private DateTime          m_LastActivity;
        private long              m_BytesReaded      = 0;
        private long              m_BytesWritten     = 0;
        private int               m_BufferSize       = 84000;
        private byte[]            m_pReadBuffer;
        private int               m_ReadBufferOffset = 0;
        private int               m_ReadBufferCount  = 0;
        private Encoding          m_pEncoding        = Encoding.Default;
        private bool              m_CRLFLines        = true;
        private int               m_Timeout          = 60000;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="stream">Stream to wrap.</param>
        /// <param name="owner">Specifies if SmartStream is owner of <b>stream</b>.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null.</exception>
        public SmartStream(Stream stream,bool owner)
        {
            if(stream == null){
                throw new ArgumentNullException("stream");
            }
            
            m_pStream = stream;
            m_IsOwner = owner;
            m_pReadBuffer = new byte[m_BufferSize];

            m_LastActivity = DateTime.Now;
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public new void Dispose()
        {
            if(m_IsDisposed){
                return;
            }
            m_IsDisposed = true;

            if(m_IsOwner){
                m_pStream.Dispose();
            }
        }

        #endregion
                               

        #region method ReadLine

        /// <summary>
        /// Reads a single line from the underlying stream synchronously. This method is a blocking
        /// wrapper around <see cref="ReadLineAsync(Memory{byte}, SizeExceededAction, CancellationToken)"/>
        /// and should be used only when asynchronous execution is not required.
        /// </summary>
        /// <param name="buffer">
        /// The destination buffer where the line data will be stored. The buffer must be large
        /// enough to hold the entire line unless <paramref name="exceededAction"/> allows truncation.
        /// </param>
        /// <param name="exceededAction">
        /// Specifies how the reader behaves when the line exceeds the size of the provided
        /// <paramref name="buffer"/>. If set to <see cref="SizeExceededAction.ThrowException"/>,
        /// a <see cref="LineSizeExceededException"/> is thrown. Otherwise, excess bytes are discarded
        /// and the returned <see cref="ReadLineResult"/> is marked as exceeded.
        /// </param>
        /// <returns>
        /// A <see cref="ReadLineResult"/> containing the raw line bytes, the number of bytes read,
        /// and convenience accessors for interpreting the line as text.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <remarks>
        /// This method blocks the calling thread until the line has been fully read. For high‑
        /// throughput or latency‑sensitive scenarios, prefer using <see cref="ReadLineAsync"/>.
        /// </remarks>
        public ReadLineResult ReadLine(Memory<byte> buffer,SizeExceededAction exceededAction)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            return ReadLineAsync(buffer,exceededAction,cts.Token).AsTask().GetAwaiter().GetResult();
        }

        #endregion

        #region method ReadLineAsync

        /// <summary>
        /// Reads a single line from the underlying stream asynchronously. The method stores the
        /// line data into the provided <paramref name="buffer"/> and returns a
        /// <see cref="ReadLineResult"/> describing the line. The returned result includes both the
        /// raw byte count (including any CR or LF terminators) and the logical line length without
        /// terminators.
        /// </summary>
        /// <param name="buffer">
        /// The destination buffer where the line data will be stored. The buffer must be large
        /// enough to hold the entire line unless <paramref name="exceededAction"/> allows truncation.
        /// </param>
        /// <param name="exceededAction">
        /// Specifies how the reader behaves when the line exceeds the size of the provided
        /// <paramref name="buffer"/>. If set to <see cref="SizeExceededAction.ThrowException"/>,
        /// a <see cref="LineSizeExceededException"/> is thrown. Otherwise, excess bytes are
        /// discarded and the result is marked as exceeded.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{ReadLineResult}"/> representing the asynchronous read operation.
        /// The task may complete synchronously if the line is already available in the internal
        /// read buffer. The returned <see cref="ReadLineResult"/> contains the raw line bytes,
        /// the number of bytes read, and convenience properties for accessing the line as ASCII,
        /// UTF-8, or UTF-32 text.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="LineSizeExceededException">
        /// Thrown when the line exceeds the size of <paramref name="buffer"/> and
        /// <paramref name="exceededAction"/> is set to <see cref="SizeExceededAction.ThrowException"/>.
        /// </exception>
        public async ValueTask<ReadLineResult> ReadLineAsync(Memory<byte> buffer,SizeExceededAction exceededAction,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            int  bytesStored = 0;
            bool exceeded    = false;
            while(true){
                // Read buffer is empty.
                if(this.BytesInReadBuffer == 0){
                    int readedCount = await m_pStream.ReadAsync(m_pReadBuffer,cancellationToken).ConfigureAwait(false);
                    // End of stream reached.
                    if(readedCount == 0){
                        return new ReadLineResult(buffer,bytesStored);
                    }

                    m_ReadBufferOffset  = 0;
                    m_ReadBufferCount   = readedCount;
                    m_BytesReaded      += readedCount;
                    m_LastActivity      = DateTime.Now;
                }

                ReadOnlySpan<byte> available = m_pReadBuffer.AsSpan(m_ReadBufferOffset,m_ReadBufferCount - m_ReadBufferOffset);

                int lfPos = available.IndexOf((byte)'\n');

                // No LF in current read buffer.
                if(lfPos < 0){
                    int count = available.Length;

                    if(exceeded || bytesStored + count > buffer.Length){
                        exceeded = true;

                        if(exceededAction == SizeExceededAction.ThrowException){
                            throw new LineSizeExceededException();
                        }
                    }
                    else{
                        available.CopyTo(buffer.Span.Slice(bytesStored));
                    }

                    bytesStored += count;
                    m_ReadBufferOffset += count;

                    continue;
                }

                // LF found.
                int bytesToCopy = lfPos + 1;

                if(exceeded || bytesStored + bytesToCopy > buffer.Length){
                    exceeded = true;

                    if(exceededAction == SizeExceededAction.ThrowException){
                        throw new LineSizeExceededException();
                    }
                }
                else{
                    available.Slice(0,bytesToCopy).CopyTo(buffer.Span.Slice(bytesStored));
                }

                bytesStored += bytesToCopy;
                m_ReadBufferOffset += bytesToCopy;

                if(CRLFLines){
                    // LF without CR.
                    if(bytesStored < 2 || buffer.Span[bytesStored - 2] != (byte)'\r'){
                        continue;
                    }
                }

                if(exceeded){
                    throw new LineSizeExceededException();
                }

                return new ReadLineResult(buffer,bytesStored);
            }
        }

        #endregion

        #region method ReadHeader

        /// <summary>
        /// Reads a MIME or SMTP header section from the underlying stream synchronously.
        /// This method is a blocking wrapper around
        /// <see cref="ReadHeaderAsync(Stream, int, int, SizeExceededAction, CancellationToken)"/>
        /// and behaves identically, except that it executes synchronously.
        /// </summary>
        /// <param name="storeStream">
        /// The destination stream where the header lines will be written.
        /// Each line is written exactly as received, including its terminating CRLF.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of header bytes allowed to be written to <paramref name="storeStream"/>.
        /// Must be at least <c>8000</c>.
        /// If the accumulated header data exceeds this limit and <paramref name="exceededAction"/> is
        /// <see cref="SizeExceededAction.ThrowException"/>, a <see cref="DataSizeExceededException"/> is thrown.
        /// </param>
        /// <param name="maxLineSize">
        /// The maximum allowed size of a single header line (including CRLF).
        /// Must be at least <c>64</c>.
        /// This value determines the size of the temporary buffer used during line reading.
        /// </param>
        /// <param name="exceededAction">
        /// Specifies how the method behaves when the header data exceeds <paramref name="maxCount"/>.
        /// If set to <see cref="SizeExceededAction.ThrowException"/>, a <see cref="DataSizeExceededException"/> is thrown.
        /// Otherwise, excess bytes are ignored and the method continues reading until the blank-line terminator.
        /// </param>
        /// <returns>
        /// The total number of bytes written to <paramref name="storeStream"/>.
        /// This count includes CRLF terminators exactly as they appear in the incoming header section.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="storeStream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than <c>8000</c>
        /// or <paramref name="maxLineSize"/> is less than <c>64</c>.
        /// </exception>
        /// <exception cref="IncompleteDataException">
        /// Thrown if the underlying stream ends before a blank-line terminator is encountered.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the header data exceeds <paramref name="maxCount"/> and
        /// <paramref name="exceededAction"/> is <see cref="SizeExceededAction.ThrowException"/>.
        /// </exception>
        /// <remarks>
        /// This method implements the blank-line terminated header semantics used by MIME, SMTP, POP3,
        /// and IMAP. A header section ends when an empty line (CRLF CRLF) is encountered.
        /// </remarks>
        public int ReadHeader(Stream storeStream,int maxCount,int maxLineSize,SizeExceededAction exceededAction)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(storeStream == null){
                throw new ArgumentNullException("storeStream");
            }
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }
            if(maxLineSize < 64){
                throw new ArgumentException("Argument 'maxLineSize' must be >= 64.");
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            return ReadHeaderAsync(storeStream,maxCount,maxLineSize,exceededAction,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ReadHeaderAsync

        /// <summary>
        /// Reads a MIME or SMTP header section from the underlying stream asynchronously.
        /// The header section is read line-by-line using <see cref="ReadLineAsync(Memory{byte}, SizeExceededAction, CancellationToken)"/>
        /// and written into the specified <paramref name="storeStream"/>.  
        /// The header section is terminated by a blank line (CRLF CRLF).
        /// </summary>
        /// <param name="storeStream">
        /// The destination stream where the header lines will be written.  
        /// Each line is written exactly as received, including its terminating CRLF.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of header bytes allowed to be written to <paramref name="storeStream"/>.  
        /// Must be at least <c>8000</c>.  
        /// If the accumulated header data exceeds this limit and <paramref name="exceededAction"/> is
        /// <see cref="SizeExceededAction.ThrowException"/>, a <see cref="DataSizeExceededException"/> is thrown.
        /// </param>
        /// <param name="maxLineSize">
        /// The maximum allowed size of a single header line (including CRLF).  
        /// Must be at least <c>64</c>.  
        /// This value determines the size of the temporary buffer used during line reading.
        /// </param>
        /// <param name="exceededAction">
        /// Specifies how the method behaves when the header data exceeds <paramref name="maxCount"/>.  
        /// If set to <see cref="SizeExceededAction.ThrowException"/>, a <see cref="DataSizeExceededException"/> is thrown.  
        /// Otherwise, excess bytes are ignored and the method continues reading until the blank-line terminator.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// The total number of bytes written to <paramref name="storeStream"/>.  
        /// This count includes CRLF terminators exactly as they appear in the incoming header section.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="storeStream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than <c>8000</c>
        /// or <paramref name="maxLineSize"/> is less than <c>64</c>.
        /// </exception>
        /// <exception cref="IncompleteDataException">
        /// Thrown if the underlying stream ends before a blank-line terminator is encountered.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the header data exceeds <paramref name="maxCount"/> and
        /// <paramref name="exceededAction"/> is <see cref="SizeExceededAction.ThrowException"/>.
        /// </exception>
        /// <remarks>
        /// This method implements the blank-line terminated header semantics used by MIME, SMTP, POP3,
        /// and IMAP.  
        /// A header section ends when an empty line (CRLF CRLF) is encountered.
        /// </remarks>
        public async ValueTask<int> ReadHeaderAsync(Stream storeStream,int maxCount,int maxLineSize,SizeExceededAction exceededAction,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed) {
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(storeStream);
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }
            if(maxLineSize < 64){
                throw new ArgumentException("Argument 'maxLineSize' must be >= 64.");
            }

            Memory<byte> buffer      = new Memory<byte>(new byte[maxLineSize]);
            Span<byte>   bufferSpan  = buffer.Span;
            bool         exceeded    = false;
            int          bytesStored = 0;
            while(true){
                ReadLineResult result = await ReadLineAsync(buffer,exceededAction,cancellationToken).ConfigureAwait(false);

                // We reached end of stream, no more data.
                if(result.BytesInBuffer == 0){
                    throw new IncompleteDataException("Data is not blank-line terminated.");
                }
                // We have blank line terminator.
                else if(result.LineBytesInBuffer == 0){
                    if(exceeded){
                        throw new DataSizeExceededException();
                    }

                    return bytesStored;
                }
                // Normal line.
                else{
                    if((bytesStored + result.LineBytesInBuffer) <= maxCount){
                        var line = buffer.Slice(0,result.BytesInBuffer);
                        await storeStream.WriteAsync(line,cancellationToken).ConfigureAwait(false);
                        bytesStored += line.Length;
                    }
                    // Maximum allowed data to store bytes is exceeded.
                    else{                                             
                        exceeded = true;
                        if(exceededAction == SizeExceededAction.ThrowException){
                            throw new DataSizeExceededException();
                        }   
                    }
                }
            }
        }

        #endregion

        #region method ReadPeriodTerminated

        /// <summary>
        /// Reads a period‑terminated data block from the underlying stream synchronously.  
        /// This method is a blocking wrapper around  
        /// <see cref="ReadPeriodTerminatedAsync(Stream, long, int, SizeExceededAction, CancellationToken)"/>  
        /// and behaves identically, except that it executes synchronously.
        /// </summary>
        /// <param name="stream">
        /// The destination stream where the decoded data block will be written.  
        /// Lines beginning with a dot (<c>.</c>) have the leading dot removed according  
        /// to SMTP/POP3 dot‑stuffing rules.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of data bytes allowed to be written to <paramref name="stream"/>.  
        /// Must be greater than or equal to <c>8000</c>.  
        /// If the accumulated data exceeds this limit and <paramref name="exceededAction"/> is  
        /// <see cref="SizeExceededAction.ThrowException"/>, a <see cref="DataSizeExceededException"/> is thrown.
        /// </param>
        /// <param name="maxLineSize">
        /// The maximum allowed size of a single line (including CRLF).  
        /// Must be at least <c>64</c>.  
        /// This value determines the size of the temporary line buffer used during reading.
        /// </param>
        /// <param name="exceededAction">
        /// Specifies how the method behaves when the data block exceeds <paramref name="maxCount"/>.  
        /// If set to <see cref="SizeExceededAction.ThrowException"/>, a  
        /// <see cref="DataSizeExceededException"/> is thrown.  
        /// Otherwise, excess data is ignored and the method continues reading until the terminating dot line.
        /// </param>
        /// <returns>
        /// The total number of bytes written to <paramref name="stream"/>.  
        /// This count includes CRLF terminators exactly as they appear in the incoming  
        /// period‑terminated data block, and excludes only the dot removed during dot‑stuffing.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than <c>0</c>  
        /// or <paramref name="maxLineSize"/> is less than <c>64</c>.
        /// </exception>
        /// <exception cref="IncompleteDataException">
        /// Thrown if the underlying stream ends before a period‑terminated block is completed.
        /// </exception>
        ///
        public int ReadPeriodTerminated(Stream stream,long maxCount,int maxLineSize,SizeExceededAction exceededAction)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }
            if(maxLineSize < 64){
                throw new ArgumentException("Argument 'maxLineSize' must be >= 64.");
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            return ReadPeriodTerminatedAsync(stream,maxCount,maxLineSize,exceededAction,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ReadPeriodTerminatedAsync

        /// <summary>
        /// Reads a period‑terminated multi‑line data block from the underlying stream.
        /// This method is used by both SMTP (<c>DATA</c>) and POP3 multi‑line responses.
        /// Lines are read using <see cref="ReadLineAsync(Memory{byte}, SizeExceededAction, CancellationToken)"/>,
        /// dot‑stuffing is removed, and accepted lines are written into <paramref name="storeStream"/>.
        /// The block ends when a line containing only a single dot (<c>.</c>) is received.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method enforces both per‑line and total‑message size limits. When a line exceeds
        /// <paramref name="maxLineSize"/>, <see cref="ReadLineAsync"/> consumes the entire overlong line
        /// and then throws <see cref="LineSizeExceededException"/>. If <paramref name="exceededAction"/>
        /// is <see cref="SizeExceededAction.JunkAndThrowException"/>, the exception is caught and the
        /// method continues reading subsequent lines until the terminating dot is reached. No further
        /// data is written to <paramref name="storeStream"/> once a line‑size violation has occurred.
        /// After the terminating dot, the method throws <see cref="LineSizeExceededException"/> to
        /// signal that the block was invalid.
        /// </para>
        ///
        /// <para>
        /// When the accumulated message size exceeds <paramref name="maxCount"/>, the method enters
        /// a similar junk mode: excess data is ignored, and the method continues reading until the
        /// terminating dot. If <paramref name="exceededAction"/> is
        /// <see cref="SizeExceededAction.ThrowException"/>, the method throws
        /// <see cref="DataSizeExceededException"/> immediately when the overflow occurs. If
        /// <paramref name="exceededAction"/> is <see cref="SizeExceededAction.JunkAndThrowException"/>,
        /// the method continues reading until the terminating dot and then throws
        /// <see cref="DataSizeExceededException"/>.
        /// </para>
        ///
        /// <para>
        /// Dot‑stuffing is automatically removed: lines beginning with a dot have the leading dot
        /// stripped before being written to <paramref name="storeStream"/>, except for the terminating
        /// dot line (<c>.</c>), which ends the block.
        /// </para>
        /// </remarks>
        /// <param name="storeStream">
        /// Destination stream for accepted data. Lines rejected due to size violations are not written.
        /// </param>
        /// <param name="maxCount">
        /// Maximum number of bytes allowed to be written to <paramref name="storeStream"/>.
        /// Must be at least <c>8000</c>. When exceeded, the method either throws immediately or
        /// continues in junk mode depending on <paramref name="exceededAction"/>.
        /// </param>
        /// <param name="maxLineSize">
        /// Maximum allowed size of a single line (including CRLF). Must be at least <c>64</c>.
        /// Overlong lines are fully consumed and discarded by <see cref="ReadLineAsync"/>.
        /// </param>
        /// <param name="exceededAction">
        /// Determines how size violations are handled:
        /// <list type="bullet">
        /// <item>
        /// <see cref="SizeExceededAction.ThrowException"/> — throw immediately when the violation occurs.
        /// </item>
        /// <item>
        /// <see cref="SizeExceededAction.JunkAndThrowException"/> — junk offending data, continue reading
        /// until the terminating dot, then throw.
        /// </item>
        /// </list>
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token.
        /// </param>
        /// <returns>
        /// Total number of bytes written to <paramref name="storeStream"/> (after dot‑stuffing removal).
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the underlying <see cref="SmartStream"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="storeStream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than <c>8000</c> or
        /// <paramref name="maxLineSize"/> is less than <c>64</c>.
        /// </exception>
        /// <exception cref="IncompleteDataException">
        /// Thrown if the underlying stream ends before a period‑terminated block is completed.
        /// </exception>
        /// <exception cref="LineSizeExceededException">
        /// Thrown immediately (for <see cref="SizeExceededAction.ThrowException"/>) or after the
        /// terminating dot (for <see cref="SizeExceededAction.JunkAndThrowException"/>) if any line
        /// exceeded <paramref name="maxLineSize"/>.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown immediately (for <see cref="SizeExceededAction.ThrowException"/>) or after the
        /// terminating dot (for <see cref="SizeExceededAction.JunkAndThrowException"/>) if the total
        /// message size exceeded <paramref name="maxCount"/>.
        /// </exception>
        public async ValueTask<int> ReadPeriodTerminatedAsync(Stream storeStream,long maxCount,int maxLineSize,SizeExceededAction exceededAction,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(storeStream);
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }
            if(maxLineSize < 64){
                throw new ArgumentException("Argument 'maxLineSize' must be >= 64.");
            }

            Memory<byte> buffer              = new Memory<byte>(new byte[maxLineSize]);
            bool         maxCountExceeded    = false;
            bool         maxLineSizeExceeded = false;
            int          bytesStored         = 0;
            while(true){
                ReadLineResult result;
                try{
                    result = await ReadLineAsync(buffer,exceededAction,cancellationToken).ConfigureAwait(false);
                }
                catch(LineSizeExceededException){
                    maxLineSizeExceeded = true;
                    if(exceededAction == SizeExceededAction.JunkAndThrowException){
                        continue;
                    }
                    else{
                        throw;
                    }
                }
                
                Span<byte> bufferSpan  = buffer.Span;
                // We reached end of stream, no more data.
                if(result.BytesInBuffer == 0){
                    throw new IncompleteDataException("Data is not period-terminated.");
                }
                // We have period terminator.
                else if(result.LineBytesInBuffer == 1 && bufferSpan[0] == '.'){
                    if(maxLineSizeExceeded){
                        throw new LineSizeExceededException();
                    }
                    if(maxCountExceeded){
                        throw new DataSizeExceededException();
                    }

                    return bytesStored;
                }
                // Normal line.
                else if(!maxLineSizeExceeded && !maxCountExceeded){
                    if((bytesStored + result.LineBytesInBuffer) <= maxCount){
                        // Period handling: If line starts with '.', it must be removed.
                        if(bufferSpan[0] == '.'){
                            // Skip the leading dot
                            var lineWithoutDot = buffer.Slice(1,result.BytesInBuffer - 1);
                            await storeStream.WriteAsync(lineWithoutDot,cancellationToken).ConfigureAwait(false);
                            bytesStored += lineWithoutDot.Length;
                        }
                        // Nomrmal line.
                        else{
                            var line = buffer.Slice(0,result.BytesInBuffer);
                            await storeStream.WriteAsync(line,cancellationToken).ConfigureAwait(false);
                            bytesStored += line.Length;
                        }
                    }
                    // Maximum allowed data to store bytes is exceeded.
                    else{                                             
                        maxCountExceeded = true;
                        if(exceededAction == SizeExceededAction.ThrowException){
                            throw new DataSizeExceededException();
                        }   
                    }
                }
            }
        }

        #endregion

        #region method ReadFixedCount

        /// <summary>
        /// Reads an exact number of bytes from the underlying stream synchronously and writes
        /// them into the specified <paramref name="storeStream"/>.  
        /// This method is a blocking wrapper around
        /// <see cref="ReadFixedCountAsync(Stream, long, CancellationToken)"/> and behaves
        /// identically, except that it executes synchronously.
        /// </summary>
        /// <param name="storeStream">
        /// The destination stream where the read bytes will be written.
        /// </param>
        /// <param name="count">
        /// The exact number of bytes to read from the underlying stream.  
        /// Must be greater than or equal to <c>0</c>.  
        /// If the stream ends before the requested number of bytes is read, an
        /// <see cref="EndOfStreamException"/> is thrown.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="storeStream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="count"/> is less than <c>0</c>.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown if the underlying stream ends before <paramref name="count"/> bytes have been read.
        /// </exception>
        /// <remarks>
        /// This method performs a raw fixed-count read and does not interpret terminators,
        /// line boundaries, or protocol framing. It is suitable for reading binary payloads
        /// or fixed-length protocol segments.
        /// </remarks>
        public void ReadFixedCount(Stream storeStream,long count)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(storeStream == null){
                throw new ArgumentNullException("storeStream");
            }
            if(count < 0){
                throw new ArgumentException("Argument 'count' value must be >= 0.");
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            ReadFixedCountAsync(storeStream,count,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ReadFixedCountAsync

        /// <summary>
        /// Reads an exact number of bytes from the underlying stream asynchronously and writes
        /// them into the specified <paramref name="storeStream"/>.  
        /// The method continues reading until <paramref name="count"/> bytes have been successfully
        /// transferred or an unexpected end of stream occurs.
        /// </summary>
        /// <param name="storeStream">
        /// The destination stream where the read bytes will be written.
        /// </param>
        /// <param name="count">
        /// The exact number of bytes to read from the underlying stream.  
        /// Must be greater than or equal to <c>0</c>.  
        /// If the stream ends before the requested number of bytes is read, an
        /// <see cref="EndOfStreamException"/> is thrown.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="storeStream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="count"/> is less than <c>0</c>.
        /// </exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown if the underlying stream ends before <paramref name="count"/> bytes have been read.
        /// </exception>
        /// <remarks>
        /// This method performs a raw fixed-count read and does not interpret terminators,
        /// line boundaries, or protocol framing. It is suitable for reading binary payloads
        /// or fixed-length protocol segments.
        /// </remarks>
        public async ValueTask ReadFixedCountAsync(Stream storeStream,long count,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(storeStream);
            if(count < 0) {
                throw new ArgumentException("Argument 'count' value must be >= 0.");
            }

            Memory<byte> buffer    = new byte[32000];
            long         remaining = count;
            while(remaining > 0){
                Memory<byte> readBlock = buffer;
                int          toRead    = (int)Math.Min(buffer.Length,remaining);

                // We need to read less than buffer size, so we need to slice the buffer.
                if(toRead < buffer.Length){
                    readBlock = buffer.Slice(0,toRead);
                }

                int readCount = await ReadAsync(readBlock,cancellationToken).ConfigureAwait(false);
                if(readCount == 0) {
                    throw new EndOfStreamException("Unexpected end of stream while reading fixed count.");
                }

                await storeStream.WriteAsync(buffer.Slice(0,readCount),cancellationToken).ConfigureAwait(false);
                remaining -= readCount;
            }
        }

        #endregion

        #region method ReadFixedCountString

        /// <summary>
        /// Reads specified number of bytes from source stream and converts it to string with current encoding.
        /// </summary>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>Returns readed data as string.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public string ReadFixedCountString(int count)
        {    
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(count < 0){
                throw new ArgumentException("Argument 'count' value must be >= 0.");
            }

            MemoryStream ms = new MemoryStream();
            ReadFixedCount(ms,count);

            return m_pEncoding.GetString(ms.ToArray());
        }

        #endregion
                
        #region method ReadAll

        /// <summary>
        /// Reads all data from source stream and stores to the specified stream.
        /// </summary>
        /// <param name="stream">Stream where to store readed data.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null.</exception>
        public void ReadAll(Stream stream)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            byte[] buffer = new byte[m_BufferSize];
            while(true){
                int readedCount = Read(buffer,0,buffer.Length);
                // End of stream reached, we readed file sucessfully.
                if(readedCount == 0){
                    break;
                }
                else{
                    stream.Write(buffer,0,readedCount);
                }
            }
        }

        #endregion

        #region method Peek

        /// <summary>
        /// Returns the next available character but does not consume it.
        /// </summary>
        /// <returns>An integer representing the next character to be read, or -1 if no more characters are available.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        public int Peek()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(nameof(SmartStream));
            }

            if(BytesInReadBuffer == 0){
                int readCount = m_pStream.Read(m_pReadBuffer);
                if(readCount == 0){
                    return -1;
                }

                m_ReadBufferOffset = 0;
                m_ReadBufferCount  = readCount;
                m_BytesReaded     += readCount;
                m_LastActivity     = DateTime.Now;
            }

            return m_pReadBuffer[m_ReadBufferOffset];
        }

        #endregion

        #region method Write

        /// <summary>
        /// Writes specified string data to stream.
        /// </summary>
        /// <param name="data">Data to write.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>data</b> is null.</exception>
        public void Write(string data)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(data == null){
                throw new ArgumentNullException("data");
            }

            byte[] dataBytes = Encoding.Default.GetBytes(data);
            Write(dataBytes,0,dataBytes.Length);
            Flush();
        }

        #endregion

        #region method WriteLine

        /// <summary>
        /// Writes the specified line to the underlying stream synchronously. If the line does not
        /// end with a CRLF terminator (<c>\r\n</c>), the terminator is appended automatically
        /// before writing. The method blocks until all data has been written and the stream has
        /// been flushed.
        /// </summary>
        /// <param name="line">
        /// The line to write. If the line does not already end with <c>\r\n</c>, the terminator
        /// is appended automatically.
        /// </param>
        /// <returns>
        /// The total number of bytes written to the stream, including any automatically appended
        /// CRLF terminator.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="line"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// This method performs a synchronous write and flush operation. For high‑throughput or
        /// latency‑sensitive scenarios, consider using <see cref="WriteLineAsync(string, CancellationToken)"/>
        /// to avoid blocking the calling thread.
        /// </remarks>
        public int WriteLine(string line)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(line == null){
                throw new ArgumentNullException("line");
            }

            if(!line.EndsWith("\r\n")){
                line += "\r\n";
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            WriteLineAsync(line,cts.Token).GetAwaiter().GetResult();

            return m_pEncoding.GetBytes(line).Length;
        }

        #endregion

        #region method WriteLineAsync

        /// <summary>
        /// Writes the specified text line to this <see cref="SmartStream"/> asynchronously,
        /// ensuring that the line is terminated with a CRLF sequence (<c>\r\n</c>).
        /// </summary>
        /// <param name="line">
        /// The text line to write. If the value does not already end with <c>\r\n</c>,
        /// the terminator is appended automatically.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <remarks>
        /// The line is encoded using the stream's configured encoding and written using
        /// <see cref="WriteAsync(System.ReadOnlyMemory{byte}, System.Threading.CancellationToken)"/>.  
        /// A flush operation is performed afterward to ensure the data is committed to the
        /// underlying stream. The byte counter and activity timestamp are updated accordingly.
        /// </remarks>
        public async ValueTask WriteLineAsync(string line,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed) {
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(line);

            if(!line.EndsWith("\r\n")) {
                line += "\r\n";
            }

            Memory<byte> dataBytes = m_pEncoding.GetBytes(line);
            await WriteAsync(dataBytes,cancellationToken).ConfigureAwait(false);
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            m_BytesWritten += dataBytes.Length;
            m_LastActivity  = DateTime.Now;
        }

        #endregion

        #region method WriteStream        

        /// <summary>
        /// Copies data from the specified <paramref name="sourceStream"/> into this stream
        /// synchronously.  
        /// This method is a blocking wrapper around
        /// <see cref="WriteStreamAsync(System.IO.Stream, long, long, System.Threading.CancellationToken)"/>
        /// and behaves identically, except that it executes synchronously.
        /// </summary>
        /// <param name="sourceStream">
        /// The stream from which data will be read.
        /// </param>
        /// <param name="count">
        /// The exact number of bytes to read from <paramref name="sourceStream"/>.  
        /// If zero, the method reads until end of stream or until <paramref name="maxCount"/>
        /// is exceeded.
        /// </param>
        /// <param name="maxCount">
        /// The maximum allowed number of bytes to process when <paramref name="count"/> is zero.  
        /// Must be greater than or equal to <c>8000</c>.  
        /// If the total number of bytes read exceeds this value, a
        /// <see cref="DataSizeExceededException"/> is thrown.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="sourceStream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than <c>8000</c>.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when reading in unlimited mode (<paramref name="count"/> equals zero)
        /// and the total number of bytes read exceeds <paramref name="maxCount"/>.
        /// </exception>
        /// <remarks>
        /// This method performs a raw binary copy operation and does not interpret terminators,
        /// line boundaries, or protocol framing.  
        /// It blocks the calling thread until the entire operation completes.
        /// </remarks>
        public void WriteStream(Stream sourceStream,long count,long maxCount)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(sourceStream);
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            WriteStreamAsync(sourceStream,count,maxCount,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method WriteStreamAsync

        /// <summary>
        /// Copies data from the specified <paramref name="sourceStream"/> into this stream
        /// asynchronously.  
        /// When <paramref name="count"/> is greater than zero, exactly that number of bytes
        /// is read from <paramref name="sourceStream"/> and written into this stream.  
        /// When <paramref name="count"/> is zero, data is read until the end of the source
        /// stream or until <paramref name="maxCount"/> bytes have been processed.
        /// </summary>
        /// <param name="sourceStream">
        /// The stream from which data will be read.
        /// </param>
        /// <param name="count">
        /// The exact number of bytes to read from <paramref name="sourceStream"/>.  
        /// If zero, the method reads until end of stream or until <paramref name="maxCount"/>
        /// is exceeded.
        /// </param>
        /// <param name="maxCount">
        /// The maximum allowed number of bytes to process when <paramref name="count"/> is zero.  
        /// Must be greater than or equal to <c>8000</c>.  
        /// If the total number of bytes read exceeds this value, a
        /// <see cref="DataSizeExceededException"/> is thrown.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous copy operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="sourceStream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than <c>8000</c>.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when reading in unlimited mode (<paramref name="count"/> equals zero)
        /// and the total number of bytes read exceeds <paramref name="maxCount"/>.
        /// </exception>
        /// <remarks>
        /// This method performs a raw binary copy operation and does not interpret terminators,
        /// line boundaries, or protocol framing.  
        /// Each block of data is forwarded to
        /// <see cref="WriteAsync(System.ReadOnlyMemory{byte}, System.Threading.CancellationToken)"/>.
        /// </remarks>
        public async ValueTask WriteStreamAsync(Stream sourceStream,long count,long maxCount,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(sourceStream);
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }
            
            Memory<byte> buffer     = new byte[Math.Min(32000,maxCount)];            
            long         totalBytes = 0;
            // Write specified amount of data from sourceStream.
            if(count > 0){
                long remaining = count;
                while(remaining > 0){
                    Memory<byte> readBlock = buffer;
                    int          toRead    = (int)Math.Min(buffer.Length,remaining);

                    // We need to read less than buffer size, so we need to slice the buffer.
                    if(toRead < buffer.Length){
                        readBlock = buffer.Slice(0,toRead);
                    }

                    int readCount = await sourceStream.ReadAsync(readBlock,cancellationToken).ConfigureAwait(false);
                    // End of stream reached.
                    if(readCount == 0) {
                        throw new IOException($"Insufficient data in source stream: expected {count} bytes");
                    }
                    totalBytes += readCount;

                    await WriteAsync(buffer.Slice(0,readCount),cancellationToken).ConfigureAwait(false);
                    remaining -= readCount;
                }
            }
            // Read till eond of stream or maxCount reached.
            else{
                while(true){
                    int readCount = await sourceStream.ReadAsync(buffer,cancellationToken).ConfigureAwait(false);
                    // End of stream reached.
                    if(readCount == 0) {
                        return;
                    }
                    totalBytes += readCount;

                    // We exceeded maximum allowed count.
                    if(totalBytes > maxCount){
                        throw new DataSizeExceededException();
                    }                   

                    await WriteAsync(buffer.Slice(0,readCount),cancellationToken).ConfigureAwait(false);
                }
            }            
        }

        #endregion

        #region method WritePeriodTerminated

        /// <summary>
        /// Synchronously writes a period‑terminated data block to the underlying stream.
        /// This method is a blocking wrapper around
        /// <see cref="WritePeriodTerminatedAsync(Stream,long,int,SizeExceededAction,System.Threading.CancellationToken)"/>.
        /// </summary>
        /// <param name="sourceStream">
        /// The stream from which lines are read and encoded into a period‑terminated block.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of bytes allowed to be written. Must be at least 8000.
        /// When the accumulated output exceeds this limit, behavior is controlled by
        /// <paramref name="exceededAction"/>.
        /// </param>
        /// <param name="maxLineSize">
        /// The maximum allowed size of a single line. Must be at least 64.
        /// </param>
        /// <param name="exceededAction">
        /// Specifies how size overflow conditions are handled.
        /// <see cref="SizeExceededAction.ThrowException"/> causes the method to throw
        /// immediately when the next line would exceed <paramref name="maxCount"/>.
        /// <see cref="SizeExceededAction.JunkAndThrowException"/> causes overflowing data
        /// to be ignored while the method continues reading until end‑of‑stream, and then
        /// throws <see cref="DataSizeExceededException"/> after the operation completes.
        /// </param>
        /// <returns>
        /// The total number of bytes written to the underlying stream, including the final
        /// period terminator sequence.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> or <paramref name="maxLineSize"/> is below
        /// the required minimum.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the encoded output exceeds <paramref name="maxCount"/> and the
        /// overflow policy requires an exception. Also thrown at end‑of‑stream when
        /// <see cref="SizeExceededAction.JunkAndThrowException"/> is used and overflow
        /// occurred.
        /// </exception>
        /// <remarks>
        /// This synchronous wrapper blocks the calling thread until the asynchronous
        /// operation completes. All data encoding rules, dot‑escaping behavior, and strict
        /// CRLF termination semantics are identical to those of the asynchronous method.
        /// </remarks>
        public long WritePeriodTerminated(Stream sourceStream,long maxCount,int maxLineSize,SizeExceededAction exceededAction)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(sourceStream);
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }
            if(maxLineSize < 64){
                throw new ArgumentException("Argument 'maxLineSize' must be >= 64.");
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            return WritePeriodTerminatedAsync(sourceStream,maxCount,maxLineSize,exceededAction,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method WritePeriodTerminatedAsync

        /// <summary>
        /// Writes a period‑terminated data block to the underlying stream asynchronously.
        /// This method reads lines from <paramref name="sourceStream"/>, applies SMTP‑style
        /// dot‑escaping, enforces size limits, and ensures the final DATA terminator is
        /// strictly CRLF.CRLF.
        /// </summary>
        /// <param name="sourceStream">
        /// The stream from which lines are read and encoded into a period‑terminated block.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of bytes allowed to be written. Must be at least 8000.
        /// When the accumulated output exceeds this limit, behavior is controlled by
        /// <paramref name="exceededAction"/>.
        /// </param>
        /// <param name="maxLineSize">
        /// The maximum allowed size of a single line. Must be at least 64.
        /// </param>
        /// <param name="exceededAction">
        /// Specifies how size overflow conditions are handled.
        /// <see cref="SizeExceededAction.ThrowException"/> causes the method to throw
        /// immediately when the next line would exceed <paramref name="maxCount"/>.
        /// <see cref="SizeExceededAction.JunkAndThrowException"/> causes overflowing data
        /// to be ignored while the method continues reading until end‑of‑stream, and then
        /// throws <see cref="DataSizeExceededException"/> after the operation completes.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// The total number of bytes written to the underlying stream, including the final
        /// period terminator sequence.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> or <paramref name="maxLineSize"/> is below
        /// the required minimum.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the encoded output exceeds <paramref name="maxCount"/> and the
        /// overflow policy requires an exception. Also thrown at end‑of‑stream when
        /// <see cref="SizeExceededAction.JunkAndThrowException"/> is used and overflow
        /// occurred.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Lines beginning with a dot (<c>.</c>) are dot‑escaped by prefixing an additional
        /// dot according to SMTP period‑terminated block semantics.
        /// </para>
        /// <para>
        /// The final DATA terminator is always written using strict CRLF sequences:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// If the last processed line ends with CRLF, the terminator <c>.\r\n</c> is written.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// If the last processed line does not end with CRLF (including LF‑only or no
        /// terminator), the sequence <c>\r\n.\r\n</c> is written.
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// All data is written using asynchronous I/O via
        /// <see cref="WriteAsync(System.ReadOnlyMemory{byte}, System.Threading.CancellationToken)"/>.
        /// </para>
        /// </remarks>
        public async ValueTask<long> WritePeriodTerminatedAsync(Stream sourceStream,long maxCount,int maxLineSize,SizeExceededAction exceededAction,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(sourceStream);
            if(maxCount < 8000){
                throw new ArgumentException("Argument 'maxCount' must be >= 8000.");
            }
            if(maxLineSize < 64){
                throw new ArgumentException("Argument 'maxLineSize' must be >= 64.");
            }

            Memory<byte> buffer         = new Memory<byte>(new byte[maxLineSize]);
                         buffer.Span[0] = (byte)'.'; 
            Memory<byte> readBuffer     = buffer.Slice(1); // Reserve first byte for additional '.'.
            bool         exceeded       = false;
            long         bytesStored    = 0;
            bool         lastLineCRLF   = false;
            var          source         = new SmartStream(sourceStream,false);
            while(true){
                ReadLineResult result = await source.ReadLineAsync(readBuffer,exceededAction,cancellationToken).ConfigureAwait(false);

                // We reached end of stream, no more data.
                if(result.BytesInBuffer == 0){
                    if(exceeded){
                        throw new DataSizeExceededException();
                    }
                    
                    if(lastLineCRLF){
                        // Write .CRLF terminator.
                        Memory<byte> crlfDotTerminator = new byte[]{(byte)'.',(byte)'\r',(byte)'\n'};
                        await WriteAsync(crlfDotTerminator,cancellationToken).ConfigureAwait(false);
                        bytesStored += crlfDotTerminator.Length;

                        return bytesStored;
                    }
                    // Last line not including CRLF, add it.
                    else{
                        // Write CRLF.CRLF terminator.
                        Memory<byte> crlfDotTerminator = new byte[]{(byte)'\r',(byte)'\n',(byte)'.',(byte)'\r',(byte)'\n'};
                        await WriteAsync(crlfDotTerminator,cancellationToken).ConfigureAwait(false);
                        bytesStored += crlfDotTerminator.Length;

                        return bytesStored;
                    }
                }

                Memory<byte> lineBuffer;
                // Period handled line. If line starts with period '.', additional period is added.
                if(result.LineBytesInBuffer > 0 && buffer.Span[1] == (byte)'.'){
                    // buffer[0] already contains '.'
                    lineBuffer = buffer.Slice(0,result.BytesInBuffer + 1);
                    
                }
                // Normal line.
                else{
                    lineBuffer = buffer.Slice(1,result.BytesInBuffer);
                }

                if((bytesStored + lineBuffer.Length) <= maxCount){                        
                    await WriteAsync(lineBuffer,cancellationToken).ConfigureAwait(false);
                    bytesStored += lineBuffer.Length;
                }
                // Maximum allowed data to store bytes is exceeded.
                else{                                             
                    exceeded = true;
                    if(exceededAction == SizeExceededAction.ThrowException){
                        throw new DataSizeExceededException();
                    }   
                }

                lastLineCRLF = false;
                if(lineBuffer.Length >= 2 && lineBuffer.Span[lineBuffer.Length - 2] == (byte)'\r' && lineBuffer.Span[lineBuffer.Length - 1] == (byte)'\n'){
                    lastLineCRLF = true;
                }
            }
        }

        #endregion


        #region override method Close

        /// <summary>
        /// Closes current stream and releases any resources.
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public override void Close()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }

            base.Close();

            m_pStream.Close();
        }

        #endregion

        #region override method Flush

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        public override void Flush()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }

            m_pStream.Flush();
        }

        #endregion

        #region override method Seek

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <b>origin</b> parameter.</param>
        /// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        public override long Seek(long offset,SeekOrigin origin)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }

            return m_pStream.Seek(offset,origin);
        }

        #endregion

        #region override method SetLength

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        public override void SetLength(long value)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }

            m_pStream.SetLength(value);

            // Clear read buffer.
            m_ReadBufferOffset = 0;
            m_ReadBufferCount  = 0;
        }

        #endregion
                
        #region override method ReadAsync
        
        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position
        /// within the stream by the number of bytes read.
        /// This overload ensures consistent behavior with the Memory&lt;byte&gt; version,
        /// preventing partial-buffer corruption when used with binary protocols (e.g. SOCKS5).
        /// </summary>
        /// <param name="buffer">The buffer to read data into.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing data.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous read operation.  
        /// The value of the <see cref="Task{TResult}.Result"/> contains the total number of bytes read.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="offset"/> or <paramref name="count"/> is negative,
        /// or when <paramref name="offset"/> + <paramref name="count"/> exceeds the buffer length.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown when the stream has been disposed.</exception>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(GetType().Name);
            }
            if(buffer == null){
                throw new ArgumentNullException(nameof(buffer));
            }
            if(offset < 0 || count < 0){
                throw new ArgumentOutOfRangeException("Offset and count must be non-negative.");
            }
            if(offset + count > buffer.Length){
                throw new ArgumentOutOfRangeException("Offset + count exceeds buffer length.");
            }            

            return ReadAsync(buffer.AsMemory(offset,count),cancellationToken).AsTask();
        }

        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream and advances the position within the stream by the
        /// number of bytes read. The method returns when at least one byte is available or the end of the stream is reached.
        /// </summary>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="ValueTask{Int32}"/> that represents the asynchronous read operation. The value of the TResult parameter
        /// contains the total number of bytes read into the buffer. The result value can be 0 if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
        /// <exception cref="NotSupportedException">The stream does not support reading.</exception>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }

            // Read buffer empty, read data from source stream to read buffer.
            if(this.BytesInReadBuffer == 0){
                int readCount = await m_pStream.ReadAsync(m_pReadBuffer,cancellationToken).ConfigureAwait(false);
                // End of stream reached.
                if(readCount == 0){
                    return 0;
                }

                m_ReadBufferOffset  = 0;
                m_ReadBufferCount   = readCount;
                m_BytesReaded      += readCount;
                m_LastActivity      = DateTime.Now;
            }

            // Copy data from read buffer to user buffer.
            int countToCopy = Math.Min(buffer.Length,this.BytesInReadBuffer);
            m_pReadBuffer.AsMemory().Slice(m_ReadBufferOffset,countToCopy).CopyTo(buffer);
            m_ReadBufferOffset += countToCopy;

            return countToCopy;
        }

        #endregion

        #region override method Read

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>buffer</b> is null reference.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Is raised when any of the arguments has out of valid range.</exception>
        public override int Read(byte[] buffer,int offset,int count)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }
            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }            
            if(offset < 0){
                throw new ArgumentOutOfRangeException("offset","Argument 'offset' value must be >= 0.");
            }
            if(count < 0){
                throw new ArgumentOutOfRangeException("count","Argument 'count' value must be >= 0.");
            }
            if(offset + count > buffer.Length){
                throw new ArgumentOutOfRangeException("count","Argument 'count' is bigger than than argument 'buffer' can store.");
            }

            using var cts = new CancellationTokenSource(m_Timeout);

            return ReadAsync(buffer.AsMemory(offset,count),cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region override method WriteAsync

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the position
        /// within the stream by the number of bytes written.
        /// This overload ensures consistent behavior with the Memory&lt;byte&gt; version,
        /// preventing partial-buffer corruption when used with binary protocols (e.g. SOCKS5).
        /// </summary>
        /// <param name="buffer">The buffer containing the data to write.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin writing data.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous write operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="offset"/> or <paramref name="count"/> is negative,
        /// or when <paramref name="offset"/> + <paramref name="count"/> exceeds the buffer length.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown when the stream has been disposed.</exception>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(GetType().Name);
            }
            if(buffer == null){
                throw new ArgumentNullException(nameof(buffer));
            }
            if(offset < 0 || count < 0){
                throw new ArgumentOutOfRangeException("Offset and count must be non-negative.");
            }
            if(offset + count > buffer.Length){
                throw new ArgumentOutOfRangeException("Offset + count exceeds buffer length.");
            }            

            return WriteAsync(buffer.AsMemory(offset,count),cancellationToken).AsTask();
        }

        /// <summary>
        /// Writes the specified block of bytes into this <see cref="SmartStream"/> asynchronously.
        /// The data is forwarded directly to the underlying stream without buffering,
        /// and the byte counters and activity timestamp are updated accordingly.
        /// </summary>
        /// <param name="buffer">
        /// The read-only memory region containing the bytes to write.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <see cref="SmartStream"/> instance has been disposed.
        /// </exception>
        /// <remarks>
        /// This method performs a raw binary write operation and does not interpret terminators,
        /// line boundaries, or protocol framing.  
        /// The write is delegated to the underlying stream via
        /// <see cref="System.IO.Stream.WriteAsync(System.ReadOnlyMemory{byte}, System.Threading.CancellationToken)"/>,
        /// ensuring that the exact number of bytes in <paramref name="buffer"/> is written.
        /// </remarks>
        public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken = default(CancellationToken))
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }

            await m_pStream.WriteAsync(buffer,cancellationToken).ConfigureAwait(false);
            m_BytesWritten += buffer.Length;
            m_LastActivity  = DateTime.Now;
        }

        #endregion

        #region override method Write

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        public override void Write(byte[] buffer,int offset,int count)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }
            
            using var cts = new CancellationTokenSource(m_Timeout);

            WriteAsync(buffer,offset,count,cts.Token).GetAwaiter().GetResult();
        }

        #endregion
                        

        #region Properties Implementation

        /// <summary>
        /// Gets if this object is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }

        /// <summary>
        /// Gets line buffer size in bytes.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public int LineBufferSize
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_BufferSize; 
            }
        }


        /// <summary>
        /// Gets this stream underlying stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public Stream SourceStream
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_pStream; 
            }
        }

        /// <summary>
        /// Gets if SmartStream is owner of source stream. This property affects like closing this stream will close SourceStream if IsOwner true.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public bool IsOwner
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_IsOwner; 
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                m_IsOwner = value;
            }
        }

        /// <summary>
        /// Gets or sets read/write timeout.
        /// </summary>
        /// <remarks>This timeout applies only synchronous read/write operations.</remarks>
        public int Timeout
        {
            get{ return m_Timeout; }

            set{ m_Timeout = value; }
        }

        /// <summary>
        /// Gets the last time when data was read or written.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public DateTime LastActivity
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_LastActivity; 
            }
        }

        /// <summary>
        /// Gets how many bytes are readed through this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public long BytesReaded
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_BytesReaded; 
            }
        }

        /// <summary>
        /// Gets how many bytes are written through this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public long BytesWritten
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_BytesWritten;
            }
        }

        /// <summary>
        /// Gets number of bytes in read buffer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public int BytesInReadBuffer
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_ReadBufferCount - m_ReadBufferOffset; 
            }
        }

        /// <summary>
        /// Gets or sets string related methods default encoding.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when null value is passed.</exception>
        public Encoding Encoding
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_pEncoding; 
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }
                if(value == null){
                    throw new ArgumentNullException();
                }

                m_pEncoding = value;
            }
        }

        /// <summary>
        /// Gets or sets if only CRLF lines accepted. If false LF lines accepted. 
        /// </summary>
        public bool CRLFLines
        {
                get{ return m_CRLFLines; }

                set { m_CRLFLines = value; }
            }


        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override bool CanRead
        { 
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_pStream.CanRead;
            } 
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override bool CanSeek
        { 
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_pStream.CanSeek;
            } 
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override bool CanWrite
        { 
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_pStream.CanWrite;
            } 
        }

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override long Length
        { 
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_pStream.Length;
            } 
        }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override long Position
        { 
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                return m_pStream.Position;
            } 

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("SmartStream");
                }

                m_pStream.Position = value;

                // Clear read buffer.
                m_ReadBufferOffset = 0;
                m_ReadBufferCount  = 0;
            }
        }

        #endregion


        //------- Obsolete  
                       
        #region override method BeginRead

        /// <summary>
        /// Begins an asynchronous read operation.
        /// </summary>
        /// <param name="buffer">The buffer to read the data into.</param>
        /// <param name="offset">The byte offset in buffer at which to begin writing data read from the stream.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="callback">An optional asynchronous callback, to be called when the read is complete.</param>
        /// <param name="state">A user-provided object that distinguishes this particular asynchronous read request from other requests.</param>
        /// <returns>An IAsyncResult that represents the asynchronous read, which could still be pending.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>buffer</b> is null reference.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Is raised when any of the arguments has out of valid range.</exception>
        public override IAsyncResult BeginRead(byte[] buffer,int offset,int count,AsyncCallback? callback,object? state)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }
            if(offset < 0){
                throw new ArgumentOutOfRangeException("offset","Argument 'offset' value must be >= 0.");
            }
            if(offset > buffer.Length){
                throw new ArgumentOutOfRangeException("offset","Argument 'offset' value must be < buffer.Length.");
            }
            if(count < 0){
                throw new ArgumentOutOfRangeException("count","Argument 'count' value must be >= 0.");
            }
            if(offset + count > buffer.Length){
                throw new ArgumentOutOfRangeException("count","Argument 'count' is bigger than than argument 'buffer' can store.");
            }

            var task = ReadAsync(buffer.AsMemory(offset, count)).AsTask();

            if(callback != null){
                task.ContinueWith(t => callback(t), TaskScheduler.Default);
            }

            return task;
        }

        #endregion

        #region override method EndRead

        /// <summary>
        /// Handles the end of an asynchronous data reading.
        /// </summary>
        /// <param name="asyncResult">The reference to the pending asynchronous request to finish.</param>
        /// <returns>The total number of bytes read into the <b>buffer</b>. This can be less than the number of bytes requested 
        /// if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>asyncResult</b> is null reference.</exception>
        public override int EndRead(IAsyncResult asyncResult)
        {
            if(asyncResult == null){
                throw new ArgumentNullException("asyncResult");
            }

            return ((Task<int>)asyncResult).GetAwaiter().GetResult();       
        }

        #endregion

        #region override method BeginWrite

        /// <summary>
        /// Begins an asynchronous write operation.
        /// </summary>
        /// <param name="buffer">The buffer to write data from.</param>
        /// <param name="offset">The byte offset in buffer from which to begin writing.</param>
        /// <param name="count">The maximum number of bytes to write.</param>
        /// <param name="callback">An optional asynchronous callback, to be called when the write is complete.</param>
        /// <param name="state">A user-provided object that distinguishes this particular asynchronous write request from other requests.</param>
        /// <returns>An IAsyncResult that represents the asynchronous write, which could still be pending.</returns>
        /// /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>buffer</b> is null reference.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Is raised when any of the arguments has out of valid range.</exception>
        public override IAsyncResult BeginWrite(byte[] buffer,int offset,int count,AsyncCallback? callback,object? state)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("SmartStream");
            }
            if(buffer == null){
                throw new ArgumentNullException("buffer");
            }            
            if(offset < 0){
                throw new ArgumentOutOfRangeException("offset","Argument 'offset' value must be >= 0.");
            }
            if(count < 0){
                throw new ArgumentOutOfRangeException("count","Argument 'count' value must be >= 0.");
            }
            if(offset + count > buffer.Length){
                throw new ArgumentOutOfRangeException("count","Argument 'count' is bigger than than argument 'buffer' can store.");
            }

            // Forward APM → TAP
            var task = WriteAsync(buffer.AsMemory(offset, count)).AsTask();

            if(callback != null){
                task.ContinueWith(t => callback(t), TaskScheduler.Default);
            }

            return task;
        }

        #endregion

        #region override method EndWrite

        /// <summary>
        /// Ends an asynchronous write operation.
        /// </summary>
        /// <param name="asyncResult">A reference to the outstanding asynchronous I/O request.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>asyncResult</b> is null reference.</exception>
        public override void EndWrite(IAsyncResult asyncResult)
        {
            if(asyncResult == null){
                throw new ArgumentNullException("asyncResult");
            }

            ((Task)asyncResult).GetAwaiter().GetResult();
        }

        #endregion

    }
}
