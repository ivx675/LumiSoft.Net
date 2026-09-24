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
    /// The <c>SUBJECT</c> search key is defined by RFC 3501 and performs a
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
        /// The <c>SUBJECT</c> search key is defined by RFC 3501 and accepts any IMAP
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


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>SUBJECT</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Subject"/> instance containing
        /// the parsed string argument.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>SUBJECT</c> or when the string
        /// argument is missing or cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SUBJECT</c> search key is defined by RFC 3501 and performs a
        /// case‑insensitive substring match against the message’s <c>Subject:</c>
        /// header field.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Subject Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"SUBJECT",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'SUBJECT' key.");
            }
            string? value = IMAP_Utils.ReadString(r);
            if(value == null){
                throw new ParseException("Parse error: Invalid 'SUBJECT' value.");
            }

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
        /// substring match), as defined by RFC 3501.
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
        /// The <c>SUBJECT</c> search key is defined by RFC 3501 and performs a
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
        /// message’s <c>Subject:</c> header field, as specified by RFC 3501.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
