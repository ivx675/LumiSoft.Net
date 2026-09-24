using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents the Gmail‑specific <c>X-GM-LABELS</c> FETCH request data‑item
    /// as defined in the Gmail IMAP Extensions documentation. When included in
    /// a FETCH command, this attribute instructs the server to return the full
    /// set of Gmail labels assigned to the message.
    /// <para>
    /// Gmail labels may include:
    /// <list type="bullet">
    /// <item><description>System labels such as <c>\Inbox</c>, <c>\Important</c>,
    /// <c>\Starred</c>, <c>\Draft</c></description></item>
    /// <item><description>User‑defined labels such as <c>Work</c>,
    /// <c>ProjectA</c>, <c>Invoices</c></description></item>
    /// </list>
    /// Labels are returned as a parenthesized list of quoted strings.
    /// </para>
    /// <para>
    /// <b>Semantics:</b>
    /// <c>X-GM-LABELS</c> exposes Gmail’s label model, which differs from
    /// traditional IMAP folders. A message may have multiple labels
    /// simultaneously, and labels do not imply physical storage locations.
    /// This data‑item is informational only and does not modify message flags.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_xGmailLabels : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Initializes a new <see cref="IMAP_t_Fetch_i_xGmailLabels"/> instance.
        /// <para>
        /// This represents the Gmail‑specific <c>X-GM-LABELS</c> FETCH data‑item,
        /// which requests the complete set of labels assigned to a message.
        /// When used without additional parameters, the FETCH command simply
        /// includes the attribute name <c>X-GM-LABELS</c>, and the server returns
        /// all system and user‑defined labels associated with the message.
        /// </para>
        /// </summary>
        public IMAP_t_Fetch_i_xGmailLabels()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>X-GM-LABELS</c> FETCH attribute string for this
        /// instance.
        /// <para>
        /// This data‑item is part of the Gmail IMAP Extensions and requests the
        /// complete set of labels assigned to the message. The value returned by
        /// this method is the literal attribute name used in the FETCH command.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "X-GM-LABELS";
        }

        #endregion
    }
}
