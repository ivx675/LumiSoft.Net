using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME multipart/encrypted body. Defined in RFC 1847.
    /// </summary>
    /// <remarks>
    /// The multipart/encrypted content type is used to transport encrypted MIME data.
    /// </remarks>
    public class MIME_b_MultipartEncrypted : MIME_b_Multipart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartEncrypted"/> class.
        /// </summary>
        /// <param name="contentType">Content type.</param>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>contentType</b> is null reference.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Is raised when <b>contentType</b> is not 'multipart/encrypted'.
        /// </exception>
        public MIME_b_MultipartEncrypted(MIME_h_ContentType contentType) : base(contentType)
        {
            if(!string.Equals(contentType.TypeWithSubtype,"multipart/encrypted",StringComparison.CurrentCultureIgnoreCase)){
                throw new ArgumentException("Specified content type must be 'multipart/encrypted'.",nameof(contentType));
            }
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses MIME multipart/encrypted body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">Stream to parse MIME body from.</param>
        /// <returns>
        /// Returns parsed MIME multipart/encrypted body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>owner</b> or <b>stream</b> is null reference.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when multipart entity content type is missing required 'boundary' parameter.
        /// </exception>
        internal new static async Task<MIME_b_MultipartEncrypted> ParseAsync(MIME_Entity owner,Stream stream)
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
          
            MIME_b_MultipartEncrypted retVal = new MIME_b_MultipartEncrypted(owner.ContentType);
            await retVal.ParseInternalAsync(owner,owner.ContentType.Param_Boundary,stream);

            return retVal;
        }

        #endregion
        

        #region Properties implementation
                
        #endregion
    }
}
