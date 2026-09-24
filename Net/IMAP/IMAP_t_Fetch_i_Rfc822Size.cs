using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RFC822.SIZE</c> FETCH request data‑item as defined
    /// in RFC 3501 section 7.4.2. When included in a FETCH command, this attribute
    /// instructs the server to return the size of the message in octets as stored
    /// on the server.
    /// </summary>
    public class IMAP_t_Fetch_i_Rfc822Size : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_Rfc822Size()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the canonical IMAP token for this FETCH request data‑item.
        /// <para>
        /// For <c>RFC822.SIZE</c>, the string representation is always the literal
        /// <c>"RFC822.SIZE"</c>, which is inserted directly into the FETCH command
        /// attribute list. No parameters or additional formatting are required.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "RFC822.SIZE";
        }

        #endregion
    }
}
