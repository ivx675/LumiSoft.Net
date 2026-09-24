using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>TEXT</c> search key, which selects messages
    /// whose full message text (headers and body) contains the specified text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>TEXT</c> search key is defined by RFC 3501 and performs a
    /// case‑insensitive substring match across the entire message text,
    /// including both header fields and body content.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Text : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Text"/>
        /// class with the specified string argument.
        /// </summary>
        /// <param name="value">
        /// The text used by the IMAP <c>TEXT</c> search key. This value is matched
        /// as a case‑insensitive substring against the entire message text,
        /// including both headers and body.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>TEXT</c> search key is defined by RFC 3501 and accepts any IMAP
        /// string, including quoted strings and literals.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Text(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>TEXT</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Text"/> instance containing
        /// the parsed string argument.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>TEXT</c> or when the string
        /// argument is missing or cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>TEXT</c> search key is defined by RFC 3501 and performs a
        /// case‑insensitive substring match across the entire message text,
        /// including both headers and body.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Text Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"TEXT",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'TEXT' key.");
            }
            string? value = IMAP_Utils.ReadString(r);
            if(value == null){
                throw new ParseException("Parse error: Invalid 'TEXT' value.");
            }

            return new IMAP_t_Search_Key_Text(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>TEXT</c> search key with its quoted string argument.
        /// </summary>
        /// <returns>
        /// A string of the form <c>TEXT "value"</c>, selecting messages whose
        /// entire message text (headers and body) contains the specified text,
        /// using a case‑insensitive substring match as defined by RFC 3501.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>TEXT</c> search key performs a case‑insensitive substring match
        /// across the full message text, including both header fields and body.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "TEXT " + TextUtils.QuoteString(m_Value);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>TEXT</c> search key and its string argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>TEXT</c> search key is defined by RFC 3501 and performs a
        /// case‑insensitive substring match against the entire message text,
        /// including both headers and body.
        /// </para>
        /// <para>
        /// The argument is written using <see cref="CommandBuilder.AddStringOrLiteral(string)"/>,
        /// which emits either a quoted string or an IMAP literal depending on the
        /// content of <c>m_Value</c>. This ensures correct IMAP formatting for
        /// values containing special characters or requiring literal form.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString("TEXT ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the string argument used by the IMAP <c>TEXT</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is matched as a case‑insensitive substring against the
        /// entire message text (headers and body), as specified by RFC 3501.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
