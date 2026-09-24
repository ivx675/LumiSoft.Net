using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP SEARCH key that matches messages based on an IMAP
    /// sequence set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IMAP sequence sets are defined by RFC 3501 and represent message
    /// number ranges or lists, such as <c>1:10</c>, <c>1,2,4:7</c>, or
    /// <c>*</c>. Sequence sets are structural tokens and are never quoted in
    /// SEARCH or FETCH commands.
    /// </para>
    /// <para>
    /// This search key is typically used with the <c>UID</c> search operator,
    /// for example: <c>UID 1:10</c>. The contained sequence set determines
    /// which messages are matched.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_SeqSet : IMAP_t_Search_Key
    {
        private IMAP_t_SeqSet m_pSeqSet;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_SeqSet"/>
        /// class with the specified IMAP sequence set.
        /// </summary>
        /// <param name="seqSet">
        /// The IMAP sequence set that defines the message numbers or ranges to
        /// match. The value must not be <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// IMAP sequence sets are defined by RFC 3501 and represent message
        /// number ranges or lists, such as <c>1:10</c>, <c>1,2,4:7</c>, or
        /// <c>*</c>. Sequence sets are structural tokens and are never quoted in
        /// SEARCH or FETCH commands.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_SeqSet(IMAP_t_SeqSet seqSet)
        {
            if(seqSet == null){
                throw new ArgumentNullException("seqSet");
            }

            m_pSeqSet = seqSet;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP sequence‑set search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the sequence‑set to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_SeqSet"/> instance containing the
        /// parsed IMAP sequence‑set.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the sequence‑set token is missing or cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// IMAP sequence sets are defined by RFC 3501 and represent message
        /// number ranges or lists, such as <c>1:10</c>, <c>1,2,4:7</c>, or
        /// <c>*</c>. Sequence sets are structural tokens and are never quoted in
        /// IMAP SEARCH or FETCH commands.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_SeqSet Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            r.ReadToFirstChar();
            string value = r.QuotedReadToDelimiter(' ');
            if(value == null){
                throw new ParseException("Parse error: Invalid 'sequence-set' value.");
            }
            
            try{
                return new IMAP_t_Search_Key_SeqSet(IMAP_t_SeqSet.Parse(value));
            }
            catch{
                throw new ParseException("Parse error: Invalid 'sequence-set' value.");
            }
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire‑format representation of the sequence set
        /// associated with this search key.
        /// </summary>
        /// <returns>
        /// The sequence set as an unquoted IMAP token, such as <c>1:10</c>,
        /// <c>1,2,4:7</c>, or <c>*</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// IMAP sequence sets are defined by RFC 3501 and represent message
        /// number ranges or lists. They are structural tokens and are never
        /// quoted in SEARCH or FETCH commands.
        /// </para>
        /// <para>The returned value is
        /// intended for logging and debugging; command serialization is handled
        /// by <c>ToCommandBuilder</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return m_pSeqSet.ToString();
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP sequence set associated with this search key to the
        /// command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized sequence set.
        /// </param>
        /// <remarks>
        /// <para>
        /// IMAP sequence sets are defined by RFC 3501 and represent message
        /// number ranges or lists, such as <c>1:10</c>, <c>1,2,4:7</c>, or
        /// <c>*</c>. Sequence sets are structural tokens and are never quoted in
        /// IMAP SEARCH or FETCH commands.
        /// </para>
        /// <para>
        /// Serialization delegates to <see cref="IMAP_t_SeqSet.ToString()"/>,
        /// which produces the correct IMAP wire‑format representation.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString(m_pSeqSet.ToString());
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP sequence set associated with this search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// IMAP sequence sets are defined by RFC 3501 and represent message
        /// number ranges or lists, such as <c>1:10</c>, <c>1,2,4:7</c>, or
        /// <c>*</c>. Sequence sets are structural tokens and are never quoted in
        /// SEARCH or FETCH commands.
        /// </para>
        /// <para>
        /// The returned <see cref="IMAP_t_SeqSet"/> instance reflects the exact
        /// sequence set supplied during construction or parsing.
        /// </para>
        /// </remarks>
        public IMAP_t_SeqSet Value
        {
            get{ return m_pSeqSet; }
        }

        #endregion

    }
}
