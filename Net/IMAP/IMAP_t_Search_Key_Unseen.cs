using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>UNSEEN</c> search key, which matches messages
    /// that do not carry the <c>\Seen</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>UNSEEN</c> search key is defined by RFC 3501 and selects
    /// messages that have not yet been read. It is the logical inverse of
    /// the <c>SEEN</c> search key.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Unseen : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Unseen()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>UNSEEN</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Unseen"/> instance representing
        /// the <c>UNSEEN</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>UNSEEN</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Unseen Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"UNSEEN",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'UNSEEN' key.");
            }

            return new IMAP_t_Search_Key_Unseen();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>UNSEEN</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>UNSEEN</c>, which matches messages that do not have
        /// the <c>\Seen</c> flag set.
        /// </returns>
        /// <remarks>
        /// The <c>UNSEEN</c> search key is defined by RFC 3501 and selects
        /// messages that have not yet been read. It is the logical inverse of
        /// the <c>SEEN</c> search key.
        /// </remarks>
        public override string ToString()
        {
            return "UNSEEN";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>UNSEEN</c> search key to the command under
        /// construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>UNSEEN</c> search key is defined by RFC 3501 and matches
        /// messages that do not carry the <c>\Seen</c> flag.
        /// </para>
        /// <para>
        /// As a flag‑type search key, <c>UNSEEN</c> carries no associated value
        /// and is serialized as a fixed keyword token in IMAP <c>SEARCH</c> and
        /// <c>UID SEARCH</c> commands.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
