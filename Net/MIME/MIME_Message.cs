using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using LumiSoft.Net.IO;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME message as defined by RFC 2045 section 2.3.
    /// </summary>
    /// <remarks>
    /// A MIME message is a specialized <see cref="MIME_Entity"/> that
    /// represents a complete MIME message. In addition to the functionality
    /// provided by <see cref="MIME_Entity"/>, this class provides methods
    /// for parsing and serializing MIME messages.
    /// </remarks>
    public class MIME_Message : MIME_Entity
    {
        private Stream? m_pStream = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_Message"/> class.
        /// </summary>
        public MIME_Message()
        {
        }

        #region override method Dispose

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        /// <remarks>
        /// If this instance owns a backing stream, the stream is disposed.
        /// </remarks>
        public override void Dispose()
        {
            if(m_pStream != null){
                m_pStream.Dispose();
                m_pStream = null;
            }

            base.Dispose();
        }

        #endregion


        #region static method ParseFromStream

        /// <summary>
        /// Parses a MIME message from the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream from which to parse the MIME message. Parsing begins at the
        /// current stream position.
        /// </param>
        /// <param name="persistentStream">
        /// Specifies whether the parsed MIME message retains access to the
        /// source stream after parsing. If <see langword="true"/>, the source
        /// stream must remain available for the lifetime of the parsed MIME
        /// message. If <see langword="false"/>, the source stream is no longer
        /// required after parsing completes.
        /// </param>
        /// <param name="ownsStream">
        /// Specifies whether the returned MIME message takes ownership of the
        /// backing stream and disposes it when the MIME message is disposed.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// This method blocks until the MIME message has been completely parsed.
        /// For asynchronous operation, use
        /// <see cref="ParseFromStreamAsync(Stream,bool,bool,CancellationToken)"/>.
        /// </remarks>
        public static MIME_Message ParseFromStream(Stream stream,bool persistentStream,bool ownsStream)
        {
            return ParseFromStreamAsync(stream,persistentStream,ownsStream,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region static method ParseFromStreamAsync

        /// <summary>
        /// Parses a MIME message from the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream from which to parse the MIME message. Parsing begins at the
        /// current stream position.
        /// </param>
        /// <param name="persistentStream">
        /// Specifies whether the parsed MIME message retains access to the
        /// source stream after parsing. If <see langword="true"/>, the source
        /// stream must remain available for the lifetime of the parsed MIME
        /// message. If <see langword="false"/>, the source stream is no longer
        /// required after parsing completes.
        /// </param>
        /// <param name="ownsStream">
        /// Specifies whether the returned MIME message takes ownership of the
        /// backing stream and disposes it when the MIME message is disposed.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// When <paramref name="persistentStream"/> is
        /// <see langword="true"/>, the parser uses the supplied stream directly
        /// if it supports seeking. Otherwise, the stream content is copied to an
        /// internal backing store which remains available for the lifetime of
        /// the parsed MIME message.
        ///
        /// When <paramref name="ownsStream"/> is
        /// <see langword="true"/>, the returned MIME message takes ownership of
        /// the backing stream and disposes it when the MIME message is disposed.
        /// </remarks>
        public static async Task<MIME_Message> ParseFromStreamAsync(Stream stream,bool persistentStream,bool ownsStream,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            
            // Stream not persistent or is not seekable, create copy of stream.
            if(!persistentStream ||!stream.CanSeek){
                var ms = new MemoryStreamEx(64000);
                await stream.CopyToAsync(ms,cancellationToken);
                if(ownsStream) {
                    await stream.DisposeAsync();
                }
                stream = ms;
                stream.Position = 0;
                ownsStream = true;
            }

            MIME_Message retVal = new MIME_Message();
            if(ownsStream){
                retVal.m_pStream = stream;
            }
            await retVal.ParseInternalAsync(stream,cancellationToken);

            return retVal;
        }

        #endregion

        #region static method ParseFromFile

        /// <summary>
        /// Parses a MIME message from the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File from which to parse the MIME message.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// This method blocks until the MIME message has been completely parsed.
        /// For asynchronous operation, use
        /// <see cref="ParseFromFileAsync(string,CancellationToken)"/>.
        /// </remarks>
        public static MIME_Message ParseFromFile(string fileName)
        {
            return ParseFromFileAsync(fileName,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region static method ParseFromFileAsync

        /// <summary>
        /// Parses a MIME message from the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File from which to parse the MIME message.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        public static async Task<MIME_Message> ParseFromFileAsync(string fileName,CancellationToken cancellationToken = default)
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }
            if(fileName.Length == 0){
                throw new ArgumentException("Argument 'fileName' cannot be empty.",nameof(fileName));
            }

            var stream = File.OpenRead(fileName);

            return await ParseFromStreamAsync(stream,true,true,cancellationToken);
        }

        #endregion

        #region static method ParseFromByteArray

        /// <summary>
        /// Parses a MIME message from the specified byte array.
        /// </summary>
        /// <param name="data">
        /// MIME message data.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// This method blocks until the MIME message has been completely parsed.
        /// For asynchronous operation, use
        /// <see cref="ParseFromByteArrayAsync(byte[],CancellationToken)"/>.
        /// </remarks>
        public static MIME_Message ParseFromByteArray(byte[] data)
        {
            return ParseFromByteArrayAsync(data,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region static method ParseFromByteArrayAsync

        /// <summary>
        /// Parses a MIME message from the specified byte array.
        /// </summary>
        /// <param name="data">
        /// MIME message data.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// The returned MIME message owns the internal stream created for the
        /// specified byte array and releases it when disposed.
        /// </remarks>
        public static async Task<MIME_Message> ParseFromByteArrayAsync(byte[] data,CancellationToken cancellationToken = default)
        {
            if(data == null){
                throw new ArgumentNullException(nameof(data));
            }

            return await ParseFromStreamAsync(new MemoryStream(data,false),true,true,cancellationToken);
        }

        #endregion


        #region method GetAllEntities

        /// <summary>
        /// Gets all MIME entities contained in this message.
        /// </summary>
        /// <param name="includeEmbbedMessage">
        /// Specifies whether entities contained in embedded
        /// <c>message/rfc822</c> body parts are included.
        /// </param>
        /// <returns>
        /// Returns all MIME entities in the MIME tree, including the current
        /// entity.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this method is accessed.
        /// </exception>
        public MIME_Entity[] GetAllEntities(bool includeEmbbedMessage)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            List<MIME_Entity>  retVal       = new List<MIME_Entity>();
            List<MIME_Entity> entitiesQueue = new List<MIME_Entity>();
            entitiesQueue.Add(this);
            
            while(entitiesQueue.Count > 0){
                MIME_Entity currentEntity = entitiesQueue[0];
                entitiesQueue.RemoveAt(0);
        
                retVal.Add(currentEntity);

                // Current entity is multipart entity, add it's body-parts for processing.
                if(currentEntity.Body != null && currentEntity.Body.GetType().IsSubclassOf(typeof(MIME_b_Multipart))){
                    MIME_EntityCollection bodyParts = ((MIME_b_Multipart)currentEntity.Body).BodyParts;
                    for(int i=0;i<bodyParts.Count;i++){
                        entitiesQueue.Insert(i,bodyParts[i]);
                    }
                }
                // Add embbed message for processing (Embbed message entities will be included).
                else if(includeEmbbedMessage && currentEntity.Body != null && currentEntity.Body is MIME_b_MessageRfc822){
                    entitiesQueue.Add(((MIME_b_MessageRfc822)currentEntity.Body).Message);
                }
            }

            return retVal.ToArray();
        }

        #endregion

        #region method GetEntityByCID

        /// <summary>
        /// Gets the MIME entity with the specified Content-ID.
        /// </summary>
        /// <param name="cid">
        /// Content-ID value to search for.
        /// </param>
        /// <returns>
        /// Returns the MIME entity with the specified Content-ID, or
        /// <see langword="null"/> if no matching entity is found.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this method is accessed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="cid"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="cid"/> is empty.
        /// </exception>
        public MIME_Entity? GetEntityByCID(string cid)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(cid == null){
                throw new ArgumentNullException(nameof(cid));
            }
            if(cid == ""){
                throw new ArgumentException("Argument 'cid' value must be specified.",nameof(cid));
            }

            foreach(MIME_Entity entity in this.AllEntities){
                if(entity.ContentID == cid){
                    return entity;
                }
            }

            return null;
        }

        #endregion

        // TODO:
        //public MIME_Entity GetEntityByPartsSpecifier(string partsSpecifier)

        #region method ConvertToMultipartSigned

        /// <summary>
        /// Converts this message to a <c>multipart/signed</c> message.
        /// </summary>
        /// <param name="signerCert">
        /// Certificate used to generate the message signature.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this method is accessed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="signerCert"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the message is already signed.
        /// </exception>
        /// <remarks>
        /// The current message body is wrapped in a
        /// <see cref="MIME_b_MultipartSigned"/> body and configured to be signed
        /// with the specified certificate.
        /// </remarks>
        public void ConvertToMultipartSigned(X509Certificate2 signerCert)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(signerCert == null){
                throw new ArgumentNullException(nameof(signerCert));
            }
            if(this.IsSigned){
                throw new InvalidOperationException("Message is already signed.");
            }

            MIME_Entity msgEntity = new MIME_Entity();
            msgEntity.Body = this.Body;
            msgEntity.ContentDisposition = this.ContentDisposition;
            msgEntity.ContentTransferEncoding = this.ContentTransferEncoding;
            this.ContentTransferEncoding = null;

            MIME_b_MultipartSigned multipartSigned = new MIME_b_MultipartSigned();
            this.Body = multipartSigned;
            multipartSigned.SetCertificate(signerCert);
            multipartSigned.BodyParts.Add(msgEntity);
        }

        #endregion

        #region method VerifySignatures

        /// <summary>
        /// Verifies all S/MIME signatures contained in this message.
        /// </summary>
        /// <returns>
        /// Returns <see langword="true"/> if all detected signatures are valid;
        /// otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this method is accessed.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when a signature uses an unsupported cryptographic algorithm.
        /// </exception>
        /// <remarks>
        /// If the message does not contain any signed entities, this method
        /// returns <see langword="true"/>.
        /// </remarks>
        public bool VerifySignatures()
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            foreach(MIME_Entity entity in this.AllEntities){
                if(string.Equals(entity.ContentType?.TypeWithSubtype,MIME_MediaTypes.Application.pkcs7_mime,StringComparison.InvariantCultureIgnoreCase)){
                    if(!((MIME_b_ApplicationPkcs7Mime)entity.Body!).VerifySignature()){
                        return false;
                    }
                }
                else if(string.Equals(entity.ContentType?.TypeWithSubtype,MIME_MediaTypes.Multipart.signed,StringComparison.InvariantCultureIgnoreCase)){
                    if(!((MIME_b_MultipartSigned)entity.Body!).VerifySignature()){
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets a value indicating whether the message contains signed content.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="true"/> if the message contains an S/MIME
        /// signed entity, such as <c>multipart/signed</c> or
        /// <c>application/pkcs7-mime</c>; otherwise <see langword="false"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this property is accessed.
        /// </exception>
        public bool IsSigned
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                foreach(MIME_Entity entity in this.AllEntities){
                    if(string.Equals(entity.ContentType?.TypeWithSubtype,MIME_MediaTypes.Application.pkcs7_mime,StringComparison.InvariantCultureIgnoreCase)){
                        return true;
                    }
                    else if(string.Equals(entity.ContentType?.TypeWithSubtype,MIME_MediaTypes.Multipart.signed,StringComparison.InvariantCultureIgnoreCase)){
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Gets all MIME entities contained in this message.
        /// </summary>
        /// <remarks>
        /// The returned array includes the current entity and all nested MIME
        /// entities. Entities contained in embedded <c>message/rfc822</c>
        /// body parts are also included.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this property is accessed.
        /// </exception>
        public MIME_Entity[] AllEntities
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                List<MIME_Entity> retVal        = new List<MIME_Entity>();
                List<MIME_Entity> entitiesQueue = new List<MIME_Entity>();
                entitiesQueue.Add(this);
            
                while(entitiesQueue.Count > 0){
                    MIME_Entity currentEntity = entitiesQueue[0];
                    entitiesQueue.RemoveAt(0);
        
                    retVal.Add(currentEntity);

                    // Current entity is multipart entity, add it's body-parts for processing.
                    if(currentEntity.Body != null && currentEntity.Body.GetType().IsSubclassOf(typeof(MIME_b_Multipart))){
                        MIME_EntityCollection bodyParts = ((MIME_b_Multipart)currentEntity.Body).BodyParts;
                        for(int i=0;i<bodyParts.Count;i++){
                            entitiesQueue.Insert(i,bodyParts[i]);
                        }
                    }
                    // Add embbed message for processing (Embbed message entities will be included).
                    else if(currentEntity.Body != null && currentEntity.Body is MIME_b_MessageRfc822){
                        entitiesQueue.Add(((MIME_b_MessageRfc822)currentEntity.Body).Message);
                    }
                }

                return retVal.ToArray();
            }
        }

        #endregion

    }
}
