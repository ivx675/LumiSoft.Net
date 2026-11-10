using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.IO;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME multipart/related body. Defined in RFC 2387.
    /// </summary>
    /// <remarks>
    /// The Multipart/Related content type provides a common mechanism for
    /// representing objects that are aggregates of related MIME body parts.
    /// </remarks>
    public class MIME_b_MultipartRelated : MIME_b_Multipart
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_MultipartRelated"/> class.
        /// </summary>
        /// <param name="contentType">Content type.</param>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>contentType</b> is null reference.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Is raised when <b>contentType</b> is not 'multipart/related'.
        /// </exception>
        public MIME_b_MultipartRelated(MIME_h_ContentType contentType) : base(contentType)
        {
            if(!string.Equals(contentType.TypeWithSubtype,"multipart/related",StringComparison.CurrentCultureIgnoreCase)){
                throw new ArgumentException("Specified content type must be 'multipart/related'.",nameof(contentType));
            }
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses MIME multipart/related body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">Stream to parse MIME body from.</param>
        /// <returns>
        /// Returns parsed MIME multipart/related body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <b>owner</b> or <b>stream</b> is null reference.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when multipart entity content type is missing required 'boundary' parameter.
        /// </exception>
        internal new static async Task<MIME_b_MultipartRelated> ParseAsync(MIME_Entity owner,Stream stream)
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
          
            MIME_b_MultipartRelated retVal = new MIME_b_MultipartRelated(owner.ContentType);
            await retVal.ParseInternalAsync(owner,owner.ContentType.Param_Boundary,stream);

            return retVal;
        }

        #endregion
        

        #region Properties implementation
                
        #endregion
    }
}
