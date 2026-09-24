using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>FLAGS</c> FETCH request data‑item as defined in
    /// RFC 3501 section 7.4.2. When included in a FETCH command, this attribute
    /// instructs the server to return the complete set of flags currently
    /// associated with each matching message.
    /// <para>
    /// <b>Semantics:</b>
    /// The <c>FLAGS</c> attribute returns the message's current flag state as a
    /// parenthesized list of atoms. System flags defined by RFC 3501 include:
    /// <list type="bullet">
    /// <item><description><c>\Seen</c> — Message has been read.</description></item>
    /// <item><description><c>\Answered</c> — Message has been replied to.</description></item>
    /// <item><description><c>\Flagged</c> — Message is marked as important.</description></item>
    /// <item><description><c>\Deleted</c> — Message is marked for deletion.</description></item>
    /// <item><description><c>\Draft</c> — Message is a draft.</description></item>
    /// <item><description><c>\Recent</c> — Message has been delivered recently.</description></item>
    /// </list>
    /// User-defined flags are arbitrary atoms without a leading backslash.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_Flags : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_Flags()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the canonical IMAP token for this FETCH request data‑item.
        /// <para>
        /// For <c>FLAGS</c>, the string representation is always the literal
        /// <c>"FLAGS"</c>, which is inserted directly into the FETCH command
        /// attribute list. No parameters or additional formatting are required.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "FLAGS";
        }

        #endregion
    }
}
