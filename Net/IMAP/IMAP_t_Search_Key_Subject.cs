using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>SUBJECT</c> search key, which selects messages
    /// whose <c>Subject:</c> header field contains the specified text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>SUBJECT</c> search key is defined by RFC 9051 and performs a
    /// case‑insensitive substring match against the message’s <c>Subject:</c>
    /// header field.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Subject : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Subject"/>
        /// class with the specified string argument.
        /// </summary>
        /// <param name="value">
        /// The text used by the IMAP <c>SUBJECT</c> search key. This value is
        /// matched as a case‑insensitive substring against the message’s
        /// <c>Subject:</c> header field.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SUBJECT</c> search key is defined by RFC 9051 and accepts any IMAP
        /// string, including quoted strings and literals.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Subject(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses a SEARCH <c>SUBJECT</c> key from the specified
        /// <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="_IMAP_Reader"/> positioned at the search key to
        /// parse.
        /// </param>
        /// <returns>
        /// A task whose result contains a new
        /// <see cref="IMAP_t_Search_Key_Subject"/> instance representing the
        /// parsed <c>SUBJECT</c> search key.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>SUBJECT</c> or when the
        /// search string cannot be parsed as a valid IMAP string.
        /// </exception>
        /// <remarks>
        /// The <c>SUBJECT</c> search key matches messages whose Subject
        /// header contains the specified string. Matching is
        /// case-insensitive.
        /// </remarks>
        internal static async Task<IMAP_t_Search_Key_Subject> ParseAsync(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }

            string word = r.ReadAtom();
            if(!string.Equals(word,"SUBJECT",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'SUBJECT' key.");
            }
            string value = await r.ReadStringAsync() ?? "";

            return new IMAP_t_Search_Key_Subject(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>SUBJECT</c> search key with its quoted string argument.
        /// </summary>
        /// <returns>
        /// A string of the form <c>SUBJECT "value"</c>, selecting messages whose
        /// <c>Subject:</c> header field contains the specified text (case‑insensitive
        /// substring match), as defined by RFC 9051.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>SUBJECT</c> search key performs a case‑insensitive substring match
        /// against the message’s <c>Subject:</c> header field.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "SUBJECT " + TextUtils.QuoteString(m_Value);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>SUBJECT</c> search key and its string argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>SUBJECT</c> search key is defined by RFC 9051 and performs a
        /// case‑insensitive substring match against the message’s <c>Subject:</c>
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
            builder.AddString("SUBJECT ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the string argument used by the IMAP <c>SUBJECT</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is matched as a case‑insensitive substring against the
        /// message’s <c>Subject:</c> header field, as specified by RFC 9051.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
