using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a header field that could not be parsed.
    /// </summary>
    /// <remarks>
    /// The original header field text is preserved so that it can be written
    /// back unchanged, while the parsing error is exposed through
    /// <see cref="Exception"/>.
    /// </remarks>
    public class MIME_h_Unparsed : MIME_h
    {
        private string    m_ParseValue = "";
        private string    m_Name       = "";
        private string    m_Value      = "";
        private Exception m_pException;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_h_Unparsed"/> class.
        /// </summary>
        /// <param name="value">The original serialized header field, including its name; for example, <c>Content-Type: text/plain</c>.</param>
        /// <param name="exception">The exception produced while parsing the header field.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> or <paramref name="exception"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> does not contain a field name followed by a colon.</exception>
        internal MIME_h_Unparsed(string value,Exception exception)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }
            if(exception == null){
                throw new ArgumentNullException(nameof(exception));
            }

            string[] name_value = value.Split(new char[]{':'},2);
            if(name_value.Length != 2){
                throw new ParseException("The header field must contain a field name followed by a colon.");
            }

            m_Name       = name_value[0];
            m_Value      = name_value[1].Trim();
            m_ParseValue = value;
            m_pException = exception;
        }


        #region override method ToString
                
        /// <summary>
        /// Returns the original serialized header field.
        /// </summary>
        /// <param name="utf8">Ignored because the original serialized value is returned.</param>
        /// <param name="reEncode">Ignored because the original serialized value is returned.</param>
        /// <returns>The original serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {
            return m_ParseValue;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this header field has been modified.
        /// </summary>
        /// <remarks>This implementation always returns <see langword="false"/> because the original value is preserved.</remarks>
        public override bool IsModified
        {
            get{ return false; }
        }

        /// <summary>
        /// Gets the header field name extracted from the original value.
        /// </summary>
        public override string Name
        {
            get { return m_Name; }
        }

        /// <summary>
        /// Gets the header field value extracted from the original value.
        /// </summary>
        public string Value
        {
            get{ return m_Value; }
        }

        /// <summary>
        /// Gets the exception that caused parsing to fail.
        /// </summary>
        public Exception Exception
        {
            get{ return m_pException; }
        }

        #endregion
    }
}
