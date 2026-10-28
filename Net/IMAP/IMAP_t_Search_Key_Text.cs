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
    /// The <c>TEXT</c> search key is defined by RFC 9051 and performs a
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
        /// The <c>TEXT</c> search key is defined by RFC 9051 and accepts any IMAP
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


        #region static method ParseAsync

        /// <summary>
        /// Parses a SEARCH <c>TEXT</c> key from the specified
        /// <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="_IMAP_Reader"/> positioned at the search key to
        /// parse.
        /// </param>
        /// <returns>
        /// A task whose result contains a new
        /// <see cref="IMAP_t_Search_Key_Text"/> instance representing the
        /// parsed <c>TEXT</c> search key.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>TEXT</c> or when the search
        /// string cannot be parsed as a valid IMAP string.
        /// </exception>
        /// <remarks>
        /// The <c>TEXT</c> search key matches messages whose headers or body
        /// contain the specified string. Matching is case-insensitive.
        /// </remarks>
        internal static async Task<IMAP_t_Search_Key_Text> ParseAsync(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }

            string word = r.ReadAtom();
            if(!string.Equals(word,"TEXT",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'TEXT' key.");
            }
            string value = await r.ReadStringAsync() ?? "";

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
        /// using a case‑insensitive substring match as defined by RFC 9051.
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
        /// The <c>TEXT</c> search key is defined by RFC 9051 and performs a
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
        /// entire message text (headers and body), as specified by RFC 9051.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
