using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP <b>command completion response</b> or a 
    /// <b>continuation request</b> as defined in RFC 3501 section 7.1 and 7.5.
    ///
    /// This response type covers:
    /// <list type="bullet">
    ///   <item><description><b>OK</b> — successful command completion</description></item>
    ///   <item><description><b>NO</b> — command completed with failure</description></item>
    ///   <item><description><b>BAD</b> — command rejected due to syntax or protocol error</description></item>
    ///   <item><description><b>+</b> — continuation request (server expects more data)</description></item>
    /// </list>
    ///
    /// Command completion responses are always <b>tagged</b> (except for the 
    /// continuation request) and never contain IMAP literals. Optional response 
    /// codes (e.g. <c>[UIDNEXT 123]</c>) may appear after the status code.
    /// </summary>
    public class IMAP_r_ServerStatus : IMAP_r
    {
        private string      m_CommandTag        = "";
        private string      m_ResponseCode      = "";
        private IMAP_t_orc? m_pOptionalResponse = null;
        private string      m_ResponseText      = "";

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="commandTag">Command tag.</param>
        /// <param name="responseCode">Response code.</param>
        /// <param name="responseText">Response text after response-code.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>commandTag</b>,<b>responseCode</b> or <b>responseText</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public IMAP_r_ServerStatus(string commandTag,string responseCode,string responseText) : this(commandTag,responseCode,null,responseText)
        {
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="commandTag">Command tag.</param>
        /// <param name="responseCode">Response code.</param>
        /// <param name="optionalResponse">Optional response. Value null means not specified.</param>
        /// <param name="responseText">Response text after response-code.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>commandTag</b>,<b>responseCode</b> or <b>responseText</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public IMAP_r_ServerStatus(string commandTag,string responseCode,IMAP_t_orc? optionalResponse,string responseText)
        {
            if(commandTag == null){
                throw new ArgumentNullException(nameof(commandTag));
            }
            if(commandTag == string.Empty){
                throw new ArgumentException("The argument 'commandTag' value must be specified.",nameof(commandTag));
            }
            if(responseCode == null){
                throw new ArgumentNullException(nameof(responseCode));
            }
            if(responseCode == string.Empty){
                throw new ArgumentException("The argument 'responseCode' value must be specified.",nameof(responseCode));
            }

            m_CommandTag        = commandTag;
            m_ResponseCode      = responseCode;
            m_pOptionalResponse = optionalResponse;
            m_ResponseText      = responseText;
        }

        /// <summary>
        /// Default cmdTag-less constructor.
        /// </summary>
        /// <param name="responseCode">Response code.</param>
        /// <param name="responseText">Response text after response-code.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>responseCode</b> or <b>responseText</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        internal IMAP_r_ServerStatus(string responseCode,string responseText)
        {
            m_ResponseCode = responseCode;
            m_ResponseText = responseText;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP server status response from a single response line.
        /// This method handles both:
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///       <b>Continuation requests</b> (the "+" response), as defined in RFC 3501 section 7.5.
        ///       These responses are untagged and indicate that the server expects additional data
        ///       from the client, typically literal content.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       <b>Command completion responses</b> (OK, NO, BAD, PREAUTH, BYE), as defined in
        ///       RFC 3501 section 7.1. These responses are tagged and may contain at most one
        ///       optional response code enclosed in square brackets (e.g. <c>[UIDNEXT 123]</c>).
        ///     </description>
        ///   </item>
        /// </list>
        /// The method does not process IMAP literals; status responses never contain literal data.
        /// </summary>
        /// <param name="responseLine">The raw IMAP response line received from the server.</param>
        /// <returns>
        /// An <see cref="IMAP_r_ServerStatus"/> instance representing the parsed IMAP response.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="responseLine"/> is null.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response line violates IMAP syntax, such as missing a command tag,
        /// missing a response code, or containing a malformed optional response code.
        /// </exception>
        public static IMAP_r_ServerStatus Parse(string responseLine)
        {
            if(responseLine == null){
                throw new ArgumentNullException("responseLine");
            }

            // We have continuation "+" response.
            if(responseLine.StartsWith("+")){
                string[] parts        = responseLine.Split(new char[]{' '},2);
                string   responseText = parts.Length == 2 ? parts[1] : "";

                return new IMAP_r_ServerStatus("+","+",responseText);
            }
            // cmdTag OK/BAD/NO [optional-reponse] text
            else{
                StringReader r = new StringReader(responseLine);

                // Command tag
                string? commandTag = r.ReadWord();
                if(commandTag == null){
                    throw new ParseException($"Invalid IMAP response (missing command tag): {responseLine}");
                }

                // Response code.
                string? responseCode = r.ReadWord();
                if(responseCode == null){
                    throw new ParseException($"Invalid IMAP response (missing response code): {responseLine}");
                }                

                // Optional status code.
                r.ReadToFirstChar();
                IMAP_t_orc? optResponse = null;
                if(r.StartsWith("[")){
                    optResponse = IMAP_t_orc.Parse(r);
                }

                string responseText = r.ReadToEnd()?.Trim() ?? "";

                return new IMAP_r_ServerStatus(commandTag,responseCode,optResponse,responseText);
            }
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire-format representation of this tagged status response.
        /// Tagged status responses begin with the client-supplied command tag (e.g.
        /// <c>A001</c>) followed by the status response code (<c>OK</c>, <c>NO</c>,
        /// <c>BAD</c>, <c>PREAUTH</c>, or <c>BYE</c>) as defined in RFC 3501 section 7.1.
        /// 
        /// If present, the optional response code is emitted inside square brackets
        /// (for example <c>[UIDNEXT 123]</c>) exactly as specified in RFC 3501 section
        /// 7.1.2. The remainder of the line is free-form human-readable response text.
        /// The returned string is terminated with CRLF and contains no IMAP literals,
        /// since status responses never include literal data.
        /// </summary>
        /// <returns>
        /// A CRLF-terminated IMAP status response line suitable for transmission to an
        /// IMAP client. Any braces or brackets appearing in the response text are emitted
        /// verbatim.
        /// </returns>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            if(!string.IsNullOrEmpty(m_CommandTag)){
                retVal.Append(m_CommandTag + " ");
            }
            retVal.Append(m_ResponseCode + " ");
            if(m_pOptionalResponse != null){
                retVal.Append("[" + m_pOptionalResponse.ToString() + "] ");
            }
            retVal.Append(m_ResponseText + "\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP command tag associated with this response.
        /// Empty for continuation requests.
        /// </summary>
        public string CommandTag
        {
            get{ return m_CommandTag; }
        }
                
        /// <summary>
        /// Gets the IMAP status code (OK, NO, BAD, or "+").
        /// </summary>
        public string ResponseCode
        {
            get{ return m_ResponseCode; }
        }

        /// <summary>
        /// Gets the optional IMAP response code (e.g. UIDNEXT, UIDVALIDITY).
        /// Null if no optional response code is present.
        /// </summary>
        public IMAP_t_orc? OptionalResponse
        {
            get{ return m_pOptionalResponse; }
        }
                
        /// <summary>
        /// Gets the human‑readable text following the status code.
        /// </summary>
        public string ResponseText
        {
            get{ return m_ResponseText; }
        }

        /// <summary>
        /// Gets a value indicating whether the server's tagged completion response
        /// represents successful command execution. In IMAP, a completion response
        /// is considered successful only when the server returns the atom
        /// <c>OK</c> as defined in RFC 3501 section 7.1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// As a result, the <c>IsSuccess</c> property simply reflects whether the
        /// server returned <c>OK</c> for the command. The <c>NO</c> and <c>BAD</c>
        /// atoms indicate command failure or protocol errors, respectively.
        /// </para>
        /// </remarks>
        /// <returns>
        /// <c>true</c> if the server returned <c>OK</c>; otherwise <c>false</c>.
        /// </returns>
        public bool IsSuccess
        {
            get{ 
                if(m_ResponseCode.Equals("OK",StringComparison.OrdinalIgnoreCase)){
                    return true;
                }
                else{
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets whether this response represents an error (NO or BAD).
        /// </summary>
        public bool IsError
        {
            get{ 
                if(m_ResponseCode.Equals("NO",StringComparison.OrdinalIgnoreCase)){
                    return true;
                }
                else if(m_ResponseCode.Equals("BAD",StringComparison.OrdinalIgnoreCase)){
                    return true;
                }
                else{
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets whether this response is a continuation request ("+").
        /// </summary>
        public bool IsContinue
        {
            get{ return m_ResponseCode.Equals("+",StringComparison.OrdinalIgnoreCase); }
        }

        #endregion
    }
}
