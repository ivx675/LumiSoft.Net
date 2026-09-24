using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>UID</c> FETCH response data item.
    /// <para>
    /// The <c>UID</c> attribute is defined in RFC 3501 section 2.3.1.1 as a
    /// server‑assigned unique identifier for a message within a mailbox.
    /// It appears in FETCH responses in the form:
    /// <code>
    /// UID SP nz-number
    /// </code>
    /// where <c>nz-number</c> is a non‑zero unsigned integer. Although the RFC
    /// specifies a 32‑bit range, real‑world servers may return values exceeding
    /// this limit; therefore the UID is stored as a <see cref="long"/>.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_r_i_Uid : IMAP_t_Fetch_r_i
    {
        private long m_UID = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_Uid"/> class
        /// using the parsed UID value from a FETCH response.
        /// <para>
        /// The IMAP <c>UID</c> is defined in RFC 3501 section 2.3.1.1 as a
        /// server‑assigned unique identifier for a message within a mailbox.
        /// Although the specification describes it as a non‑zero unsigned 32‑bit
        /// integer, real‑world servers may return values outside this range.
        /// For this reason, the UID is stored as a <see cref="long"/>.
        /// </para>
        /// </summary>
        /// <param name="uid">
        /// The UID value parsed from the FETCH response. Must be a non‑negative number.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="uid"/> is negative.
        /// </exception>
        public IMAP_t_Fetch_r_i_Uid(long uid)
        {
            if(uid < 0){
                throw new ArgumentException("Invalid UID value: UID must be >= 0.",nameof(uid));
            }

            m_UID = uid;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the IMAP <c>UID</c> FETCH response data item.
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the start of the UID data item.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token.
        /// </param>
        /// <returns>
        /// A strongly‑typed <see cref="IMAP_t_Fetch_r_i_Uid"/> instance containing the parsed UID.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the UID data item is missing or contains an invalid numeric value.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Uid> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* RFC 3501 7.4.2. FETCH Response – UID data item.
               Syntax: UID SP nz-number
               Meaning: Server-assigned unique identifier for the message within the mailbox.
               Notes:
                 • UID is defined as a non-zero unsigned 32-bit integer (nz-number).
                 • Real-world servers may return values exceeding 32-bit; store in long.
                 • UID increases monotonically for the lifetime of the mailbox.
               Example:
                 S: * 23 FETCH (UID 48291)
            */

            if(!string.Equals("UID",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: UID data-item not found.");
            }

            string uidString  = imapReader.ReadAtom();
            if(!long.TryParse(uidString,out long uid)){
                throw new ParseException("Invalid FETCH UID data-item: malformed UID value.");
            }

            return new IMAP_t_Fetch_r_i_Uid(uid);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP <c>UID</c> value parsed from the FETCH response.
        /// <para>
        /// The <c>UID</c> is a server‑assigned unique identifier for a message within
        /// a mailbox, defined in RFC 3501 section 2.3.1.1. Although the specification
        /// describes it as a non‑zero unsigned 32‑bit integer, real‑world servers may
        /// return values exceeding this range; therefore the UID is stored as a
        /// <see cref="long"/>.
        /// </para>
        /// </summary>
        public long Uid
        {
            get{ return m_UID; }
        }

        #endregion
    }
}
