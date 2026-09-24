using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP <c>CAPABILITY</c> response as defined in
    /// RFC 3501 section 7.2.1. A CAPABILITY response is sent by the server to
    /// announce the set of protocol features, extensions, authentication
    /// mechanisms, and server-specific tokens that it supports.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CAPABILITY responses are always untagged and begin with the asterisk
    /// (<c>*</c>) response tag followed by the atom <c>CAPABILITY</c>. The
    /// remainder of the line consists of zero or more capability atoms
    /// separated by whitespace. Each capability is a case-insensitive atom
    /// identifying a protocol feature such as <c>IMAP4rev1</c>, <c>STARTTLS</c>,
    /// <c>LITERAL+</c>, <c>SASL-IR</c>, or <c>AUTH=PLAIN</c>.
    /// </para>
    /// <para>
    /// According to RFC 3501, CAPABILITY responses never contain IMAP literals,
    /// quoted strings, or multi-line data. All capability information appears
    /// on a single line. This class stores the capability atoms exactly as
    /// reported by the server without modification.
    /// </para>
    /// <para>
    /// CAPABILITY responses may appear in the server greeting, in response to
    /// a client-issued <c>CAPABILITY</c> command, or at other times when the
    /// server wishes to inform the client of its supported features.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Capability : IMAP_r_u
    {
        private string[] m_pCapabilities;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Capability"/> class
        /// using the capability atoms supplied by the IMAP server. Each element in
        /// <paramref name="capabilities"/> represents a single capability token as
        /// defined in RFC 3501 section 7.2.1.
        /// </summary>
        /// <param name="capabilities">
        /// The array of capability atoms parsed from the server's CAPABILITY
        /// response. Each entry must be a valid IMAP atom and must not contain
        /// literals, quoted strings, or multi-line data.
        /// </param>
        /// <remarks>
        /// <para>
        /// The IMAP <c>CAPABILITY</c> response is an untagged server response that
        /// advertises the set of protocol features, extensions, authentication
        /// mechanisms, and server-specific tokens supported by the server. Each
        /// capability is a case-insensitive atom and appears on a single line
        /// following the <c>* CAPABILITY</c> prefix.
        /// </para>
        /// <para>
        /// This constructor stores the capability atoms exactly as provided by the
        /// parser. No normalization, filtering, or interpretation is performed.
        /// Clients may inspect the capability list to determine server features such
        /// as <c>IMAP4rev1</c>, <c>STARTTLS</c>, <c>LITERAL+</c>, <c>SASL-IR</c>,
        /// <c>AUTH=PLAIN</c>, <c>IDLE</c>, <c>UIDPLUS</c>, and other extensions.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Capability(string[] capabilities)
        {
            if(capabilities == null){
                throw new ArgumentNullException("capabilities");
            }

            m_pCapabilities = capabilities;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP <c>CAPABILITY</c> response line into an
        /// <see cref="IMAP_r_u_Capability"/> instance. According to RFC 3501
        /// section 7.2.1, a CAPABILITY response is an untagged server response
        /// beginning with the asterisk (<c>*</c>) tag followed by the atom
        /// <c>CAPABILITY</c> and zero or more capability atoms.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The CAPABILITY response lists the protocol features, extensions,
        /// authentication mechanisms, and server-specific tokens supported by
        /// the IMAP server. Each capability is a case-insensitive atom. The
        /// response never contains IMAP literals, quoted strings, or multi-line
        /// data; all capabilities appear on a single line separated by one or
        /// more whitespace characters.
        /// </para>
        /// <para>
        /// This method validates the untagged response marker (<c>*</c>), the
        /// required <c>CAPABILITY</c> keyword, and then splits the remainder of
        /// the line into individual capability atoms. Empty or whitespace-only
        /// segments are removed. A <see cref="ParseException"/> is thrown if the
        /// response does not conform to the required syntax.
        /// </para>
        /// </remarks>
        /// <param name="response">
        /// The raw IMAP response line beginning with <c>* CAPABILITY</c>.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_r_u_Capability"/> containing the parsed
        /// capability atoms.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not begin with <c>* CAPABILITY</c>
        /// or otherwise violates the CAPABILITY response grammar.
        /// </exception>
        public static IMAP_r_u_Capability Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException("response");
            }

            /* RFC 3501 section 7.2.1 — CAPABILITY Response

               The CAPABILITY response is an untagged server response that lists the
               set of commands, extensions, and protocol features supported by the
               IMAP server. It consists solely of atoms and never includes IMAP
               literals, quoted strings, or multi-line data.

               Capability-Response = "* CAPABILITY" *(SP capability) CRLF

               capability = atom
                            ; Case-insensitive token identifying a protocol feature.
                            ; Literals ("{size}") are not permitted in CAPABILITY.
                            ; Quoted strings are not permitted.
                            ; Each capability is a single atom.

               Examples:
                 * CAPABILITY IMAP4rev1 LITERAL+ SASL-IR STARTTLS LOGINDISABLED
                 * CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=CRAM-MD5 IDLE UIDPLUS
                 * CAPABILITY IMAP4rev1 MOVE ESEARCH UNSELECT
            */

            StringReader r = new StringReader(response);

            // Command tag
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP response (expected '*'): {response}");
            }

            // CAPABILITY.
            string? word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP response (missing CAPABILITY): {response}");
            }            
            if(!string.Equals(word,"CAPABILITY",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP response (expected 'CAPABILITY'): {response}");
            }

            string   rest = r.ReadToEnd() ?? string.Empty;
            string[] capabilities = rest.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);

            return new IMAP_r_u_Capability(capabilities);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire-format representation of this untagged
        /// <c>CAPABILITY</c> response. According to RFC 3501 section 7.2.1,
        /// a CAPABILITY response begins with the asterisk (<c>*</c>) tag
        /// followed by the atom <c>CAPABILITY</c> and zero or more capability
        /// atoms separated by single spaces.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each capability atom identifies a protocol feature, extension,
        /// authentication mechanism, or server-specific token supported by
        /// the IMAP server. The response is always a single line and never
        /// contains IMAP literals, quoted strings, or multi-line data.
        /// </para>
        /// <para>
        /// This method emits the capability list exactly as stored in this
        /// instance, preserving the order and case of the capability atoms.
        /// The returned string is terminated with CRLF and is suitable for
        /// transmission to an IMAP client.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A CRLF-terminated IMAP <c>* CAPABILITY</c> response line containing
        /// all capability atoms reported by the server.
        /// </returns>
        public override string ToString()
        {
            // Example:    S: * CAPABILITY IMAP4rev1 STARTTLS AUTH=GSSAPI XPIG-LATIN

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* CAPABILITY");
            foreach(string capability in m_pCapabilities){
                retVal.Append(" " + capability);
            }
            retVal.Append("\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties impelementation

        /// <summary>
        /// Gets the list of capability atoms reported by the IMAP server in its
        /// untagged <c>CAPABILITY</c> response as defined in RFC 3501 section 7.2.1.
        /// Each element represents a single capability token such as
        /// <c>IMAP4rev1</c>, <c>STARTTLS</c>, <c>LITERAL+</c>, <c>SASL-IR</c>,
        /// <c>AUTH=PLAIN</c>, or other server-supported extensions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Capability atoms are case-insensitive IMAP tokens that identify protocol
        /// features, authentication mechanisms, and server extensions. CAPABILITY
        /// responses never contain literals, quoted strings, or multi-line data.
        /// </para>
        /// <para>
        /// The returned array reflects the capability list exactly as provided by
        /// the server, preserving both order and original casing.
        /// </para>
        /// </remarks>
        /// <returns>
        /// An array of capability atoms advertised by the server.
        /// </returns>
        public string[] Capabilities
        {
            get{ return m_pCapabilities; }
        }

        #endregion
    }
}
