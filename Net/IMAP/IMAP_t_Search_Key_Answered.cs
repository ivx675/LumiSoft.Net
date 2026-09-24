using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>ANSWERED</c> search key, which matches messages
    /// marked with the <c>\Answered</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>ANSWERED</c> search key is defined by RFC 3501 as selecting all
    /// messages that have been replied to. A message carries the <c>\Answered</c>
    /// flag when the user or client has indicated that a response has been sent.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Answered : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Answered()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>ANSWERED</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Answered"/> instance representing the
        /// IMAP <c>ANSWERED</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>ANSWERED</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Answered Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"ANSWERED",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'ANSWERED' key.");
            }

            return new IMAP_t_Search_Key_Answered();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>ANSWERED</c> search key, which matches messages
        /// marked with the <c>\Answered</c> flag.
        /// </summary>
        /// <returns>
        /// The string <c>"ANSWERED"</c>, representing the IMAP search criterion
        /// that selects all messages flagged as answered.
        /// </returns>
        /// <remarks>
        /// The <c>ANSWERED</c> search key is defined by RFC 3501 and filters
        /// messages that have been replied to. When included in a <c>SEARCH</c>
        /// or <c>UID SEARCH</c> command, the server returns only those messages
        /// carrying the <c>\Answered</c> flag.
        /// </remarks>
        public override string ToString()
        {
            return "ANSWERED";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this search key to the IMAP command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
