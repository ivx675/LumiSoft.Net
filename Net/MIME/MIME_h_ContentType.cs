using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a <c>Content-Type</c> header field.
    /// </summary>
    /// <remarks>
    /// Defined in RFC 2045, section 5.1. Media types and parameter names are
    /// compared case-insensitively.
    ///
    /// <code>
    /// content := &quot;Content-Type&quot; &quot;:&quot; type &quot;/&quot; subtype
    ///            *(&quot;;&quot; parameter)
    ///
    ///     parameter := attribute "=" value
    ///     attribute := token
    ///     value := token / quoted-string
    /// </code>
    /// </remarks>
    public class MIME_h_ContentType : MIME_h
    {
        private bool                       m_IsModified  = false;
        private string?                    m_ParseValue  = null;
        private string                     m_Type        = "";
        private string                     m_SubType     = "";
        private MIME_h_ParameterCollection m_pParameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_h_ContentType"/> class.
        /// </summary>
        /// <param name="mediaType">The media type and subtype, for example <c>text/plain</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="mediaType"/> is not in the form <c>type/subtype</c> or either component is not a valid token.</exception>
        public MIME_h_ContentType(string mediaType)
        {
            if(mediaType == null){
                throw new ArgumentNullException(nameof(mediaType));
            }

            string[] type_subtype = mediaType.Split(new char[]{'/',},2);
            if(type_subtype.Length == 2){
                if(type_subtype[0] == "" || !MIME_Reader.IsToken(type_subtype[0])){
                    throw new ArgumentException("The media type must contain a valid type token.",nameof(mediaType));
                }                
                if(type_subtype[1] == "" || !MIME_Reader.IsToken(type_subtype[1])){
                    throw new ArgumentException("The media type must contain a valid subtype token.",nameof(mediaType));
                }

                m_Type    = type_subtype[0];
                m_SubType = type_subtype[1];
            }
            else{
                throw new ArgumentException("The media type must contain a type and subtype separated by '/'.",nameof(mediaType));
            }

            m_pParameters = new MIME_h_ParameterCollection(this);
            m_IsModified  = true;
        }

        /// <summary>
        /// Internal parser constructor.
        /// </summary>
        private MIME_h_ContentType()
        {
            m_pParameters = new MIME_h_ParameterCollection(this);
        }


        #region static method Parse

        /// <summary>
        /// Parses a <c>Content-Type</c> header field from its serialized representation.
        /// </summary>
        /// <param name="value">The serialized header field, including its name; for example, <c>Content-Type: text/plain</c>.</param>
        /// <returns>A parsed <see cref="MIME_h_ContentType"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> is not a valid <c>Content-Type</c> header field.</exception>
        public static MIME_h_ContentType Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            // We should not have encoded words here, but some email clients do this, so encoded them if any.
            string valueDecoded = MIME_Encoding_EncodedWord.DecodeS(value);

            MIME_h_ContentType retVal = new MIME_h_ContentType();
            
            string[] name_value = valueDecoded.Split(new char[]{':'},2);
            if(name_value.Length != 2){
                throw new ParseException("The Content-Type header field must contain a field name followed by a colon.");
            }
            
            MIME_Reader r = new MIME_Reader(name_value[1]);
            string? type = r.Token();
            if(type == null){
                throw new ParseException("The Content-Type header field is missing a media type.");
            }
            retVal.m_Type = type;

            if(r.Char(false) != '/'){
                throw new ParseException("The Content-Type media type and subtype must be separated by '/'.");
            }

            string? subtype = r.Token();
            if(subtype == null){
                throw new ParseException("The Content-Type header field is missing a media subtype.");
            }
            retVal.m_SubType = subtype;

            if(r.Available > 0){
                retVal.m_pParameters.Parse(r);
            }

            retVal.m_ParseValue = value;
            retVal.m_IsModified = false;

            return retVal;
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the serialized <c>Content-Type</c> header field.
        /// </summary>
        /// <param name="utf8">Specifies whether regenerated header values may contain UTF-8 characters as defined by RFC 6532.</param>
        /// <param name="reEncode">Specifies whether the header field is regenerated. If <see langword="false"/> and the parsed value is unchanged, its original representation may be returned.</param>
        /// <returns>The serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {
            if(m_ParseValue != null && !reEncode && !this.IsModified){
                return m_ParseValue;
            }
            else{
                StringBuilder retVal = new StringBuilder();
                retVal.Append("Content-Type: " + m_Type + "/" + m_SubType);
                retVal.Append(m_pParameters.ToString(utf8));
                retVal.Append("\r\n");

                return retVal.ToString();
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this header field has been modified
        /// since it was parsed.
        /// </summary>
        public override bool IsModified
        {
            get{ return m_IsModified || m_pParameters.IsModified; }
        }

        /// <summary>
        /// Gets the header field name, which is always <c>Content-Type</c>.
        /// </summary>
        public override string Name
        {
            get { return "Content-Type"; }
        }

        /// <summary>
        /// Gets the media type, such as <c>application</c>, <c>image</c>, or <c>text</c>.
        /// </summary>
        /// <remarks>Registered media types are listed by IANA.</remarks>
        public string Type
        {
            get{ return m_Type; }
        }

        /// <summary>
        /// Gets the media subtype. For <c>text/plain</c>, the subtype is <c>plain</c>.
        /// </summary>
        /// <remarks>Registered media types and subtypes are listed by IANA.</remarks>
        public string SubType
        {
            get{ return m_SubType; }
        }

        /// <summary>
        /// Gets the media type and subtype in the form <c>type/subtype</c>.
        /// For example, <c>text/plain</c>.
        /// </summary>
        public string TypeWithSubtype
        {
            get{ return m_Type + "/" + m_SubType; }
        }

        /// <summary>
        /// Gets the <c>Content-Type</c> parameter collection.
        /// </summary>
        public MIME_h_ParameterCollection Parameters
        {
            get{ return m_pParameters; }
        }

        /// <summary>
        /// Gets or sets the <c>name</c> parameter value. A <see langword="null"/>
        /// value means that the parameter is not specified.
        /// </summary>
        public string? Param_Name
        {
            get{ return m_pParameters["name"]; }

            set{ m_pParameters["name"] = value; }
        }
        
        /// <summary>
        /// Gets or sets the <c>charset</c> parameter value. A <see langword="null"/>
        /// value means that the parameter is not specified.
        /// </summary>
        public string? Param_Charset
        {
            get{ return m_pParameters["charset"]; }

            set{ m_pParameters["charset"] = value; }
        }

        /// <summary>
        /// Gets or sets the <c>boundary</c> parameter value. A <see langword="null"/>
        /// value means that the parameter is not specified.
        /// </summary>
        public string? Param_Boundary
        {
            get{ return m_pParameters["boundary"]; }

            set{ m_pParameters["boundary"] = value; }
        }

        #endregion
    }
}
