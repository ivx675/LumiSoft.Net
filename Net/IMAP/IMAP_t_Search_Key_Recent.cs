using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RECENT</c> search key, which matches messages
    /// carrying the <c>\Recent</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>RECENT</c> search key is defined by RFC 3501 and selects messages
    /// that the server considers newly delivered to the mailbox. The
    /// <c>\Recent</c> flag is session‑specific and may vary between clients
    /// simultaneously accessing the same mailbox.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Recent : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Recent()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>RECENT</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Recent"/> instance representing
        /// the <c>RECENT</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>RECENT</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Recent Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"RECENT",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'RECENT' key.");
            }

            return new IMAP_t_Search_Key_Recent();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>RECENT</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>RECENT</c>, which matches messages that have the
        /// <c>\Recent</c> flag set.
        /// </returns>
        /// <remarks>
        /// The <c>RECENT</c> search key is defined by RFC 3501 and selects messages
        /// that the server considers newly delivered to the mailbox. The
        /// <c>\Recent</c> flag is session‑specific and may differ between clients
        /// that have the same mailbox open.
        /// </remarks>
        public override string ToString()
        {
            return "RECENT";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this <c>RECENT</c> search key to the IMAP command under
        /// construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// The <c>RECENT</c> search key is a flag‑type criterion defined by
        /// RFC 3501. It carries no associated value and is emitted as a fixed
        /// keyword token in IMAP <c>SEARCH</c> and <c>UID SEARCH</c> commands.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
