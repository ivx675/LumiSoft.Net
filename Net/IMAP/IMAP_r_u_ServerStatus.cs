using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP status response (<c>* OK</c>, <c>* NO</c>, <c>* BAD</c>,
    /// <c>* PREAUTH</c>, <c>* BYE</c>) as defined in RFC 3501 section 7.1.
    /// 
    /// Untagged status responses are sent by the server outside the context of a specific
    /// client command and always begin with the asterisk (<c>*</c>) tag. They may include
    /// an optional response code enclosed in square brackets (e.g. <c>[UIDNEXT 123]</c>),
    /// followed by free-form human‑readable text. Status responses never contain IMAP
    /// literals; any braces or brackets appearing in the response text are treated as
    /// plain characters.
    /// </summary>
    public class IMAP_r_u_ServerStatus : IMAP_r_u
    {
        private string      m_ResponseCode      = "";
        private IMAP_t_orc? m_pOptionalResponse = null;
        private string      m_ResponseText      = "";

        /// <summary>
        /// Initializes a new untagged IMAP status response with the specified response code
        /// and response text. This overload creates a status response without an optional
        /// response code, corresponding to lines such as <c>* OK Completed</c>.
        /// </summary>
        /// <param name="responseCode">
        /// The IMAP status response code (<c>OK</c>, <c>NO</c>, <c>BAD</c>, <c>PREAUTH</c>, or <c>BYE</c>).
        /// </param>
        /// <param name="responseText">
        /// The free-form human-readable text that follows the status code. Status responses
        /// never contain IMAP literals; any braces or brackets appearing in the text are
        /// treated as plain characters.
        /// </param>
        public IMAP_r_u_ServerStatus(string responseCode,string responseText) : this(responseCode,null,responseText)
        {
        }

        /// <summary>
        /// Initializes a new untagged IMAP status response with the specified response code,
        /// optional response code, and response text. Untagged status responses always begin
        /// with the asterisk (<c>*</c>) tag and correspond to server‑generated status lines
        /// such as <c>* OK</c>, <c>* NO</c>, <c>* BAD</c>, <c>* PREAUTH</c>, or <c>* BYE</c>.
        /// 
        /// If present, the optional response code is the bracketed element defined in
        /// RFC 3501 section 7.1.2 (for example <c>[UIDNEXT 123]</c>). Only one optional
        /// response code may appear in a status response. The remaining text is free‑form
        /// human‑readable response text. Status responses never contain IMAP literals; any
        /// braces or brackets appearing in the text are treated as plain characters.
        /// </summary>
        /// <param name="responseCode">
        /// The IMAP status response code (<c>OK</c>, <c>NO</c>, <c>BAD</c>, <c>PREAUTH</c>,
        /// or <c>BYE</c>).
        /// </param>
        /// <param name="optionalResponse">
        /// The optional bracketed response code (e.g. <c>[UIDNEXT 123]</c>), or <c>null</c>
        /// if no optional response code is present.
        /// </param>
        /// <param name="responseText">
        /// The free‑form text that follows the status code. This text never contains IMAP
        /// literals and may be empty.
        /// </param>
        public IMAP_r_u_ServerStatus(string responseCode,IMAP_t_orc? optionalResponse,string responseText)
        {
            if(responseCode == null){
                throw new ArgumentNullException("responseCode");
            }
            if(responseCode == string.Empty){
                throw new ArgumentException("The argument 'responseCode' value must be specified.","responseCode");
            }

            m_ResponseCode      = responseCode;
            m_pOptionalResponse = optionalResponse;
            m_ResponseText      = responseText;
        }


        #region static method Parse

        /// <summary>
        /// Parses an untagged IMAP status response from a single response line.
        /// Untagged status responses begin with the asterisk (<c>*</c>) tag and use the
        /// same syntax as tagged status responses, except that the tag is always fixed.
        /// 
        /// This method parses:
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///       The mandatory untagged response marker (<c>*</c>).
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       The mandatory IMAP status response code (<c>OK</c>, <c>NO</c>, <c>BAD</c>,
        ///       <c>PREAUTH</c>, or <c>BYE</c>), as defined in RFC 3501 section 7.1.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       An optional response code enclosed in square brackets (e.g. <c>[UIDNEXT 123]</c>),
        ///       as defined in RFC 3501 section 7.1.2. Only one optional response code may appear.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       The free-form response text that follows the status code. Status responses never
        ///       contain IMAP literals; any braces or brackets appearing in the text are treated
        ///       as plain characters.
        ///     </description>
        ///   </item>
        /// </list>
        /// </summary>
        /// <param name="responseLine">The raw IMAP response line received from the server.</param>
        /// <returns>
        /// An <see cref="IMAP_r_u_ServerStatus"/> instance representing the parsed untagged
        /// IMAP status response.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="responseLine"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response line violates IMAP syntax, such as missing the untagged
        /// marker (<c>*</c>), missing a response code, or containing a malformed optional
        /// response code.
        /// </exception>
        public static IMAP_r_u_ServerStatus Parse(string responseLine)
        {
            if(responseLine == null){
                throw new ArgumentNullException("responseLine");
            }

            StringReader r = new StringReader(responseLine);

            // Command tag
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP response (missing *): {responseLine}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP response (expected '*'): {responseLine}");
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

            return new IMAP_r_u_ServerStatus(responseCode,optResponse,responseText);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire-format representation of this untagged status response.
        /// The returned string always begins with the untagged marker (<c>*</c>) followed
        /// by the status response code (<c>OK</c>, <c>NO</c>, <c>BAD</c>, <c>PREAUTH</c>,
        /// or <c>BYE</c>). If present, the optional response code is emitted inside square
        /// brackets (e.g. <c>[UIDNEXT 123]</c>) exactly as defined in RFC 3501 section 7.1.2.
        /// The remainder of the line is the free-form response text, followed by CRLF.
        /// </summary>
        /// <returns>
        /// A CRLF-terminated IMAP status response line suitable for transmission to an IMAP
        /// client. Status responses never contain IMAP literals; any braces or brackets in
        /// the response text are emitted verbatim.
        /// </returns>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append("* " + m_ResponseCode + " ");            
            if(m_pOptionalResponse != null){
                retVal.Append("[" + m_pOptionalResponse.ToString() + "] ");
            }
            retVal.Append(m_ResponseText + "\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP status response code for this untagged status response.
        /// Valid status codes are <c>OK</c>, <c>NO</c>, <c>BAD</c>, <c>PREAUTH</c>, and
        /// <c>BYE</c>, as defined in RFC 3501 section 7.1. The response code indicates
        /// the server's status condition and precedes any optional response code or
        /// free‑form response text.
        /// </summary>
        public string ResponseCode
        {
            get{ return m_ResponseCode; }
        }

        /// <summary>
        /// Gets the optional IMAP response code associated with this untagged status
        /// response. Optional response codes are the bracketed elements defined in
        /// RFC 3501 section 7.1.2 (for example <c>[UIDNEXT 123]</c>). Only one optional
        /// response code may appear in a status response. If no optional response code
        /// is present, this property returns <c>null</c>.
        /// </summary>
        public IMAP_t_orc? OptionalResponse
        {
            get{ return m_pOptionalResponse; }
        }
                
        /// <summary>
        /// Gets the free‑form human‑readable text associated with this untagged IMAP
        /// status response. The response text follows the status response code and any
        /// optional response code, and may be empty. As defined in RFC 3501 section 7.1,
        /// status responses never contain IMAP literals; any braces, brackets, or other
        /// characters appearing in this text are treated as plain text.
        /// </summary>
        public string ResponseText
        {
            get{ return m_ResponseText; }
        }

        /// <summary>
        /// Gets a value indicating whether this untagged IMAP status response represents
        /// an error condition. According to RFC 3501 section 7.1, the status code <c>OK</c>
        /// indicates successful completion or a positive server condition, while all other
        /// status codes (<c>NO</c>, <c>BAD</c>, <c>PREAUTH</c>, <c>BYE</c>) represent error,
        /// failure, or exceptional conditions. This property returns <c>true</c> for any
        /// status code other than <c>OK</c>.
        /// </summary>
        public bool IsError
        {
            get{ return !m_ResponseCode.Equals("OK",StringComparison.OrdinalIgnoreCase); }
        }

        #endregion
    }
}
