using LumiSoft.Net.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Threading;

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
        private Memory<byte>      m_pReadBuffer;
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
            m_pReadBuffer = new Memory<byte>(new byte[m_BufferSize]);

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
            byte lastByte    = 0;
            bool exceeded    = false;
            while(true){                        
                // Read buffer empty, read data from source stream to read buffer.
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

                byte b = m_pReadBuffer.Span[m_ReadBufferOffset++];
                
                // Line buffer full.
                if(exceeded || bytesStored >= buffer.Length){
                    exceeded = true;

                    if(exceededAction == SizeExceededAction.ThrowException){                                
                        throw new LineSizeExceededException();
                    }
                }
                // Store byte.
                else{
                    buffer.Span[bytesStored++] = b;
                }

                // We have LF line.
                if(b == '\n'){
                    if(!this.CRLFLines || this.CRLFLines  && lastByte == '\r'){
                        if(exceeded) {
                            throw new LineSizeExceededException();
                        }

                        return new ReadLineResult(buffer,bytesStored);
                    }                   
                }

                lastByte = b;
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
        /// Reads a period-terminated data block from the underlying stream asynchronously.
        /// The data is read line-by-line using <see cref="ReadLineAsync(Memory{byte}, SizeExceededAction, CancellationToken)"/>
        /// and written into the specified <paramref name="storeStream"/>.
        /// A data block is terminated by a single dot (<c>.</c>) on a line by itself.
        /// </summary>
        /// <param name="storeStream">
        /// The destination stream where the decoded data block will be written.
        /// Lines beginning with a dot (<c>.</c>) have the leading dot removed according to SMTP/POP3 dot-stuffing rules.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of data bytes allowed to be written to <paramref name="storeStream"/>.
        /// Must be at least <c>8000</c>.
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
        /// If set to <see cref="SizeExceededAction.ThrowException"/>, a <see cref="DataSizeExceededException"/> is thrown.
        /// Otherwise, excess data is ignored and the method continues reading until the terminating dot line.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// The total number of bytes written to <paramref name="storeStream"/> after dot-stuffing removal.
        /// This count includes CRLF terminators exactly as they appear in the incoming period-terminated data block.
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
        /// Thrown if the underlying stream ends before a period-terminated block is completed.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the data block exceeds <paramref name="maxCount"/> and
        /// <paramref name="exceededAction"/> is <see cref="SizeExceededAction.ThrowException"/>.
        /// </exception>
        /// <remarks>
        /// This method implements the period-terminated block semantics used by SMTP (<c>DATA</c>)
        /// and POP3 multi-line responses.
        /// Dot-stuffing is automatically removed: lines beginning with <c>.</c> have the leading dot stripped,
        /// except for the terminating line (<c>.</c>).
        /// </remarks>
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

            Memory<byte> buffer      = new Memory<byte>(new byte[maxLineSize]);
            bool         exceeded    = false;
            int          bytesStored = 0;
            while(true){
                ReadLineResult result = await ReadLineAsync(buffer,exceededAction,cancellationToken).ConfigureAwait(false);

                
                Span<byte> bufferSpan  = buffer.Span;
                // We reached end of stream, no more data.
                if(result.BytesInBuffer == 0){
                    throw new IncompleteDataException("Data is not period-terminated.");
                }
                // We have period terminator.
                else if(result.LineBytesInBuffer == 1 && bufferSpan[0] == '.'){
                    if(exceeded){
                        throw new DataSizeExceededException();
                    }

                    return bytesStored;
                }
                // Normal line.
                else{
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
                        exceeded = true;
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

                await storeStream.WriteAsync(readBlock,cancellationToken).ConfigureAwait(false);
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
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(this.BytesInReadBuffer == 0){
                BufferRead(false,null);
            }

            // We are end of stream.
            if(this.BytesInReadBuffer == 0){
                return -1;
            }
            else{
                return m_pReadBuffer.Span[m_ReadBufferOffset];
            }
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
                        return;
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

        #region method WriteHeader

        /// <summary>
        /// Reads header from source <b>stream</b> and writes it to stream.
        /// </summary>
        /// <param name="stream">Stream from where to read header.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null.</exception>
        public void WriteHeader(Stream stream)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            SmartStream reader = new SmartStream(stream,false);
            reader.ReadHeader(this,0,SizeExceededAction.ThrowException);
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
            m_pReadBuffer.Slice(m_ReadBufferOffset,countToCopy).CopyTo(buffer);
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
        private delegate void BufferCallback(Exception? x);

        #region class ReadLineAsyncOperation

        /// <summary>
        /// This class implements asynchronous line reading.
        /// </summary>
        private class ReadLineAsyncOperation : IAsyncResult
        {
            private SmartStream        m_pOwner;
            private byte[]             m_pBuffer;
            private int                m_OffsetInBuffer         = 0;
            private int                m_MaxCount               = 0;
            private SizeExceededAction m_SizeExceededAction     = SizeExceededAction.JunkAndThrowException;
            private AsyncCallback?     m_pAsyncCallback         = null;
            private object?            m_pAsyncState            = null;
            private AutoResetEvent     m_pAsyncWaitHandle;
            private bool               m_CompletedSynchronously = false;
            private bool               m_IsCompleted            = false;
            private bool               m_IsEndCalled            = false;
            private int                m_BytesReaded            = 0;
            private int                m_BytesStored            = 0;
            private Exception?         m_pException             = null;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="owner">Owner stream.</param>
            /// <param name="buffer">Buffer where to store data.</param>
            /// <param name="offset">The location in <b>buffer</b> to begin storing the data.</param>
            /// <param name="maxCount">Maximum number of bytes to read.</param>
            /// <param name="exceededAction">Specifies how this method behaves when maximum line size exceeded.</param>
            /// <param name="callback">The AsyncCallback delegate that is executed when asynchronous operation completes.</param>
            /// <param name="asyncState">User-defined object that qualifies or contains information about an asynchronous operation.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>owner</b>,<b>buffer</b> is null reference.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Is raised when any of the arguments has out of valid range.</exception>
            public ReadLineAsyncOperation(SmartStream owner,byte[] buffer,int offset,int maxCount,SizeExceededAction exceededAction,AsyncCallback? callback,object? asyncState)
            {
                if(owner == null){
                    throw new ArgumentNullException("owner");
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
                if(maxCount < 0){
                    throw new ArgumentOutOfRangeException("maxCount","Argument 'maxCount' value must be >= 0.");
                }
                if(offset + maxCount > buffer.Length){
                    throw new ArgumentOutOfRangeException("maxCount","Argument 'maxCount' is bigger than than argument 'buffer' can store.");
                }

                m_pOwner             = owner;
                m_pBuffer            = buffer;
                m_OffsetInBuffer     = offset;
                m_MaxCount           = maxCount;
                m_SizeExceededAction = exceededAction;
                m_pAsyncCallback     = callback;
                m_pAsyncState        = asyncState;

                m_pAsyncWaitHandle = new AutoResetEvent(false);

                DoLineReading();                                
            }


            #region method Buffering_Completed

            /// <summary>
            /// Is called when asynchronous read buffer buffering has completed.
            /// </summary>
            /// <param name="x">Exception that occured during async operation.</param>
            private void Buffering_Completed(Exception? x)
            {
                if(x != null){
                    m_pException = x;
                    Completed();
                }
                // We reached end of stream, no more data.
                else if(m_pOwner.BytesInReadBuffer == 0){
                    Completed();
                }
                // Continue line reading.
                else{
                    DoLineReading();
                }
            }

            #endregion

            #region method DoLineReading

            /// <summary>
            /// Does line reading.
            /// </summary>
            private void DoLineReading()
            {           
                try{
                    while(true){
                        // Read buffer empty, buff next data block.
                        if(m_pOwner.BytesInReadBuffer == 0){
                            // Buffering started asynchronously.
                            if(m_pOwner.BufferRead(true,this.Buffering_Completed)){
                                return;
                            }
                            // Buffering completed synchronously, continue processing.
                            else{
                                // We reached end of stream, no more data.
                                if(m_pOwner.BytesInReadBuffer == 0){
                                    Completed();
                                    return;
                                }
                            }
                        }

                        var readBuffer = m_pOwner.m_pReadBuffer;
                        byte b = readBuffer.Span[m_pOwner.m_ReadBufferOffset++];
                        m_BytesReaded++;

                        // We have LF line.
                        if(b == '\n'){
                            break;               
                        }
                        // We have CRLF line.
                        else if(b == '\r' && m_pOwner.Peek() == '\n'){
                            // Consume LF char.
                            m_pOwner.ReadByte();
                            m_BytesReaded++;

                            break;
                        }
                        // We have CR line.
                        else if(b == '\r'){
                            break;
                        }
                        // We have normal line data char.
                        else{
                            // Line buffer full.
                            if(m_BytesStored >= m_MaxCount){
                                if(m_SizeExceededAction == SizeExceededAction.ThrowException){
                                    throw new LineSizeExceededException();
                                }
                                // Just skip storing.
                                else{
                                }
                            }
                            else{
                                m_pBuffer[m_OffsetInBuffer++] = b;
                                m_BytesStored++;
                            }
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;                    
                }

                Completed();
            }

            #endregion

            #region method Completed

            /// <summary>
            /// This method must be called when asynchronous operation has completed.
            /// </summary>
            private void Completed()
            {
                m_IsCompleted = true;
                m_pAsyncWaitHandle.Set();
                if(m_pAsyncCallback != null){
                    m_pAsyncCallback(this);
                }
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets a user-defined object that qualifies or contains information about an asynchronous operation.
            /// </summary>
            public object? AsyncState
            {
                get{ return m_pAsyncState; }
            }

            /// <summary>
            /// Gets a WaitHandle that is used to wait for an asynchronous operation to complete.
            /// </summary>
            public WaitHandle AsyncWaitHandle
            {
                get{ return m_pAsyncWaitHandle; }
            }

            /// <summary>
            /// Gets an indication of whether the asynchronous operation completed synchronously.
            /// </summary>
            public bool CompletedSynchronously
            {
                get{ return m_CompletedSynchronously; }
            }

            /// <summary>
            /// Gets an indication whether the asynchronous operation has completed.
            /// </summary>
            public bool IsCompleted
            {
                get{ return m_IsCompleted; }
            }


            /// <summary>
            /// Gets or sets if <b>EndReadLine</b> method is called for this asynchronous operation.
            /// </summary>
            internal bool IsEndCalled
            {
                get{ return m_IsEndCalled; }

                set{ m_IsEndCalled = value; }
            }

            /// <summary>
            /// Gets store buffer.
            /// </summary>
            internal byte[] Buffer
            {
                get{ return m_pBuffer; }
            }

            /// <summary>
            /// Gets number of bytes readed from source stream.
            /// </summary>
            internal int BytesReaded
            {
                get{ return m_BytesReaded; }
            }

            /// <summary>
            /// Gets number of bytes stored in to <b>Buffer</b>.
            /// </summary>
            internal int BytesStored
            {
                get{ return m_BytesStored; }
            }

            #endregion
        }

        #endregion

        #region class ReadToTerminatorAsyncOperation

        /// <summary>
        /// This class implements asynchronous line-based terminated data reader, where terminator is on line itself.
        /// </summary>
        private class ReadToTerminatorAsyncOperation : IAsyncResult
        {
            private SmartStream        m_pOwner;
            private string             m_Terminator             = "";
            private byte[]             m_pTerminatorBytes;
            private Stream             m_pStoreStream;
            private long               m_MaxCount               = 0;            
            private SizeExceededAction m_SizeExceededAction     = SizeExceededAction.JunkAndThrowException;
            private AsyncCallback?     m_pAsyncCallback         = null;
            private object?            m_pAsyncState            = null;
            private AutoResetEvent     m_pAsyncWaitHandle;
            private bool               m_CompletedSynchronously = false;
            private bool               m_IsCompleted            = false;
            private bool               m_IsEndCalled            = false;
            private byte[]             m_pLineBuffer;
            private long               m_BytesStored            = 0;
            private Exception?         m_pException             = null;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="owner">Owner stream.</param>
            /// <param name="terminator">Data terminator.</param>
            /// <param name="storeStream">Stream where to store readed header.</param>
            /// <param name="maxCount">Maximum number of bytes to read. Value 0 means not limited.</param>
            /// <param name="exceededAction">Specifies how this method behaves when maximum line size exceeded.</param>
            /// <param name="callback">The AsyncCallback delegate that is executed when asynchronous operation completes.</param>
            /// <param name="asyncState">User-defined object that qualifies or contains information about an asynchronous operation.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>owner</b>,<b>terminator</b> or <b>storeStream</b> is null reference.</exception>
            public ReadToTerminatorAsyncOperation(SmartStream owner,string terminator,Stream storeStream,long maxCount,SizeExceededAction exceededAction,AsyncCallback? callback,object? asyncState)
            {
                if(owner == null){
                    throw new ArgumentNullException("owner");
                }
                if(terminator == null){
                    throw new ArgumentNullException("terminator");
                }
                if(storeStream == null){
                    throw new ArgumentNullException("storeStream");
                }
                if(maxCount < 0){
                    throw new ArgumentException("Argument 'maxCount' must be >= 0.");
                }

                m_pOwner             = owner;
                m_Terminator         = terminator;
                m_pTerminatorBytes   = Encoding.ASCII.GetBytes(terminator);
                m_pStoreStream       = storeStream;
                m_MaxCount           = maxCount;
                m_SizeExceededAction = exceededAction;
                m_pAsyncCallback     = callback;
                m_pAsyncState        = asyncState;

                m_pAsyncWaitHandle = new AutoResetEvent(false);

                m_pLineBuffer = new byte[32000];

                // Start reading data.
                #pragma warning disable
                m_pOwner.BeginReadLine(m_pLineBuffer,0,m_pLineBuffer.Length - 2,m_SizeExceededAction,new AsyncCallback(this.ReadLine_Completed),null);
                #pragma warning restore
            }


            #region mehtod ReadLine_Completed

            /// <summary>
            /// This method is called when asyynchronous line reading has completed.
            /// </summary>
            /// <param name="asyncResult">An IAsyncResult that represents an asynchronous call.</param>
            private void ReadLine_Completed(IAsyncResult asyncResult)
            {
                try{
                    int storedCount = 0;                    
                    try{
                        #pragma warning disable
                        storedCount = m_pOwner.EndReadLine(asyncResult);
                        #pragma warning restore
                    }
                    catch(LineSizeExceededException){
                        if(m_SizeExceededAction == SizeExceededAction.ThrowException){
                            throw;
                        }
                        m_pException = new LineSizeExceededException();
                        storedCount = 32000 - 2;
                    }

                    // Source stream closed berore we reached terminator.
                    if(storedCount == -1){
                        throw new IncompleteDataException();
                    }

                    // Check for terminator.
                    if(Net_Utils.CompareArray(m_pTerminatorBytes,m_pLineBuffer,storedCount)){
                        Completed();
                    }
                    else{
                        // We have exceeded maximum allowed data count.
                        if(m_MaxCount > 0 && (m_BytesStored + storedCount + 2) > m_MaxCount){
                            if(m_SizeExceededAction == SizeExceededAction.ThrowException){
                                throw new DataSizeExceededException();
                            }
                            // Just skip storing.
                            else{
                                m_pException = new DataSizeExceededException();
                            }
                        }
                        else{
                            // Store readed line.
                            m_pLineBuffer[storedCount++] = (byte)'\r';
                            m_pLineBuffer[storedCount++] = (byte)'\n';
                            m_pStoreStream.Write(m_pLineBuffer,0,storedCount);
                            m_BytesStored += storedCount;                           
                        }

                        // Strart reading new line.
                        #pragma warning disable
                        m_pOwner.BeginReadLine(m_pLineBuffer,0,m_pLineBuffer.Length - 2,m_SizeExceededAction,new AsyncCallback(this.ReadLine_Completed),null);
                        #pragma warning restore
                    }
                }
                catch(Exception x){
                    m_pException = x;
                    Completed();                
                }
            }

            #endregion

            #region method Completed

            /// <summary>
            /// This method must be called when asynchronous operation has completed.
            /// </summary>
            private void Completed()
            {
                m_IsCompleted = true;
                m_pAsyncWaitHandle.Set();
                if(m_pAsyncCallback != null){
                    m_pAsyncCallback(this);
                }
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets terminator.
            /// </summary>
            public string Terminator
            {
                get{ return m_Terminator; }
            }

            /// <summary>
            /// Gets a user-defined object that qualifies or contains information about an asynchronous operation.
            /// </summary>
            public object? AsyncState
            {
                get{ return m_pAsyncState; }
            }

            /// <summary>
            /// Gets a WaitHandle that is used to wait for an asynchronous operation to complete.
            /// </summary>
            public WaitHandle AsyncWaitHandle
            {
                get{ return m_pAsyncWaitHandle; }
            }

            /// <summary>
            /// Gets an indication of whether the asynchronous operation completed synchronously.
            /// </summary>
            public bool CompletedSynchronously
            {
                get{ return m_CompletedSynchronously; }
            }

            /// <summary>
            /// Gets an indication whether the asynchronous operation has completed.
            /// </summary>
            public bool IsCompleted
            {
                get{ return m_IsCompleted; }
            }


            /// <summary>
            /// Gets or sets if <b>EndReadLine</b> method is called for this asynchronous operation.
            /// </summary>
            internal bool IsEndCalled
            {
                get{ return m_IsEndCalled; }

                set{ m_IsEndCalled = value; }
            }

            /// <summary>
            /// Gets number of bytes stored in to <b>storeStream</b>.
            /// </summary>
            internal long BytesStored
            {
                get{ return m_BytesStored; }
            }

            /// <summary>
            /// Gets exception happened on asynchronous operation. Returns null if operation was successfull.
            /// </summary>
            internal Exception? Exception
            {
                get{ return m_pException; }
            }

            #endregion

        }

        #endregion

        #region class ReadToStreamAsyncOperation

        /// <summary>
        /// This class implements asynchronous read to stream data reader.
        /// </summary>
        private class ReadToStreamAsyncOperation : IAsyncResult
        {
            private SmartStream        m_pOwner;
            private Stream             m_pStoreStream;
            private long               m_Count                  = 0;
            private AsyncCallback?     m_pAsyncCallback         = null;
            private object?            m_pAsyncState            = null;
            private AutoResetEvent     m_pAsyncWaitHandle;
            private bool               m_CompletedSynchronously = false;
            private bool               m_IsCompleted            = false;
            private bool               m_IsEndCalled            = false;
            private long               m_BytesStored            = 0;
            private Exception?         m_pException             = null;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="owner">Owner stream.</param>
            /// <param name="storeStream">Stream where to store readed data.</param>
            /// <param name="count">Number of bytes to read from source stream.</param>
            /// <param name="callback">The AsyncCallback delegate that is executed when asynchronous operation completes.</param>
            /// <param name="asyncState">User-defined object that qualifies or contains information about an asynchronous operation.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>owner</b> or <b>storeStream</b> is null reference.</exception>
            /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
            public ReadToStreamAsyncOperation(SmartStream owner,Stream storeStream,long count,AsyncCallback? callback,object? asyncState)
            {
                if(owner == null){
                    throw new ArgumentNullException("owner");
                }
                if(storeStream == null){
                    throw new ArgumentNullException("storeStream");
                }
                if(count < 0){
                    throw new ArgumentException("Argument 'count' must be >= 0.");
                }

                m_pOwner             = owner;
                m_pStoreStream       = storeStream;
                m_Count              = count;
                m_pAsyncCallback     = callback;
                m_pAsyncState        = asyncState;

                m_pAsyncWaitHandle = new AutoResetEvent(false);

                if(m_Count == 0){
                    Completed();                    
                }
                else{
                    DoDataReading();
                }
            }


            #region method Buffering_Completed

            /// <summary>
            /// Is called when asynchronous read buffer buffering has completed.
            /// </summary>
            /// <param name="x">Exception that occured during async operation.</param>
            private void Buffering_Completed(Exception? x)
            {
                if(x != null){
                    m_pException = x;
                    Completed();
                }
                // We reached end of stream, no more data.
                else if(m_pOwner.BytesInReadBuffer == 0){
                    m_pException = new IncompleteDataException();
                    Completed();
                }
                // Continue line reading.
                else{
                    DoDataReading();
                }
            }

            #endregion

            #region method DoReading

            /// <summary>
            /// Does data reading.
            /// </summary>
            private void DoDataReading()
            {
                try{
                    while(true){
                        // Read buffer empty, buff next data block.
                        if(m_pOwner.BytesInReadBuffer == 0){
                            // Buffering started asynchronously.
                            if(m_pOwner.BufferRead(true,this.Buffering_Completed)){
                                return;
                            }
                            // Buffering completed synchronously, continue processing.
                            else{
                                // We reached end of stream, no more data.
                                if(m_pOwner.BytesInReadBuffer == 0){
                                    throw new IncompleteDataException();
                                }
                            }
                        }
                                       
                        int countToRead = (int)Math.Min(m_Count - m_BytesStored,m_pOwner.BytesInReadBuffer);
                        var readBuffer = m_pOwner.m_pReadBuffer;
                        m_pStoreStream.Write(readBuffer.Span.Slice(m_pOwner.m_ReadBufferOffset,countToRead));
                        m_BytesStored += countToRead;
                        m_pOwner.m_ReadBufferOffset += countToRead; 

                        // We have readed all data.
                        if(m_Count == m_BytesStored){
                            Completed();
                            return;
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;
                    Completed();
                }
            }

            #endregion

            #region method Completed

            /// <summary>
            /// This method must be called when asynchronous operation has completed.
            /// </summary>
            private void Completed()
            {
                m_IsCompleted = true;
                m_pAsyncWaitHandle.Set();
                if(m_pAsyncCallback != null){
                    m_pAsyncCallback(this);
                }
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets a user-defined object that qualifies or contains information about an asynchronous operation.
            /// </summary>
            public object? AsyncState
            {
                get{ return m_pAsyncState; }
            }

            /// <summary>
            /// Gets a WaitHandle that is used to wait for an asynchronous operation to complete.
            /// </summary>
            public WaitHandle AsyncWaitHandle
            {
                get{ return m_pAsyncWaitHandle; }
            }

            /// <summary>
            /// Gets an indication of whether the asynchronous operation completed synchronously.
            /// </summary>
            public bool CompletedSynchronously
            {
                get{ return m_CompletedSynchronously; }
            }

            /// <summary>
            /// Gets an indication whether the asynchronous operation has completed.
            /// </summary>
            public bool IsCompleted
            {
                get{ return m_IsCompleted; }
            }


            /// <summary>
            /// Gets or sets if <b>EndReadLine</b> method is called for this asynchronous operation.
            /// </summary>
            internal bool IsEndCalled
            {
                get{ return m_IsEndCalled; }

                set{ m_IsEndCalled = value; }
            }

            /// <summary>
            /// Gets number of bytes stored in to <b>storeStream</b>.
            /// </summary>
            internal long BytesStored
            {
                get{ return m_BytesStored; }
            }

            /// <summary>
            /// Gets exception happened on asynchronous operation. Returns null if operation was successfull.
            /// </summary>
            internal Exception? Exception
            {
                get{ return m_pException; }
            }

            #endregion

        }

        #endregion
        
        #region method BeginReadLine
        
        /// <summary>
        /// Begins an asynchronous line reading from the source stream.
        /// </summary>
        /// <param name="buffer">Buffer where to store readed line data.</param>
        /// <param name="offset">The location in <b>buffer</b> to begin storing the data.</param>
        /// <param name="maxCount">Maximum number of bytes to read.</param>
        /// <param name="exceededAction">Specifies how this method behaves when maximum line size exceeded.</param>
        /// <param name="callback">The AsyncCallback delegate that is executed when asynchronous operation completes.</param>
        /// <param name="state">An object that contains any additional user-defined data.</param>
        /// <returns>An IAsyncResult that represents the asynchronous call.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>buffer</b> is null reference.</exception>
        /// <exception cref="ArgumentOutOfRangeException">is raised when any of the arguments has invalid value.</exception>
        [Obsolete("Use method 'ReadLine' instead.")]
        public IAsyncResult BeginReadLine(byte[] buffer,int offset,int maxCount,SizeExceededAction exceededAction,AsyncCallback callback,object state)
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
            if(maxCount < 0){
                throw new ArgumentOutOfRangeException("maxCount","Argument 'maxCount' value must be >= 0.");
            }
            if(offset + maxCount > buffer.Length){
                throw new ArgumentOutOfRangeException("maxCount","Argument 'maxCount' is bigger than than argument 'buffer' can store.");
            }

            return new ReadLineAsyncOperation(this,buffer,offset,maxCount,exceededAction,callback,state);
        }

        #endregion

        #region method EndReadLine

        /// <summary>
        /// Handles the end of an asynchronous line reading.
        /// </summary>
        /// <param name="asyncResult">An IAsyncResult that represents an asynchronous call.</param>
        /// <returns>Returns number of bytes stored to <b>buffer</b>. Returns -1 if no more data, end of stream reached.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>asyncResult</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when invalid <b>asyncResult</b> passed to this method.</exception>
        /// <exception cref="InvalidOperationException">Is raised when <b>EndReadLine</b> has already been called for specified <b>asyncResult</b>.</exception>
        /// <exception cref="LineSizeExceededException">Is raised when <b>maxCount</b> value is exceeded.</exception>        
        [Obsolete("Use method 'ReadLine' instead.")]
        public int EndReadLine(IAsyncResult asyncResult)
        {
            if(asyncResult == null){
                throw new ArgumentNullException("asyncResult");
            }
            if(!(asyncResult is ReadLineAsyncOperation)){
                throw new ArgumentException("Argument 'asyncResult' was not returned by a call to the BeginReadLine method.");
            }

            ReadLineAsyncOperation ar = (ReadLineAsyncOperation)asyncResult;
            if(ar.IsEndCalled){
                throw new InvalidOperationException("EndReadLine is already called for specified 'asyncResult'.");
            }
            ar.AsyncWaitHandle.WaitOne();
            ar.AsyncWaitHandle.Close();
            ar.IsEndCalled = true;
            
            if(ar.BytesReaded == 0){
                return -1;
            }
            else{
                return ar.BytesStored;
            }
        }

        #endregion
        
        #region method BufferRead

        /// <summary>
        /// Begins buffering read-buffer.
        /// </summary>
        /// <param name="async">If true then this method can complete asynchronously. If false, this method completes always syncronously.</param>
        /// <param name="asyncCallback">The callback that is executed when asynchronous operation completes. 
        /// If operation completes synchronously, no callback called.</param>
        /// <returns>
        /// Returns true if asynchronous operation in progress or false if operation completed synchronously. 
        /// </returns>
        /// <exception cref="InvalidOperationException">Is raised when there is data in read buffer and this method is called.</exception>
        private bool BufferRead(bool async,BufferCallback? asyncCallback)
        {
            if(this.BytesInReadBuffer != 0){
                throw new InvalidOperationException("There is already data in read buffer.");
            }

            #region async

            if(async){
                _ = Task.Run(async () => {
                    Exception? error = null;

                    try{
                        int countReaded = await m_pStream.ReadAsync(m_pReadBuffer, CancellationToken.None);

                        m_ReadBufferOffset = 0;
                        m_ReadBufferCount  = countReaded;
                        m_BytesReaded      += countReaded;
                        m_LastActivity     = DateTime.Now;
                    }
                    catch(Exception e){
                        error = e;
                    }

                    asyncCallback?.Invoke(error);
                });
                
                return true;
            }

            #endregion

            #region sync

            else{
                int countReaded =  m_pStream.ReadAsync(m_pReadBuffer,CancellationToken.None).GetAwaiter().GetResult();
                m_ReadBufferOffset = 0;
                m_ReadBufferCount  =  countReaded;
                m_BytesReaded      += countReaded;
                m_LastActivity     =  DateTime.Now;

                return false;
            }

            #endregion                        
        }

        #endregion
        
        #region class ReadLineAsyncOP

        /// <summary>
        /// This class implements read line operation.
        /// </summary>
        /// <remarks>This class can be reused on multiple calls of <see cref="SmartStream.ReadLine(ReadLineAsyncOP,bool)">SmartStream.ReadLine</see> method.</remarks>
        public class ReadLineAsyncOP : IDisposable,IAsyncOP
        {
            private object             m_pLock           = new object(); 
            private AsyncOP_State      m_State           = AsyncOP_State.WaitingForStart;
            private Exception?         m_pException      = null;
            private bool               m_RiseCompleted   = false;
            private SmartStream?       m_pOwner          = null;
            private byte[]             m_pBuffer;
            private SizeExceededAction m_ExceededAction  = SizeExceededAction.JunkAndThrowException;
            private int                m_BytesInBuffer   = 0;
            private int                m_LastByte        = -1;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="buffer">Line buffer.</param>
            /// <param name="exceededAction">Specifies how line-reader behaves when maximum line size exceeded.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>buffer</b> is null reference.</exception>
            public ReadLineAsyncOP(byte[] buffer,SizeExceededAction exceededAction)
            {
                if(buffer == null){
                    throw new ArgumentNullException("buffer");
                }

                m_pBuffer        = buffer;
                m_ExceededAction = exceededAction;
            }

            /// <summary>
            /// Destructor.
            /// </summary>
            ~ReadLineAsyncOP()
            {
                Dispose();
            }

            #region method Dispose

            /// <summary>
            /// Cleans up any resources being used.
            /// </summary>
            public void Dispose()
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }

                m_State             = AsyncOP_State.Disposed;
                m_pOwner            = null;
                m_pException        = null;
                this.CompletedAsync = null;
                this.Completed      = null;
            }

            #endregion


            #region method Start

            /// <summary>
            /// Starts asynchronous operation.
            /// </summary>
            /// <param name="async">If true then this method can complete asynchronously. If false, this method always completes syncronously.</param>
            /// <param name="stream">Owner SmartStream.</param>
            /// <returns>Returns true if asynchronous operation in progress or false if operation completed synchronously.</returns>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
            /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null reference.</exception>
            internal bool Start(bool async,SmartStream stream)
            {   
                if(m_State == AsyncOP_State.Disposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(m_State == AsyncOP_State.Active){
                    throw new InvalidOperationException("There is existing active operation. There may be only one active operation at same time.");
                }
                if(stream == null){
                    throw new ArgumentNullException("stream");
                }
                
                m_pOwner        = stream;
                m_State         = AsyncOP_State.Active;
                m_RiseCompleted = false;
                m_pException    = null;
                m_BytesInBuffer = 0;
                m_LastByte      = -1;
   
                if(DoLineReading(async)){
                    SetState(AsyncOP_State.Completed);
                }

                // Set flag rise CompletedAsync event flag. The event is raised when async op completes.
                // If already completed sync, that flag has no effect.
                lock(m_pLock){
                    m_RiseCompleted = true;
                    
                    return m_State == AsyncOP_State.Active;
                }
            }

            #endregion


            #region method Buffering_Completed

            /// <summary>
            /// Is called when asynchronous read buffer buffering has completed.
            /// </summary>
            /// <param name="x">Exception that occured during async operation. Value null means no errors.</param>
            private void Buffering_Completed(Exception? x)
            {
                bool setCompletedState = false;

                try{
                    ArgumentNullException.ThrowIfNull(m_pOwner);

                    if (x != null){
                        m_pException = x;
                        
                        setCompletedState = true;
                    }
                    // We reached end of stream, no more data.
                    else if(m_pOwner.BytesInReadBuffer == 0){
                        setCompletedState = true;
                    }
                    // Continue line reading.
                    else{
                        if(DoLineReading(true)){
                            setCompletedState = true;
                        }
                    }
                }
                catch(Exception e){
                    m_pException = e;

                    setCompletedState = true;
                }

                // SetState may not be in try/catch. If CompletedAsync event consumer causes unhandled Exception,
                // we may not catch it, we need to let it happen on active thread.
                if(setCompletedState){
                    SetState(AsyncOP_State.Completed);
                }
            }

            #endregion

            #region method DoLineReading

            /// <summary>
            /// Starts/continues line reading.
            /// </summary>
            /// <param name="async">If true then this method can complete asynchronously. If false, this method completes always syncronously.</param>
            /// <returns>Returns true if line reading has completed.</returns>
            private bool DoLineReading(bool async)
            {
                try{
                    ArgumentNullException.ThrowIfNull(m_pOwner);

                    while (true){                        
                        // Read buffer empty, buff next data block.
                        if(m_pOwner.BytesInReadBuffer == 0){
                            // Buffering started asynchronously.
                            if(m_pOwner.BufferRead(async,this.Buffering_Completed)){
                                return false;
                            }
                            // Buffering completed synchronously, continue processing.
                            else{
                                // We reached end of stream, no more data.
                                if(m_pOwner.BytesInReadBuffer == 0){                                    
                                    return true;
                                }
                            }
                        }

                        var readBuffer = m_pOwner.m_pReadBuffer;
                        byte b = readBuffer.Span[m_pOwner.m_ReadBufferOffset++];
                        
                        // Line buffer full.
                        if(m_BytesInBuffer >= m_pBuffer.Length){
                            if(m_pException == null){
                                m_pException = new LineSizeExceededException();
                            }

                            if(m_ExceededAction == SizeExceededAction.ThrowException){                                
                                return true;
                            }
                        }
                        // Store byte.
                        else{
                            m_pBuffer[m_BytesInBuffer++] = b;
                        }

                        // We have LF line.
                        if(b == '\n'){
                            if(!m_pOwner.CRLFLines || m_pOwner.CRLFLines  && m_LastByte == '\r'){
                                return true;
                            }                   
                        }

                        m_LastByte = b;
                    }
                }
                catch(Exception x){
                    m_pException = x;
                }

                return true;
            }

            #endregion


            #region method SetState

            /// <summary>
            /// Sets operation state.
            /// </summary>
            /// <param name="state">New state.</param>
            private void SetState(AsyncOP_State state)
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }
            
                // Note: Get riseCompleted in lock, otherwise we get race condition Start method m_RiseCompleted = true.
                bool riseCompleted = m_RiseCompleted;
                lock(m_pLock){
                    m_State = state;
                    riseCompleted = m_RiseCompleted;
                }

                if(m_State == AsyncOP_State.Completed && riseCompleted){
                    OnCompletedAsync();
                }
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets asynchronous operation state.
            /// </summary>
            public AsyncOP_State State
            {
                get{ return m_State; }
            }

            /// <summary>
            /// Gets error occured during asynchronous operation. Value null means no error.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public Exception? Error
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_pException; 
                }
            }

            /// <summary>
            /// Gets line size exceeded action.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public SizeExceededAction SizeExceededAction
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_ExceededAction;
                }
            }

            /// <summary>
            /// Gets line buffer.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public byte[] Buffer
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_pBuffer; 
                }
            }

            /// <summary>
            /// Gets number of bytes stored in the buffer. Ending line-feed characters included.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public int BytesInBuffer
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_BytesInBuffer; 
                }
            }

            /// <summary>
            /// Gets number of line data bytes stored in the buffer. Ending line-feed characters not included.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public int LineBytesInBuffer
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    int retVal = m_BytesInBuffer;

                    if(m_BytesInBuffer > 1){
                        if(m_pBuffer[m_BytesInBuffer - 1] == '\n'){
                            retVal--;
                            if(m_pBuffer[m_BytesInBuffer - 2] == '\r'){
                                retVal--;
                            }
                        }
                    }
                    else if(m_BytesInBuffer > 0){
                        if(m_pBuffer[m_BytesInBuffer - 1] == '\n'){
                            retVal--;
                        }
                    }

                    return retVal; 
                }
            }

            /// <summary>
            /// Gets line as ASCII string. Returns null if EOS(end of stream) reached. Ending line-feed characters not included.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public string? LineAscii
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    if(this.BytesInBuffer == 0){
                        return null;
                    }
                    else{
                        return Encoding.ASCII.GetString(m_pBuffer,0,this.LineBytesInBuffer); 
                    }
                }
            }

            /// <summary>
            /// Gets line as UTF-8 string. Returns null if EOS(end of stream) reached. Ending line-feed characters not included.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public string? LineUtf8
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    if(this.BytesInBuffer == 0){
                        return null;
                    }
                    else{
                        return Encoding.UTF8.GetString(m_pBuffer,0,this.LineBytesInBuffer);
                    }
                }
            }

            /// <summary>
            /// Gets line as UTF-32 string. Returns null if EOS(end of stream) reached. Ending line-feed characters not included.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public string? LineUtf32
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    if(this.BytesInBuffer == 0){
                        return null;
                    }
                    else{
                        return Encoding.UTF32.GetString(m_pBuffer,0,this.LineBytesInBuffer);
                    }
                }
            }

            #endregion

            #region Events implementation

            /// <summary>
            /// Is called when asynchronous operation has completed.
            /// </summary>
            public event EventHandler<EventArgs<ReadLineAsyncOP>>? CompletedAsync = null;

            #region method OnCompletedAsync

            /// <summary>
            /// Raises <b>CompletedAsync</b> event.
            /// </summary>
            private void OnCompletedAsync()
            {
                if(this.CompletedAsync != null){
                    this.CompletedAsync(this,new EventArgs<ReadLineAsyncOP>(this));
                }

                // For obsolete support.
                if(this.Completed != null){
                    this.Completed(this,new EventArgs<ReadLineAsyncOP>(this));
                }
            }

            #endregion

            #endregion


            //---------- Obsolete stuff

            #region Obsolete

            /// <summary>
            /// Is called when asynchronous operation has completed.
            /// </summary>
            [Obsolete("Use CompletedAsync event istead.")]
            public event EventHandler<EventArgs<ReadLineAsyncOP>>? Completed = null;

            #endregion
        }

        #endregion
                         
        #region method ReadLine

        /// <summary>
        /// Starts line reading.
        /// </summary>
        /// <param name="op">Read line opeartion.</param>
        /// <param name="async">If true then this method can complete asynchronously. If false, this method completes always syncronously.</param>
        /// <returns>Returns true if read line completed synchronously, false if asynchronous operation pending.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>op</b> is null reference.</exception>
        public bool ReadLine(ReadLineAsyncOP op,bool async)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(op == null){
                throw new ArgumentNullException("op");
            }

            return !op.Start(async,this);
        }

        #endregion
        
        #region method BeginReadHeader

        /// <summary>
        /// Begins an asynchronous header reading from the source stream.
        /// </summary>
        /// <param name="storeStream">Stream where to store readed header.</param>
        /// <param name="maxCount">Maximum number of bytes to read. Value 0 means not limited.</param>
        /// <param name="exceededAction">Specifies action what is done if <b>maxCount</b> number of bytes has exceeded.</param>
        /// <param name="callback">The AsyncCallback delegate that is executed when asynchronous operation completes.</param>
        /// <param name="state">An object that contains any additional user-defined data.</param>
        /// <returns>An IAsyncResult that represents the asynchronous call.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>storeStream</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public IAsyncResult BeginReadHeader(Stream storeStream,int maxCount,SizeExceededAction exceededAction,AsyncCallback? callback,object? state)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(storeStream == null){
                throw new ArgumentNullException("storeStream");
            }
            if(maxCount < 0){
                throw new ArgumentException("Argument 'maxCount' must be >= 0.");
            }

            return new ReadToTerminatorAsyncOperation(this,"",storeStream,maxCount,exceededAction,callback,state);
        }

        #endregion

        #region method EndReadHeader

        /// <summary>
        /// Handles the end of an asynchronous header reading.
        /// </summary>
        /// <param name="asyncResult">An IAsyncResult that represents an asynchronous call.</param>
        /// <returns>Returns number of bytes stored to <b>storeStream</b>.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>asyncResult</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when invalid <b>asyncResult</b> passed to this method.</exception>
        /// <exception cref="InvalidOperationException">Is raised when <b>EndReadLine</b> has already been called for specified <b>asyncResult</b>.</exception>
        /// <exception cref="LineSizeExceededException">Is raised when source stream has too big line.</exception>
        /// <exception cref="DataSizeExceededException">Is raised when reading exceeds <b>maxCount</b> specified value.</exception>
        /// <exception cref="IncompleteDataException">Is raised when source stream closed before header-terminator reached.</exception>
        public int EndReadHeader(IAsyncResult asyncResult)
        {
            if(asyncResult == null){
                throw new ArgumentNullException("asyncResult");
            }
            if(!(asyncResult is ReadToTerminatorAsyncOperation)){
                throw new ArgumentException("Argument 'asyncResult' was not returned by a call to the BeginReadHeader method.");
            }

            ReadToTerminatorAsyncOperation ar = (ReadToTerminatorAsyncOperation)asyncResult;
            if(ar.IsEndCalled){
                throw new InvalidOperationException("EndReadHeader is already called for specified 'asyncResult'.");
            }
            ar.AsyncWaitHandle.WaitOne();
            ar.AsyncWaitHandle.Close();
            ar.IsEndCalled = true;
            if(ar.Exception != null){
                throw ar.Exception;
            }

            return (int)ar.BytesStored;
        }

        #endregion

        #region method BeginReadFixedCount

        /// <summary>
        /// Begins an asynchronous data reading from the source stream.
        /// </summary>
        /// <param name="storeStream">Stream where to store readed header.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <param name="callback">The AsyncCallback delegate that is executed when asynchronous operation completes.</param>
        /// <param name="state">An object that contains any additional user-defined data.</param>
        /// <returns>An IAsyncResult that represents the asynchronous call.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>storeStream</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public IAsyncResult BeginReadFixedCount(Stream storeStream,long count,AsyncCallback? callback,object? state)
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

            return new ReadToStreamAsyncOperation(this,storeStream,count,callback,state);
        }

        #endregion

        #region method EndReadFixedCount

        /// <summary>
        /// Handles the end of an asynchronous data reading.
        /// </summary>
        /// <param name="asyncResult">An IAsyncResult that represents an asynchronous call.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>asyncResult</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when invalid <b>asyncResult</b> passed to this method.</exception>
        /// <exception cref="InvalidOperationException">Is raised when <b>EndReadToStream</b> has already been called for specified <b>asyncResult</b>.</exception>
        public void EndReadFixedCount(IAsyncResult asyncResult)
        {
            if(asyncResult == null){
                throw new ArgumentNullException("asyncResult");
            }
            if(!(asyncResult is ReadToStreamAsyncOperation)){
                throw new ArgumentException("Argument 'asyncResult' was not returned by a call to the BeginReadFixedCount method.");
            }

            ReadToStreamAsyncOperation ar = (ReadToStreamAsyncOperation)asyncResult;
            if(ar.IsEndCalled){
                throw new InvalidOperationException("EndReadFixedCount is already called for specified 'asyncResult'.");
            }
            ar.AsyncWaitHandle.WaitOne();
            ar.AsyncWaitHandle.Close();
            ar.IsEndCalled = true;
            if(ar.Exception != null){
                throw ar.Exception;
            }
        }

        #endregion

        #region class ReadPeriodTerminatedAsyncOP

        /// <summary>
        /// This class implements read period-terminated operation.
        /// </summary>
        public class ReadPeriodTerminatedAsyncOP : IDisposable,IAsyncOP
        {
            private object             m_pLock           = new object(); 
            private AsyncOP_State      m_State           = AsyncOP_State.WaitingForStart;
            private Exception?         m_pException      = null;
            private bool               m_RiseCompleted   = false;
            private SmartStream?       m_pOwner          = null;
            private Stream             m_pStream;
            private long               m_MaxCount        = 0;
            private SizeExceededAction m_ExceededAction  = SizeExceededAction.JunkAndThrowException;
            private ReadLineAsyncOP    m_pReadLineOP;
            private long               m_BytesStored     = 0;
            private int                m_LinesStored     = 0;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="stream">Stream wehre to sore readed data.</param>
            /// <param name="maxCount">Maximum number of bytes to read. Value 0 means not limited.</param>
            /// <param name="exceededAction">Specifies how period-terminated reader behaves when <b>maxCount</b> exceeded.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null reference.</exception>
            public ReadPeriodTerminatedAsyncOP(Stream stream,long maxCount,SizeExceededAction exceededAction)
            {
                if(stream == null){
                    throw new ArgumentNullException("stream");
                }
                if(maxCount < 0){
                    throw new ArgumentException("Argument 'maxCount' must be >= 0.","maxCount");
                }

                m_pStream        = stream;
                m_MaxCount       = maxCount;
                m_ExceededAction = exceededAction;

                m_pReadLineOP = new ReadLineAsyncOP(new byte[32000],exceededAction);
                m_pReadLineOP.CompletedAsync += new EventHandler<EventArgs<ReadLineAsyncOP>>(m_pReadLineOP_CompletedAsync);
            }

            /// <summary>
            /// Destructor.
            /// </summary>
            ~ReadPeriodTerminatedAsyncOP()
            {
                Dispose();
            }

            #region method Dispose

            /// <summary>
            /// Cleans up any resources being used.
            /// </summary>
            public void Dispose()
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }

                m_State             = AsyncOP_State.Disposed;
                m_pOwner            = null;
                m_pReadLineOP.Dispose();
                m_pException        = null;
                this.CompletedAsync = null;
                this.Completed      = null;
            }

            #endregion


            #region method Start

            /// <summary>
            /// Starts asynchronous operation.
            /// </summary>
            /// <param name="async">If true then this method can complete asynchronously. If false, this method always completes syncronously.</param>
            /// <param name="stream">Owner SmartStream.</param>
            /// <returns>Returns true if asynchronous operation in progress or false if operation completed synchronously.</returns>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
            /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null reference.</exception>
            /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
            internal bool Start(bool async,SmartStream stream)
            {
                if(m_State == AsyncOP_State.Disposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(m_State == AsyncOP_State.Active){
                    throw new InvalidOperationException("There is existing active operation. There may be only one active operation at same time.");
                }
                if(stream == null){
                    throw new ArgumentNullException("stream");
                }

                m_pOwner        = stream;
                m_State         = AsyncOP_State.Active;
                m_RiseCompleted = false;
                m_pException    = null;
                m_BytesStored   = 0;
                m_LinesStored   = 0;

                if(DoRead(async)){
                    SetState(AsyncOP_State.Completed);
                }
   
                // Set flag rise CompletedAsync event flag. The event is raised when async op completes.
                // If already completed sync, that flag has no effect.
                lock(m_pLock){
                    m_RiseCompleted = true;
                    
                    return m_State == AsyncOP_State.Active;
                }
            }

            #endregion


            #region method m_pReadLineOP_CompletedAsync

            /// <summary>
            /// Is called when asynchronous line reading has completed.
            /// </summary>
            /// <param name="sender">Sender.</param>
            /// <param name="e">Event data.</param>
            private void m_pReadLineOP_CompletedAsync(object? sender,EventArgs<ReadLineAsyncOP> e)
            {
                bool setCompletedState = false;

                try{
                    if(ProcessReadedLine()){
                        setCompletedState = true;
                    }
                    else{
                        if(DoRead(true)){
                            setCompletedState = true;
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;

                    setCompletedState = true;
                }

                // SetState may not be in try/catch. If CompletedAsync event consumer causes unhandled Exception,
                // we may not catch it, we need to let it happen on active thread.
                if(setCompletedState){
                    SetState(AsyncOP_State.Completed);
                }
            }

            #endregion

            #region method DoRead

            /// <summary>
            /// Continues period-terminated reading.
            /// </summary>
            /// <param name="async">If true then this method can complete asynchronously. If false, this method completes always syncronously.</param>
            /// <returns>Returns true if operation has completed synchronously, false if asynchronous operation pending.</returns>
            private bool DoRead(bool async)
            {
                try{
                    ArgumentNullException.ThrowIfNull(m_pOwner);

                    while (m_pOwner.ReadLine(m_pReadLineOP,async)){
                        if(ProcessReadedLine()){
                            return true;
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;
                }

                return false;
            }

            #endregion

            #region method ProcessReadedLine

            /// <summary>
            /// Processes readed line.
            /// </summary>
            /// <returns>Returns true if read period-terminated operation has completed.</returns>
            private bool ProcessReadedLine()
            {
                if(m_pReadLineOP.Error != null){
                    m_pException = m_pReadLineOP.Error;

                    return true;
                }
                // We reached end of stream, no more data.
                else if(m_pReadLineOP.BytesInBuffer == 0){
                    m_pException = new IncompleteDataException("Data is not period-terminated.");

                    return true;
                }
                // We have period terminator.
                else if(m_pReadLineOP.LineBytesInBuffer == 1 && m_pReadLineOP.Buffer[0] == '.'){
                    return true;
                }
                // Normal line.
                else{
                    if(m_MaxCount < 1 || (m_BytesStored + m_pReadLineOP.BytesInBuffer) < m_MaxCount){
                        // Period handling: If line starts with '.', it must be removed.
                        if(m_pReadLineOP.Buffer[0] == '.'){
                            m_pStream.Write(m_pReadLineOP.Buffer,1,m_pReadLineOP.BytesInBuffer - 1);
                            m_BytesStored += m_pReadLineOP.BytesInBuffer - 1;
                            m_LinesStored++;
                        }
                        // Nomrmal line.
                        else{
                            m_pStream.Write(m_pReadLineOP.Buffer,0,m_pReadLineOP.BytesInBuffer);
                            m_BytesStored += m_pReadLineOP.BytesInBuffer;
                            m_LinesStored++;
                        }                        
                    }
                    // Maximum allowed to store bytes exceeded.
                    else{
                        if(m_ExceededAction == SizeExceededAction.ThrowException){
                            m_pException = new DataSizeExceededException();

                            return true;
                        }
                        else if(m_pException == null){
                            m_pException = new DataSizeExceededException();
                        }
                    }
                }

                return false;
            }

            #endregion


            #region method SetState

            /// <summary>
            /// Sets operation state.
            /// </summary>
            /// <param name="state">New state.</param>
            private void SetState(AsyncOP_State state)
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }
            
                // Note: Get riseCompleted in lock, otherwise we get race condition Start method m_RiseCompleted = true.
                bool riseCompleted = m_RiseCompleted;
                lock(m_pLock){
                    m_State = state;
                    riseCompleted = m_RiseCompleted;
                }

                if(m_State == AsyncOP_State.Completed && riseCompleted){
                    OnCompletedAsync();
                }
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets asynchronous operation state.
            /// </summary>
            public AsyncOP_State State
            {
                get{ return m_State; }
            }

            /// <summary>
            /// Gets error occured during asynchronous operation. Value null means no error.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public Exception? Error
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_pException; 
                }
            }

            /// <summary>
            /// Gets stream where period terminated data has stored.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public Stream Stream
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_pStream; 
                }
            }

            /// <summary>
            /// Gets number of bytes stored to <see cref="Stream">Stream</see> stream.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public long BytesStored
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_BytesStored; 
                }
            }

            /// <summary>
            /// Gets number of lines stored to <see cref="Stream">Stream</see> stream.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
            public int LinesStored
            {
                get{
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }                    
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("This property is only valid in AsyncOP_State.Completed state.");
                    }

                    return m_LinesStored; 
                }
            }

            #endregion

            #region Events implementation

            /// <summary>
            /// Is raised when asynchronous operation has completed.
            /// </summary>
            public event EventHandler<EventArgs<ReadPeriodTerminatedAsyncOP>>? CompletedAsync = null;

            #region method OnCompletedAsync

            /// <summary>
            /// Raises <b>CompletedAsync</b> event.
            /// </summary>
            private void OnCompletedAsync()
            {
                if(this.CompletedAsync != null){
                    this.CompletedAsync(this,new EventArgs<ReadPeriodTerminatedAsyncOP>(this));
                }

                // For obsolete support.
                if(this.Completed != null){
                    this.Completed(this,new EventArgs<ReadPeriodTerminatedAsyncOP>(this));
                }
            }

            #endregion

            #endregion


            //---------- Obsolete stuff

            #region Obsolete

            /// <summary>
            /// Is called when asynchronous operation has completed.
            /// </summary>
            [Obsolete("Use CompletedAsync event istead.")]
            public event EventHandler<EventArgs<ReadPeriodTerminatedAsyncOP>>? Completed = null;

            #endregion
        }

        #endregion

        #region method ReadPeriodTerminated

        /// <summary>
        /// Begins period-terminated data reading.
        /// </summary>
        /// <param name="op">Read period terminated opeartion.</param>
        /// <param name="async">If true then this method can complete asynchronously. If false, this method completed always syncronously.</param>
        /// <returns>Returns true if read line completed synchronously, false if asynchronous operation pending.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>op</b> is null reference.</exception>
        public bool ReadPeriodTerminated(ReadPeriodTerminatedAsyncOP op,bool async)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(op == null){
                throw new ArgumentNullException("op");
            }

            return !op.Start(async,this);
        }

        #endregion
                
        #region method ReadHeader

        /// <summary>
        /// Reads header from stream and stores to the specified <b>storeStream</b>.
        /// </summary>
        /// <param name="storeStream">Stream where to store readed header.</param>
        /// <param name="maxCount">Maximum number of bytes to read. Value 0 means not limited.</param>
        /// <param name="exceededAction">Specifies action what is done if <b>maxCount</b> number of bytes has exceeded.</param>
        /// <returns>Returns how many bytes readed from source stream.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>storeStream</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        /// <exception cref="LineSizeExceededException">Is raised when source stream has too big line.</exception>
        /// <exception cref="DataSizeExceededException">Is raised when reading exceeds <b>maxCount</b> specified value.</exception>
        /// <exception cref="IncompleteDataException">Is raised when source stream closed before header-terminator reached.</exception>
        public int ReadHeader(Stream storeStream,int maxCount,SizeExceededAction exceededAction)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(storeStream == null){
                throw new ArgumentNullException("storeStream");
            }            
            if(maxCount < 0){
                throw new ArgumentException("Argument 'maxCount' must be >= 0.");
            }

            IAsyncResult ar = BeginReadHeader(storeStream,maxCount,exceededAction,null,null);

            return EndReadHeader(ar);
        }

        #endregion        
                       
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

        #region method WriteStreamAsync

        #region class WriteStreamAsyncOP

        /// <summary>
        /// This class represents SmartStream.WriteStreamAsync asynchronous operation.
        /// </summary>
        public class WriteStreamAsyncOP : IDisposable,IAsyncOP
        {
            private object        m_pLock         = new object();
            private AsyncOP_State m_State         = AsyncOP_State.WaitingForStart;
            private Exception?    m_pException    = null;
            private bool          m_RiseCompleted = false;
            private SmartStream?  m_pOwner        = null;
            private Stream        m_pStream;
            private long          m_Count         = 0;
            private byte[]        m_pBuffer;
            private long          m_BytesWritten  = 0;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="stream">Stream which data to write.</param>
            /// <param name="count">Number of bytes to write. Value -1 means all stream data will be written.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null reference.</exception>
            public WriteStreamAsyncOP(Stream stream,long count)
            {
                if(stream == null){
                    throw new ArgumentNullException("stream");
                }

                m_pStream = stream;
                m_Count   = count;
                m_pBuffer = new byte[32000];
            }

            #region method Dispose

            /// <summary>
            /// Cleans up any resources being used.
            /// </summary>
            public void Dispose()
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }
                SetState(AsyncOP_State.Disposed);
                
                m_pException = null;
                m_pOwner     = null;

                this.CompletedAsync = null;
            }

            #endregion


            #region method Start

            /// <summary>
            /// Starts operation processing.
            /// </summary>
            /// <param name="owner">Owner SmartStream.</param>
            /// <returns>Returns true if asynchronous operation in progress or false if operation completed synchronously.</returns>
            /// <exception cref="ArgumentNullException">Is raised when <b>owner</b> is null reference.</exception>
            internal bool Start(SmartStream owner)
            {
                if(owner == null){
                    throw new ArgumentNullException("owner");
                }
                
                m_pOwner = owner;

                SetState(AsyncOP_State.Active);
                
                BeginReadData();

                // Set flag rise CompletedAsync event flag. The event is raised when async op completes.
                // If already completed sync, that flag has no effect.
                lock(m_pLock){
                    m_RiseCompleted = true;

                    return m_State == AsyncOP_State.Active;
                }
            }

            #endregion


            #region method SetState

            /// <summary>
            /// Sets operation state.
            /// </summary>
            /// <param name="state">New state.</param>
            private void SetState(AsyncOP_State state)
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }

                // Note: Get riseCompleted in lock, otherwise we get race condition Start method m_RiseCompleted = true.
                bool riseCompleted = m_RiseCompleted;
                lock(m_pLock){
                    m_State = state;
                    riseCompleted = m_RiseCompleted;
                }

                if(m_State == AsyncOP_State.Completed && riseCompleted){
                    OnCompletedAsync();
                }
            }

            #endregion

            #region method BeginReadData

            /// <summary>
            /// Starts reading data.
            /// </summary>
            private void BeginReadData()
            {                
                try{
                    while(true){
                        bool isBeginReadCompleted = false;
                        bool isCompletedSync      = false;
                        int count = m_Count == -1 ? m_pBuffer.Length : (int)Math.Min(m_pBuffer.Length,m_Count - m_BytesWritten);
                        IAsyncResult readResult = m_pStream.BeginRead(
                            m_pBuffer,
                            0,
                            count,
                            delegate(IAsyncResult r){
                                lock(m_pLock){
                                    // BeginRead completed synchronously.
                                    if(!isBeginReadCompleted){
                                        isCompletedSync = true;
                                        return;
                                    }
                                }

                                ProcessReadDataResult(r);
                            },
                            null
                        );

                        lock(m_pLock){
                            isBeginReadCompleted = true;
                        }

                        // Read data completed synchonously.
                        if(isCompletedSync){
                            // Operation completed asynchronously, it will continue processing.
                            if(ProcessReadDataResult(readResult)){
                                break;
                            }
                            // Error happened in ProcessReadDataResult method.
                            if(this.State != AsyncOP_State.Active){
                                break;
                            }
                        }
                        // Read data completed asynchonously.
                        else{
                            break;
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;
                    SetState(AsyncOP_State.Completed);
                }
            }

            #endregion

            #region method ProcessReadDataResult

            /// <summary>
            /// Processes read data result.
            /// </summary>
            /// <param name="readResult">Asynchronous result.</param>
            /// <returns>Retruns true if this method completed asynchronously, otherwise false.</returns>
            private bool ProcessReadDataResult(IAsyncResult readResult)
            {
                try{
                    ArgumentNullException.ThrowIfNull(m_pOwner);

                    int countReaded = m_pStream.EndRead(readResult);
                    if(countReaded == 0){
                        // We readed all stream data and write count not specified, we are done.
                        if(m_Count == -1){
                            SetState(AsyncOP_State.Completed);
                        }
                        // Source stream has less data than specified by count.
                        else{
                            m_pException = new ArgumentException("Argument 'stream' has less data than specified in 'count'.","stream");
                            SetState(AsyncOP_State.Completed);
                        }
                    }
                    else{
                        bool isBeginWriteCompleted = false;
                        bool isCompletedSync       = false;
                        IAsyncResult writeResult = m_pOwner.BeginWrite(
                            m_pBuffer,
                            0,
                            countReaded,
                            delegate(IAsyncResult r){
                                lock(m_pLock){
                                    // BeginWrite completed synchronously.
                                    if(!isBeginWriteCompleted){
                                        isCompletedSync = true;
                                        return;
                                    }
                                }

                                try{
                                    m_pOwner.EndWrite(r);
                                    m_BytesWritten += countReaded;

                                    // We have read and sent all requested data.
                                    if(m_Count == m_BytesWritten){
                                        SetState(AsyncOP_State.Completed);
                                    }
                                    // Start reading next data block(s).
                                    else{                                        
                                        BeginReadData();
                                    }
                                }
                                catch(Exception x){
                                    m_pException = x;
                                    SetState(AsyncOP_State.Completed);
                                }
                            },
                            null
                        );

                        lock(m_pLock){
                            isBeginWriteCompleted = true;
                        }

                        // BeginWrite completed synchronously.
                        if(isCompletedSync){
                            m_pOwner.EndWrite(writeResult);
                            m_BytesWritten += countReaded;

                            // We have read and sent all requested data.
                            if(m_Count == m_BytesWritten){
                                SetState(AsyncOP_State.Completed);
                            }
                        }
                        else{
                            return true;
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;
                    SetState(AsyncOP_State.Completed);
                }

                return false;
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets asynchronous operation state.
            /// </summary>
            public AsyncOP_State State
            {
                get{ return m_State; }
            }

            /// <summary>
            /// Gets error happened during operation. Returns null if no error.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public Exception? Error
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Error' is accessible only in 'AsyncOP_State.Completed' state.");
                    }

                    return m_pException; 
                }
            }

            /// <summary>
            /// Gets number of bytes written.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public long BytesWritten
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Socket' is accessible only in 'AsyncOP_State.Completed' state.");
                    }
                    if(m_pException != null){
                        throw m_pException;
                    }

                    return m_BytesWritten; 
                }
            }

            #endregion

            #region Events implementation

            /// <summary>
            /// Is called when asynchronous operation has completed.
            /// </summary>
            public event EventHandler<EventArgs<WriteStreamAsyncOP>>? CompletedAsync = null;

            #region method OnCompletedAsync

            /// <summary>
            /// Raises <b>CompletedAsync</b> event.
            /// </summary>
            private void OnCompletedAsync()
            {
                if(this.CompletedAsync != null){
                    this.CompletedAsync(this,new EventArgs<WriteStreamAsyncOP>(this));
                }
            }

            #endregion

            #endregion
        }

        #endregion

        /// <summary>
        /// Starts writing stream data to this stream.
        /// </summary>
        /// <param name="op">Asynchronous operation.</param>
        /// <returns>Returns true if aynchronous operation is pending (The <see cref="WriteStreamAsyncOP.CompletedAsync"/> event is raised upon completion of the operation).
        /// Returns false if operation completed synchronously.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>op</b> is null reference.</exception>
        public bool WriteStreamAsync(WriteStreamAsyncOP op)
        {
            if(this.m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(op == null){
                throw new ArgumentNullException("op");
            }
            if(op.State != AsyncOP_State.WaitingForStart){
                throw new ArgumentException("Invalid argument 'op' state, 'op' must be in 'AsyncOP_State.WaitingForStart' state.","op");
            }

            return op.Start(this);
        }

        #endregion

        #region method WriteStream

        /// <summary>
        /// Writes all source <b>stream</b> data to stream.
        /// </summary>
        /// <param name="stream">Stream which data to write.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null.</exception>
        public void WriteStream(Stream stream)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            WriteStreamAsync(stream,0,int.MaxValue,CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Writes specified number of bytes from source <b>stream</b> to stream.
        /// </summary>
        /// <param name="stream">Stream which data to write.</param>
        /// <param name="count">Number of bytes to write.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when <b>count</b> argument has invalid value.</exception>
        public void WriteStream(Stream stream,long count)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }
            if(count < 0){
                throw new ArgumentException("Argument 'count' value must be >= 0.");
            }

            WriteStreamAsync(stream,count,int.MaxValue,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion
        
        #region method WritePeriodTerminatedAsync

        #region class WritePeriodTerminatedAsyncOP

        /// <summary>
        /// This class represents SmartStream.WritePeriodTerminatedAsync asynchronous operation.
        /// </summary>
        public class WritePeriodTerminatedAsyncOP : IDisposable,IAsyncOP
        {
            private object           m_pLock         = new object();
            private AsyncOP_State    m_State         = AsyncOP_State.WaitingForStart;
            private Exception?       m_pException    = null;
            private SmartStream      m_pStream;
            private SmartStream?     m_pOwner        = null;
            private ReadLineAsyncOP? m_pReadLineOP   = null;
            private int              m_BytesWritten  = 0;
            private bool             m_EndsCRLF      = false;
            private bool             m_RiseCompleted = false;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="stream">Source stream. Reading starts from stream current location.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null reference.</exception>
            public WritePeriodTerminatedAsyncOP(Stream stream)
            {
                if(stream == null){
                    throw new ArgumentNullException("stream");
                }

                m_pStream = new SmartStream(stream,false);
            }

            #region method Dispose

            /// <summary>
            /// Cleans up any resources being used.
            /// </summary>
            public void Dispose()
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }
                SetState(AsyncOP_State.Disposed);
                
                m_pException  = null;
                m_pOwner      = null;
                m_pReadLineOP = null;

                this.CompletedAsync = null;
            }

            #endregion


            #region method Start

            /// <summary>
            /// Starts operation processing.
            /// </summary>
            /// <param name="owner">Owner SmartStream.</param>
            /// <returns>Returns true if asynchronous operation in progress or false if operation completed synchronously.</returns>
            /// <exception cref="ArgumentNullException">Is raised when <b>owner</b> is null reference.</exception>
            internal bool Start(SmartStream owner)
            {
                if(owner == null){
                    throw new ArgumentNullException("owner");
                }

                m_pOwner = owner;

                SetState(AsyncOP_State.Active);

                try{
                    // Read line.
                    m_pReadLineOP = new ReadLineAsyncOP(new byte[32000],SizeExceededAction.ThrowException);
                    m_pReadLineOP.CompletedAsync += delegate(object? s,EventArgs<ReadLineAsyncOP> e){
                        if(!ProcessReadLineResultAsync()){
                            BeginReadLine();
                        }
                    };
                    
                    BeginReadLine();
                }
                catch(Exception x){
                    m_pException = x;
                    SetState(AsyncOP_State.Completed);
                    m_pReadLineOP?.Dispose();
                }

                // Set flag rise CompletedAsync event flag. The event is raised when async op completes.
                // If already completed sync, that flag has no effect.
                lock(m_pLock){
                    m_RiseCompleted = true;

                    return m_State == AsyncOP_State.Active;
                }
            }

            #endregion


            #region method SetState

            /// <summary>
            /// Sets operation state.
            /// </summary>
            /// <param name="state">New state.</param>
            private void SetState(AsyncOP_State state)
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }

                lock(m_pLock){
                    m_State = state;

                    if(m_State == AsyncOP_State.Completed && m_RiseCompleted){
                        OnCompletedAsync();
                    }
                }
            }

            #endregion

            #region method BeginReadLine

            /// <summary>
            /// Starts reading line of data.
            /// </summary>
            private void BeginReadLine()
            {                
                try{
                    ArgumentNullException.ThrowIfNull(m_pReadLineOP);

                    while (this.State == AsyncOP_State.Active){
                        // Read data completed synchonously.
                        if(m_pStream.ReadLine(m_pReadLineOP,true)){
                            // Operation completed asynchronously, it will continue processing.
                            if(ProcessReadLineResultAsync()){
                                break;
                            }
                        }
                        // Read data completed asynchonously.
                        else{
                            break;
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;
                    SetState(AsyncOP_State.Completed);
                }
            }

            #endregion

            #region method ProcessReadLineResultAsync

            /// <summary>
            /// Processes read line result.
            /// </summary>
            /// <returns>Retruns true if this method completed asynchronously, otherwise false.</returns>
            private bool ProcessReadLineResultAsync()
            {
                try{
                    ArgumentNullException.ThrowIfNull(m_pOwner);
                    ArgumentNullException.ThrowIfNull(m_pReadLineOP);

                    if (m_pReadLineOP.Error != null){
                        m_pException = m_pReadLineOP.Error;
                        SetState(AsyncOP_State.Completed);
                    }
                    else{
                        // We have readed all source stream data, we are done.
                        if(m_pReadLineOP.BytesInBuffer == 0){
                            byte[] period_crlf = new byte[]{(byte)'.',(byte)'\r',(byte)'\n'};
                            // Line last doesn't end CRLF, we need to add it. We must get CRLF.CRLF.
                            if(!m_EndsCRLF){
                                period_crlf = new byte[]{(byte)'\r',(byte)'\n',(byte)'.',(byte)'\r',(byte)'\n'};
                            }

                            // Send terminator.
                            m_BytesWritten += period_crlf.Length;
                            m_pOwner.Write(period_crlf,0,period_crlf.Length);

                            SetState(AsyncOP_State.Completed);

                            return false;
                        }
                        // Write readed line.
                        else{
                            // Check if line ends CRLF.
                            if(m_pReadLineOP.BytesInBuffer >= 2 && m_pReadLineOP.Buffer[m_pReadLineOP.BytesInBuffer - 2] == '\r' && m_pReadLineOP.Buffer[m_pReadLineOP.BytesInBuffer - 1] == '\n'){
                                m_EndsCRLF = true;
                            }
                            else{
                                m_EndsCRLF = false;
                            }
                            
                            m_BytesWritten += m_pReadLineOP.BytesInBuffer;

                            byte[] writeBuffer        = m_pReadLineOP.Buffer;
                            int    bytesInWriteBuffer = m_pReadLineOP.BytesInBuffer;

                            // Period handling. If line starts with period(.), additional period is added.
                            if(writeBuffer[0] == '.'){
                                byte[] buffer = new byte[bytesInWriteBuffer + 1];
                                buffer[0] = (byte)'.';
                                Array.Copy(writeBuffer,0,buffer,1,bytesInWriteBuffer);

                                writeBuffer = buffer;
                                bytesInWriteBuffer = buffer.Length;
                            }                            
                            
                            bool writeOpAsynchronous = true;
                            bool beginWriteMethodDone = false;
                            IAsyncResult writeResult = m_pOwner.BeginWrite(
                                writeBuffer,
                                0,
                                bytesInWriteBuffer,
                                delegate(IAsyncResult r){
                                    // Note: This delegate can be synchronous and called from inside BeginWrite method.
                                    //       In case of synchronous, be aware of calling nested methods to avoid stack overflow.
                                    
                                    try{
                                        m_pOwner.EndWrite(r);

                                        lock(m_pLock){
                                            if(!beginWriteMethodDone){
                                                writeOpAsynchronous = false;
                                            }
                                        }

                                        // Operation is asynchronous.
                                        if(writeOpAsynchronous){
                                            BeginReadLine();                                 
                                        }
                                    }
                                    catch(Exception x){
                                        m_pException = x;
                                        SetState(AsyncOP_State.Completed);
                                    }
                                },
                                null
                            );

                            lock(m_pLock){
                                beginWriteMethodDone = true;

                                return writeOpAsynchronous;
                            }                            
                        }
                    }
                }
                catch(Exception x){
                    m_pException = x;
                    SetState(AsyncOP_State.Completed);
                }

                return false;
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets asynchronous operation state.
            /// </summary>
            public AsyncOP_State State
            {
                get{ return m_State; }
            }

            /// <summary>
            /// Gets error happened during operation. Returns null if no error.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public Exception? Error
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Error' is accessible only in 'AsyncOP_State.Completed' state.");
                    }

                    return m_pException; 
                }
            }

            /// <summary>
            /// Gets number of bytes written.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public int BytesWritten
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Socket' is accessible only in 'AsyncOP_State.Completed' state.");
                    }
                    if(m_pException != null){
                        throw m_pException;
                    }

                    return m_BytesWritten; 
                }
            }

            #endregion

            #region Events implementation

            /// <summary>
            /// Is called when asynchronous operation has completed.
            /// </summary>
            public event EventHandler<EventArgs<WritePeriodTerminatedAsyncOP>>? CompletedAsync = null;

            #region method OnCompletedAsync

            /// <summary>
            /// Raises <b>CompletedAsync</b> event.
            /// </summary>
            private void OnCompletedAsync()
            {
                if(this.CompletedAsync != null){
                    this.CompletedAsync(this,new EventArgs<WritePeriodTerminatedAsyncOP>(this));
                }
            }

            #endregion

            #endregion
        }

        #endregion

        /// <summary>
        /// Starts writing period handled and terminated data to this stream.
        /// </summary>
        /// <param name="op">Asynchronous operation.</param>
        /// <returns>Returns true if aynchronous operation is pending (The <see cref="WritePeriodTerminatedAsyncOP.CompletedAsync"/> event is raised upon completion of the operation).
        /// Returns false if operation completed synchronously.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>op</b> is null reference.</exception>
        public bool WritePeriodTerminatedAsync(WritePeriodTerminatedAsyncOP op)
        {
            if(this.m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(op == null){
                throw new ArgumentNullException("op");
            }
            if(op.State != AsyncOP_State.WaitingForStart){
                throw new ArgumentException("Invalid argument 'op' state, 'op' must be in 'AsyncOP_State.WaitingForStart' state.","op");
            }

            return op.Start(this);
        }

        #endregion

        #region method WritePeriodTerminated

        /// <summary>
        /// Writes period handled and terminated data to this stream.
        /// </summary>
        /// <param name="stream">Source stream. Reading starts from stream current location.</param>
        /// <returns>Returns number of bytes written to stream.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null.</exception>
        /// <exception cref="LineSizeExceededException">Is raised when <b>stream</b> has too big line.</exception>        
        public long WritePeriodTerminated(Stream stream)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            ManualResetEvent wait = new ManualResetEvent(false);
            WritePeriodTerminatedAsyncOP op = new WritePeriodTerminatedAsyncOP(stream);
            op.CompletedAsync += delegate(object? s1,EventArgs<WritePeriodTerminatedAsyncOP> e1){
                wait.Set();
            };
            if(!this.WritePeriodTerminatedAsync(op)){
                wait.Set();
            }
            wait.WaitOne();
            wait.Close();

            if(op.Error != null){
                throw op.Error;
            }
            else{
                return op.BytesWritten;
            }
        }

        #endregion

    }
}
