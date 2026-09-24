using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RFC822.TEXT</c> FETCH request data‑item as defined
    /// in RFC 3501 section 7.4.2. When included in a FETCH command, this attribute
    /// instructs the server to return the textual body of the message, excluding
    /// the message header.
    /// <para>
    /// The client includes <c>RFC822.TEXT</c> in the FETCH attribute list to
    /// request the message body text exactly as transmitted, without MIME parsing
    /// or structure information.
    /// </para>
    /// <para>
    /// <b>Semantics:</b>
    /// <c>RFC822.TEXT</c> returns the raw message body section following the header
    /// delimiter. It may contain any octets permitted in a message body, including
    /// binary data if the server supports the BINARY extension. The returned data
    /// is always delivered as a literal in the FETCH response.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_Rfc822Text : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_Rfc822Text()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the canonical IMAP token for this FETCH request data‑item.
        /// <para>
        /// For <c>RFC822.TEXT</c>, the string representation is always the literal
        /// <c>"RFC822.TEXT"</c>, which is inserted directly into the FETCH command
        /// attribute list. No parameters or additional formatting are required.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "RFC822.TEXT";
        }

        #endregion
    }
}
