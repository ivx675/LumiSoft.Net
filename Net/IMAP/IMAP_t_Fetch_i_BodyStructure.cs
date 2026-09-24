using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>BODYSTRUCTURE</c> FETCH request data‑item as
    /// defined in RFC 3501 section 7.4.2. When included in a FETCH command,
    /// this attribute instructs the server to return the full MIME body
    /// structure of the message, including all parts, subparts, media types,
    /// encodings, sizes, and disposition information.
    /// <para>
    /// <b>Semantics:</b>
    /// The <c>BODYSTRUCTURE</c> attribute returns a detailed MIME description
    /// of the message, including:
    /// <list type="bullet">
    /// <item><description>Media type and subtype</description></item>
    /// <item><description>Content‑Transfer‑Encoding</description></item>
    /// <item><description>Part size in octets</description></item>
    /// <item><description>Line count for text parts</description></item>
    /// <item><description>Multipart subtype and child parts</description></item>
    /// <item><description>Content‑Disposition and parameters</description></item>
    /// <item><description>Content‑Language</description></item>
    /// <item><description>MD5 (optional)</description></item>
    /// </list>
    /// The structure is recursive for multipart messages. Any field may be
    /// returned as <c>NIL</c> if not present in the message.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_BodyStructure : IMAP_t_Fetch_i
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_t_Fetch_i_BodyStructure()
        {
        }


        #region override method ToString

        /// <summary>
        /// Returns the canonical IMAP token for this FETCH request data‑item.
        /// <para>
        /// For <c>BODYSTRUCTURE</c>, the string representation is always the
        /// literal <c>"BODYSTRUCTURE"</c>, which is inserted directly into the
        /// FETCH command attribute list. No parameters or additional formatting
        /// are required.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            return "BODYSTRUCTURE";
        }

        #endregion
    }
}
