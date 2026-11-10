using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME multipart/alternative body. Defined in RFC 2046 section 5.1.4.
    /// </summary>
    /// <remarks>
    /// The "multipart/alternative" subtype is intended for use when each
    /// body part is an alternative version of the same information.
    ///
    /// In general, user agents that compose "multipart/alternative"
    /// entities must place the body parts in increasing order of
    /// preference, with the preferred format last.
    /// </remarks>
    public class MIME_b_MultipartAlternative : MIME_b_Multipart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartAlternative"/> class.
        /// An automatically generated boundary is assigned.
        /// </summary>
        public MIME_b_MultipartAlternative() : base()
        {
            MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
            contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            this.ContentType = contentType_multipartAlternative;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartAlternative"/> class.
        /// </summary>
        /// <param name="contentType">Content type.</param>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>contentType</b> is null reference.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Is raised when <b>contentType</b> is not 'multipart/alternative'.
        /// </exception>
        public MIME_b_MultipartAlternative(MIME_h_ContentType contentType) : base(contentType)
        {
            if(!string.Equals(contentType.TypeWithSubtype,"multipart/alternative",StringComparison.CurrentCultureIgnoreCase)){
                throw new ArgumentException("Specified content type must be 'multipart/alternative'.",nameof(contentType));
            }
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses MIME multipart/alternative body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">Stream to parse MIME body from.</param>
        /// <returns>
        /// Returns parsed MIME multipart/alternative body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>owner</b> or <b>stream</b> is null reference.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when multipart entity content type is missing required 'boundary' parameter.
        /// </exception>
        internal new static async Task<MIME_b_MultipartAlternative> ParseAsync(MIME_Entity owner,Stream stream)
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
          
            MIME_b_MultipartAlternative retVal = new MIME_b_MultipartAlternative(owner.ContentType);
            await retVal.ParseInternalAsync(owner,owner.ContentType.Param_Boundary,stream);

            return retVal;
        }

        #endregion
        

        #region Properties implementation
                
        #endregion
    }
}
