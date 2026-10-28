using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>NEW</c> search key, which matches messages
    /// that are both <c>\Recent</c> and not marked with <c>\Seen</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>NEW</c> search key is defined by RFC 3501 as a composite
    /// search criterion equivalent to <c>RECENT UNSEEN</c>. It selects
    /// messages that have been newly delivered to the mailbox and have
    /// not yet been read.
    /// </para>
    /// <para>
    /// This search key is obsolete in IMAP4rev2 (RFC 9051) because the
    /// <c>\Recent</c> message flag has been removed from the protocol.
    /// New applications should use other search criteria and should not
    /// rely on the presence of the <c>NEW</c> search key.
    /// </para>
    /// <para>
    /// Support for this search key is retained only for compatibility
    /// with IMAP4rev1 clients.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_New : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_New()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>NEW</c> search key from the provided
        /// <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="_IMAP_Reader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_New"/> instance representing
        /// the <c>NEW</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>NEW</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_New Parse(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }

            string word = r.ReadAtom();
            if(!string.Equals(word,"NEW",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'NEW' key.");
            }

            return new IMAP_t_Search_Key_New();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>NEW</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>NEW</c>, which matches messages that are both
        /// <c>\Recent</c> and not marked with <c>\Seen</c>.
        /// </returns>
        /// <remarks>
        /// The <c>NEW</c> search key is defined by RFC 3501 as a convenience
        /// composite: it selects messages that are newly delivered to the mailbox
        /// (<c>\Recent</c>) and have not yet been read (<c>\Seen</c> not set).
        /// Like other flag‑type search keys, it carries no associated value and is
        /// serialized as a fixed keyword in IMAP <c>SEARCH</c> and
        /// <c>UID SEARCH</c> commands.
        /// </remarks>
        public override string ToString()
        {
            return "NEW";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>NEW</c> search key to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>NEW</c> search key is defined by RFC 3501 as a composite search
        /// criterion equivalent to <c>RECENT NOT SEEN</c>. It matches messages that
        /// are newly delivered to the mailbox and have not yet been read.
        /// </para>
        /// <para>
        /// Although <c>NEW</c> is conceptually composite, it is serialized as the
        /// literal keyword <c>NEW</c> in IMAP <c>SEARCH</c> and <c>UID SEARCH</c>
        /// commands.
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
