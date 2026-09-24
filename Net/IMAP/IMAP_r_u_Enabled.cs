using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP untagged <c>ENABLED</c> response as defined in
    /// RFC 5161 (IMAP ENABLE Extension). An <c>ENABLED</c> response
    /// reports the set of optional IMAP capabilities that the server has
    /// successfully activated for the current session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After a client issues an <c>ENABLE</c> command, the server may return
    /// an untagged <c>ENABLED</c> response listing the capabilities that are
    /// now active. The general response format is:
    /// </para>
    /// <code>
    /// * ENABLED &lt;capability1&gt; [&lt;capability2&gt; ...]
    /// </code>
    /// </remarks>
    public class IMAP_r_u_Enabled : IMAP_r_u
    {
        private string[] m_Capabilities;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Enabled"/> class
        /// using the specified list of capability names reported in an IMAP
        /// <c>ENABLED</c> response.
        /// </summary>
        /// <param name="capabilities">
        /// The collection of capability names that the server indicates are
        /// enabled for the current session. The array must not be <c>null</c>.
        /// Capability names are preserved exactly as parsed, including any
        /// vendor‑specific or non‑standard tokens.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="capabilities"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>ENABLED</c> response is defined in RFC 5161 (IMAP ENABLE
        /// Extension). After a client issues an <c>ENABLE</c> command, the server
        /// may return an untagged <c>ENABLED</c> response listing the capabilities
        /// that have been successfully activated for the current session.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Enabled(string[] capabilities)
        {
            if(capabilities == null){
                throw new ArgumentNullException("capabilities");
            }

            m_Capabilities = capabilities;
        }
        

        #region static method Parse

        /// <summary>
        /// Parses an IMAP untagged <c>ENABLED</c> response as defined in
        /// RFC 5161 and returns an <see cref="IMAP_r_u_Enabled"/> instance
        /// containing the capability names reported by the server.
        /// </summary>
        /// <param name="response">
        /// The raw IMAP server response line beginning with
        /// <c>* ENABLED</c>. The value must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// A <see cref="IMAP_r_u_Enabled"/> object containing the list of
        /// capabilities that the server indicates are enabled for the current
        /// session.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not contain the required leading
        /// <c>*</c> token or the <c>ENABLED</c> keyword.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>ENABLED</c> response is part of the IMAP ENABLE extension
        /// defined in RFC 5161. It reports which optional IMAP capabilities
        /// have been successfully activated for the current session.
        /// </para>
        /// <para>
        /// The general response format is:
        /// </para>
        /// <code>
        /// * ENABLED &lt;capability1&gt; [&lt;capability2&gt; ...]
        /// </code>
        /// <para>
        /// The ENABLED response is informational: malformed capability tokens do
        /// not invalidate the IMAP session. This parser therefore favors tolerant
        /// handling of non‑critical formatting issues.
        /// </para>
        /// </remarks>
        public static IMAP_r_u_Enabled Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            /*
            RFC 5161 — ENABLE Extension
            ---------------------------

            The ENABLE command allows a client to activate optional IMAP extensions
            on a per‑session basis. When the server processes an ENABLE command, it
            returns an untagged ENABLED response listing the capabilities that are
            now active for the current connection.

            Response format:

                * ENABLED <capability1> [<capability2> ...]

            Field definitions:

                <capability>
                    An IMAP atom naming a server capability that has been successfully
                    enabled. Capabilities are returned exactly as the server reports
                    them. Unknown or vendor‑specific capability names must be preserved
                    for forward compatibility.

            Meaning:
                The ENABLED response informs the client which extensions are active
                for the session. It does not imply that the server supports all
                capabilities listed in its initial CAPABILITY response; only those
                explicitly enabled are reported here.

            Example:

                C: A01 ENABLE UTF8=ACCEPT
                S: * ENABLED UTF8=ACCEPT
                S: A01 OK ENABLE completed

            Notes:
                - ENABLED responses are always untagged.
                - The server may return zero or more capabilities.
                - ENABLED responses never contain literals; all fields are atoms.
                - ENABLED capabilities are session‑scoped and may differ from the
                  server’s global CAPABILITY list.
                - Clients typically use ENABLE to activate extensions such as
                  UTF8=ACCEPT or CONDSTORE without reissuing CAPABILITY.
            */


            StringReader r = new StringReader(response);
            
            // "*"
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP ENABLED response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP ENABLED response (expected '*'): {response}");
            }

            // "ENABLED"
            string? word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP ENABLED response (missing ENABLED): {response}");
            }            
            if(!string.Equals(word,"ENABLED",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP ENABLED response (expected 'ENABLED'): {response}");
            }

            string[] capabilities = r.ReadToEnd()?.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries) ?? [];

            return new IMAP_r_u_Enabled(capabilities);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire‑format representation of this
        /// <c>ENABLED</c> response using the capability names stored in
        /// <see cref="Capabilities"/>.
        /// </summary>
        /// <returns>
        /// A string containing the serialized untagged <c>ENABLED</c> response,
        /// terminated with CRLF, suitable for transmission to an IMAP client
        /// or for logging.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>ENABLED</c> response is defined in RFC 5161 (IMAP ENABLE
        /// Extension). It reports which optional IMAP capabilities the server
        /// has successfully activated for the current session.
        /// </para>
        /// <para>
        /// The serialized form follows the required syntax:
        /// </para>
        /// <code>
        /// * ENABLED &lt;capability1&gt; [&lt;capability2&gt; ...]\r\n
        /// </code>
        /// <para>
        /// If no capabilities were enabled, the response consists only of the
        /// <c>* ENABLED</c> prefix followed by CRLF. The ENABLED response is
        /// informational and does not affect the session state.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            // Example: S: * ENABLED X-GOOD-IDEA

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* ENABLED");
            foreach(string capability in m_Capabilities){
                retVal.Append(" " + capability);
            }
            retVal.Append("\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the list of capability names reported by the server in this
        /// <c>ENABLED</c> response. Each entry identifies an IMAP extension that
        /// the server has successfully activated for the current session.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>ENABLED</c> response is defined in RFC 5161 (IMAP ENABLE
        /// Extension). After the client issues an <c>ENABLE</c> command, the
        /// server may return an untagged <c>ENABLED</c> response listing the
        /// capabilities that are now active.
        /// </para>
        /// <para>
        /// The general response format is:
        /// </para>
        /// <code>
        /// * ENABLED &lt;capability1&gt; [&lt;capability2&gt; ...]
        /// </code>
        /// </remarks>
        public string[] Capabilities
        {
            get{ return m_Capabilities; }
        }

        #endregion
    }
}
