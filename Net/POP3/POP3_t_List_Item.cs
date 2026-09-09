using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3
{   
    /// <summary>
    /// Represents a single POP3 LIST scan‑listing entry as defined in RFC 1939.
    /// Each entry contains the message number and the octet size of that message
    /// as returned by the POP3 server during a LIST operation.
    /// </summary>
    /// <param name="messageNumber">
    /// The 1‑based POP3 message sequence number associated with the entry.
    /// </param>
    /// <param name="sizeInBytes">
    /// The size of the message in octets as reported by the POP3 server.
    /// </param>
    /// <remarks>
    /// <para>
    /// LIST entries provide lightweight metadata about messages in the mailbox.
    /// They do not include message headers, content, or UIDL values.
    /// </para>
    /// <para>
    /// This struct is immutable and uses a primary constructor for compact,
    /// efficient representation. It is intended for use in LIST responses only.
    /// </para>
    /// </remarks>
    public readonly struct POP3_t_List_Item(int messageNumber, int sizeInBytes)
    {
        /// <summary>
        /// Gets the 1‑based POP3 message sequence number associated with this LIST entry.
        /// </summary>
        public int MessageNumber { get; } = messageNumber;

        /// <summary>
        /// Gets the size of the message in octets as reported by the POP3 server.
        /// </summary>
        public int SizeInBytes   { get; } = sizeInBytes;

        /// <summary>
        /// Returns the LIST entry in standard POP3 format: &lt;msg-number&gt; &lt;size&gt;.
        /// </summary>
        public override string ToString() => $"{MessageNumber} {SizeInBytes}";
    }
}
