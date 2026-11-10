using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.IO;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents the base class for single-part MIME bodies, such as
    /// <c>application/*</c>, <c>text/*</c>, <c>image/*</c>,
    /// <c>audio/*</c>, and <c>video/*</c>.
    /// </summary>
    public abstract class MIME_b_SinglepartBase : MIME_b
    {
        private bool   m_IsModified         = false;
        private Stream m_pEncodedDataStream;
                
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MIME_b_SinglepartBase"/> class.
        /// </summary>
        /// <param name="contentType">
        /// Content type of the MIME body.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="contentType"/> is
        /// <see langword="null"/>.
        /// </exception>
        public MIME_b_SinglepartBase(MIME_h_ContentType contentType) : base(contentType)
        {
            if(contentType == null){
                throw new ArgumentNullException(nameof(contentType));
            }

            m_pEncodedDataStream = new MemoryStreamEx();
        }

        #region override method Dispose

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        internal override void Dispose()
        {
            m_pEncodedDataStream.Dispose();
        }

        #endregion
        
  
        #region override SetParent

        /// <summary>
        /// Sets body parent.
        /// </summary>
        /// <param name="entity">Owner entity.</param>
        /// <param name="setContentType">If true sets entity.ContentType header value.</param>
        internal override void SetParent(MIME_Entity entity,bool setContentType)
        {
            base.SetParent(entity,setContentType);

            // Owner entity has no content-type or has different content-type, just add/overwrite it.
            if(setContentType && (entity.ContentType == null || !string.Equals(entity.ContentType.TypeWithSubtype,this.MediaType,StringComparison.InvariantCultureIgnoreCase))){
                entity.ContentType = new MIME_h_ContentType(MediaType);
            }
        }

        #endregion

        #region override method ToStreamAsync

        /// <inheritdoc/>
        internal protected override async Task ToStreamAsync(Stream stream,bool utf8Headers,bool reEncodeHeaders,CancellationToken cancellationToken = default)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            await GetEncodedDataStream().CopyToAsync(stream,cancellationToken);
        }

        #endregion

        #region method SetModified

        /// <summary>
        /// Sets IsModified property value.
        /// </summary>
        /// <param name="isModified">Modified flag.</param>
        protected void SetModified(bool isModified)
        {
            m_IsModified = isModified;
        }

        #endregion


        #region method GetEncodedDataStream

        /// <summary>
        /// Gets a read-only stream containing the encoded body data.
        /// </summary>
        /// <returns>
        /// Returns a stream that provides access to the encoded body data.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <remarks>
        /// The returned stream should be disposed as soon as it is no longer
        /// needed.
        ///
        /// If this body belongs to a parsed MIME message, no other entity body
        /// streams should be accessed until the returned stream has been
        /// disposed, because parsed MIME entities may share the same underlying
        /// backing stream.
        /// </remarks>
        public Stream GetEncodedDataStream()
        {
            if(this.Entity == null){
                throw new InvalidOperationException("Body must be associated with a MIME entity.");
            }

            m_pEncodedDataStream.Position = 0;

            return new ReadWriteControlledStream(m_pEncodedDataStream,FileAccess.Read);
        }

        #endregion

        #region method SetEncodedData

        /// <summary>
        /// Sets the encoded body data.
        /// </summary>
        /// <param name="stream">
        /// Stream containing body data already encoded with the specified
        /// transfer encoding.
        /// </param>
        /// <param name="transferEncoding">
        /// Content transfer encoding of the supplied data.
        /// </param>
        /// <remarks>
        /// The supplied data is stored without additional encoding and the
        /// Content-Transfer-Encoding header field is updated accordingly.
        ///
        /// This method blocks until the body data has been completely stored.
        /// For asynchronous operation, use
        /// <see cref="SetEncodedDataAsync(Stream,string,CancellationToken)"/>.
        /// </remarks>
        public void SetEncodedData(Stream stream,string transferEncoding)
        {
            SetEncodedDataAsync(stream,transferEncoding,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method SetEncodedDataAsync

        /// <summary>
        /// Sets the encoded body data.
        /// </summary>
        /// <param name="stream">
        /// Stream containing body data already encoded with the specified
        /// transfer encoding.
        /// </param>
        /// <param name="transferEncoding">
        /// Content transfer encoding of the supplied data.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> or
        /// <paramref name="transferEncoding"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="transferEncoding"/> is empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <remarks>
        /// The supplied data is stored without additional encoding and the
        /// Content-Transfer-Encoding header field is updated accordingly.
        /// </remarks>
        public async Task SetEncodedDataAsync(Stream stream,string transferEncoding,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(transferEncoding == null){
                throw new ArgumentNullException(nameof(transferEncoding));
            }
            if(string.IsNullOrEmpty(transferEncoding)){
                throw new ArgumentException("Transfer encoding cannot be empty.",nameof(transferEncoding));
            }
            if(this.Entity == null){
                throw new InvalidOperationException("Body must be associated with a MIME entity.");
            }

            // Owner entity has no content-type or has different content-type, just add/overwrite it.
            if(this.Entity.ContentType == null || !string.Equals(this.Entity.ContentType.TypeWithSubtype,this.MediaType,StringComparison.InvariantCultureIgnoreCase)){
                this.Entity.ContentType = new MIME_h_ContentType(this.MediaType);
            }
            this.Entity.ContentTransferEncoding = transferEncoding;

            // Release old stream and create new memory-tmp file stream.
            await m_pEncodedDataStream.DisposeAsync();
            m_pEncodedDataStream = new MemoryStreamEx();

            await stream.CopyToAsync(m_pEncodedDataStream,cancellationToken);
            m_pEncodedDataStream.Position = 0;
        }

        #endregion

        #region method GetDataStream

        /// <summary>
        /// Gets a read-only stream containing the decoded body data.
        /// </summary>
        /// <returns>
        /// Returns a stream that provides access to the decoded body data.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the body uses an unsupported
        /// <c>Content-Transfer-Encoding</c>.
        /// </exception>
        /// <remarks>
        /// The returned stream should be disposed as soon as it is no longer
        /// needed.
        ///
        /// If this body belongs to a parsed MIME message, no other entity body
        /// streams should be accessed until the returned stream has been
        /// disposed, because parsed MIME entities may share the same underlying
        /// backing stream.
        /// </remarks>
        public Stream GetDataStream()
        {             
            if(this.Entity == null){
                throw new InvalidOperationException("Body must be associated with a MIME entity.");
            }

            /* RFC 2045 6.1.
                This is the default value -- that is, "Content-Transfer-Encoding: 7BIT" is assumed if the
                Content-Transfer-Encoding header field is not present.
            */
            string transferEncoding = MIME_TransferEncodings.SevenBit;
            if(this.Entity.ContentTransferEncoding != null){
                transferEncoding = this.Entity.ContentTransferEncoding.ToLowerInvariant();
            }

            m_pEncodedDataStream.Position = 0;            
            if(transferEncoding == MIME_TransferEncodings.QuotedPrintable){                
                return new QuotedPrintableStream(m_pEncodedDataStream,FileAccess.Read);
            }
            else if(transferEncoding == MIME_TransferEncodings.Base64){
                return new Base64Stream(m_pEncodedDataStream,false,true,FileAccess.Read);
            }            
            else if(transferEncoding == MIME_TransferEncodings.Binary){
                return new ReadWriteControlledStream(m_pEncodedDataStream,FileAccess.Read);
            }
            else if(transferEncoding == MIME_TransferEncodings.EightBit){
                return new ReadWriteControlledStream(m_pEncodedDataStream,FileAccess.Read);
            }
            else if(transferEncoding == MIME_TransferEncodings.SevenBit){
                return new ReadWriteControlledStream(m_pEncodedDataStream,FileAccess.Read);
            }
            else{
                throw new NotSupportedException($"Content-Transfer-Encoding '{this.Entity.ContentTransferEncoding}' is not supported.");
            }
        }

        #endregion

        #region method SetData

        /// <summary>
        /// Sets the body data.
        /// </summary>
        /// <param name="stream">
        /// Stream containing the body data.
        /// </param>
        /// <param name="transferEncoding">
        /// Content transfer encoding to use when storing the body data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> or
        /// <paramref name="transferEncoding"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="transferEncoding"/> is empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the specified content transfer encoding is not supported.
        /// </exception>
        /// <remarks>
        /// The supplied data is encoded using the specified content transfer
        /// encoding and stored as the body content. The
        /// <c>Content-Transfer-Encoding</c> header field is updated
        /// accordingly.
        ///
        /// This method blocks until the body data has been completely stored.
        /// For asynchronous operation, use
        /// <see cref="SetDataAsync(Stream,string,CancellationToken)"/>.
        /// </remarks>
        public void SetData(Stream stream, string transferEncoding)
        {
            SetDataAsync(stream,transferEncoding,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method SetDataAsync

        /// <summary>
        /// Sets the body data.
        /// </summary>
        /// <param name="stream">
        /// Stream containing the body data.
        /// </param>
        /// <param name="transferEncoding">
        /// Content transfer encoding to use when storing the body data.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> or
        /// <paramref name="transferEncoding"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="transferEncoding"/> is empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the specified content transfer encoding is not supported.
        /// </exception>
        /// <remarks>
        /// The supplied data is encoded using the specified content transfer
        /// encoding and stored as the body content. The
        /// <c>Content-Transfer-Encoding</c> header field is updated
        /// accordingly.
        /// </remarks>
        public async Task SetDataAsync(Stream stream,string transferEncoding,CancellationToken cancellationToken = default)
        {            
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(transferEncoding == null){
                throw new ArgumentNullException(nameof(transferEncoding));
            }
            if(transferEncoding.Length == 0){
                throw new ArgumentException("Transfer encoding cannot be empty.",nameof(transferEncoding));
            }
            if(this.Entity == null){
                throw new InvalidOperationException("Body must be associated with a MIME entity.");
            }

            if(string.Equals(transferEncoding,MIME_TransferEncodings.QuotedPrintable,StringComparison.InvariantCultureIgnoreCase)){
                // Release old stream and create new memory-tmp file stream.
                await m_pEncodedDataStream.DisposeAsync();
                m_pEncodedDataStream = new MemoryStreamEx();

                this.Entity.ContentTransferEncoding = transferEncoding;
                using(var encoderStream = new QuotedPrintableStream(m_pEncodedDataStream,FileAccess.ReadWrite)){
                    await stream.CopyToAsync(encoderStream,cancellationToken);
                }
                m_pEncodedDataStream.Position = 0;
            }
            else if(string.Equals(transferEncoding,MIME_TransferEncodings.Base64,StringComparison.InvariantCultureIgnoreCase)){
                // Release old stream and create new memory-tmp file stream.
                await m_pEncodedDataStream.DisposeAsync();
                m_pEncodedDataStream = new MemoryStreamEx();

                this.Entity.ContentTransferEncoding = transferEncoding;
                using(var encoderStream = new Base64Stream(m_pEncodedDataStream,false,true,FileAccess.ReadWrite)){
                    await stream.CopyToAsync(encoderStream,cancellationToken);
                }
                m_pEncodedDataStream.Position = 0;
            }            
            else if(string.Equals(transferEncoding,MIME_TransferEncodings.Binary,StringComparison.InvariantCultureIgnoreCase)){
                await SetEncodedDataAsync(stream,transferEncoding,cancellationToken);
            }
            else if(string.Equals(transferEncoding,MIME_TransferEncodings.EightBit,StringComparison.InvariantCultureIgnoreCase)){
                await SetEncodedDataAsync(stream,transferEncoding,cancellationToken);
            }
            else if(string.Equals(transferEncoding,MIME_TransferEncodings.SevenBit,StringComparison.InvariantCultureIgnoreCase)){
                await SetEncodedDataAsync(stream,transferEncoding,cancellationToken);
            }
            else{
                throw new NotSupportedException($"Content-Transfer-Encoding '{transferEncoding}' is not supported.");
            }
        }

        #endregion

        #region method SetDataFromFile

        /// <summary>
        /// Sets body data from the specified file.
        /// </summary>
        /// <param name="file">File name with optional path.</param>
        /// <param name="transferEncoding">Specifies content-transfer-encoding to use to encode data.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>file</b> is null reference.</exception>
        /// <exception cref="InvalidOperationException">Is raised when this method is accessed and this body is not bounded to any entity.</exception>
        public void SetDataFromFile(string file,string transferEncoding)
        {
            if(file == null){
                throw new ArgumentNullException("file");
            }
            
            using(FileStream fs = File.OpenRead(file)){
                SetData(fs,transferEncoding);
            }            
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
        /// Raised when <paramref name="stream"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the body uses an unsupported
        /// <c>Content-Transfer-Encoding</c>.
        /// </exception>
        /// <remarks>
        /// The body data is decoded according to the
        /// <c>Content-Transfer-Encoding</c> header field before being written
        /// to the specified stream.
        ///
        /// This method blocks until the body data has been completely written.
        /// For asynchronous operation, use
        /// <see cref="DataToStreamAsync(Stream,CancellationToken)"/>.
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
        /// Raised when <paramref name="stream"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the body uses an unsupported
        /// <c>Content-Transfer-Encoding</c>.
        /// </exception>
        /// <remarks>
        /// The body data is decoded according to the
        /// <c>Content-Transfer-Encoding</c> header field before being written
        /// to the specified stream.
        /// </remarks>
        public async Task DataToStreamAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            using(Stream dataStream = GetDataStream()){
                await dataStream.CopyToAsync(stream,128000,cancellationToken);
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
        /// Raised when <paramref name="fileName"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the body uses an unsupported
        /// <c>Content-Transfer-Encoding</c>.
        /// </exception>
        /// <remarks>
        /// This method blocks until the body data has been completely written.
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
        /// Raised when <paramref name="fileName"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when this body is not associated with a MIME entity.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the body uses an unsupported
        /// <c>Content-Transfer-Encoding</c>.
        /// </exception>
        /// <remarks>
        /// The body data is decoded according to the
        /// <c>Content-Transfer-Encoding</c> header field before being written
        /// to the specified file.
        /// </remarks>
        public async Task DataToFileAsync(string fileName,CancellationToken cancellationToken = default)
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }
            if(fileName.Length == 0){
                throw new ArgumentException("File name cannot be empty.",nameof(fileName));
            }

            await using(FileStream stream = File.Create(fileName)){
                await DataToStreamAsync(stream,cancellationToken);
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this header field has been modified
        /// since it was parsed.
        /// </summary>
        public override bool IsModified
        {
            get{ return m_IsModified; }
        }
                
        /// <summary>
        /// Gets the size of the encoded body data, in bytes.
        /// </summary>
        public long EncodedDataSize
        {
            get{ return m_pEncodedDataStream.Length; }
        }

        /// <summary>
        /// Gets the encoded body data.
        /// </summary>
        /// <remarks>
        /// Use this property with care because the encoded body data is loaded
        /// into memory. For large bodies, this may require a significant amount
        /// of memory.
        ///
        /// For large amounts of data, use
        /// <see cref="GetEncodedDataStream"/> instead.
        /// </remarks>
        public byte[] EncodedData
        {
            get{ 
                MemoryStream ms = new MemoryStream();
                Net_Utils.StreamCopy(this.GetEncodedDataStream(),ms,84000);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Gets the decoded body data.
        /// </summary>
        /// <remarks>
        /// Accessing this property creates a copy of the decoded body data in
        /// memory.
        ///
        /// For large amounts of data, use
        /// <see cref="GetDataStream"/> instead.
        /// </remarks>
        /// <exception cref="NotSupportedException">
        /// Raised when the body uses an unsupported
        /// <c>Content-Transfer-Encoding</c>.
        /// </exception>
        public byte[] Data
        {
            get{
                MemoryStream ms = new MemoryStream();
                Net_Utils.StreamCopy(this.GetDataStream(),ms,84000);

                return ms.ToArray(); 
            }
        }


        /// <summary>
        /// Gets or sets encoded data stream.
        /// </summary>
        protected Stream EncodedStream
        {
            get{ return m_pEncodedDataStream; }

            set{ m_pEncodedDataStream = value; }
        }

        #endregion
    }
}
