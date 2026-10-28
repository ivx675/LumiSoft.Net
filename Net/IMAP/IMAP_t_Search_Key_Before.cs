using LumiSoft.Net.IMAP.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>BEFORE</c> search key, which matches messages
    /// whose internal date is earlier than the specified value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>BEFORE</c> search key is defined by RFC 9051 and selects all
    /// messages with an internal date strictly earlier than the given day.
    /// The comparison is performed at day granularity.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Before : IMAP_t_Search_Key
    {
        private DateOnly m_Date;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Before"/>
        /// class with the specified date.
        /// </summary>
        /// <param name="date">
        /// The date used by the IMAP <c>BEFORE</c> search key. Messages whose
        /// internal date is strictly earlier than this value will match the
        /// search criterion.
        /// </param>
        public IMAP_t_Search_Key_Before(DateOnly date)
        {
            m_Date = date;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>BEFORE</c> search key from the provided
        /// <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="_IMAP_Reader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Before"/> instance containing the
        /// parsed date value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>BEFORE</c> or when the date
        /// value cannot be parsed using IMAP date syntax.
        /// </exception>
        internal static IMAP_t_Search_Key_Before Parse(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }

            string? word = r.ReadAtom();
            if(!string.Equals(word,"BEFORE",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'BEFORE' key.");
            }
            
            DateOnly date = r.ReadDate();

            return new IMAP_t_Search_Key_Before(date);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>BEFORE</c> search key, which matches messages
        /// with an internal date earlier than the specified value.
        /// </summary>
        /// <returns>
        /// A string in the form <c>BEFORE dd-MMM-yyyy</c>, where the date is
        /// formatted using the invariant culture as required by IMAP.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>BEFORE</c> search key is defined by RFC 9051 and filters messages
        /// whose internal date (the server‑assigned message date) is strictly earlier
        /// than the given day. The comparison is performed at day granularity.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "BEFORE " + m_Date.ToString("dd-MMM-yyyy",System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends this search key to the IMAP command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// This implementation emits the search key as a simple string token.
        /// Complex keys override this method to provide literal or structured
        /// serialization as required.
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the date used by the IMAP <c>BEFORE</c> search key.
        /// </summary>
        /// <remarks>
        /// Messages whose internal date is strictly earlier than this value
        /// will match the <c>BEFORE</c> search criterion. The date is interpreted
        /// at day granularity, as defined by RFC 9051.
        /// </remarks>
        public DateOnly Date
        {
            get{ return m_Date; }
        }

        #endregion
    }
}
