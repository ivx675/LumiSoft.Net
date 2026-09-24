using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the Gmail‑specific IMAP extension data item <c>X-GM-MSGID</c>
    /// returned within a <c>FETCH</c> response. Gmail assigns each message a
    /// stable 64‑bit identifier that remains constant for the lifetime of the
    /// message, regardless of label changes, mailbox movement, UID renumbering,
    /// or UIDVALIDITY changes.
    /// <para>
    /// <c>X-GM-MSGID</c> is part of Gmail’s proprietary IMAP extensions and is not
    /// defined by RFC 3501. The identifier is transmitted as a decimal 64‑bit
    /// integer. Although Gmail defines the value as unsigned, all known values fit
    /// within the positive range of a signed <see cref="long"/>, which is used for
    /// consistency with other IMAP numeric fields such as <c>UID</c>,
    /// <c>UIDVALIDITY</c>, and <c>UIDNEXT</c>.
    /// </para>
    /// <para>
    /// The value exposed by this class is suitable for stable message tracking,
    /// deduplication, cross‑folder correlation, and threading (when paired with
    /// the <c>X-GM-THRID</c> extension).
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_r_i_xGmailMsgId : IMAP_t_Fetch_r_i
    {
        private long m_MsgID = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_xGmailMsgId"/>
        /// class using the Gmail‑specific immutable message identifier returned in the
        /// <c>X-GM-MSGID</c> FETCH data item.
        /// <para>
        /// Gmail assigns each message a stable 64‑bit identifier that remains constant
        /// for the lifetime of the message, regardless of label changes, mailbox
        /// movement, UID renumbering, or UIDVALIDITY changes. Although Gmail defines
        /// the identifier as an unsigned 64‑bit integer, all known values fit within
        /// the positive range of a signed <see cref="long"/>.
        /// </para>
        /// </summary>
        /// <param name="msgID">
        /// The Gmail message identifier as transmitted in the <c>X-GM-MSGID</c> FETCH
        /// response. The value is expected to be a non‑negative 64‑bit integer.
        /// </param>
        public IMAP_t_Fetch_r_i_xGmailMsgId(long msgID)
        {
            m_MsgID = msgID;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the Gmail‑specific IMAP extension data item <c>X-GM-MSGID</c> from a
        /// <c>FETCH</c> response. Gmail assigns each message a stable 64‑bit identifier
        /// that remains constant for the lifetime of the message, regardless of label
        /// changes, mailbox movement, or UID renumbering.
        /// <para>
        /// The method expects the next atom in the protocol stream to be
        /// <c>X-GM-MSGID</c>, followed by a decimal integer representing the immutable
        /// Gmail message identifier. The value is parsed into a signed 64‑bit integer
        /// for consistency with other IMAP numeric fields such as <c>UID</c>,
        /// <c>UIDVALIDITY</c>, and <c>UIDNEXT</c>.
        /// </para>
        /// <para>
        /// <c>X-GM-MSGID</c> is part of Gmail’s proprietary IMAP extensions and is not
        /// defined by RFC 3501. Servers other than Gmail do not return this data item.
        /// </para>
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the <c>X-GM-MSGID</c> data item within a
        /// <c>FETCH</c> response.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token used for asynchronous operations. This method does not
        /// perform literal reads and therefore does not normally observe cancellation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_t_Fetch_r_i_xGmailMsgId"/> instance containing the parsed
        /// Gmail message identifier.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the next atom is not <c>X-GM-MSGID</c>, or if the identifier is
        /// missing or not a valid 64‑bit integer.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_xGmailMsgId> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* Gmail IMAP Extension: X-GM-MSGID
               Reference: https://developers.google.com/gmail/imap_extensions

               X-GM-MSGID is a Gmail-specific FETCH data item that exposes a stable,
               64-bit message identifier assigned internally by Gmail. Unlike IMAP UIDs,
               which may change when a mailbox is rebuilt or messages are moved between
               folders, X-GM-MSGID is immutable for the lifetime of the message.

               Syntax (FETCH response):
                   X-GM-MSGID SP msgid

               Example:
                   S: * 23 FETCH (X-GM-MSGID 1234567890123456789)

               Notes:
                 • The value is a decimal 64-bit integer.
                 • It uniquely identifies the message across all Gmail folders/labels.
                 • It does NOT change when:
                     – the message is moved between labels,
                     – the UIDVALIDITY changes,
                     – the UID is reassigned,
                     – the message is archived or starred.
                 • Useful for:
                     – deduplication,
                     – cross-folder correlation,
                     – stable message tracking,
                     – threading (paired with X-GM-THRID).

               This parser reads the X-GM-MSGID atom and converts the numeric value into
               a 64-bit integer. If the atom is missing or the value is not a valid
               integer, a ParseException is thrown.
            */

            if(!string.Equals("X-GM-MSGID",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: X-GM-MSGID data-item not found.");
            }

            string msgidString = imapReader.ReadAtom();
            if(!long.TryParse(msgidString,out long msgid)){
                throw new ParseException("Invalid FETCH X-GM-MSGID data-item: malformed X-GM-MSGID value.");
            }

            return new IMAP_t_Fetch_r_i_xGmailMsgId(msgid);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the Gmail‑specific immutable message identifier associated with the
        /// <c>X-GM-MSGID</c> FETCH data item. Gmail assigns each message a stable
        /// 64‑bit identifier that remains constant for the lifetime of the message,
        /// regardless of label changes, mailbox movement, or UID renumbering.
        /// <para>
        /// Although Gmail defines the identifier as an unsigned 64‑bit integer, all
        /// known values fit within the positive range of a signed 64‑bit integer.
        /// For consistency with other IMAP numeric fields (<c>UID</c>,
        /// <c>UIDVALIDITY</c>, <c>UIDNEXT</c>), the value is stored as a <see cref="long"/>.
        /// </para>
        /// </summary>
        public long MsgId
        {
            get{ return m_MsgID; }
        }

        #endregion
    }
}
