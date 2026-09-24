using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>INTERNALDATE</c> FETCH request data‑item as defined
    /// in RFC 3501 section 7.4.2. When included in a FETCH command, this attribute
    /// instructs the server to return the message's internal creation timestamp.
    /// <para>
    /// <b>Semantics:</b>
    /// The <c>INTERNALDATE</c> attribute returns the server‑assigned timestamp
    /// indicating when the message was added to the mailbox. This value is distinct
    /// from the message's RFC822 <c>Date:</c> header field and is always returned
    /// as an IMAP date‑time string in the format:
    /// <code>
    /// DD-Mmm-YYYY HH:MM:SS +ZZZZ
    /// </code>
    /// The returned timestamp reflects the server's notion of message arrival and
    /// is used for sorting, searching, and synchronization.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_InternalDate : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_InternalDate()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the canonical IMAP token for this FETCH request data‑item.
        /// <para>
        /// For <c>INTERNALDATE</c>, the string representation is always the literal
        /// <c>"INTERNALDATE"</c>, which is inserted directly into the FETCH command
        /// attribute list. No parameters or additional formatting are required.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "INTERNALDATE";
        }

        #endregion
    }
}
