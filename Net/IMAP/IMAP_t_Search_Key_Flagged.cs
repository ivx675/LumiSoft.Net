using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>FLAGGED</c> search key, which matches messages
    /// marked with the <c>\Flagged</c> system flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>FLAGGED</c> search key is defined by RFC 3501 and selects all
    /// messages that have been marked as important or requiring special
    /// attention. Clients typically use the <c>\Flagged</c> flag to highlight
    /// messages that need follow‑up or are otherwise noteworthy.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Flagged : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Flagged()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>FLAGGED</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Flagged"/> instance representing
        /// the <c>FLAGGED</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>FLAGGED</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Flagged Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"FLAGGED",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'FLAGGED' key.");
            }

            return new IMAP_t_Search_Key_Flagged();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>FLAGGED</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>FLAGGED</c>, which matches messages marked with the
        /// <c>\Flagged</c> system flag.
        /// </returns>
        /// <remarks>
        /// The <c>FLAGGED</c> search key is defined by RFC 3501 and selects all
        /// messages that have been marked as important or requiring special
        /// attention.
        /// </remarks>
        public override string ToString()
        {
            return "FLAGGED";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this <c>FLAGGED</c> search key to the IMAP command under
        /// construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// Since <c>FLAGGED</c> is a flag‑type search key, it carries no associated
        /// value and is emitted as a fixed keyword token, as specified by RFC 3501.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
