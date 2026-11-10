using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.IO;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME body with a media type whose top-level type is
    /// <c>message</c>.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 2046 section 5.2. A message media type encapsulates an
    /// entire message or a portion of a message.
    /// </remarks>
    public class MIME_b_Message : MIME_b_SinglepartBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_Message"/> class.
        /// </summary>
        /// <param name="mediaType">MIME media type.</param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="mediaType"/> is <see langword="null"/>.
        /// </exception>
        public MIME_b_Message(string mediaType) : base(new MIME_h_ContentType(mediaType))
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses a message MIME body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">Stream containing the encoded body data.</param>
        /// <returns>Returns the parsed MIME body.</returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="owner"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME entity is missing the required Content-Type header.
        /// </exception>
        internal static MIME_b_Message Parse(MIME_Entity owner,Stream stream)
        {
            if(owner == null){
                throw new ArgumentNullException(nameof(owner));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }            
            if(owner.ContentType == null){
                throw new ParseException("MIME entity is missing the required Content-Type header.");
            }

            MIME_b_Message retVal = new MIME_b_Message(owner.ContentType.TypeWithSubtype);
            retVal.EncodedStream = stream;
            retVal.SetModified(false);

            return retVal;
        }

        #endregion
        

        #region Properties implementation
                
        #endregion
    }
}
