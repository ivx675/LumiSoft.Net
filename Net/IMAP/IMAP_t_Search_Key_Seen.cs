using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>SEEN</c> search key, which matches messages
    /// marked with the <c>\Seen</c> system flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>SEEN</c> search key is defined by RFC 9051 and selects messages
    /// that have already been read or otherwise marked as seen by the client.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Seen : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Seen()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>SEEN</c> search key from the provided
        /// <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="_IMAP_Reader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Seen"/> instance representing
        /// the <c>SEEN</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>SEEN</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Seen Parse(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }

            string word = r.ReadAtom();
            if(!string.Equals(word,"SEEN",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'SEEN' key.");
            }

            return new IMAP_t_Search_Key_Seen();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>SEEN</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>SEEN</c>, which matches messages that have the
        /// <c>\Seen</c> flag set.
        /// </returns>
        /// <remarks>
        /// The <c>SEEN</c> search key is defined by RFC 9051 and selects messages
        /// that have already been read or otherwise marked as seen by the client.
        /// </remarks>
        public override string ToString()
        {
            return "SEEN";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this <c>SEEN</c> search key to the IMAP command under
        /// construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// The <c>SEEN</c> search key is a flag‑type criterion defined by RFC 9051.
        /// It carries no associated value and is emitted as a fixed keyword token
        /// in IMAP <c>SEARCH</c> and <c>UID SEARCH</c> commands.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
