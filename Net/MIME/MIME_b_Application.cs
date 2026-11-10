using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME body with a media type whose top-level type is
    /// <c>application</c>.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 2046 section 4.5. The application media type is used
    /// for discrete data that does not fit other media type categories and
    /// is typically intended for processing by an application.
    /// </remarks>
    public class MIME_b_Application : MIME_b_SinglepartBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_Application"/> class.
        /// </summary>
        /// <param name="mediaType">
        /// MIME media type, for example <c>application/pdf</c> or
        /// <c>application/octet-stream</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="mediaType"/> is <see langword="null"/>.
        /// </exception>
        public MIME_b_Application(string mediaType) : base(new MIME_h_ContentType(mediaType))
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses an application MIME body from the specified stream.
        /// </summary>
        /// <param name="owner">
        /// MIME entity that owns the body.
        /// </param>
        /// <param name="stream">
        /// Stream containing the encoded body data.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="owner"/> or <paramref name="stream"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the MIME entity does not contain the required
        /// Content-Type header.
        /// </exception>
        internal static MIME_b_Application Parse(MIME_Entity owner,Stream stream)
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

            MIME_b_Application retVal = new MIME_b_Application(owner.ContentType.TypeWithSubtype);
            retVal.EncodedStream = stream;
            retVal.SetModified(false);

            return retVal;
        }

        #endregion
        

        #region Properties implementation
                
        #endregion
    }
}
