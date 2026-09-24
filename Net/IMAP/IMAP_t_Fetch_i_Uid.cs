using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>UID</c> FETCH request data‑item as defined in
    /// RFC 3501 section 6.4.5. When included in a FETCH command, the <c>UID</c>
    /// data‑item instructs the server to return the unique identifier assigned
    /// to each matching message.
    /// <para>
    /// Semantics:
    /// The <c>UID</c> data‑item is a FETCH attribute that causes the server to
    /// return the message's UID in the corresponding FETCH response. UIDs are
    /// server‑assigned, non‑zero unsigned integers that increase monotonically
    /// within a mailbox. They are stable across sessions and are used for
    /// persistent message identification.
    /// </para>
    /// <para>
    /// Notes:
    /// <list type="bullet">
    /// <item><description>UIDs are mailbox‑specific and persist until the mailbox
    /// is recreated or its UIDVALIDITY changes.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_Uid : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_Uid()
        {
        }


        #region override method ToString

         /// <summary>
        /// Returns the canonical IMAP FETCH attribute name for this data‑item.
        /// <para>
        /// For the <c>UID</c> FETCH request attribute, the string representation is
        /// always the literal <c>"UID"</c>, as defined in RFC 3501 section 7.4.2.
        /// This value is inserted directly into the FETCH command attribute list,
        /// for example:
        /// <code>
        /// FETCH 1:10 (UID FLAGS RFC822.SIZE)
        /// </code>
        /// </para>
        /// <para>
        /// The returned string does not include any parameters, delimiters, or
        /// additional formatting; it is the exact token the server expects in the
        /// FETCH request.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "UID";
        }

        #endregion
    }
}
