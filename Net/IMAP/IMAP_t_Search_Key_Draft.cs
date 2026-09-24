using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>DRAFT</c> search key, which matches messages
    /// marked with the <c>\Draft</c> flag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>DRAFT</c> search key is defined by RFC 3501 and selects all
    /// messages that have been marked with the <c>\Draft</c> flag. This flag
    /// is typically used by mail clients to indicate messages that are still
    /// being composed or are not yet ready to be sent.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Draft : IMAP_t_Search_Key
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Search_Key_Draft()
        {
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>DRAFT</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Draft"/> instance representing
        /// the <c>DRAFT</c> search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not the IMAP <c>DRAFT</c> search key.
        /// </exception>
        internal static IMAP_t_Search_Key_Draft Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"DRAFT",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'DRAFT' key.");
            }

            return new IMAP_t_Search_Key_Draft();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>DRAFT</c> search key.
        /// </summary>
        /// <returns>
        /// The string <c>DRAFT</c>, which matches messages marked with the
        /// <c>\Draft</c> flag.
        /// </returns>
        /// <remarks>
        /// The <c>DRAFT</c> search key is defined by RFC 3501 and selects all
        /// messages that have been marked with the <c>\Draft</c> flag. This flag
        /// is typically used by clients to indicate messages still being composed
        /// or not yet ready for sending.
        /// </remarks>
        public override string ToString()
        {
            return "DRAFT";
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this <c>DRAFT</c> search key to the IMAP command under
        /// construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// Since <c>DRAFT</c> is a flag‑type search key, it carries no associated
        /// value and is emitted as a fixed keyword token, as defined by RFC 3501.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion
    }
}
