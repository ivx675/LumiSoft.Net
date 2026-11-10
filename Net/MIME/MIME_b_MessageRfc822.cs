using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.Mail;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME <c>message/rfc822</c> body.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 2046 section 5.2.1. A message/rfc822 body encapsulates
    /// a complete RFC 5322 message, including its headers and body.
    /// </remarks>
    public class MIME_b_MessageRfc822 : MIME_b
    {
        private Mail_Message m_pMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MessageRfc822"/> class.
        /// </summary>
        public MIME_b_MessageRfc822() : base(new MIME_h_ContentType("message/rfc822"))
        {
            m_pMessage = new Mail_Message();
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses a <c>message/rfc822</c> body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">
        /// Stream containing the encapsulated message.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="owner"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        internal static async Task<MIME_b_MessageRfc822> ParseAsync(MIME_Entity owner,Stream stream)
        {
            if(owner == null){
                throw new ArgumentNullException(nameof(owner));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            MIME_b_MessageRfc822 retVal = new MIME_b_MessageRfc822();
            retVal.m_pMessage = await Mail_Message.ParseFromStreamAsync(stream,true,false);

            return retVal;
        }

        #endregion


        #region method ToStreamAsync

        /// <inheritdoc/>
        internal protected override async Task ToStreamAsync(Stream stream,bool utf8Headers,bool reEncodeHeaders,CancellationToken cancellationToken = default)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            await m_pMessage.ToStreamAsync(stream,utf8Headers,reEncodeHeaders,cancellationToken);
        }

        #endregion


        #region Properties implementation
        
        /// <summary>
        /// Gets a value indicating whether the encapsulated message has been modified.
        /// </summary>
        public override bool IsModified
        {
            get{ return m_pMessage.IsModified; }
        }
    
        /// <summary>
        /// Gets or sets the encapsulated message.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Raised when a <see langword="null"/> value is assigned.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not bound to a MIME entity.
        /// </exception>
        public Mail_Message Message
        {
            get{ return m_pMessage; }

            set{
                if(value == null){
                    throw new ArgumentNullException("value");
                }
                if(this.Entity == null){
                    throw new InvalidOperationException("Body must be bounded to some entity first.");
                }

                // Owner entity has no content-type or has different content-type, just add/overwrite it.
                if(this.Entity.ContentType == null || !string.Equals(this.Entity.ContentType.TypeWithSubtype,this.MediaType,StringComparison.InvariantCultureIgnoreCase)){
                    this.Entity.ContentType = new MIME_h_ContentType(this.MediaType);
                }

                m_pMessage = value;
            }
        }

        #endregion
    }
}
