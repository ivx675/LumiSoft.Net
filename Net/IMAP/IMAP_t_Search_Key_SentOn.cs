using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>SENTON</c> search key, which selects messages
    /// whose envelope (header) date exactly matches the specified date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>SENTON</c> search key is defined by RFC 3501 and performs an
    /// exact day‑level comparison against the message’s *envelope date*
    /// (the date found in the message header).
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_SentOn : IMAP_t_Search_Key
    {
        private DateTime m_Date;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_SentOn"/>
        /// class with the specified envelope date.
        /// </summary>
        /// <param name="value">
        /// The envelope (header) date used by the IMAP <c>SENTON</c> search key.
        /// Messages whose envelope date exactly matches this value will satisfy
        /// the search criterion.
        /// </param>
        public IMAP_t_Search_Key_SentOn(DateTime value)
        {
            m_Date = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>SENTON</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_SentOn"/> instance containing
        /// the parsed envelope‑date value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>SENTON</c> or when the date
        /// value is missing or cannot be parsed using IMAP date syntax.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SENTON</c> search key is defined by RFC 3501 and performs an
        /// exact day‑level comparison against the message’s *envelope date*
        /// (the date found in the message header).
        /// </para>
        /// <para>
        /// The date must follow the IMAP <c>dd-MMM-yyyy</c> format and is parsed
        /// using <see cref="IMAP_Utils.ParseDate"/>.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_SentOn Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"SENTON",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'SENTON' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'SENTON' value.");
            }
            DateTime date;
            try{
                date = IMAP_Utils.ParseDate(value);
            }
            catch{
                throw new ParseException("Parse error: Invalid 'SENTON' value.");
            }

            return new IMAP_t_Search_Key_SentOn(date);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>SENTON</c> search key with the date formatted
        /// according to IMAP requirements.
        /// </summary>
        /// <returns>
        /// A string of the form <c>SENTON dd-MMM-yyyy</c>, selecting messages
        /// whose envelope (header) date exactly matches the specified date.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>SENTON</c> search key is defined by RFC 3501 and performs an
        /// exact day‑level comparison against the message’s *envelope date*
        /// (the date found in the message header).
        /// </para>
        /// <para>
        /// The date is serialized using the IMAP‑required <c>dd-MMM-yyyy</c>
        /// format with invariant‑culture English month abbreviations.
        /// </para>
        /// <para>
        /// Example: <c>SENTON 18-Sep-2026</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "SENTON " + m_Date.ToString("dd-MMM-yyyy",System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>SENTON</c> search key and its date argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>SENTON</c> search key is defined by RFC 3501 and performs an
        /// exact day‑level comparison against the message’s *envelope date*
        /// (the date found in the message header).
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
        /// Gets the envelope (header) date used by the IMAP <c>SENTON</c>
        /// search key.
        /// </summary>
        /// <remarks>
        /// Messages whose envelope date exactly matches this value will satisfy
        /// the <c>SENTON</c> search criterion. The comparison is performed at
        /// day granularity, as defined by RFC 3501.
        /// </remarks>
        public DateTime Date
        {
            get{ return m_Date; }
        }

        #endregion
    }
}
