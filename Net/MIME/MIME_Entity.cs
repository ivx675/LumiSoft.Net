using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.IO;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME entity.
    /// </summary>
    /// <remarks>
    /// A MIME entity consists of a header section and an optional body.
    /// MIME entities can be nested to form multipart structures.
    /// Defined in RFC 2045 section 2.4.
    /// </remarks>
    public class MIME_Entity : IDisposable
    {
        private bool              m_IsDisposed = false;
        private MIME_Entity?      m_pParent    = null;
        private MIME_h_Collection m_pHeader;
        private MIME_b?           m_pBody      = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_Entity"/> class.
        /// </summary>
        /// <remarks>
        /// The entity is created with an empty header collection and no body.
        /// </remarks>
        public MIME_Entity()
        {
            m_pHeader = new MIME_h_Collection(new MIME_h_Provider());
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used. This method is thread-safe.
        /// </summary>
        public virtual void Dispose()
        {
            lock(this){
                if(m_IsDisposed){
                    return;
                }
                m_IsDisposed = true;
                
                m_pBody?.Dispose();
                m_pParent = null;
            }
        }

        #endregion


        #region static method CreateEntity_Text_Plain

        /// <summary>
        /// Creates text/plain MIME entity.
        /// </summary>
        /// <param name="transferEncoding">Content transfer encoding.</param>
        /// <param name="text">Text.</param>
        /// <returns>Returns created entity.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>transferEncoding</b>, <b>charset</b> or <b>text</b> is null reference.</exception>
        /// <exception cref="NotSupportedException">Is raised when body contains not supported Content-Transfer-Encoding.</exception>
        public static MIME_Entity CreateEntity_Text_Plain(string transferEncoding,string text)
        {
            if(transferEncoding == null){
                throw new ArgumentNullException(nameof(transferEncoding));
            }
            if(text == null){
                throw new ArgumentNullException(nameof(text));
            }

            MIME_Entity retVal = new MIME_Entity();
            MIME_b_Text body = new MIME_b_Text(MIME_MediaTypes.Text.plain);
            retVal.Body = body;
            body.SetText(transferEncoding,text);

            return retVal;
        }

        #endregion

        #region static method CreateEntity_Text_Html

        /// <summary>
        /// Creates text/html MIME entity.
        /// </summary>
        /// <param name="transferEncoding">Content transfer encoding.</param>
        /// <param name="text">Text.</param>
        /// <returns>Returns created entity.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>transferEncoding</b>, <b>charset</b> or <b>text</b> is null reference.</exception>
        /// <exception cref="NotSupportedException">Is raised when body contains not supported Content-Transfer-Encoding.</exception>
        public static MIME_Entity CreateEntity_Text_Html(string transferEncoding,string text)
        {
            if(transferEncoding == null){
                throw new ArgumentNullException(nameof(transferEncoding));
            }
            if(text == null){
                throw new ArgumentNullException(nameof(text));
            }

            MIME_Entity retVal = new MIME_Entity();
            MIME_b_Text body = new MIME_b_Text(MIME_MediaTypes.Text.html);
            retVal.Body = body;
            body.SetText(transferEncoding,text);

            return retVal;
        }

        #endregion

        #region static method CreateEntity_Attachment

        /// <summary>
        /// Creates attachment(application/octet-stream) entity.
        /// </summary>
        /// <param name="fileName">File name with path.</param>
        /// <returns>Returns created entity.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>fileName</b> is null reference.</exception>
        public static MIME_Entity CreateEntity_Attachment(string fileName)
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }

            using(Stream stream = File.OpenRead(fileName)){
                return CreateEntity_Attachment(Path.GetFileName(fileName),stream);
            }
        }

        /// <summary>
        /// Creates attachment(application/octet-stream) entity.
        /// </summary>
        /// <param name="attachmentName">Attachment file name what appears in mail message. Value null means argument 'fileName' file name is used.</param>
        /// <param name="fileName">File name with path.</param>
        /// <returns>Returns created entity.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>fileName</b> is null reference.</exception>
        public static MIME_Entity CreateEntity_Attachment(string attachmentName,string fileName)
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }

            using(Stream stream = File.OpenRead(fileName)){
                return CreateEntity_Attachment(string.IsNullOrEmpty(attachmentName) ? Path.GetFileName(fileName) : attachmentName,stream);
            }
        }

        /// <summary>
        /// Creates attachment(application/octet-stream) entity.
        /// </summary>
        /// <param name="attachmentName">Attachment file name what appears in mail message.</param>
        /// <param name="stream">Attachment data stream. Data is read from stream current position.</param>
        /// <returns>Returns created entity.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>attachmentName</b> or <b>stream</b> is null reference.</exception>
        public static MIME_Entity CreateEntity_Attachment(string attachmentName,Stream stream)
        {
            if(attachmentName == null){
                throw new ArgumentNullException(nameof(attachmentName));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            long fileSize = stream.CanSeek ? (stream.Length - stream.Position) : -1;

            MIME_Entity retVal = new MIME_Entity();
            MIME_b_Application body = new MIME_b_Application(MIME_MediaTypes.Application.octet_stream);
            retVal.Body = body;
            body.SetData(stream,MIME_TransferEncodings.Base64);
            retVal.ContentType?.Param_Name = Path.GetFileName(attachmentName);

            MIME_h_ContentDisposition disposition = new MIME_h_ContentDisposition(MIME_DispositionTypes.Attachment);
            disposition.Param_FileName           = Path.GetFileName(attachmentName);
            disposition.Param_Size               = fileSize;
            //disposition.Param_CreationDate     = fileInfo.CreationTime;
            //disposition.Param_ModificationDate = fileInfo.LastWriteTime;
            //disposition.Param_ReadDate         = fileInfo.LastAccessTime;
            retVal.ContentDisposition = disposition;

            return retVal;
        }

        #endregion


        #region method ToStream

        /// <summary>
        /// Stores the MIME entity to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream to which to write the MIME entity.
        /// </param>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers. If <see langword="false"/>, rewritten non-ASCII
        /// header values are encoded using UTF-8 with RFC 2047 encoded-words
        /// and RFC 2231 parameter encoding.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether all header fields are regenerated and re-encoded.
        /// If <see langword="false"/>, unchanged parsed header fields retain
        /// their original wire representation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        public void ToStream(Stream stream,bool utf8Headers = false,bool reEncodeHeaders = false)
        {
            ToStreamAsync(stream,utf8Headers,reEncodeHeaders).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToStreamAsync

        /// <summary>
        /// Stores the MIME entity to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream to which to write the MIME entity.
        /// </param>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers. If <see langword="false"/>, rewritten non-ASCII
        /// header values are encoded using UTF-8 with RFC 2047 encoded-words
        /// and RFC 2231 parameter encoding.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether all header fields are regenerated and re-encoded.
        /// If <see langword="false"/>, unchanged parsed header fields retain
        /// their original wire representation.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        public async Task ToStreamAsync(Stream stream,bool utf8Headers = false,bool reEncodeHeaders = false,CancellationToken cancellationToken = default)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if(m_pBody == null) {
                throw new InvalidOperationException("MIME entity body has not been set.");
            }

            await m_pHeader.ToStreamAsync(stream,utf8Headers,reEncodeHeaders,cancellationToken);
            await stream.WriteAsync(new byte[]{(int)'\r',(int)'\n'},0,2);
            await m_pBody.ToStreamAsync(stream, utf8Headers, reEncodeHeaders, cancellationToken);
        }

        #endregion

        #region method ToFile

        /// <summary>
        /// Stores the MIME entity to the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File to which to write the MIME entity.
        /// </param>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers. If <see langword="false"/>, rewritten non-ASCII
        /// header values are encoded using UTF-8 with RFC 2047 encoded-words
        /// and RFC 2231 parameter encoding.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether all header fields are regenerated and re-encoded.
        /// If <see langword="false"/>, unchanged parsed header fields retain
        /// their original wire representation.
        /// </param>
        /// <remarks>
        /// This method blocks until the MIME entity has been completely written.
        /// For asynchronous operation, use <see cref="ToFileAsync(string,bool,bool,CancellationToken)"/>.
        /// </remarks>
        public void ToFile(string fileName,bool utf8Headers = false,bool reEncodeHeaders = false)
        {
            ToFileAsync(
                fileName,
                utf8Headers,
                reEncodeHeaders,
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToFileAsync

        /// <summary>
        /// Stores the MIME entity to the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File to which to write the MIME entity.
        /// </param>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers. If <see langword="false"/>, rewritten non-ASCII
        /// header values are encoded using UTF-8 with RFC 2047 encoded-words
        /// and RFC 2231 parameter encoding.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether all header fields are regenerated and re-encoded.
        /// If <see langword="false"/>, unchanged parsed header fields retain
        /// their original wire representation.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        public async Task ToFileAsync(
            string fileName,
            bool utf8Headers = false,
            bool reEncodeHeaders = false,
            CancellationToken cancellationToken = default
        )
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }
            if(fileName.Length == 0){
                throw new ArgumentException("Argument 'fileName' cannot be empty.", nameof(fileName));
            }

            await using(FileStream stream = File.Create(fileName)){
                await ToStreamAsync(
                    stream,
                    utf8Headers,
                    reEncodeHeaders,
                    cancellationToken
                );
            }
        }

        #endregion

        #region mehtod ToByteArray

        /// <summary>
        /// Stores the MIME entity to a byte array.
        /// </summary>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers. If <see langword="false"/>, rewritten non-ASCII
        /// header values are encoded using UTF-8 with RFC 2047 encoded-words
        /// and RFC 2231 parameter encoding.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether all header fields are regenerated and re-encoded.
        /// If <see langword="false"/>, unchanged parsed header fields retain
        /// their original wire representation.
        /// </param>
        /// <returns>
        /// Returns the serialized MIME entity.
        /// </returns>
        /// <remarks>
        /// The entire MIME entity is buffered in memory. For large messages
        /// or messages containing large attachments, use
        /// <see cref="ToStream(Stream,bool,bool)"/> or
        /// <see cref="ToStreamAsync(Stream,bool,bool,CancellationToken)"/>
        /// instead.
        /// </remarks>
        public byte[] ToByteArray(
            bool utf8Headers = false,
            bool reEncodeHeaders = false
        )
        {
            return ToByteArrayAsync(
                utf8Headers,
                reEncodeHeaders,
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToByteArrayAsync

        /// <summary>
        /// Stores the MIME entity to a byte array.
        /// </summary>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers. If <see langword="false"/>, rewritten non-ASCII
        /// header values are encoded using UTF-8 with RFC 2047 encoded-words
        /// and RFC 2231 parameter encoding.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether all header fields are regenerated and re-encoded.
        /// If <see langword="false"/>, unchanged parsed header fields retain
        /// their original wire representation.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the serialized MIME entity.
        /// </returns>
        /// <remarks>
        /// The entire MIME entity is buffered in memory. For large messages
        /// or messages containing large attachments, use
        /// <see cref="ToStreamAsync(Stream,bool,bool,CancellationToken)"/>
        /// instead.
        /// </remarks>
        public async Task<byte[]> ToByteArrayAsync(
            bool utf8Headers = false,
            bool reEncodeHeaders = false,
            CancellationToken cancellationToken = default
        )
        {
            using MemoryStream stream = new MemoryStream();

            await ToStreamAsync(
                stream,
                utf8Headers,
                reEncodeHeaders,
                cancellationToken
            );

            return stream.ToArray();
        }

        #endregion

        #region method DataToStream

        /// <summary>
        /// Writes the decoded body data to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream to which to write the decoded body data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not a single-part MIME body.
        /// </exception>
        /// <remarks>
        /// This method is only valid for single-part MIME bodies. The written
        /// data is decoded according to the entity's Content-Transfer-Encoding.
        /// </remarks>
        public void DataToStream(Stream stream)
        {
            DataToStreamAsync(stream,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method DataToStreamAsync

        /// <summary>
        /// Writes the decoded body data to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream to which to write the decoded body data.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not a single-part MIME body.
        /// </exception>
        /// <remarks>
        /// This method is only valid for single-part MIME bodies. The written
        /// data is decoded according to the entity's Content-Transfer-Encoding.
        /// </remarks>
        public async Task DataToStreamAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(this.Body == null){
                throw new InvalidOperationException("MIME entity body has not been set.");
            }
            if(this.Body is not MIME_b_SinglepartBase){
                throw new InvalidOperationException("This method is only valid for single-part MIME bodies.");
            }

            MIME_b_SinglepartBase body = (MIME_b_SinglepartBase)this.Body;
            using(Stream dataStream = body.GetDataStream()){
                await dataStream.CopyToAsync(stream,cancellationToken);
            }
        }

        #endregion

        #region method DataToFile

        /// <summary>
        /// Writes the decoded body data to the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File to which to write the decoded body data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not a single-part MIME body.
        /// </exception>
        /// <remarks>
        /// This method is only valid for single-part MIME bodies. The written
        /// data is decoded according to the entity's Content-Transfer-Encoding.
        /// For asynchronous operation, use
        /// <see cref="DataToFileAsync(string,CancellationToken)"/>.
        /// </remarks>
        public void DataToFile(string fileName)
        {
            DataToFileAsync(fileName,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method DataToFileAsync

        /// <summary>
        /// Writes the decoded body data to the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File to which to write the decoded body data.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not a single-part MIME body.
        /// </exception>
        /// <remarks>
        /// This method is only valid for single-part MIME bodies. The written
        /// data is decoded according to the entity's Content-Transfer-Encoding.
        /// </remarks>
        public async Task DataToFileAsync(string fileName,CancellationToken cancellationToken = default)
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }
            if(fileName.Length == 0){
                throw new ArgumentException("Argument 'fileName' cannot be empty.",nameof(fileName));
            }

            await using(FileStream stream = File.Create(fileName)){
                await DataToStreamAsync(stream, cancellationToken);
            }
        }

        #endregion

        #region method DataToByteArray

        /// <summary>
        /// Returns the decoded body data as a byte array.
        /// </summary>
        /// <returns>
        /// Returns the decoded body data.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not a single-part MIME body.
        /// </exception>
        /// <remarks>
        /// This method is only valid for single-part MIME bodies. The returned
        /// data is decoded according to the entity's Content-Transfer-Encoding.
        ///
        /// The entire decoded body is buffered in memory. For large attachments,
        /// use <see cref="DataToStream(Stream)"/> or
        /// <see cref="DataToFile(string)"/> instead.
        /// </remarks>
        public byte[] DataToByteArray()
        {
            return DataToByteArrayAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method DataToByteArrayAsync

        /// <summary>
        /// Returns the decoded body data as a byte array.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the decoded body data.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Raised when the MIME entity body has not been set.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not a single-part MIME body.
        /// </exception>
        /// <remarks>
        /// This method is only valid for single-part MIME bodies. The returned
        /// data is decoded according to the entity's Content-Transfer-Encoding.
        ///
        /// The entire decoded body is buffered in memory. For large attachments,
        /// use <see cref="DataToStreamAsync(Stream,CancellationToken)"/> or
        /// <see cref="DataToFileAsync(string,CancellationToken)"/> instead.
        /// </remarks>
        public async Task<byte[]> DataToByteArrayAsync(CancellationToken cancellationToken = default)
        {
            using(MemoryStream stream = new MemoryStream()){
                await DataToStreamAsync(stream, cancellationToken);

                return stream.ToArray();
            }
        }

        #endregion


        #region method ParseInternalAsync

        /// <summary>
        /// Asynchronously parses a MIME entiy from a stream by reading and processing its headers, 
        /// verifying constraints, and delegating the remaining body stream to the body provider.
        /// </summary>
        /// <param name="stream">The source stream containing the raw MIME message.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
        internal async Task ParseInternalAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            // 1. Read headers (enforces 1 MB limit, handles loose mode fallback, and leaves stream positioned at body start)
            long maxHeaderBytes = 1 * 1024 * 1024; // 1 MB
            string rawHeader = await ReadHeaderAsync(stream,maxHeaderBytes,true,cancellationToken);
                        
            // Feed header to your header parser component
            m_pHeader.ParseFromString(rawHeader);

            // 2. Stream is now perfectly positioned at the body start.
            // Create the BoundedStream and hand off to the body provider.
            long bodyStart = stream.Position;
            long bodyLength = stream.Length - bodyStart;

            var bodyStream = new BoundedStream(stream,bodyStart,bodyLength);
            bodyStream.Position = 0;

            m_pBody = await ParseBodyAsync(bodyStream,cancellationToken);
            m_pBody.SetParent(this,false);
        }

        #endregion

        #region method SetParent

        /// <summary>
        /// Sets MIME entity parent entity.
        /// </summary>
        /// <param name="parent">Parent entity.</param>
        internal void SetParent(MIME_Entity parent)
        {
            m_pParent = parent;
        }

        #endregion


        #region method ReadHeaderAsync

        /// <summary>
        /// Asynchronously reads and extracts MIME headers from the stream up to the standard <c>\r\n\r\n</c> terminator, 
        /// enforcing a strict maximum size limit to protect against memory exhaustion attacks.
        /// <para>
        /// This method uses an efficient vectorized sliding window with a 3-byte overlap to reliably detect 
        /// terminators even if they span across buffer read boundaries. Upon completion, the underlying 
        /// stream's position is automatically repositioned precisely at the start of the body payload.
        /// </para>
        /// </summary>
        /// <param name="stream">The source stream positioned at the start of the MIME entity.</param>
        /// <param name="maxHeaderSize">The maximum allowed size of the header block in bytes (e.g., 1,048,576 for 1 MB).</param>
        /// <param name="looseMode">
        /// If <c>true</c>, reaching the end of the stream without finding a <c>\r\n\r\n</c> terminator 
        /// will treat all accumulated content as headers and assume an empty body rather than throwing an exception.
        /// </param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A string containing the raw ASCII header block.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is null.</exception>
        /// <exception cref="InvalidDataException">
        /// Thrown if the headers exceed the <paramref name="maxHeaderSize"/> threshold, 
        /// or if the end of the stream is reached before finding the <c>\r\n\r\n</c> terminator (when <paramref name="looseMode"/> is <c>false</c>).
        /// </exception>
        private async Task<string> ReadHeaderAsync(Stream stream,long maxHeaderSize,bool looseMode,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            int bufferSize = 8192;
            byte[] buffer = new byte[bufferSize];
            int bytesInBuffer = 0;
            long totalBytesRead = 0;
            int searchOffset = 0;

            using var headerMemoryStream = new MemoryStream();

            while(true){
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(bytesInBuffer, bufferSize - bytesInBuffer), cancellationToken);
                bytesInBuffer += bytesRead;
                totalBytesRead += bytesRead;

                // Enforce strict header size limit against DoS
                if(totalBytesRead > maxHeaderSize){
                    throw new InvalidDataException($"MIME headers exceeded the maximum allowed size of {maxHeaderSize / 1024} KB.");
                }

                // Check for EOF
                if(bytesRead == 0){
                    if(looseMode){
                        // Loose Mode Fallback: Treat all accumulated content as headers, zero-length body.
                        if(bytesInBuffer > 0){
                            headerMemoryStream.Write(buffer,0,bytesInBuffer);
                        }

                        return Encoding.UTF8.GetString(headerMemoryStream.ToArray());
                    }

                    throw new InvalidDataException("Reached end of stream while searching for MIME headers (\r\n\r\n not found).");
                }

                // Search for \r\n\r\n using vectorized SIMD search
                ReadOnlySpan<byte> span = buffer.AsSpan(0, bytesInBuffer);
                int relativeIndex = span.Slice(searchOffset).IndexOf("\r\n\r\n"u8);
                int matchIndex = relativeIndex != -1 ? searchOffset + relativeIndex : -1;

                if(matchIndex != -1){
                    // 1. Write the header block up to the terminator
                    headerMemoryStream.Write(buffer, 0, matchIndex);

                    // 2. Adjust underlying stream position to the start of the body payload
                    long excessBytes = bytesInBuffer - (matchIndex + 4);
                    stream.Position -= excessBytes;

                    // 3. Return decoded headers string
                    return Encoding.UTF8.GetString(headerMemoryStream.ToArray());
                }

                // Buffer management for sliding window tail overlap
                int overlapSize = 3;
                if(bytesInBuffer > overlapSize){
                    int safeToWriteCount = bytesInBuffer - overlapSize;
                    headerMemoryStream.Write(buffer, 0, safeToWriteCount);

                    // Retain the last 3 bytes at the front of the buffer
                    Array.Copy(buffer, safeToWriteCount, buffer, 0, overlapSize);
                    bytesInBuffer = overlapSize;
            
                    // Reset search offset to 0 so the next iteration scans the overlap region
                    searchOffset = 0;
                }
                else{
                    searchOffset = bytesInBuffer;
                }
            }
        }

        #endregion

        #region method ParseBodyAsync

        private async Task<MIME_b> ParseBodyAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            var defaultContentType = new MIME_h_ContentType("text/plain");

            string typeWithSubtype = defaultContentType.TypeWithSubtype;
            string type = "text";
            try{
                if(this.ContentType != null){
                    typeWithSubtype = this.ContentType.TypeWithSubtype;
                    type            = this.ContentType.Type;
                }
            }
            catch{
                // Do nothing, content will be MIME_b_Unknown.
                typeWithSubtype = "unknown/unknown";
            }

            if(string.Equals(typeWithSubtype,"application/pkcs7-mime",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_ApplicationPkcs7Mime.Parse(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"message/rfc822",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MessageRfc822.ParseAsync(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"message/delivery-status",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_MessageDeliveryStatus.Parse(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"multipart/alternative",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MultipartAlternative.ParseAsync(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"multipart/encrypted",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MultipartEncrypted.ParseAsync(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"multipart/form-data",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MultipartFormData.ParseAsync(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"multipart/mixed",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MultipartMixed.ParseAsync(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"multipart/related",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MultipartRelated.ParseAsync(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"multipart/report",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MultipartReport.ParseAsync(this,stream);
            }
            else if(string.Equals(typeWithSubtype,"multipart/signed",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_MultipartSigned.ParseAsync(this,stream);
            }
            else if(string.Equals(type,"application",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_Application.Parse(this,stream);
            }
            else if(string.Equals(type,"audio",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_Audio.Parse(this,stream);
            }
            else if(string.Equals(type,"image",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_Image.Parse(this,stream);
            }
            else if(string.Equals(type,"message",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_Message.Parse(this,stream);
            }
            else if(string.Equals(type,"multipart",StringComparison.OrdinalIgnoreCase)){
                return await MIME_b_Multipart.ParseAsync(this,stream);
            }
            else if(string.Equals(type,"text",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_Text.Parse(this,stream);
            }
            else if(string.Equals(type,"video",StringComparison.OrdinalIgnoreCase)){
                return MIME_b_Video.Parse(this,stream);
            }
            
            return MIME_b_Unknown.Parse(this,stream);
        }

        #endregion


        #region Properties Implementation

        // Permanent headerds list: http://www.rfc-editor.org/rfc/rfc4021.txt

        /// <summary>
        /// Gets if this object is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }

        /// <summary>
        /// Gets whether this entity has been modified since it was loaded.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is riased when this class is disposed and this property is accessed.</exception>
        public bool IsModified
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pHeader.IsModified || (m_pBody?.IsModified ?? false); 
            }
        }

        /// <summary>
        /// Gets the parent entity.
        /// </summary>
        /// <remarks>
        /// Returns <c>null</c> if this entity is the root entity.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public MIME_Entity? Parent
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pParent; 
            }
        }

        /// <summary>
        /// Gets the collection of header fields associated with this entity.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public MIME_h_Collection Header
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_pHeader; 
            }
        }
                                
        /// <summary>
        /// Gets or sets the MIME-Version header field.
        /// </summary>
        /// <remarks>
        /// Specifies the MIME version used by this entity.
        /// Returns <c>null</c> if the header field is not present.
        /// The value is typically <c>1.0</c>.
        /// Defined in RFC 2045 section 4.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? MimeVersion
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("MIME-Version");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("MIME-Version");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("MIME-Version");
                    if(h == null){
                        h = new MIME_h_Unstructured("MIME-Version",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-ID header field.
        /// </summary>
        /// <remarks>
        /// Specifies a unique identifier for this MIME entity.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2045 section 7.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentID
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-ID");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                if(value == null){
                    m_pHeader.RemoveAll("Content-ID");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-ID");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-ID",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-Description header field.
        /// </summary>
        /// <remarks>
        /// Provides a human-readable description of this MIME entity.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2045 section 8.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentDescription
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Description");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Description");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Description");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-Description",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets content transfer encoding. Value null means that header field does not exist. 
        /// RFC defined values are in <see cref="MIME_TransferEncodings">MIME_TransferEncodings</see>. Defined in RFC 2045 6.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        /// <remarks>Coding method used in a MIME message body part.</remarks>
        public string? ContentTransferEncoding
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Transfer-Encoding");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value.Trim();
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Transfer-Encoding");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Transfer-Encoding");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-Transfer-Encoding",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-Transfer-Encoding header field.
        /// </summary>
        /// <remarks>
        /// Specifies the transfer encoding used for the entity body.
        /// Common values include 7bit, 8bit, binary, quoted-printable, and base64.
        /// Returns <c>null</c> if the header field is not present.
        /// RFC-defined values are available in <see cref="MIME_TransferEncodings"/>.
        /// Defined in RFC 2045 section 6.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public MIME_h_ContentType? ContentType
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Type");
                if(h != null){
                    if(!(h is MIME_h_ContentType)){
                        throw new ParseException("Header field 'ContentType' parsing failed.");
                    }

                    return (MIME_h_ContentType)h;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Type");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Type");
                    if(h == null){
                        m_pHeader.Add(value);
                    }
                    else{
                        m_pHeader.ReplaceFirst(value);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-Base header field.
        /// </summary>
        /// <remarks>
        /// Specifies the base URI used to resolve relative URIs within this MIME entity.
        /// Returns <c>null</c> if the header field is not present.
        /// See also <see cref="ContentLocation"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentBase
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Base");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Base");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Base");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-Base",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-Location header field.
        /// </summary>
        /// <remarks>
        /// Specifies a URI associated with this MIME entity.
        /// The URI may identify the original location of the content or provide a
        /// globally unique identifier for it.
        /// Returns <c>null</c> if the header field is not present.
        /// See also <see cref="ContentBase"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentLocation
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Location");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Location");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Location");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-Location",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-features header field.
        /// </summary>
        /// <remarks>
        /// Specifies media feature information associated with this MIME entity.
        /// Returns <c>null</c> if the header field is not present.
        /// This header is rarely used in modern MIME messages.
        /// See RFC 2533 and RFC 2506.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? Contentfeatures
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-features");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                if(value == null){
                    m_pHeader.RemoveAll("Content-features");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-features");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-features",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-Disposition header field.
        /// </summary>
        /// <remarks>
        /// Specifies how the MIME entity should be presented to the recipient.
        /// Common disposition types are <c>inline</c> and <c>attachment</c>.
        /// This header may also specify a suggested file name and other
        /// disposition parameters.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2183.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the header field cannot be parsed as a
        /// <see cref="MIME_h_ContentDisposition"/>.
        /// </exception>
        public MIME_h_ContentDisposition? ContentDisposition
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Disposition");
                if(h != null){
                    if(!(h is MIME_h_ContentDisposition)){
                        throw new ParseException("Header field 'ContentDisposition' parsing failed.");
                    }

                    return (MIME_h_ContentDisposition)h;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Disposition");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Disposition");
                    if(h == null){
                        m_pHeader.Add(value);
                    }
                    else{
                        m_pHeader.ReplaceFirst(value);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-Language header field.
        /// </summary>
        /// <remarks>
        /// Specifies the natural language or languages of this MIME entity.
        /// Common values include language tags such as <c>en</c>, <c>en-US</c>,
        /// and <c>et</c>.
        /// Returns <c>null</c> if the header field is not present.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentLanguage
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Language");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Language");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Language");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-Language",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Content-Alternative header field.
        /// </summary>
        /// <remarks>
        /// Provides information about alternative representations of this MIME entity.
        /// Returns <c>null</c> if the header field is not present.
        /// This header is rarely used in modern MIME messages.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentAlternative
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Alternative");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Alternative");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Alternative");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-Alternative",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the Content-MD5 header field.
        /// </summary>
        /// <remarks>
        /// Specifies an MD5 checksum of the entity body for integrity verification.
        /// Returns <c>null</c> if the header field is not present.
        /// This header is rarely used in modern MIME messages.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentMD5
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-MD5");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-MD5");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-MD5");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-MD5",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the Content-Duration header field.
        /// </summary>
        /// <remarks>
        /// Specifies the duration of the entity body, in seconds.
        /// Returns <c>null</c> if the header field is not present.
        /// This header is most commonly used with audio and video content and is
        /// rarely used in modern MIME messages.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ContentDuration
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = m_pHeader.GetFirst("Content-Duration");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    m_pHeader.RemoveAll("Content-Duration");
                }
                else{
                    MIME_h? h = m_pHeader.GetFirst("Content-Duration");
                    if(h == null){
                        h = new MIME_h_Unstructured("Content-Duration",value);
                        m_pHeader.Add(h);
                    }
                    else{
                        ((MIME_h_Unstructured)h).Value = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the MIME entity body.
        /// </summary>
        /// <remarks>
        /// Returns <c>null</c> if the entity does not currently have a body.
        /// Assigning <c>null</c> is not permitted.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Is raised when a null value is assigned.
        /// </exception>
        public MIME_b? Body
        {
            get{ return m_pBody; }

            set{
                if(value == null){
                    throw new ArgumentNullException("Body");
                }

                m_pBody = value;
                m_pBody.SetParent(this,true);
            }
        }
                        
        #endregion

    }
}
