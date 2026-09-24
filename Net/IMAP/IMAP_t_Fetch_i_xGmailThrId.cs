using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the Gmail‑specific <c>X-GM-THRID</c> FETCH request data‑item
    /// as defined in the Gmail IMAP Extensions documentation. When included in
    /// a FETCH command, this attribute instructs the server to return the stable
    /// 64‑bit thread identifier associated with the message.
    /// <para>
    /// Gmail assigns each conversation a unique thread ID. All messages that
    /// Gmail considers part of the same conversation share the same
    /// <c>X-GM-THRID</c> value. The thread ID is returned as a decimal
    /// 64‑bit integer.
    /// </para>
    /// <para>
    /// <b>Semantics:</b>
    /// <c>X-GM-THRID</c> provides a persistent, cross‑folder thread identifier
    /// that does not change when labels are added or removed, when messages are
    /// moved, or when UIDVALIDITY changes. This makes it suitable for:
    /// <list type="bullet">
    /// <item><description>Conversation grouping</description></item>
    /// <item><description>Cross‑folder correlation</description></item>
    /// <item><description>Stable thread tracking across sync cycles</description></item>
    /// </list>
    /// The value is informational only and does not modify message flags or
    /// server state.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_xGmailThrId : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_xGmailThrId()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>X-GM-THRID</c> FETCH attribute string for this
        /// instance.
        /// <para>
        /// This data‑item is part of the Gmail IMAP Extensions and requests the
        /// stable 64‑bit thread identifier associated with the message. The value
        /// returned by this method is the literal attribute name used in the FETCH
        /// command.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "X-GM-THRID";
        }

        #endregion
    }
}
