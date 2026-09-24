using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>ALL</c> search key, which selects every message
    /// in the currently selected mailbox.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>ALL</c> search key is defined by RFC 3501 as the most permissive
    /// search criterion. When used in a <c>SEARCH</c> or <c>UID SEARCH</c>
    /// command, it instructs the server to return all messages without applying
    /// any additional filtering.
    /// </para>
    /// <para>
    /// This search key is commonly used as a baseline criterion or as part of
    /// compound search expressions. For example, clients may combine <c>ALL</c>
    /// with other search keys to ensure predictable behavior when constructing
    /// complex queries.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_All : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_All()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>ALL</c> search key from the provided <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// An instance of <see cref="IMAP_t_Search_Key_All"/> representing the
        /// IMAP <c>ALL</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>ALL</c> search key.
        /// </exception>
        /// <remarks>
        /// The <c>ALL</c> search key is defined by RFC 3501 as selecting every message
        /// in the currently selected mailbox. This method validates that the next
        /// token in the input stream matches <c>ALL</c> (case‑insensitive) and returns
        /// the corresponding search‑key object.
        /// </remarks>
        internal static IMAP_t_Search_Key_All Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"ALL",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'ALL' key.");
            }

            return new IMAP_t_Search_Key_All();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP search key string representing the <c>ALL</c> criterion.
        /// </summary>
        /// <returns>
        /// The string <c>"ALL"</c>, which instructs the IMAP server to match every
        /// message in the currently selected mailbox. This is the most permissive
        /// search key and is defined by RFC 3501 as selecting all messages without
        /// restriction.
        /// </returns>
        public override string ToString()
        {
            return "ALL";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this search key to the IMAP command being constructed.
        /// </summary>
        /// <param name="builder">
        /// The command builder to append the search key to.
        /// </param>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
