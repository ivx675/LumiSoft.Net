using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>KEYWORD</c> search key, which selects messages
    /// that have the specified keyword (flag) set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>KEYWORD</c> search key is defined by RFC 3501 and matches messages
    /// whose flags include the specified keyword. The keyword argument is an
    /// IMAP string and may be represented as an atom, a quoted string, or an
    /// IMAP literal.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Keyword : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Keyword"/>
        /// class with the specified keyword value.
        /// </summary>
        /// <param name="value">
        /// The keyword (flag) to match. This is an IMAP string and may originate
        /// from an atom, a quoted string, or an IMAP literal.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>KEYWORD</c> search key selects messages that have the specified
        /// keyword set, as defined by RFC 3501.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Keyword(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>KEYWORD</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Keyword"/> instance containing the
        /// parsed keyword value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>KEYWORD</c>, or when the keyword
        /// value cannot be parsed as a valid IMAP string.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>KEYWORD</c> search key selects messages that have the specified
        /// keyword (flag) set, as defined by RFC 3501. The keyword argument is an
        /// IMAP string and may be represented as an atom, a quoted string, or an
        /// IMAP literal.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Keyword Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"KEYWORD",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'KEYWORD' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'KEYWORD' value.");
            }

            return new IMAP_t_Search_Key_Keyword(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>KEYWORD</c> search key formatted using the stored
        /// keyword value.
        /// </summary>
        /// <returns>
        /// A string of the form <c>KEYWORD flag</c>, where <c>flag</c> is the
        /// keyword to match.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>KEYWORD</c> search key selects messages that have the specified
        /// keyword (flag) set, as defined by RFC 3501. The keyword is an IMAP
        /// string and may be represented as an atom, a quoted string, or an IMAP
        /// literal when serialized through <c>ToCommandBuilder</c>.
        /// </para>
        /// <para>
        /// This method produces a simplified, human‑readable representation and
        /// does not emit literals or quoted strings. It is intended for logging
        /// and debugging rather than wire‑format serialization.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "KEYWORD " + m_Value;
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>KEYWORD</c> search key and its flag argument to the
        /// command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>KEYWORD</c> search key is defined by RFC 3501 and selects messages
        /// that have the specified keyword (flag) set. The flag argument is an IMAP
        /// string and may be represented as an atom, a quoted string, or an IMAP
        /// literal.
        /// </para>
        /// <para>
        /// Serialization is performed using
        /// <see cref="CommandBuilder.AddStringOrLiteral(string)"/>, ensuring correct
        /// IMAP formatting for both quoted strings and literals.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString("KEYWORD ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the keyword (flag) used by this IMAP <c>KEYWORD</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value represents the keyword that must be present on a message for
        /// the <c>KEYWORD</c> search key to match, as defined by RFC 3501.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
