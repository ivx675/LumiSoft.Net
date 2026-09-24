using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>FROM</c> search key, which selects messages
    /// whose <c>From:</c> header field contains the specified text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>FROM</c> search key is defined by RFC 3501 and performs a
    /// case‑insensitive substring match against the message’s <c>From:</c>
    /// header field.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_From : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_From"/>
        /// class with the specified string argument.
        /// </summary>
        /// <param name="value">
        /// The text used by the IMAP <c>FROM</c> search key. This value is matched
        /// as a case‑insensitive substring against the message’s <c>From:</c>
        /// header field.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>FROM</c> search key is defined by RFC 3501 and accepts any IMAP
        /// string, including quoted strings and literals.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_From(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>FROM</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_From"/> instance containing
        /// the parsed string argument.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>FROM</c> or when the string
        /// argument is missing or cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>FROM</c> search key is defined by RFC 3501 and performs a
        /// case‑insensitive substring match against the message’s <c>From:</c>
        /// header field.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_From Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"FROM",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'FROM' key.");
            }
            string? value = IMAP_Utils.ReadString(r);
            if(value == null){
                throw new ParseException("Parse error: Invalid 'FROM' value.");
            }

            return new IMAP_t_Search_Key_From(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>FROM</c> search key with its quoted string argument.
        /// </summary>
        /// <returns>
        /// A string of the form <c>FROM "value"</c>, selecting messages whose
        /// <c>From:</c> header field contains the specified text (case‑insensitive
        /// substring match).
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>FROM</c> search key is defined by RFC 3501 and performs a
        /// case‑insensitive substring match against the message’s <c>From:</c>
        /// header field.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "FROM " + TextUtils.QuoteString(m_Value);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>FROM</c> search key and its string argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>FROM</c> search key is defined by RFC 3501 and performs a
        /// case‑insensitive substring match against the message’s <c>From:</c>
        /// header field.
        /// </para>
        /// <para>
        /// The argument is written using <see cref="CommandBuilder.AddStringOrLiteral(string)"/>,
        /// which emits either a quoted string or an IMAP literal depending on the
        /// content of <c>m_Value</c>. This ensures correct formatting for values
        /// containing special characters or requiring literal form.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString("FROM ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the string argument used by the IMAP <c>FROM</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is matched as a case‑insensitive substring against the
        /// message’s <c>From:</c> header field, as defined by RFC 3501.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
