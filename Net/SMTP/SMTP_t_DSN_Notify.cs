using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP
{
    /// <summary>
    /// Specifies the SMTP DSN (Delivery Status Notification) NOTIFY settings
    /// as defined in RFC 3461. These values indicate under which conditions
    /// a delivery status notification should be generated for a recipient.
    /// </summary>
    /// <remarks>
    /// The NOTIFY parameter may include one or more of the values
    /// <c>Success</c>, <c>Failure</c>, and <c>Delay</c>. These may be combined
    /// unless the <c>Never</c> flag is used. When <c>Never</c> is specified,
    /// it must not be combined with any other NOTIFY values.
    ///
    /// If the NOTIFY parameter is not present in the RCPT TO command, the
    /// value is <c>NotSpecified</c>. In this case, servers may interpret the
    /// absence of NOTIFY as either <c>Failure</c> or <c>Failure,Delay</c>
    /// for compatibility with clients that do not implement DSN extensions.
    /// </remarks>
    [Flags()]
    public enum SMTP_t_DSN_Notify
    {
        /// <summary>
        /// Indicates that no NOTIFY parameter was supplied in the RCPT TO
        /// command. Servers may treat this as <c>Failure</c> or
        /// <c>Failure,Delay</c> for compatibility with non‑DSN clients.
        /// </summary>
        NotSpecified = 0,

        /// <summary>
        /// Indicates that no delivery status notification should be generated
        /// under any circumstances. This value must not be combined with any
        /// other NOTIFY flags.
        /// </summary>
        Never =  1,

        /// <summary>
        /// A DSN should be generated when the message is successfully delivered.
        /// </summary>
        Success = 2,

        /// <summary>
        /// A DSN should be generated when delivery fails.
        /// </summary>
        Failure = 4,

        /// <summary>
        /// A DSN should be generated when delivery is delayed.
        /// </summary>
        Delay = 8,
    }
}
