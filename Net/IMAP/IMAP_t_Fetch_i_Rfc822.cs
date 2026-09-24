using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RFC822</c> FETCH request data‑item as defined
    /// in RFC 3501 section 7.4.2. When included in a FETCH command, this
    /// attribute instructs the server to return the entire raw message,
    /// including both the header and the body, exactly as stored in the
    /// mailbox.
    /// </summary>
    public class IMAP_t_Fetch_i_Rfc822 : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Returns the canonical IMAP token for this FETCH request data‑item.
        /// <para>
        /// For <c>RFC822</c>, the string representation is always the literal
        /// <c>"RFC822"</c>, which is inserted directly into the FETCH command
        /// attribute list. No parameters or additional formatting are required.
        /// </para>
        /// </summary>
        public IMAP_t_Fetch_i_Rfc822()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns this as string.
        /// </summary>
        /// <returns>Returns this as string.</returns>
        public override string ToString()
        {
            return "RFC822";
        }

        #endregion
    }
}
