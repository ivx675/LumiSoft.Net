using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>UID</c> search key, which restricts a search
    /// to messages whose unique identifiers fall within a specified
    /// sequence‑set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>UID</c> search key is defined by RFC 3501 and uses the same
    /// sequence‑set grammar as standard IMAP sequence‑sets, but operates on
    /// stable message UIDs rather than ephemeral message sequence numbers.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Uid : IMAP_t_Search_Key
    {
        private IMAP_t_SeqSet m_pSeqSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Uid"/>
        /// class with the specified UID sequence‑set.
        /// </summary>
        /// <param name="seqSet">
        /// The IMAP sequence‑set specifying the UIDs to match. UID sets use the
        /// same grammar as standard IMAP sequence‑sets but operate on stable
        /// message unique identifiers.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>UID</c> search key is defined by RFC 3501 and restricts the
        /// search to messages whose unique identifiers fall within the provided
        /// sequence‑set.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Uid(IMAP_t_SeqSet seqSet)
        {
            if(seqSet == null){
                throw new ArgumentNullException("seqSet");
            }

            m_pSeqSet = seqSet;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>UID</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Uid"/> instance containing the
        /// parsed UID sequence‑set.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>UID</c>, or when the sequence‑set
        /// cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>UID</c> search key is defined by RFC 3501 and restricts the
        /// search to messages whose unique identifiers fall within the specified
        /// sequence‑set. UID sequence‑sets follow the same grammar as standard
        /// IMAP sequence‑sets but operate on stable message UIDs.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Uid Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"UID",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'UID' key.");
            }
            r.ReadToFirstChar();
            string value = r.QuotedReadToDelimiter(' ');
            if(value == null){
                throw new ParseException("Parse error: Invalid 'UID' value.");
            }
            
            try{
                return new IMAP_t_Search_Key_Uid(IMAP_t_SeqSet.Parse(value));
            }
            catch{
                throw new ParseException("Parse error: Invalid 'UID' value.");
            }
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>UID</c> search key with its sequence‑set argument.
        /// </summary>
        /// <returns>
        /// A string of the form <c>UID set</c>, where <c>set</c> is the
        /// sequence‑set specifying the UIDs to match. UID sets follow the same
        /// grammar as IMAP sequence‑sets but operate on stable message UIDs.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>UID</c> search key is defined by RFC 3501 and restricts the
        /// search to messages whose unique identifiers fall within the specified
        /// sequence‑set.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "UID " + m_pSeqSet.ToString();
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>UID</c> search key and its sequence‑set argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>UID</c> search key is defined by RFC 3501 and restricts the
        /// search to messages whose unique identifiers fall within the specified
        /// sequence‑set.
        /// </para>
        /// <para>
        /// Serialization is delegated to <see cref="ToString()"/>, which formats
        /// the key as <c>UID set</c> using <see cref="IMAP_t_SeqSet.ToString()"/>
        /// for proper IMAP sequence‑set grammar.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the UID sequence‑set associated with this IMAP <c>UID</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is an <see cref="IMAP_t_SeqSet"/> representing the set of
        /// message unique identifiers to match. UID sets use the same grammar as
        /// standard IMAP sequence‑sets (single numbers, ranges, and wildcards),
        /// but operate on stable message UIDs rather than volatile sequence
        /// numbers.
        /// </para>
        /// </remarks>
        public IMAP_t_SeqSet Value
        {
            get{ return m_pSeqSet; }
        }

        #endregion

    }
}
