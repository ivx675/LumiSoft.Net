using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>ENVELOPE</c> FETCH request data‑item as defined
    /// in RFC 3501 section 7.4.2. When included in a FETCH command, this
    /// attribute instructs the server to return the structured message envelope,
    /// which contains parsed header information such as date, subject, sender,
    /// recipients, and message ID.
    /// <para>
    /// <b>Semantics:</b>
    /// The <c>ENVELOPE</c> attribute returns a structured set of header fields,
    /// including:
    /// <list type="bullet">
    /// <item><description>Date</description></item>
    /// <item><description>Subject</description></item>
    /// <item><description>From</description></item>
    /// <item><description>Sender</description></item>
    /// <item><description>Reply‑To</description></item>
    /// <item><description>To</description></item>
    /// <item><description>Cc</description></item>
    /// <item><description>Bcc</description></item>
    /// <item><description>In‑Reply‑To</description></item>
    /// <item><description>Message‑ID</description></item>
    /// </list>
    /// The server may return <c>NIL</c> for any field that is absent. All address
    /// lists are returned in IMAP address structure format.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_Envelope : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_Envelope()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the canonical IMAP token for this FETCH request data‑item.
        /// <para>
        /// For <c>ENVELOPE</c>, the string representation is always the literal
        /// <c>"ENVELOPE"</c>, which is inserted directly into the FETCH command
        /// attribute list. No parameters or additional formatting are required.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "ENVELOPE";
        }

        #endregion
    }
}
