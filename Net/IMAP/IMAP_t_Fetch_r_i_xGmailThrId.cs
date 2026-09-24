using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the Gmail‑specific IMAP extension data item <c>X-GM-THRID</c>
    /// returned within a <c>FETCH</c> response. Gmail assigns each message a
    /// stable 64‑bit thread identifier that is shared by all messages belonging
    /// to the same conversation thread.
    /// <para>
    /// The thread identifier remains constant regardless of label changes,
    /// mailbox movement, UID renumbering, or UIDVALIDITY changes. This makes
    /// <c>X-GM-THRID</c> suitable for conversation grouping, threading logic,
    /// and cross‑folder correlation.
    /// </para>
    /// <para>
    /// Although Gmail defines the thread identifier as an unsigned 64‑bit
    /// integer, all known values fit within the positive range of a signed
    /// <see cref="long"/>. The value is stored as a <see cref="long"/> for
    /// consistency with other IMAP numeric fields such as <c>UID</c>,
    /// <c>UIDVALIDITY</c>, and <c>UIDNEXT</c>.
    /// </para>
    /// <para>
    /// <c>X-GM-THRID</c> is part of Gmail’s proprietary IMAP extensions and is
    /// not defined by RFC 3501. Servers other than Gmail do not return this
    /// data item.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_r_i_xGmailThrId : IMAP_t_Fetch_r_i
    {
        private long m_ThreadID = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_xGmailThrId"/>
        /// class using the Gmail‑specific immutable thread identifier returned in the
        /// <c>X-GM-THRID</c> FETCH data item.
        /// <para>
        /// Gmail assigns each message a stable 64‑bit thread ID that is shared by all
        /// messages belonging to the same conversation thread. The identifier remains
        /// constant regardless of label changes, mailbox movement, UID renumbering, or
        /// UIDVALIDITY changes.
        /// </para>
        /// <para>
        /// Although Gmail defines the thread identifier as an unsigned 64‑bit integer,
        /// all known values fit within the positive range of a signed <see cref="long"/>.
        /// The constructor enforces non‑negativity to ensure the value conforms to the
        /// expected semantics of the <c>X-GM-THRID</c> extension.
        /// </para>
        /// </summary>
        /// <param name="threadID">
        /// The Gmail thread identifier as transmitted in the <c>X-GM-THRID</c> FETCH
        /// response. The value must be a non‑negative 64‑bit integer.
        /// </param>
        /// <exception cref="ParseException">
        /// Thrown if <paramref name="threadID"/> is negative, which indicates an
        /// invalid or malformed <c>X-GM-THRID</c> value.
        /// </exception>
        public IMAP_t_Fetch_r_i_xGmailThrId(long threadID)
        {
            if(threadID < 0){
                throw new ParseException("Invalid X-GM-THRID value: must be non-negative.");
            }

            m_ThreadID = threadID;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the Gmail‑specific IMAP extension data item <c>X-GM-THRID</c> from a
        /// <c>FETCH</c> response. Gmail assigns each message a stable 64‑bit thread
        /// identifier that is shared by all messages belonging to the same
        /// conversation thread.
        /// <para>
        /// The method expects the next atom in the protocol stream to be
        /// <c>X-GM-THRID</c>, followed by a decimal integer representing the Gmail
        /// thread identifier. The value is parsed into a signed 64‑bit integer for
        /// consistency with other IMAP numeric fields such as <c>UID</c>,
        /// <c>UIDVALIDITY</c>, and <c>UIDNEXT</c>.
        /// </para>
        /// <para>
        /// <c>X-GM-THRID</c> is part of Gmail’s proprietary IMAP extensions and is not
        /// defined by RFC 3501. Servers other than Gmail do not return this data item.
        /// The thread identifier remains stable regardless of label changes, mailbox
        /// movement, UID renumbering, or UIDVALIDITY changes.
        /// </para>
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the <c>X-GM-THRID</c> data item within a
        /// <c>FETCH</c> response.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token used for asynchronous operations. This method does not
        /// perform literal reads and therefore does not normally observe cancellation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_t_Fetch_r_i_xGmailThrId"/> instance containing the parsed
        /// Gmail thread identifier.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the next atom is not <c>X-GM-THRID</c>, or if the identifier is
        /// missing or not a valid 64‑bit integer.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_xGmailThrId> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* Gmail IMAP Extension: X-GM-THRID
               Reference: https://developers.google.com/gmail/imap_extensions

               X-GM-THRID is a Gmail-specific FETCH data item that exposes a stable,
               64-bit thread identifier assigned internally by Gmail. All messages that
               Gmail considers part of the same conversation share the same thread ID.

               Syntax (FETCH response):
                   X-GM-THRID SP thrid

               Example:
                   S: * 23 FETCH (X-GM-THRID 987654321012345678)

               Notes:
                 • The value is a decimal 64-bit integer.
                 • It uniquely identifies a Gmail conversation thread.
                 • All messages belonging to the same thread return the same THRID.
                 • The value does NOT change when:
                     – labels are added or removed,
                     – messages are moved between folders,
                     – UIDVALIDITY changes,
                     – UIDs are reassigned.
                 • Useful for:
                     – conversation grouping,
                     – threading logic,
                     – cross-folder correlation,
                     – stable thread tracking.

               This parser reads the X-GM-THRID atom and converts the numeric value into
               a 64-bit integer. If the atom is missing or the value is not a valid
               integer, a ParseException is thrown.
            */

            if(!string.Equals("X-GM-THRID",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: X-GM-THRID data-item not found.");
            }

            string msgidString = imapReader.ReadAtom();
            if(!long.TryParse(msgidString,out long msgid)){
                throw new ParseException("Invalid FETCH X-GM-THRID data-item: malformed X-GM-THRID value.");
            }

            return new IMAP_t_Fetch_r_i_xGmailThrId(msgid);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the Gmail‑specific immutable thread identifier associated with the
        /// <c>X-GM-THRID</c> FETCH data item. Gmail assigns each message a stable
        /// 64‑bit thread ID that is shared by all messages belonging to the same
        /// conversation thread.
        /// <para>
        /// Although Gmail defines the thread identifier as an unsigned 64‑bit integer,
        /// all known values fit within the positive range of a signed <see cref="long"/>.
        /// The value is stored as a <see cref="long"/> for consistency with other IMAP
        /// numeric fields such as <c>UID</c>, <c>UIDVALIDITY</c>, and <c>UIDNEXT</c>.
        /// </para>
        /// <para>
        /// The thread identifier remains constant regardless of label changes, mailbox
        /// movement, UID renumbering, or UIDVALIDITY changes, making it suitable for
        /// conversation grouping, threading logic, and cross‑folder correlation.
        /// </para>
        /// </summary>
        public long ThreadId
        {
            get{ return m_ThreadID; }
        }

        #endregion
    }
}
