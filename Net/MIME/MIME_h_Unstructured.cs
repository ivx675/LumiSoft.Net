using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a header field whose value contains unstructured text.
    /// </summary>
    /// <remarks>
    /// Unstructured values may contain encoded words and folded whitespace as
    /// defined by RFC 5322.
    /// </remarks>
    public class MIME_h_Unstructured : MIME_h
    {
        private string? m_ParseValue = null;
        private string  m_Name       = "";
        private string  m_Value      = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_h_Unstructured"/> class.
        /// </summary>
        /// <param name="name">The header field name.</param>
        /// <param name="value">The unstructured header field value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
        public MIME_h_Unstructured(string name,string value)
        {
            if(name == null){
                throw new ArgumentNullException(nameof(name));
            }
            if(name == string.Empty){
                throw new ArgumentException("The header field name cannot be empty.", nameof(name));
            }
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            m_Name  = name;
            m_Value = value;
        }

        /// <summary>
        /// Internal parser constructor.
        /// </summary>
        private MIME_h_Unstructured()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses an unstructured header field from its serialized representation.
        /// </summary>
        /// <param name="value">The serialized header field, including its name; for example, <c>Subject: Hello</c>.</param>
        /// <returns>A parsed <see cref="MIME_h_Unstructured"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when the header field name is missing.</exception>
        public static MIME_h_Unstructured Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            MIME_h_Unstructured retVal = new MIME_h_Unstructured();

            string[] name_value = value.Split(new char[]{':'},2);
            if(name_value[0].Trim() == string.Empty){
                throw new ParseException("The header field name cannot be empty.");
            }

            retVal.m_Name  = name_value[0];

            retVal.m_Value = MIME_Encoding_EncodedWord.DecodeTextS(MIME_Utils.UnfoldHeader(name_value.Length == 2 ? name_value[1].TrimStart() : ""));

            retVal.m_ParseValue = value;

            return retVal;
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the serialized header field.
        /// </summary>
        /// <param name="utf8">Specifies whether the value may be written using UTF-8 header syntax. If <see langword="false"/>, non-ASCII text is encoded as an RFC 2047 encoded-word.</param>
        /// <param name="reEncode">Specifies whether the header field is regenerated. If <see langword="false"/> and the parsed value is unchanged, its original representation is returned.</param>
        /// <returns>The serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {
            if(!reEncode && m_ParseValue != null){
                return m_ParseValue;
            }
            else{
                if(utf8){
                    return m_Name + ": " + m_Value + "\r\n";
                }
                else{
                    return m_Name + ": " + MIME_Encoding_EncodedWord.EncodeS(MIME_EncodedWordEncoding.B,Encoding.UTF8,true,m_Value) + "\r\n";                    
                }
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
            get{ return m_ParseValue == null; }
        }

        /// <summary>
        /// Gets the header field name.
        /// </summary>
        public override string Name
        {
            get { return m_Name; }
        }

        /// <summary>
        /// Gets or sets the unstructured header field value.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the assigned value is <see langword="null"/>.</exception>
        public string Value
        {
            get{ return m_Value; }

            set{
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }

                m_Value = value;
                // Reset parse value.
                m_ParseValue = null;
            }
        }

        #endregion

    }
}
