using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP
{
    /// <summary>
    /// Represents a single SMTP reply line as defined in RFC 5321 section 4.2.
    /// A reply line consists of a 3‑digit reply code, an optional enhanced status
    /// code (RFC 3463 / RFC 5248), textual diagnostic information, and an indicator
    /// specifying whether the line is the final line of a multi‑line reply.
    /// </summary>
    public class SMTP_t_ReplyLine
    {
        private int                        m_ReplyCode           = 0;
        private SMTP_t_EnhancedStatusCode? m_pEnhancedStatusCode = null;
        private string                     m_Text                = "";
        private bool                       m_IsLastLine          = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_t_ReplyLine"/> class
        /// using the specified reply code, text, and line‑termination indicator.
        /// </summary>
        /// <param name="replyCode">The 3‑digit SMTP reply code.</param>
        /// <param name="text">The textual portion of the reply line.</param>
        /// <param name="isLastLine">
        /// <c>true</c> if this line is the final line of the reply; otherwise <c>false</c>.
        /// </param>
        public SMTP_t_ReplyLine(int replyCode,string text,bool isLastLine) : this(replyCode,null,text,isLastLine)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_t_ReplyLine"/> class
        /// using the specified reply code, enhanced status code, text, and line‑termination indicator.
        /// </summary>
        /// <param name="replyCode">The 3‑digit SMTP reply code.</param>
        /// <param name="enhancedStatusCode">
        /// Optional enhanced status code (RFC 3463 / RFC 5248). May be <c>null</c>.
        /// </param>
        /// <param name="text">The textual portion of the reply line.</param>
        /// <param name="isLastLine">
        /// <c>true</c> if this line is the final line of the reply; otherwise <c>false</c>.
        /// </param>

        public SMTP_t_ReplyLine(int replyCode,SMTP_t_EnhancedStatusCode? enhancedStatusCode,string text,bool isLastLine)
        {
            if(text == null){
                text = "";
            }

            m_ReplyCode           = replyCode;
            m_pEnhancedStatusCode = enhancedStatusCode;
            m_Text                = text;
            m_IsLastLine          = isLastLine;
        }


        #region static method Parse

        /// <summary>
        /// Parses a raw SMTP reply line into a <see cref="SMTP_t_ReplyLine"/> instance.
        /// </summary>
        /// <param name="line">The raw SMTP reply line received from the server.</param>
        /// <returns>A parsed <see cref="SMTP_t_ReplyLine"/> instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="line"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the reply line does not conform to RFC 5321 formatting rules.
        /// </exception>
        /// <remarks>
        /// <para>
        /// RFC 5321 reply lines follow one of two formats:
        /// </para>
        /// <code>
        /// Reply-code "-" [ textstring ] CRLF   (multi-line, continuation)
        /// Reply-code SP [ textstring ] CRLF    (final line)
        /// </code>
        /// <para>
        /// Enhanced status codes (RFC 3463 / RFC 5248) may appear at the beginning of
        /// the text portion of any line, typically in the final line of a multi‑line reply.
        /// </para>
        /// </remarks>
        public static SMTP_t_ReplyLine Parse(string line)
        {
            if(line == null){
                throw new ArgumentNullException("line");
            }

            /* RFC 5321 4.2.
                Reply-line     = *( Reply-code "-" [ textstring ] CRLF )
                                 Reply-code [ SP textstring ] CRLF
             
                Since, in violation of this specification, the text is sometimes not sent, clients that do not
                receive it SHOULD be prepared to process the code alone (with or without a trailing space character).
            */

            if(line.Length < 3){
                throw new ParseException("Invalid SMTP server reply-line '" + line + "'.");
            }

            int replyCode = 0;
            if(!int.TryParse(line.Substring(0,3),out replyCode)){
                throw new ParseException("Invalid SMTP server reply-line '" + line + "' reply-code.");
            }
            
            bool isLastLine = true;            
            if(line.Length > 3){
                isLastLine = (line[3] == ' ');
            }
            
            SMTP_t_EnhancedStatusCode? enhachedStatusCode = null;
            string                     text               = "";
            if(line.Length > 4){
                text = line.Substring(4);

                string[] stausCode_text = text.Split(' ',2);
                // Check if we have a enhached status code
                if(SMTP_t_EnhancedStatusCode.TryParse(stausCode_text[0],out enhachedStatusCode)){
                    text = stausCode_text[1];
                }
            }

            return new SMTP_t_ReplyLine(replyCode,enhachedStatusCode,text,isLastLine);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Converts this reply line into its RFC 5321 wire format representation.
        /// </summary>
        /// <returns>The reply line formatted as SMTP text.</returns>
        public override string ToString()
        {
            string sep = m_IsLastLine ? " " : "-";
            if (m_pEnhancedStatusCode != null){
                return $"{m_ReplyCode}{sep}{m_pEnhancedStatusCode.Raw} {m_Text}\r\n";
            }
            else{
                return $"{m_ReplyCode}{sep}{m_Text}\r\n";
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the 3‑digit SMTP reply code.
        /// </summary>
        public int ReplyCode
        {
            get{ return m_ReplyCode; }
        }
        
        /// <summary>
        /// Gets the enhanced status code (RFC 3463 / RFC 5248), if present.
        /// </summary>
        public SMTP_t_EnhancedStatusCode? EnhachedStatusCode
        {
            get{ return m_pEnhancedStatusCode; }
        }

        /// <summary>
        /// Gets the textual portion of the reply line.
        /// </summary>
        public string Text
        {
            get{ return m_Text; }
        }

        /// <summary>
        /// Gets whether this line is the final line of the reply.
        /// </summary>
        public bool IsLastLine
        {
            get{ return m_IsLastLine; }
        }

        /// <summary>
        /// Gets whether the reply code indicates a successful operation (2xx).
        /// </summary>
        public bool IsSuccess
        {
            get{ return m_ReplyCode >= 200 && m_ReplyCode <= 299; }
        }

        /// <summary>
        /// Gets whether the reply code indicates a temporary failure (4xx).
        /// </summary>
        public bool IsTemporaryError
        {
            get { return m_ReplyCode >= 400 && m_ReplyCode <= 499; }
        }

        /// <summary>
        /// Gets whether the reply code indicates a permanent failure (5xx).
        /// </summary>
        public bool IsPermanentError
        {
            get{ return m_ReplyCode >= 500 && m_ReplyCode <= 599; }
        }

        #endregion
    }
}
