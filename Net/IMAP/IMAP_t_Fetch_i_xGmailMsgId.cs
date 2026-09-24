using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the Gmail‑specific <c>X-GM-MSGID</c> FETCH request data‑item
    /// as defined in the Gmail IMAP Extensions documentation. When included in
    /// a FETCH command, this attribute instructs the server to return the stable
    /// 64‑bit immutable message identifier assigned internally by Gmail.
    /// <para>
    /// Unlike IMAP UIDs, which may change when messages are moved, when
    /// UIDVALIDITY changes, or when folders are rebuilt, the <c>X-GM-MSGID</c>
    /// value remains constant for the lifetime of the message. The identifier is
    /// returned as a decimal 64‑bit integer.
    /// </para>
    /// <para>
    /// <b>Semantics:</b>
    /// <c>X-GM-MSGID</c> provides a persistent, cross‑folder message identifier
    /// suitable for:
    /// <list type="bullet">
    /// <item><description>Deduplication</description></item>
    /// <item><description>Cross‑folder correlation</description></item>
    /// <item><description>Stable message tracking across sync cycles</description></item>
    /// <item><description>Conversation analysis (when paired with <c>X-GM-THRID</c>)</description></item>
    /// </list>
    /// The value is informational only and does not modify message flags or
    /// server state.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_xGmailMsgId : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Initializes a new <see cref="IMAP_t_Fetch_i_xGmailMsgId"/> instance.
        /// <para>
        /// This represents the Gmail‑specific <c>X-GM-MSGID</c> FETCH data‑item,
        /// which requests the immutable 64‑bit message identifier assigned internally
        /// by Gmail. When used without additional parameters, the FETCH command
        /// simply includes the attribute name <c>X-GM-MSGID</c>, and the server
        /// returns the stable message ID for the target message.
        /// </para>
        /// </summary>
        public IMAP_t_Fetch_i_xGmailMsgId()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>X-GM-MSGID</c> FETCH attribute string for this
        /// instance.
        /// <para>
        /// This data‑item is part of the Gmail IMAP Extensions and requests the
        /// stable, immutable 64‑bit message identifier assigned internally by
        /// Gmail. The value returned by this method is the literal attribute name
        /// used in the FETCH command.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "X-GM-MSGID";
        }

        #endregion
    }
}
