using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>INTERNALDATE</c> FETCH response data item.
    /// <para>
    /// The <c>INTERNALDATE</c> attribute is defined in RFC 3501 section 7.4.2 as
    /// the server‑stored internal creation timestamp of a message. It appears in
    /// FETCH responses in the form:
    /// <code>
    /// INTERNALDATE SP "DD-Mmm-YYYY HH:MM:SS +ZZZZ"
    /// </code>
    /// where the value is always a quoted IMAP date‑time string. This timestamp
    /// reflects when the message was added to the mailbox, not the header
    /// <c>Date:</c> field.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_r_i_InternalDate : IMAP_t_Fetch_r_i
    {
        private DateTime m_Date;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_InternalDate"/> class
        /// using the parsed INTERNALDATE value from a FETCH response.
        /// <para>
        /// The IMAP <c>INTERNALDATE</c> represents the server‑stored internal creation
        /// timestamp of the message, as defined in RFC 3501 section 7.4.2. This value
        /// reflects when the message was added to the mailbox, not the header
        /// <c>Date:</c> field.
        /// </para>
        /// </summary>
        /// <param name="date">
        /// The internal message creation timestamp.
        /// </param>
        public IMAP_t_Fetch_r_i_InternalDate(DateTime date)
        {
            m_Date = date;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the IMAP <c>INTERNALDATE</c> FETCH response data item.
        /// <para>
        /// The <c>INTERNALDATE</c> attribute is defined in RFC 3501 section 7.4.2 as
        /// the server‑stored internal creation time of the message. It appears in
        /// FETCH responses in the form:
        /// <code>
        /// INTERNALDATE SP "DD-Mmm-YYYY HH:MM:SS +ZZZZ"
        /// </code>
        /// where the value is always a quoted IMAP date‑time string. The timestamp
        /// reflects when the message was added to the mailbox, not the header
        /// <c>Date:</c> field.
        /// </para>
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the start of the INTERNALDATE data item.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token.
        /// </param>
        /// <returns>
        /// A <see cref="IMAP_t_Fetch_r_i_InternalDate"/> instance containing the parsed
        /// internal message timestamp.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the INTERNALDATE data item is missing or the date‑time value
        /// is malformed.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_InternalDate> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* RFC 3501 7.4.2. FETCH Response – INTERNALDATE data item.
               Syntax: INTERNALDATE SP date-time
               Meaning: The server-stored internal creation time of the message.
               Notes:
                 • Returned as an IMAP date-time string: "DD-Mmm-YYYY HH:MM:SS +ZZZZ".
                 • The value reflects when the message was added to the mailbox,
                   not the message's header Date field.
                 • Must be parsed as a fixed-format IMAP date-time, not locale-dependent.
               Example:
                 S: * 23 FETCH (INTERNALDATE "17-Jul-2024 14:22:05 +0300")
            */

            if(!string.Equals("INTERNALDATE", imapReader.ReadAtom(), StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: INTERNALDATE data-item not found.");
            }

            string dateString = await imapReader.ReadStringAsync(cancellationToken) ?? "";

            try{
                return new IMAP_t_Fetch_r_i_InternalDate(IMAP_Utils.ParseDate(dateString));
            }
            catch{
                throw new ParseException("Invalid FETCH INTERNALDATE data-item: malformed date-time value.");
            }
        }


        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP <c>INTERNALDATE</c> value parsed from the FETCH response.
        /// <para>
        /// The <c>INTERNALDATE</c> represents the server‑stored internal creation
        /// timestamp of the message, as defined in RFC 3501 section 7.4.2. This
        /// timestamp reflects when the message was added to the mailbox, not the
        /// header <c>Date:</c> field. The value is parsed from the quoted IMAP
        /// date‑time string returned by the server.
        /// </para>
        /// </summary>
        public DateTime Date
        {
            get{ return m_Date; }
        }

        #endregion
    }
}
