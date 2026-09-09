using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace LumiSoft.Net.POP3 
{
    /// <summary>
    /// Represents a single POP3 server response line as defined in RFC 1939 and RFC 2449.
    /// A POP3 response line always begins with a status indicator (<c>+OK</c> or <c>-ERR</c>),
    /// optionally followed by a POP3 response code (RESP-CODES extension), and then
    /// human‑readable text supplied by the server.
    /// </summary>
    public class POP3_ServerResponse 
    {
        private string  m_Status       = "";
        private string? m_ResponseCode = null;
        private string  m_Text         = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="POP3_ServerResponse"/> class.
        /// </summary>
        /// <param name="status">
        /// The POP3 status indicator returned by the server (<c>+OK</c> or <c>-ERR</c>).
        /// </param>
        /// <param name="responseCode">
        /// Optional POP3 response code (RFC 2449 RESP-CODES), such as <c>AUTH</c>,
        /// <c>IN-USE</c>, or <c>SYS-PERM</c>. May be <c>null</c> if no code was present.
        /// </param>
        /// <param name="text">
        /// The human‑readable text portion of the response line, excluding any RESP-CODE.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="status"/> is <c>null</c> or empty.
        /// </exception>
        public POP3_ServerResponse(string status,string? responseCode,string text)
        {
            if(string.IsNullOrEmpty(status)){
                throw new ArgumentException("Argument 'status' value cannot be null or mepty.",nameof(status));
            }

            m_Status       = status;
            m_ResponseCode = responseCode;
            m_Text         = text;
        }


        /// <summary>
        /// Parses a POP3 response line into its component parts: status indicator,
        /// optional RESP-CODE, and text.  
        /// The expected format is:
        /// <code>
        /// +OK [RESP-CODE] text
        /// -ERR [RESP-CODE] text
        /// </code>
        /// RESP-CODES are defined in RFC 2449 and appear in square brackets immediately
        /// after the status indicator.
        /// </summary>
        /// <param name="responseLine">
        /// The raw POP3 response line received from the server.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the parsed components.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="responseLine"/> is <c>null</c>, empty, or whitespace.
        /// </exception>
        public static POP3_ServerResponse Parse(string responseLine)
        {
            if(string.IsNullOrWhiteSpace(responseLine)){
                throw new ArgumentException("Argument 'status' value cannot be null or mepty.",nameof(responseLine));
            }

            // Response line: +OK [RESP-CODE] text

            string  status       = "";
            string? responseCode = null;
            string  text         = "";

            string[] status_text = responseLine.Split(' ',2);
            status = status_text[0];
            if(status_text.Length > 1){
                text = status_text[1];

                if(text.StartsWith('[')){
                    string[] respCode_text = text.Split(' ',2);
                    if(respCode_text[0].EndsWith(']')){
                        responseCode = respCode_text[0].Substring(1,respCode_text[0].Length - 2);
                        text         = respCode_text.Length > 1 ? respCode_text[1] : "";
                    }
                }
            }

            return new POP3_ServerResponse(status,responseCode,text.Trim());
        }


        #region Properties implementation

        /// <summary>
        /// Gets the POP3 status indicator (<c>+OK</c> or <c>-ERR</c>).
        /// </summary>
        public string Status 
        { 
            get { return m_Status; } 
        }

        /// <summary>
        /// Gets the optional POP3 response code (RESP-CODES), or <c>null</c> if none was present.
        /// </summary>
        public string? ResponseCode 
        { 
            get { return m_ResponseCode; } 
        }

        /// <summary>
        /// Gets the human‑readable text portion of the response line.
        /// </summary>
        public string Text 
        { 
            get { return m_Text; } 
        }

        /// <summary>
        /// Gets whether the response indicates success (<c>+OK</c>).
        /// </summary>
        public bool IsSuccess
        {
            get { return string.Equals(m_Status,"+OK",StringComparison.OrdinalIgnoreCase); }
        }

        #endregion
    }
}
