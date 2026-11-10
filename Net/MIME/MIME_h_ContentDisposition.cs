using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a <c>Content-Disposition</c> header field.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 2183. Disposition types are case-insensitive.
    ///
    /// <code>
    /// disposition := &quot;Content-Disposition&quot; &quot;:&quot; disposition-type
    ///                *(&quot;;&quot; disposition-parm)
    ///
    ///     disposition := "Content-Disposition" ":" disposition-type *(";" disposition-parm)
    ///
    ///     disposition-type := "inline" / "attachment" / extension-token
    ///                         ; values are not case-sensitive
    ///
    ///     disposition-parm := filename-parm 
    ///                         / creation-date-parm
    ///                         / modification-date-parm
    ///                         / read-date-parm
    ///                         / size-parm
    ///                         / parameter
    ///
    ///     filename-parm := "filename" "=" value
    ///
    ///     creation-date-parm := "creation-date" "=" quoted-date-time
    /// 
    ///     modification-date-parm := "modification-date" "=" quoted-date-time
    ///
    ///     read-date-parm := "read-date" "=" quoted-date-time
    ///
    ///     size-parm := "size" "=" 1*DIGIT
    ///
    ///     quoted-date-time := quoted-string
    ///                         ; contents MUST be an RFC 822 `date-time'
    ///                         ; numeric timezones (+HHMM or -HHMM) MUST be used
    /// </code>
    /// </remarks>
    public class MIME_h_ContentDisposition : MIME_h
    {
        private bool                       m_IsModified      = false;
        private string?                    m_ParseValue      = null;
        private string                     m_DispositionType = "";
        private MIME_h_ParameterCollection m_pParameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_h_ContentDisposition"/> class.
        /// </summary>
        /// <param name="dispositionType">The disposition type, such as <c>inline</c> or <c>attachment</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dispositionType"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="dispositionType"/> is empty.</exception>
        public MIME_h_ContentDisposition(string dispositionType)
        {
            if(dispositionType == null){
                throw new ArgumentNullException(nameof(dispositionType));
            }
            if(dispositionType == string.Empty){
                throw new ArgumentException("The disposition type cannot be empty.",nameof(dispositionType));
            }

            m_DispositionType = dispositionType;

            m_pParameters = new MIME_h_ParameterCollection(this);
            m_IsModified  = true;
        }

        /// <summary>
        /// Internal parser constructor.
        /// </summary>
        private MIME_h_ContentDisposition()
        {
            m_pParameters = new MIME_h_ParameterCollection(this);
        }


        #region static method Parse

        /// <summary>
        /// Parses a <c>Content-Disposition</c> header field from its serialized representation.
        /// </summary>
        /// <param name="value">The serialized header field, including its name; for example, <c>Content-Disposition: attachment; filename=&quot;file.txt&quot;</c>.</param>
        /// <returns>A parsed <see cref="MIME_h_ContentDisposition"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> is not a valid <c>Content-Disposition</c> header field.</exception>
        public static MIME_h_ContentDisposition Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            // We should not have encoded words here, but some email clients do this, so encoded them if any.
            string valueDecoded = MIME_Encoding_EncodedWord.DecodeS(value);

            MIME_h_ContentDisposition retVal = new MIME_h_ContentDisposition();
            
            string[] name_value = valueDecoded.Split(new char[]{':'},2);
            if(name_value.Length != 2){
                throw new ParseException("The Content-Disposition header field must contain a field name followed by a colon.");
            }

            MIME_Reader r = new MIME_Reader(name_value[1]);
            string? type = r.Token();
            if(type == null){
                throw new ParseException("The Content-Disposition header field is missing a disposition type.");
            }
            retVal.m_DispositionType = type.Trim();

            retVal.m_pParameters.Parse(r);

            retVal.m_ParseValue = value;

            return retVal;
        }

        #endregion
        

        #region override method ToString
                
        /// <summary>
        /// Returns the serialized <c>Content-Disposition</c> header field.
        /// </summary>
        /// <param name="utf8">Specifies whether regenerated header values may contain UTF-8 characters as defined by RFC 6532.</param>
        /// <param name="reEncode">Specifies whether the header field is regenerated. If <see langword="false"/> and the parsed value is unchanged, its original representation may be returned.</param>
        /// <returns>The serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {
            if(m_ParseValue == null || reEncode || this.IsModified){
                return "Content-Disposition: " + m_DispositionType + m_pParameters.ToString(utf8) + "\r\n";
            }
            else{
                return m_ParseValue;
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
        /// Gets the header field name, which is always <c>Content-Disposition</c>.
        /// </summary>
        public override string Name
        {
            get { return "Content-Disposition"; }
        }

        /// <summary>
        /// Gets the disposition type, such as <c>inline</c> or <c>attachment</c>.
        /// </summary>
        public string DispositionType
        {
            get{ return m_DispositionType; }
        }

        /// <summary>
        /// Gets the <c>Content-Disposition</c> parameter collection.
        /// </summary>
        public MIME_h_ParameterCollection Parameters
        {
            get{ return m_pParameters; }
        }

        /// <summary>
        /// Gets or sets the suggested file name. A <see langword="null"/> value
        /// means that the parameter is not specified. Defined in RFC 2183, section 2.3.
        /// </summary>
        public string? Param_FileName
        {
            get{ return this.Parameters["filename"]; }

            set{ m_pParameters["filename"] = value; }
        }

        /// <summary>
        /// Gets or sets the file creation date. A <see langword="null"/> value
        /// means that the parameter is not specified. Defined in RFC 2183, section 2.4.
        /// </summary>
        public DateTime? Param_CreationDate
        {
            get{ 
                string? value = this.Parameters["creation-date"];
                if(value == null){
                    return null;
                }
                else{
                    return MIME_Utils.ParseRfc2822DateTime(value);
                }
            }

            set{ 
                if(value == null){
                    this.Parameters.Remove("creation-date");
                }
                else{
                    this.Parameters["creation-date"] = MIME_Utils.DateTimeToRfc2822(value.Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the file modification date. A <see langword="null"/> value
        /// means that the parameter is not specified. Defined in RFC 2183, section 2.5.
        /// </summary>
        public DateTime? Param_ModificationDate
        {
            get{
                string? value = this.Parameters["modification-date"];
                if(value == null){
                    return null;
                }
                else{
                    return MIME_Utils.ParseRfc2822DateTime(value);
                } 
            }

            set{ 
                if(value == null){
                    this.Parameters.Remove("modification-date");
                }
                else{
                    this.Parameters["modification-date"] = MIME_Utils.DateTimeToRfc2822(value.Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the date when the file was last read. A <see langword="null"/>
        /// value means that the parameter is not specified. Defined in RFC 2183, section 2.6.
        /// </summary>
        public DateTime? Param_ReadDate
        {
            get{
                string? value = this.Parameters["read-date"];
                if(value == null){
                    return null;
                }
                else{
                    return MIME_Utils.ParseRfc2822DateTime(value);
                }
            }

            set{ 
                if(value == null){
                    this.Parameters.Remove("read-date");
                }
                else{
                    this.Parameters["read-date"] = MIME_Utils.DateTimeToRfc2822(value.Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the file size in bytes. A <see langword="null"/> value
        /// means that the parameter is not specified. Defined in RFC 2183, section 2.7.
        /// </summary>
        public long? Param_Size
        {
            get{
                string? value = this.Parameters["size"];
                if(value == null){
                    return null;
                }
                else{
                    return Convert.ToInt64(value);
                }
            }

            set{ 
                if(value == null){
                    this.Parameters.Remove("size");
                }
                else{
                    this.Parameters["size"] = value.ToString();
                }
            }
        }

        #endregion
    }
}
