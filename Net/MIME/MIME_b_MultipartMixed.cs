using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME multipart/mixed body. Defined in RFC 2046 section 5.1.3.
    /// </summary>
    /// <remarks>
    /// The "mixed" subtype of "multipart" is intended for use when the body
    /// parts are independent and need to be bundled in a particular order.
    /// </remarks>
    public class MIME_b_MultipartMixed : MIME_b_Multipart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartMixed"/> class.
        /// An automatically generated boundary is assigned.
        /// </summary>
        public MIME_b_MultipartMixed() : base()
        {
            MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
            contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            this.ContentType = contentType_multipartMixed;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartMixed"/> class.
        /// </summary>
        /// <param name="contentType">Content type.</param>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>contentType</b> is null reference.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Is raised when <b>contentType</b> is not 'multipart/mixed'.
        /// </exception>
        public MIME_b_MultipartMixed(MIME_h_ContentType contentType) : base(contentType)
        {
            if(!string.Equals(contentType.TypeWithSubtype,"multipart/mixed",StringComparison.CurrentCultureIgnoreCase)){
                throw new ArgumentException("Specified content type must be 'multipart/mixed'.",nameof(contentType));
            }
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses MIME multipart/mixed body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">Stream to parse MIME body from.</param>
        /// <returns>
        /// Returns parsed MIME multipart/mixed body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>owner</b> or <b>stream</b> is null reference.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when multipart entity content type is missing required 'boundary' parameter.
        /// </exception>
        internal new static async Task<MIME_b_MultipartMixed> ParseAsync(MIME_Entity owner,Stream stream)
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
          
            MIME_b_MultipartMixed retVal = new MIME_b_MultipartMixed(owner.ContentType);
            await retVal.ParseInternalAsync(owner,owner.ContentType.Param_Boundary,stream);

            return retVal;
        }

        #endregion
                        

        #region Properties implementation
                
        #endregion
    }
}
