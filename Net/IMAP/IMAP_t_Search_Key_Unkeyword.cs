using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>UNKEYWORD</c> search key, which selects messages
    /// that do <em>not</em> have the specified keyword (flag) set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>UNKEYWORD</c> search key is defined by RFC 3501 and is the logical
    /// inverse of the <c>KEYWORD</c> search key. It matches messages whose flags
    /// do not include the specified keyword.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Unkeyword : IMAP_t_Search_Key
    {
        private string m_Value = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Unkeyword"/>
        /// class with the specified keyword value.
        /// </summary>
        /// <param name="value">
        /// The keyword (flag) that must <em>not</em> be present on a message for
        /// this search key to match. The value is an IMAP string and may originate
        /// from an atom, a quoted string, or an IMAP literal.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>UNKEYWORD</c> search key is defined by RFC 3501 and selects
        /// messages that do not have the specified keyword set.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Unkeyword(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>UNKEYWORD</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Unkeyword"/> instance containing the
        /// parsed keyword value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>UNKEYWORD</c>, or when the keyword
        /// value cannot be parsed as a valid IMAP string.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>UNKEYWORD</c> search key selects messages that do <em>not</em>
        /// have the specified keyword (flag) set, as defined by RFC 3501.
        /// </para>
        /// <para>
        /// The keyword argument is an IMAP string and may be represented as an atom,
        /// a quoted string, or an IMAP literal.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Unkeyword Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"UNKEYWORD",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'UNKEYWORD' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'UNKEYWORD' value.");
            }

            return new IMAP_t_Search_Key_Unkeyword(value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a human‑readable representation of the IMAP <c>UNKEYWORD</c>
        /// search key using the stored keyword value.
        /// </summary>
        /// <returns>
        /// A string of the form <c>UNKEYWORD flag</c>, where <c>flag</c> is the
        /// keyword that must *not* be present on a message for the search key to
        /// match.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>UNKEYWORD</c> search key selects messages that do not have the
        /// specified keyword (flag) set, as defined by RFC 3501.
        /// </para>
        /// <para>
        /// This method produces a simplified, human‑readable representation intended
        /// for logging and debugging. It does not emit quoted strings or IMAP
        /// literals; wire‑format serialization is performed by
        /// <c>ToCommandBuilder</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "UNKEYWORD " + m_Value;
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>UNKEYWORD</c> search key and its keyword argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>UNKEYWORD</c> search key is defined by RFC 3501 and selects
        /// messages that do <em>not</em> have the specified keyword (flag) set.
        /// The keyword argument is an IMAP string and may be represented as an
        /// atom, a quoted string, or an IMAP literal.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString("UNKEYWORD ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the keyword (flag) used by this IMAP <c>UNKEYWORD</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value represents the keyword that must <em>not</em> be present on a
        /// message for the <c>UNKEYWORD</c> search key to match, as defined by
        /// RFC 3501.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
