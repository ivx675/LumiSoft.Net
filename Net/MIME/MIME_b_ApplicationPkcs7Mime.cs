using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME <c>application/pkcs7-mime</c> body.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 5751 section 3.2. This media type is used to carry
    /// S/MIME signed or encrypted CMS data.
    /// </remarks>
    public class MIME_b_ApplicationPkcs7Mime : MIME_b_Application
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public MIME_b_ApplicationPkcs7Mime() : base(MIME_MediaTypes.Application.pkcs7_mime)
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses an <c>application/pkcs7-mime</c> body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">Stream containing the encoded body data.</param>
        /// <returns>Returns the parsed MIME body.</returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="owner"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        internal static new MIME_b_ApplicationPkcs7Mime Parse(MIME_Entity owner,Stream stream)
        {
            if(owner == null){
                throw new ArgumentNullException(nameof(owner));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            MIME_b_ApplicationPkcs7Mime retVal = new MIME_b_ApplicationPkcs7Mime();
            retVal.EncodedStream = stream;
            retVal.SetModified(false);

            return retVal;
        }

        #endregion


        #region method GetCertificates

        /// <summary>
        /// Gets the certificates contained in the PKCS #7 message.
        /// </summary>
        /// <returns>
        /// Returns the certificates contained in the PKCS #7 message, or
        /// <see langword="null"/> if no data is available.
        /// </returns>
        public X509Certificate2Collection? GetCertificates()
        {
            if(this.Data == null){
                return null;
            }

            SignedCms signedCms = new SignedCms();
            signedCms.Decode(this.Data);

            return signedCms.Certificates;
        }

        #endregion
                
        #region method VerifySignature

        /// <summary>
        /// Verifies that the contained signature is valid and that the signed
        /// content has not been modified.
        /// </summary>
        /// <returns>
        /// Returns <see langword="true"/> if the signature is valid; otherwise
        /// <see langword="false"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body does not contain signed-data.
        /// </exception>
        public bool VerifySignature()
        {
            if(this.Entity == null) {
                throw new InvalidOperationException("Body is not associated with an MIME entity.");
            }
            if(this.Entity.ContentType == null){
                throw new InvalidOperationException("MIME entity Content-Type is not set.");
            }

            if (!string.Equals(this.Entity.ContentType.Parameters["smime-type"],"signed-data",StringComparison.InvariantCultureIgnoreCase)){
                throw new InvalidOperationException("The method is only valid if Content-Type parameter smime-type=signed-data.");
            }

            // Check this.Data exists.
            if(this.Data == null){
               return false;
            }

            try{
                SignedCms signedCms = new SignedCms();
                signedCms.Decode(this.Data);
                signedCms.CheckSignature(true);

                return true;
            }
            catch{                
            }

            return false;
        }

        #endregion
               
        #region method GetSignedMime

        /// <summary>
        /// Gets the MIME message contained in a signed-data S/MIME message.
        /// </summary>
        /// <returns>
        /// Returns the signed MIME message, or <see langword="null"/> if no
        /// content is available.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body does not contain signed-data.
        /// </exception>
        public MIME_Message? GetSignedMime()
        {
            if(this.Entity == null) {
                throw new InvalidOperationException("Body is not associated with an MIME entity.");
            }
            if(this.Entity.ContentType == null){
                throw new InvalidOperationException("MIME entity Content-Type is not set.");
            }

            if(!string.Equals(this.Entity.ContentType.Parameters["smime-type"],"signed-data",StringComparison.InvariantCultureIgnoreCase)){
                throw new InvalidOperationException("The method is only valid if Content-Type parameter smime-type=signed-data.");
            }

            if(this.Data != null){
                SignedCms signedCms = new SignedCms();
                signedCms.Decode(this.Data);

                return MIME_Message.ParseFromStream(new MemoryStream(signedCms.ContentInfo.Content),true,true);
            }
            else{
                return null;
            }
        }

        #endregion
                
        #region method GetEnvelopedMime

        /// <summary>
        /// Decrypts and returns the MIME message contained in an enveloped-data
        /// S/MIME message.
        /// </summary>
        /// <param name="cert">
        /// Certificate used to decrypt the message.
        /// </param>
        /// <returns>
        /// Returns the decrypted MIME message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="cert"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body does not contain enveloped-data.
        /// </exception>
        public MIME_Message GetEnvelopedMime(X509Certificate2 cert)
        {
            if(cert == null){
                throw new ArgumentNullException(nameof(cert));
            }
            if(this.Entity == null) {
                throw new InvalidOperationException("Body is not associated with an MIME entity.");
            }
            if(this.Entity.ContentType == null){
                throw new InvalidOperationException("MIME entity Content-Type is not set.");
            }

            if(!string.Equals(this.Entity.ContentType.Parameters["smime-type"],"enveloped-data",StringComparison.InvariantCultureIgnoreCase)){
                throw new InvalidOperationException("The method is only valid if Content-Type parameter smime-type=enveloped-data.");
            }

            EnvelopedCms envelopedCms = new EnvelopedCms();          
            envelopedCms.Decode(this.Data);

            X509Certificate2Collection certificates = new X509Certificate2Collection(cert);
            envelopedCms.Decrypt(certificates);
            
            return MIME_Message.ParseFromStream(new MemoryStream(envelopedCms.ContentInfo.Content),true,true);
        }

        #endregion


        #region Properties implementation

        #endregion
    }
}
