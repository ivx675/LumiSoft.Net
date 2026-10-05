using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP 
{
    /// <summary>
    /// Represents a complete SMTP server response consisting of one or more
    /// reply lines as defined in RFC 5321 section 4.2.  
    /// A server response may contain multiple lines, where all but the final
    /// line use the continuation format (<c>reply-code "-"</c>). The last line
    /// contains the final reply code and optional enhanced status code.
    /// </summary>
    public class SMTP_ServerResponse 
    {
        private SMTP_t_ReplyLine[] m_pReplyLines;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_ServerResponse"/> class
        /// using a single reply line constructed from the specified reply code,
        /// enhanced status code, and reply text.
        /// </summary>
        /// <param name="replyCode">
        /// The 3‑digit SMTP reply code. Valid values range from 200 to 599,
        /// representing success (2xx), intermediate replies (3xx),
        /// temporary failures (4xx), and permanent failures (5xx).
        /// </param>
        /// <param name="enhancedStatusCode">
        /// Optional enhanced status code (RFC 3463 / RFC 5248) providing
        /// additional semantic information about the reply.
        /// </param>
        /// <param name="text">
        /// The textual message associated with the reply line.  
        /// If <c>null</c>, an empty string is used.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="replyCode"/> is outside the valid range
        /// of 200–599.
        /// </exception>
        /// <remarks>
        /// This constructor is a convenience overload for creating a response
        /// consisting of exactly one reply line.  
        /// Multi‑line responses should be created using the
        /// <see cref="SMTP_ServerResponse(SMTP_t_ReplyLine[])"/> constructor.
        /// </remarks>
        public SMTP_ServerResponse(int replyCode,SMTP_t_EnhancedStatusCode? enhancedStatusCode,string text) 
        {
            if(replyCode < 200 || replyCode > 599){
                throw new ArgumentOutOfRangeException(nameof(replyCode),"Reply code must be between 200 and 599.");
            }
            if(text == null){
                text = "";
            }

            m_pReplyLines = [new SMTP_t_ReplyLine(replyCode,enhancedStatusCode,text)];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_ServerResponse"/> class
        /// using the specified array of reply lines.
        /// </summary>
        /// <param name="replyLines">
        /// An array of <see cref="SMTP_t_ReplyLine"/> objects representing the full
        /// SMTP server response. The array must contain at least one entry.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="replyLines"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="replyLines"/> is empty.
        /// </exception>
        public SMTP_ServerResponse(SMTP_t_ReplyLine[] replyLines) 
        {
            if(replyLines == null){
                throw new ArgumentNullException(nameof(replyLines));
            }
            if(replyLines.Length == 0){
                throw new ArgumentException("Argument 'replyLines' must contain at least 1 entry.",nameof(replyLines));
            }

            m_pReplyLines = replyLines;
        }


        #region override method ToString

        /// <summary>
        /// Converts the SMTP response into its wire-format textual representation
        /// as defined in RFC 5321 section 4.2.  
        /// Each reply line is rendered with the appropriate continuation syntax
        /// (<c>reply-code "-"</c>) for all but the final line, which contains the
        /// definitive reply code, optional enhanced status code, and reply text.
        /// </summary>
        /// <returns>
        /// A string containing the complete SMTP reply, including all reply lines
        /// terminated with CRLF.
        /// </returns>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            for(int i = 0; i < m_pReplyLines.Length; i++){
                var replyLine = m_pReplyLines[i];

                // Last line
                if(i == m_pReplyLines.Length - 1){
                    if(replyLine.EnhachedStatusCode != null){
                        retVal.Append($"{replyLine.ReplyCode} {replyLine.EnhachedStatusCode.ToString()} {replyLine.Text}\r\n");
                    }
                    else{
                        retVal.Append($"{replyLine.ReplyCode} {replyLine.Text}\r\n");
                    }                    
                }
                else{
                    retVal.Append($"{replyLine.ReplyCode}-{replyLine.Text}\r\n");
                }
            } 

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the reply lines of the SMTP response. 
        /// </summary>
        public SMTP_t_ReplyLine[] ReplyLines
        {
            get { return m_pReplyLines; }
        }

        /// <summary>
        /// Gets the final reply line of the SMTP response.  
        /// The last line contains the definitive reply code and optional enhanced
        /// status code and determines the overall outcome of the SMTP operation.
        /// </summary>
        public SMTP_t_ReplyLine LastReplyLine
        {
            get { return m_pReplyLines[m_pReplyLines.Length -1]; }
        }

        /// <summary>
        /// Gets the 3‑digit SMTP reply code.
        /// </summary>
        public int ReplyCode
        {
            get{ return LastReplyLine.ReplyCode; }
        }

        /// <summary>
        /// Gets the last reply line enhanced status code (RFC 3463 / RFC 5248), if present.
        /// </summary>
        public SMTP_t_EnhancedStatusCode? EnhachedStatusCode
        {
            get{ return LastReplyLine.EnhachedStatusCode; }
        }

        /// <summary>
        /// Gets whether the reply code indicates a successful operation (2xx).
        /// </summary>
        public bool IsSuccess
        {
            get{ return ReplyCode >= 200 && ReplyCode <= 299; }
        }

        /// <summary>
        /// Gets whether the reply code indicates a temporary failure (4xx).
        /// </summary>
        public bool IsTemporaryError
        {
            get { return ReplyCode >= 400 && ReplyCode <= 499; }
        }

        /// <summary>
        /// Gets whether the reply code indicates a permanent failure (5xx).
        /// </summary>
        public bool IsPermanentError
        {
            get{ return ReplyCode >= 500 && ReplyCode <= 599; }
        }

        #endregion
    }
}
