using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME body with a media type whose top-level type is
    /// <c>text</c>, such as <c>text/plain</c> or <c>text/html</c>.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 2045 and RFC 2046. Text bodies contain character data
    /// encoded using the charset specified in the Content-Type header.
    /// </remarks>
    public class MIME_b_Text : MIME_b_SinglepartBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_Text"/> class.
        /// </summary>
        /// <param name="mediaType">
        /// MIME media type, for example <c>text/plain</c> or <c>text/html</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="mediaType"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="mediaType"/> contains an invalid value.
        /// </exception>
        public MIME_b_Text(string mediaType) : base(new MIME_h_ContentType(mediaType))
        {            
        }

        #region static method Parse

        /// <summary>
        /// Parses a text MIME body from the specified stream.
        /// </summary>
        /// <param name="owner">
        /// MIME entity that owns the body.
        /// </param>
        /// <param name="stream">
        /// Stream containing the encoded body data.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME text body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="owner"/> or <paramref name="stream"/> is
        /// <see langword="null" />.
        /// </exception>
        internal static MIME_b_Text Parse(MIME_Entity owner,Stream stream)
        {
            if(owner == null){
                throw new ArgumentNullException(nameof(owner));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            MIME_b_Text? retVal = null;
            if(owner.ContentType != null){
                retVal = new MIME_b_Text(owner.ContentType.TypeWithSubtype);
            }
            else{
                retVal = new MIME_b_Text("text/plain");
            }

            retVal.EncodedStream = stream;
            retVal.SetModified(false);

            return retVal;
        }

        #endregion


        #region method SetText

        /// <summary>
        /// Sets the body text using UTF-8 encoding and the specified
        /// Content-Transfer-Encoding value.
        /// </summary>
        /// <param name="transferEncoding">
        /// Content-Transfer-Encoding to use when storing the text.
        /// </param>
        /// <param name="text">
        /// Text to store in the body.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="transferEncoding"/> or
        /// <paramref name="text"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Raised when the body is not bound to a MIME entity or the entity
        /// does not have a Content-Type header.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the specified Content-Transfer-Encoding is not supported.
        /// </exception>
        public void SetText(string transferEncoding,string text)
        {
            if(transferEncoding == null){
                throw new ArgumentNullException(nameof(transferEncoding));
            }
            if(text == null){
                throw new ArgumentNullException(nameof(text));
            }
            if(this.Entity == null){
                throw new InvalidOperationException("Body must be bounded to some MIME entity first.");
            }
            if(this.Entity.ContentType == null){
                throw new InvalidOperationException("Mime entity ContentType must be specified first.");
            }

            SetData(new MemoryStream(Encoding.UTF8.GetBytes(text)),transferEncoding);
            this.Entity.ContentType.Param_Charset = "utf-8";            
        }

        #endregion
        
        #region method GetCharset

        /// <summary>
        /// Gets the character encoding specified by the Content-Type charset
        /// parameter.
        /// </summary>
        /// <returns>
        /// Returns the resolved character encoding.
        /// </returns>
        /// <remarks>
        /// If no charset parameter is specified, UTF-8 is returned.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Raised when the charset value is not supported by the current
        /// platform.
        /// </exception>
        public Encoding GetCharset()
        {
            // RFC 2046 4.1.2. The default character set, US-ASCII.
            // We default to UTF-8.
            
            if(this.Entity?.ContentType == null || string.IsNullOrEmpty(this.Entity.ContentType.Param_Charset)){
                return Encoding.UTF8;
            }
            else{
                // Handle custome/extended charsets, just remove "x-" from start.
                if(this.Entity.ContentType.Param_Charset.ToLower().StartsWith("x-")){
                    return Encoding.GetEncoding(this.Entity.ContentType.Param_Charset.Substring(2));
                }
                // Cp1252 is not IANA reggistered, some mail clients send it, it equal to windows-1252.
                else if(string.Equals(this.Entity.ContentType.Param_Charset,"cp1252",StringComparison.InvariantCultureIgnoreCase)){
                    return Encoding.GetEncoding("windows-1252");
                }
                // utf8 is invalid(must be utf-8), consider it as utf-8.
                else if(string.Equals(this.Entity.ContentType.Param_Charset,"utf8",StringComparison.InvariantCultureIgnoreCase)){
                    return Encoding.GetEncoding("utf-8");
                }
                // iso8859_1 is invalid(must be iso8859-1), consider it as iso8859_1.
                else if(string.Equals(this.Entity.ContentType.Param_Charset,"iso8859_1",StringComparison.InvariantCultureIgnoreCase)){
                    return Encoding.GetEncoding("iso-8859-1");
                }
                else{
                    return Encoding.GetEncoding(this.Entity.ContentType.Param_Charset);
                }
            }
        }

        #endregion
                

        #region Properties implementation
                
        /// <summary>
        /// Gets the decoded body text.
        /// </summary>
        /// <value>
        /// The body content decoded using the charset returned by
        /// <see cref="GetCharset"/>.
        /// </value>
        /// <exception cref="ArgumentException">
        /// Raised when the charset specified in Content-Type is not supported.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Raised when the Content-Transfer-Encoding value is not supported.
        /// </exception>
        public string Text
        {
            get{ return GetCharset().GetString(this.Data); }
        }

        #endregion
    }
}
