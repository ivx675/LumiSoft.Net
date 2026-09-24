using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>CC</c> search key, which matches messages whose
    /// <c>Cc:</c> header field contains the specified value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>CC</c> search key is defined by RFC 3501 and filters messages
    /// based on the contents of the <c>Cc:</c> header field. The comparison is
    /// case‑insensitive and typically performs substring matching.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Cc : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Cc"/>
        /// class with the specified value.
        /// </summary>
        /// <param name="value">
        /// The string used by the IMAP <c>CC</c> search key. Messages whose
        /// <c>Cc:</c> header field contains this value will match the search
        /// criterion. The comparison is case‑insensitive and typically performs
        /// substring matching, as defined by RFC 3501.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Search_Key_Cc(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>CC</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Cc"/> instance containing the
        /// parsed <c>CC</c> value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>CC</c> or when the value
        /// cannot be parsed as an IMAP string.
        /// </exception>
        internal static IMAP_t_Search_Key_Cc Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"CC",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'CC' key.");
            }
            string? value = IMAP_Utils.ReadString(r);
            if(value == null){
                throw new ParseException("Parse error: Invalid 'CC' value.");
            }

            return new IMAP_t_Search_Key_Cc(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>CC</c> search key as a string.
        /// </summary>
        /// <returns>
        /// A string in the form <c>CC "value"</c>, where the value is quoted
        /// according to IMAP <c>astring</c> rules.
        /// </returns>
        public override string ToString()
        {
            return "CC " + TextUtils.QuoteString(m_Value);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Serializes this <c>CC</c> search key into the IMAP command being built.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// The value is emitted using <see cref="CommandBuilder.AddStringOrLiteral"/>,
        /// allowing it to be sent either as a quoted string or as a literal depending
        /// on its content and client configuration. This ensures correct handling of
        /// UTF‑8 text, special characters, and values requiring literal encoding.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString("CC ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the value used by the IMAP <c>CC</c> search key.
        /// </summary>
        /// <remarks>
        /// Messages whose <c>Cc:</c> header field contains this value will match
        /// the search criterion. The comparison is case‑insensitive and typically
        /// performs substring matching, as defined by RFC 3501.
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
