using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP <c>FLAGS</c> response as defined in
    /// RFC 3501 section 7.2.6. A FLAGS response reports the complete set
    /// of flags that are applicable to the currently selected mailbox.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The FLAGS response is sent by the server as part of a
    /// <c>SELECT</c> or <c>EXAMINE</c> command. It contains a parenthesized
    /// list of all flags that may appear in messages within the mailbox.
    /// </para>
    /// <para>
    /// At minimum, the server must include the standard system-defined
    /// flags: <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
    /// <c>\Seen</c>, and <c>\Draft</c>. Servers may also include additional
    /// implementation-specific flags.
    /// </para>
    /// <para>
    /// The client MUST record the update from the FLAGS response, as it
    /// defines the complete set of legal message flags for the mailbox.
    /// </para>
    /// <para>
    /// Example server response:
    /// <c>* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)</c>
    /// </para>
    /// <para>
    /// This class stores the flags exactly as returned by the server.
    /// Interpretation and usage of the flags is left to the caller.
    /// </para>
    /// <para>
    /// This class derives from <see cref="IMAP_r_u"/>, the base type for
    /// all untagged IMAP responses.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Flags : IMAP_r_u
    {
        private string[] m_pFlags;

        /// <summary>
        /// Initializes a new <see cref="IMAP_r_u_Flags"/> instance using the
        /// flag list returned by the server in the IMAP <c>FLAGS</c> response.
        /// </summary>
        /// <param name="flags">
        /// The list of flags reported by the server. This array contains all
        /// flags applicable to the selected mailbox, including the standard
        /// system-defined flags (<c>\Answered</c>, <c>\Flagged</c>,
        /// <c>\Deleted</c>, <c>\Seen</c>, <c>\Draft</c>) and any additional
        /// implementation-specific flags the server may provide.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The FLAGS response is sent by the server as part of a
        /// <c>SELECT</c> or <c>EXAMINE</c> command. It defines the complete
        /// set of flags that may legally appear in future FETCH responses for
        /// the selected mailbox.
        /// </para>
        /// <para>
        /// If the server returns an empty flag list (e.g., <c>* FLAGS ()</c>),
        /// an empty array should be supplied.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Flags(string[] flags)
        {
            if(flags == null){
                throw new ArgumentNullException("flags");
            }

            m_pFlags = flags;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP <c>FLAGS</c> response and returns a corresponding
        /// <see cref="IMAP_r_u_Flags"/> instance.
        /// </summary>
        /// <param name="response">
        /// The raw untagged IMAP FLAGS response line, for example:
        /// <c>* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)</c>.
        /// The value must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// A populated <see cref="IMAP_r_u_Flags"/> object containing the
        /// flags returned by the server. If the FLAGS list is empty
        /// (e.g., <c>* FLAGS ()</c>), the returned object will contain an
        /// empty flag array.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not conform to the IMAP FLAGS
        /// response format defined in RFC 3501 section 7.2.6.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The FLAGS response is sent by the server as part of a
        /// <c>SELECT</c> or <c>EXAMINE</c> command. It contains a parenthesized
        /// list of all flags that may appear in messages within the selected
        /// mailbox.
        /// </para>
        /// <para>
        /// At minimum, the server must include the standard system-defined
        /// flags: <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// <c>\Seen</c>, and <c>\Draft</c>. Servers may also include additional
        /// implementation-specific flags.
        /// </para>
        /// <para>
        /// The client MUST record the update from the FLAGS response, as it
        /// defines the complete set of flags that may legally appear in future
        /// FETCH responses for the selected mailbox.
        /// </para>
        /// <para>
        /// Example server response:
        /// <c>* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)</c>
        /// </para>
        /// </remarks>
        public static IMAP_r_u_Flags Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException("response");
            }

            /*
                RFC 3501 7.2.6. FLAGS Response.
                -------------------------------
                The FLAGS response occurs as a result of a SELECT or EXAMINE
                command. The parenthesized list identifies the flags applicable
                to the mailbox. At minimum, the system-defined flags must be
                included, but servers may also include additional implementation-
                specific flags.

                The client MUST record the update from the FLAGS response.

                Example:
                    S: * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)
            */

            StringReader r = new StringReader(response);
            
            // "*"
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP FLAGS response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP FLAGS response (expected '*'): {response}");
            }

            // "FLAGS"
            string? word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP FLAGS response (missing FLAGS): {response}");
            }            
            if(!string.Equals(word,"FLAGS",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP FLAGS response (expected 'FLAGS'): {response}");
            }

            string[] flags = r.ReadParenthesized().Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);

            return new IMAP_r_u_Flags(flags);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Converts this IMAP <c>FLAGS</c> response to its wire‑format string
        /// representation.
        /// </summary>
        /// <returns>
        /// A correctly formatted IMAP FLAGS response line ending with CRLF,
        /// for example:
        /// <c>* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)\r\n</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The FLAGS response is sent by the server as part of a
        /// <c>SELECT</c> or <c>EXAMINE</c> command. It reports the complete
        /// set of flags that may appear in messages within the selected
        /// mailbox.
        /// </para>
        /// <para>
        /// This method serializes the stored flags into the exact IMAP
        /// wire‑format defined in RFC 3501 section 7.2.6:
        /// <c>* FLAGS (&lt;flag-list&gt;)\r\n</c>.
        /// </para>
        /// <para>
        /// If the flag list is empty, the generated string will be:
        /// <c>* FLAGS ()\r\n</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            // Example:    S: * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)

            return $"* FLAGS ({string.Join(" ", m_pFlags)})\r\n";
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the list of flags returned by the server in the IMAP
        /// <c>FLAGS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The FLAGS response is sent by the server as part of a
        /// <c>SELECT</c> or <c>EXAMINE</c> command and contains a parenthesized
        /// list of all flags that may appear in messages within the selected
        /// mailbox.
        /// </para>
        /// <para>
        /// At minimum, the server must include the standard system-defined
        /// flags: <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// <c>\Seen</c>, and <c>\Draft</c>. Servers may also include additional
        /// implementation-specific flags.
        /// </para>
        /// <para>
        /// This property exposes the flags exactly as returned by the server.
        /// The client MUST record these flags, as they define the complete set
        /// of legal message flags for the mailbox.
        /// </para>
        /// <para>
        /// If the server returns an empty flag list (e.g., <c>* FLAGS ()</c>),
        /// this property will contain an empty array.
        /// </para>
        /// </remarks>
        public string[] Flags
        {
            get{ return m_pFlags; }
        }

        #endregion
    }
}
