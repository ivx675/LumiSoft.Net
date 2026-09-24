using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>BCC</c> search key, which matches messages whose
    /// <c>Bcc:</c> header field contains the specified value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>BCC</c> search key is defined by RFC 3501 and filters messages
    /// based on the contents of the <c>Bcc:</c> header field. The comparison is
    /// case-insensitive and typically performs substring matching.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Bcc : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Bcc"/>
        /// class with the specified value.
        /// </summary>
        /// <param name="value">
        /// The string used by the IMAP <c>BCC</c> search key. Messages whose
        /// <c>Bcc:</c> header field contains this value will match the search
        /// criterion. The comparison is case‑insensitive and typically performs
        /// substring matching, as defined by RFC 3501.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Search_Key_Bcc(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>BCC</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Bcc"/> instance containing the
        /// parsed <c>BCC</c> value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>BCC</c> or when the value
        /// cannot be parsed as an IMAP string.
        /// </exception>
        internal static IMAP_t_Search_Key_Bcc Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"BCC",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'BCC' key.");
            }
            string? value = IMAP_Utils.ReadString(r);
            if(value == null){
                throw new ParseException("Parse error: Invalid 'BCC' value.");
            }

            return new IMAP_t_Search_Key_Bcc(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>BCC</c> search key, which matches messages whose
        /// <c>Bcc:</c> header field contains the specified value.
        /// </summary>
        /// <returns>
        /// A string in the form <c>BCC "value"</c>, where the value is quoted
        /// according to IMAP string‑literal rules.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>BCC</c> search key is defined by RFC 3501 and filters messages
        /// based on the contents of the <c>Bcc:</c> header field. The comparison
        /// is case‑insensitive and typically performs substring matching.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "BCC " + TextUtils.QuoteString(m_Value);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Serializes this <c>BCC</c> search key into the IMAP command being built.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// The value is emitted using <see cref="CommandBuilder.AddStringOrLiteral"/>,
        /// allowing the argument to be sent either as a quoted string or as a literal
        /// depending on its content and client configuration.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString("BCC ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the value used by the IMAP <c>BCC</c> search key.
        /// </summary>
        /// <remarks>
        /// Messages whose <c>Bcc:</c> header field contains this value will match
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
