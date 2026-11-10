using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME multipart/signed body. Defined in RFC 5751.
    /// </summary>
    public class MIME_b_MultipartSigned : MIME_b_Multipart
    {
        private X509Certificate2? m_pSignerCert = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartSigned"/> class.
        /// The protocol is set to 'application/x-pkcs7-signature', the micalg
        /// parameter is set to 'sha1', and an automatically generated boundary
        /// is assigned.
        /// </summary>
        public MIME_b_MultipartSigned() : base()
        {
            MIME_h_ContentType contentType_multipartSigned = new MIME_h_ContentType(MIME_MediaTypes.Multipart.signed);
            contentType_multipartSigned.Parameters["protocol"] = "application/x-pkcs7-signature";
            contentType_multipartSigned.Parameters["micalg"]   = "sha1";
            contentType_multipartSigned.Param_Boundary         = Guid.NewGuid().ToString().Replace('-','.');
            this.ContentType = contentType_multipartSigned;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartSigned"/> class.
        /// </summary>
        /// <param name="contentType">Content type.</param>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>contentType</b> is null reference.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Is raised when <b>contentType</b> is not 'multipart/signed'.
        /// </exception>
        public MIME_b_MultipartSigned(MIME_h_ContentType contentType) : base(contentType)
        {
            if(!string.Equals(contentType.TypeWithSubtype,"multipart/signed",StringComparison.CurrentCultureIgnoreCase)){
                throw new ArgumentException("Specified content type must be 'multipart/signed'.",nameof(contentType));
            }
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses MIME multipart/signed body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">Stream to parse MIME body from.</param>
        /// <returns>
        /// Returns parsed MIME multipart/signed body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>owner</b> or <b>stream</b> is null reference.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when multipart entity content type is missing required 'boundary' parameter.
        /// </exception>
        internal new static async Task<MIME_b_MultipartSigned> ParseAsync(MIME_Entity owner,Stream stream)
        {
            if(owner == null){
                throw new ArgumentNullException(nameof(owner));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(owner.ContentType == null || owner.ContentType.Param_Boundary == null){
                throw new ParseException("Multipart entity content type is missing required 'boundary' parameter.");
            }
          
            MIME_b_MultipartSigned retVal = new MIME_b_MultipartSigned(owner.ContentType);
            await retVal.ParseInternalAsync(owner,owner.ContentType.Param_Boundary,stream);

            return retVal;
        }

        #endregion


        #region method SetCertificate

        /// <summary>
        /// Sets the signer certificate used when generating the signature.
        /// </summary>
        /// <param name="signerCert">Signer certificate.</param>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>signerCert</b> is null reference.
        /// </exception>
        public void SetCertificate(X509Certificate2 signerCert)
        {
            if(signerCert == null){
                throw new ArgumentNullException("signerCert");
            }

            m_pSignerCert = signerCert;            
        }

        #endregion

        #region method GetCertificates

        /// <summary>
        /// Gets certificates contained in the PKCS #7 signature.
        /// </summary>
        /// <returns>
        /// Returns certificates contained in the PKCS #7 signature,
        /// or null if no certificates are available.
        /// </returns>
        public X509Certificate2Collection? GetCertificates()
        {
            // multipart/signed must always have only 2 entities, otherwise invalid data.
            if(this.BodyParts.Count != 2){
                return null;
            }
                
            // Get signature. It should be 2 entity.
            MIME_Entity signatureEntity = this.BodyParts[1];
            if(signatureEntity.Body != null){
                SignedCms signedCms = new SignedCms();
                signedCms.Decode(((MIME_b_SinglepartBase)signatureEntity.Body).Data);

                return signedCms.Certificates;
            }
            else{
                return null;
            }
        }

        #endregion

        #region method VerifySignature

        /// <summary>
        /// Verifies that the signature is valid and the signed data has not been modified.
        /// </summary>
        /// <returns>
        /// Returns true if the signature is valid; otherwise, false.
        /// </returns>
        public bool VerifySignature()
        {
            // Message is signed when it's saved out.
            if(m_pSignerCert != null){
                return true;
            }
            // multipart/signed must always contain exactly 2 body parts; otherwise the data is invalid.
            if(this.BodyParts.Count != 2){
                return false;
            }
                
            // Get signature. It should be the second entity.
            MIME_Entity signatureEntity = this.BodyParts[1];
            if(signatureEntity.Body == null){
                return false;
            }

            // Store entity to temporary stream.          
            MemoryStream tmpDataEntityStream = new MemoryStream();
            this.BodyParts[0].ToStream(tmpDataEntityStream);

            try{
                SignedCms signedCms = new SignedCms(new ContentInfo(tmpDataEntityStream.ToArray()),true);
                signedCms.Decode(((MIME_b_SinglepartBase)signatureEntity.Body).Data);
                signedCms.CheckSignature(true);

                return true;
            }
            catch{
                return false;
            }
        }

        #endregion


        #region method ToStreamAsync

        /// <inheritdoc/>
        internal protected override async Task ToStreamAsync(Stream stream,bool utf8Headers,bool reEncodeHeaders,CancellationToken cancellationToken = default)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            // We have signer certificate, sign this entity.
            if(this.BodyParts.Count > 0 && m_pSignerCert != null){
                // Remove old signature if there is any.
                if(this.BodyParts.Count > 1){
                    this.BodyParts.Remove(1);
                }

                // Store entity to tmp stream.
                MemoryStream tmpDataEntityStream = new MemoryStream();
                this.BodyParts[0].ToStream(tmpDataEntityStream);
        
                // Compute PKCS #7 message.
                SignedCms signedCms = new SignedCms(new ContentInfo(tmpDataEntityStream.ToArray()),true);
                signedCms.ComputeSignature(new CmsSigner(m_pSignerCert));
                byte[] pkcs7 = signedCms.Encode();
   
                // Create PKCS 7 entity.
                MIME_Entity entity_application_pkcs7 = new MIME_Entity();
                MIME_b_Application application_pkcs7 = new MIME_b_Application(MIME_MediaTypes.Application.x_pkcs7_signature);
                entity_application_pkcs7.Body = application_pkcs7;
                application_pkcs7.SetData(new MemoryStream(pkcs7),MIME_TransferEncodings.Base64);
                entity_application_pkcs7.ContentType?.Param_Name = "smime.p7s";
                entity_application_pkcs7.ContentDescription = "S/MIME Cryptographic Signature";
                this.BodyParts.Add(entity_application_pkcs7);

                signedCms.Decode(application_pkcs7.Data);
                signedCms.CheckSignature(true);
            }

            await base.ToStreamAsync(stream,utf8Headers,reEncodeHeaders,cancellationToken);
        }

        #endregion


        #region Properties implementation

        #endregion
    }
}
