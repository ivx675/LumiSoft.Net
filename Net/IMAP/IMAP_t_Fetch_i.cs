using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Base class for all IMAP FETCH request data‑items as defined in RFC 3501
    /// section 6.4.5. Each derived type represents a specific attribute that may
    /// be included in a FETCH command, such as <c>BODY</c>, <c>BODY.PEEK</c>,
    /// <c>FLAGS</c>, <c>UID</c>, or Gmail‑specific extensions like
    /// <c>X-GM-LABELS</c>, <c>X-GM-THRID</c>, and <c>X-GM-MSGID</c>.
    /// <para>
    /// FETCH data‑items instruct the server which message attributes or content
    /// fragments to return. They may request metadata, message flags, envelope
    /// information, body structure, or specific body sections with optional
    /// partial‑fetch parameters.
    /// </para>
    /// </summary>
    public abstract class IMAP_t_Fetch_i
    {
    }
}
