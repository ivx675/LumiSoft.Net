using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>ON</c> search key, which selects messages whose
    /// internal date exactly matches the specified date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>ON</c> search key is defined by RFC 3501 and performs an exact
    /// day‑level comparison against a message’s internal date. It is one of
    /// the three primary IMAP date search keys, alongside <c>BEFORE</c> and
    /// <c>SINCE</c>.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_On : IMAP_t_Search_Key
    {
        private DateTime m_Date;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_On"/>
        /// class with the specified date.
        /// </summary>
        /// <param name="value">
        /// The date used by the IMAP <c>ON</c> search key. Messages whose
        /// internal date exactly matches this value will satisfy the search
        /// criterion.
        /// </param>
        public IMAP_t_Search_Key_On(DateTime value)
        {
            m_Date = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>ON</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_On"/> instance representing
        /// the <c>ON</c> date search criterion.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>ON</c> or when the date value
        /// is missing or invalid.
        /// </exception>
        /// <remarks>
        /// The <c>ON</c> search key performs an exact match against a message’s
        /// internal date. The date value must follow the IMAP date format
        /// <c>dd-MMM-yyyy</c>.
        /// </remarks>
        internal static IMAP_t_Search_Key_On Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"ON",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'ON' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'ON' value.");
            }
            DateTime date;
            try{
                date = IMAP_Utils.ParseDate(value);
            }
            catch{
                throw new ParseException("Parse error: Invalid 'ON' value.");
            }

            return new IMAP_t_Search_Key_On(date);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>ON</c> search key with the date formatted according
        /// to IMAP requirements.
        /// </summary>
        /// <returns>
        /// A string of the form <c>ON dd-MMM-yyyy</c>, selecting messages whose
        /// internal date exactly matches the specified date.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>ON</c> search key is defined by RFC 3501 and performs an exact
        /// date match against a message’s internal date. The date is serialized
        /// using the <c>dd-MMM-yyyy</c> format and the invariant culture, as
        /// required by the IMAP specification.
        /// </para>
        /// <para>
        /// Example: <c>ON 18-Sep-2026</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "ON " + m_Date.ToString("dd-MMM-yyyy",System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>ON</c> search key and its date argument to the
        /// command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>ON</c> search key is defined by RFC 3501 and performs an exact
        /// match against a message’s internal date. The date is serialized using
        /// the IMAP‑required <c>dd-MMM-yyyy</c> format and the invariant culture.
        /// </para>
        /// <para>
        /// Example output: <c>ON 18-Sep-2026</c>.
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
        /// Gets the date used by the IMAP <c>ON</c> search key.
        /// </summary>
        /// <remarks>
        /// Messages whose internal date exactly matches this value will satisfy
        /// the <c>ON</c> search criterion. The comparison is performed at day
        /// granularity, as defined by RFC 3501.
        /// </remarks>
        public DateTime Date
        {
            get{ return m_Date; }
        }

        #endregion
    }
}
