using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>SINCE</c> search key, which selects messages
    /// whose internal date is on or after the specified date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>SINCE</c> search key is defined by RFC 3501 and operates on the
    /// message’s *internal date*—the server‑assigned timestamp indicating when
    /// the message was received or created.
    /// </para>
    /// <para>
    /// Messages whose internal date is the same day or a later day satisfy
    /// the search criterion.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Since : IMAP_t_Search_Key
    {
        private DateTime m_Date;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Since"/>
        /// class with the specified internal date.
        /// </summary>
        /// <param name="value">
        /// The internal date used by the IMAP <c>SINCE</c> search key.
        /// Messages whose internal date is the same day or a later day will
        /// satisfy the search criterion.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>SINCE</c> search key is defined by RFC 3501 and operates on the
        /// message’s *internal date*—the server‑assigned timestamp indicating when
        /// the message was received or created.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Since(DateTime value)
        {
            m_Date = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>SINCE</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Since"/> instance containing
        /// the parsed internal‑date value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>SINCE</c> or when the date
        /// value is missing or cannot be parsed using IMAP date syntax.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SINCE</c> search key is defined by RFC 3501 and compares the
        /// message’s *internal date*—the server‑assigned timestamp indicating when
        /// the message was received or created—to the specified value.
        /// </para>
        /// <para>
        /// Messages whose internal date is the same day or a later day satisfy
        /// the search criterion.
        /// </para>
        /// <para>
        /// The date must follow the IMAP <c>dd-MMM-yyyy</c> format and is parsed
        /// using <see cref="IMAP_Utils.ParseDate"/>.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Since Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"SINCE",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'SINCE' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'SINCE' value.");
            }
            DateTime date;
            try{
                date = IMAP_Utils.ParseDate(value);
            }
            catch{
                throw new ParseException("Parse error: Invalid 'SINCE' value.");
            }

            return new IMAP_t_Search_Key_Since(date);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>SINCE</c> search key with the date formatted
        /// according to IMAP requirements.
        /// </summary>
        /// <returns>
        /// A string of the form <c>SINCE dd-MMM-yyyy</c>, selecting messages
        /// whose internal date (the message's file system / delivery date)
        /// is on or after the specified date.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>SINCE</c> search key is defined by RFC 3501 and compares the
        /// message’s *internal date*—the date assigned by the server when the
        /// message was received or created—to the provided value.
        /// </para>
        /// <para>
        /// Messages whose internal date is the same day or a later day satisfy
        /// the search criterion.
        /// </para>
        /// <para>
        /// The date is serialized using the IMAP‑required <c>dd-MMM-yyyy</c>
        /// format with invariant‑culture English month abbreviations.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "SINCE " + m_Date.ToString("dd-MMM-yyyy",System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>SINCE</c> search key and its date argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>SINCE</c> search key is defined by RFC 3501 and compares the
        /// message’s *internal date*—the server‑assigned timestamp indicating when
        /// the message was received or created—to the specified value.
        /// </para>
        /// <para>
        /// Messages whose internal date is the same day or a later day satisfy
        /// the search criterion.
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
        /// Gets the internal date used by the IMAP <c>SINCE</c> search key.
        /// </summary>
        /// <remarks>
        /// Messages whose internal date is the same day or a later day will satisfy
        /// the <c>SINCE</c> search criterion. The comparison is performed at
        /// day granularity, as defined by RFC 3501.
        /// </remarks>
        public DateTime Date
        {
            get{ return m_Date; }
        }

        #endregion
    }
}
