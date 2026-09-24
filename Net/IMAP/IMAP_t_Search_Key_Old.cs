using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>OLD</c> search key, which matches messages that
    /// do not carry the <c>\Recent</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>OLD</c> search key is defined by RFC 3501 and selects messages
    /// that are not newly delivered to the mailbox. It is effectively the
    /// inverse of the <c>RECENT</c> search key.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Old : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Old()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>OLD</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Old"/> instance representing
        /// the <c>OLD</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>OLD</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Old Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"OLD",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'OLD' key.");
            }

            return new IMAP_t_Search_Key_Old();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>OLD</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>OLD</c>, which matches messages that do not have the
        /// <c>\Recent</c> flag set.
        /// </returns>
        /// <remarks>
        /// The <c>OLD</c> search key is defined by RFC 3501 and selects messages
        /// that are not newly delivered to the mailbox. It is the logical inverse
        /// of <c>RECENT</c>, but is emitted as a standalone keyword in IMAP
        /// <c>SEARCH</c> and <c>UID SEARCH</c> commands.
        /// </remarks>
        public override string ToString()
        {
            return "OLD";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>OLD</c> search key to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>OLD</c> search key is defined by RFC 3501 and matches messages
        /// that do not carry the <c>\Recent</c> flag. It is effectively the inverse
        /// of <c>RECENT</c>, but is emitted as a standalone keyword in IMAP
        /// <c>SEARCH</c> and <c>UID SEARCH</c> commands.
        /// </para>
        /// <para>
        /// As a flag‑type search key, <c>OLD</c> carries no associated value and is
        /// serialized as a fixed keyword token.
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
