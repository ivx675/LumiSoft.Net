using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>DELETED</c> search key, which matches messages
    /// marked with the <c>\Deleted</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>DELETED</c> search key is defined by RFC 3501 and selects all
    /// messages that have been flagged for deletion. Such messages are not
    /// removed immediately; they remain in the mailbox until an explicit
    /// <c>EXPUNGE</c> command is issued.
    /// </para>
    /// <para>
    /// Since <c>DELETED</c> is a flag‑type search key, it carries no associated
    /// value and is serialized as a fixed keyword in IMAP <c>SEARCH</c> and
    /// <c>UID SEARCH</c> commands.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Deleted : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Deleted()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>DELETED</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Deleted"/> instance representing
        /// the <c>DELETED</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>DELETED</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Deleted Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"DELETED",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'DELETED' key.");
            }

            return new IMAP_t_Search_Key_Deleted();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>DELETED</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>DELETED</c>, which matches messages that have the
        /// <c>\Deleted</c> flag set.
        /// </returns>
        public override string ToString()
        {
            return "DELETED";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this <c>DELETED</c> search key to the IMAP command under
        /// construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// This implementation emits the search key as a simple string token,
        /// since <c>DELETED</c> does not carry any associated value. Flag‑type
        /// search keys rely on fixed keyword serialization as defined by
        /// RFC 3501.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
