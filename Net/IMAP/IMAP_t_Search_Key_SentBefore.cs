using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>SENTBEFORE</c> search key, which selects messages
    /// whose envelope (header) date is earlier than the specified date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>SENTBEFORE</c> search key is defined by RFC 3501 and compares the
    /// message’s *envelope date*—the date found in the message header—to the
    /// provided value. Messages whose envelope date is strictly earlier than
    /// the given date satisfy the search criterion.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_SentBefore : IMAP_t_Search_Key
    {
        private DateTime m_Date;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_SentBefore"/>
        /// class with the specified envelope date.
        /// </summary>
        /// <param name="value">
        /// The envelope (header) date used by the IMAP <c>SENTBEFORE</c> search key.
        /// Messages whose envelope date is strictly earlier than this value will
        /// satisfy the search criterion.
        /// </param>
        public IMAP_t_Search_Key_SentBefore(DateTime value)
        {
            m_Date = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>SENTBEFORE</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_SentBefore"/> instance containing
        /// the parsed envelope‑date value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>SENTBEFORE</c> or when the date
        /// value is missing or cannot be parsed using IMAP date syntax.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SENTBEFORE</c> search key compares the message’s *envelope*
        /// (header) date to the specified value. Messages whose envelope date is
        /// strictly earlier than the given date satisfy the criterion.
        /// </para>
        /// <para>
        /// The date must follow the IMAP <c>dd-MMM-yyyy</c> format and is parsed
        /// using <see cref="IMAP_Utils.ParseDate"/>.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_SentBefore Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"SENTBEFORE",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'SENTBEFORE' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'SENTBEFORE' value.");
            }
            DateTime date;
            try{
                date = IMAP_Utils.ParseDate(value);
            }
            catch{
                throw new ParseException("Parse error: Invalid 'SENTBEFORE' value.");
            }

            return new IMAP_t_Search_Key_SentBefore(date);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>SENTBEFORE</c> search key with the date formatted
        /// according to IMAP requirements.
        /// </summary>
        /// <returns>
        /// A string of the form <c>SENTBEFORE dd-MMM-yyyy</c>, selecting messages
        /// whose <em>envelope date</em> is earlier than the specified date.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>SENTBEFORE</c> search key is defined by RFC 3501 and compares
        /// the message’s *envelope* date (the date in the message header), not
        /// the internal IMAP date. The comparison is performed at day granularity.
        /// </para>
        /// <para>
        /// Example: <c>SENTBEFORE 18-Sep-2026</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "SENTBEFORE " + m_Date.ToString("dd-MMM-yyyy",System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>SENTBEFORE</c> search key and its date argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>SENTBEFORE</c> search key is defined by RFC 3501 and compares
        /// the message’s *envelope* date (the date in the message header) to the
        /// specified value. Messages whose envelope date is earlier than the
        /// given date satisfy the criterion.
        /// </para>
        /// <para>
        /// The date is serialized using the IMAP‑required <c>dd-MMM-yyyy</c>
        /// format with invariant‑culture English month abbreviations.
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
        /// Gets the envelope (header) date used by the IMAP <c>SENTBEFORE</c>
        /// search key.
        /// </summary>
        /// <remarks>
        /// Messages whose envelope date is strictly earlier than this value will
        /// satisfy the <c>SENTBEFORE</c> search criterion. The comparison is
        /// performed at day granularity, as defined by RFC 3501.
        /// </remarks>
        public DateTime Date
        {
            get{ return m_Date; }
        }

        #endregion
    }
}
