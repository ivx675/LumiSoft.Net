using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>TO</c> search key, which selects messages
    /// whose <c>To:</c> header field contains the specified text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>TO</c> search key is defined by RFC 9051 and performs a
    /// case‑insensitive substring match against the message’s <c>To:</c>
    /// header field.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_To : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_To"/>
        /// class with the specified string argument.
        /// </summary>
        /// <param name="value">
        /// The text used by the IMAP <c>TO</c> search key. This value is matched
        /// as a case‑insensitive substring against the message’s <c>To:</c>
        /// header field.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>TO</c> search key is defined by RFC 9051 and accepts any IMAP
        /// string, including quoted strings and literals.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_To(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses a SEARCH <c>TO</c> key from the specified
        /// <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="_IMAP_Reader"/> positioned at the search key to
        /// parse.
        /// </param>
        /// <returns>
        /// A task whose result contains a new
        /// <see cref="IMAP_t_Search_Key_To"/> instance representing the
        /// parsed <c>TO</c> search key.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>TO</c> or when the search
        /// string cannot be parsed as a valid IMAP string.
        /// </exception>
        /// <remarks>
        /// The <c>TO</c> search key matches messages whose TO header
        /// contains the specified string. Matching is case-insensitive.
        /// </remarks>
        internal static async Task<IMAP_t_Search_Key_To> ParseAsync(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }

            string word = r.ReadAtom();
            if(!string.Equals(word,"TO",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'TO' key.");
            }
            string value = await r.ReadStringAsync() ?? "";

            return new IMAP_t_Search_Key_To(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>TO</c> search key with its quoted string argument.
        /// </summary>
        /// <returns>
        /// A string of the form <c>TO "value"</c>, selecting messages whose
        /// <c>To:</c> header field contains the specified text (case‑insensitive
        /// substring match).
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>TO</c> search key is defined by RFC 9051 and performs a
        /// case‑insensitive substring match against the message’s <c>To:</c>
        /// header field.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "TO " + TextUtils.QuoteString(m_Value);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>TO</c> search key and its string argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>TO</c> search key is defined by RFC 9051 and performs a
        /// case‑insensitive substring match against the message’s <c>To:</c>
        /// header field.
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
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString("TO ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the string argument used by the IMAP <c>TO</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is matched as a case‑insensitive substring against the
        /// message’s <c>To:</c> header field, as specified by RFC 9051.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
