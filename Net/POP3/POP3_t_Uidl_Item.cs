using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3 
{
    /// <summary>
    /// Represents a single POP3 UIDL listing entry as defined in RFC 1939.
    /// Each entry contains the POP3 message number and the unique identifier
    /// assigned by the server for that message.
    /// </summary>
    /// <param name="messageNumber">
    /// The 1-based POP3 message sequence number associated with the entry.
    /// </param>
    /// <param name="uniqueId">
    /// The server-defined unique identifier for the message. This value is an
    /// opaque string that remains stable across sessions and may contain any
    /// printable characters except whitespace.
    /// </param>
    /// <remarks>
    /// <para>
    /// UIDL entries allow POP3 clients to track which messages have already been
    /// downloaded. The unique identifier is server-defined and its internal
    /// format is not specified by the POP3 protocol.
    /// </para>
    /// <para>
    /// This struct is immutable and uses a primary constructor for compact,
    /// efficient representation. It is intended for use in UIDL responses only.
    /// </para>
    /// </remarks>
    public readonly struct POP3_t_Uidl_Item(int messageNumber,string uniqueId)
    {
        /// <summary>
        /// Gets the 1-based POP3 message number associated with this UIDL entry.
        /// </summary>
        public int MessageNumber { get; } = messageNumber;

        /// <summary>
        /// Gets the unique identifier assigned by the POP3 server for this message.
        /// This value is an opaque string and may contain any printable characters
        /// except whitespace.
        /// </summary>
        public string UniqueId   { get; } = uniqueId;

        /// <summary>
        /// Returns the UIDL entry in standard POP3 format:
        /// "messageNumber uniqueId".
        /// </summary>
        public override string ToString() => $"{MessageNumber} {UniqueId}";
    }
}
