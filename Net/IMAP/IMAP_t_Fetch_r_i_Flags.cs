using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>FLAGS</c> data item returned within a <c>FETCH</c>
    /// response. The <c>FLAGS</c> element reports the complete set of system and
    /// user‑defined flags currently associated with the message as defined by
    /// RFC 3501 section 7.4.2.
    /// <para>
    /// System flags are atoms beginning with a backslash (for example
    /// <c>\Seen</c>, <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
    /// <c>\Draft</c>, and <c>\Recent</c>). User‑defined flags are arbitrary atoms
    /// without the leading backslash. Servers may send unsolicited <c>FLAGS</c>
    /// updates when message state changes occur, meaning the values represented by
    /// this class may change over the lifetime of a mailbox session.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_r_i_Flags : IMAP_t_Fetch_r_i
    {
        private string[] m_pFlags;

        /// <summary>
        /// Initializes a new <see cref="IMAP_t_Fetch_r_i_Flags"/> instance using the
        /// flag atoms returned by the server in the IMAP <c>FLAGS</c> data item.
        /// </summary>
        /// <param name="flags">
        /// The complete set of message flags exactly as transmitted by the server.
        /// This array may contain both system flags (those beginning with a backslash,
        /// such as <c>\Seen</c>, <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// <c>\Draft</c>, and <c>\Recent</c>) and any user‑defined flags.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Fetch_r_i_Flags(string[] flags)
        {
            if(flags == null){
                throw new ArgumentNullException("flags");
            }

            m_pFlags = flags;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the IMAP <c>FLAGS</c> data item from a <c>FETCH</c> response using the
        /// supplied <see cref="_IMAP_Reader"/>. The <c>FLAGS</c> data item reports the
        /// complete set of system and user-defined flags currently associated with the
        /// message.
        /// <para>
        /// System flags defined by RFC 3501 include <c>\Seen</c>, <c>\Answered</c>,
        /// <c>\Flagged</c>, <c>\Deleted</c>, <c>\Draft</c>, and <c>\Recent</c>. User‑
        /// defined flags are arbitrary atoms without the leading backslash. Servers may
        /// send unsolicited <c>FLAGS</c> updates when message state changes occur.
        /// </para>
        /// </summary>
        /// <param name="imapReader">
        /// The reader positioned at the <c>FLAGS</c> data item within a <c>FETCH</c>
        /// response. The reader must be ready to consume the <c>FLAGS</c> atom and the
        /// subsequent parenthesized flag list.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token used for asynchronous operations. This method does not
        /// perform literal reads and therefore does not normally observe cancellation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_t_Fetch_r_i_Flags"/> instance containing the parsed flag
        /// atoms exactly as transmitted by the server.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the next atom is not <c>FLAGS</c>, or if the parenthesized flag
        /// list is missing or malformed.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Flags> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* RFC 3501 7.4.2. FETCH Response – FLAGS data item.
               Syntax: FLAGS SP flag-list
               Meaning: The set of flags currently associated with the message.
               Notes:
                 • The server returns all flags it considers applicable to the message.
                 • The flag-list is a parenthesized list of atoms, e.g.:
                     FLAGS (\Seen \Answered \Flagged)
                 • Flags may be system flags (beginning with "\") or user-defined flags.
                 • System flags defined by RFC 3501:
                     \Seen       – Message has been read.
                     \Answered   – Message has been replied to.
                     \Flagged    – Message is marked as important.
                     \Deleted    – Message is marked for deletion.
                     \Draft      – Message is a draft.
                     \Recent     – Message has been delivered recently.
                 • User-defined flags are arbitrary atoms without the leading "\".
                 • The server may send FLAGS in unsolicited FETCH responses when flag
                   changes occur (e.g., another client modifies the message state).
               Example:
                 S: * 12 FETCH (FLAGS (\Seen \Answered))
            */

            if(!string.Equals("FLAGS", imapReader.ReadAtom(), StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: FLAGS data-item not found.");
            }

            string flagList = imapReader.ReadParenthesized();

            return new IMAP_t_Fetch_r_i_Flags(flagList.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries));
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the complete set of IMAP message flags associated with the FETCH
        /// response item. The array contains both system flags (those beginning with
        /// a backslash, such as <c>\Seen</c> or <c>\Answered</c>) and any user‑defined
        /// flags returned by the server.
        /// <para>
        /// IMAP flags are simple atoms defined by RFC 3501 and may be updated
        /// asynchronously by the server. A <c>FETCH</c> response containing a
        /// <c>FLAGS</c> data item reflects the server’s current view of the message
        /// state at the time the response was generated.
        /// </para>
        /// </summary>
        public string[] Flags
        {
            get{ return m_pFlags; }
        }

        #endregion
    }
}
