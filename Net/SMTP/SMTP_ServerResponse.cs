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
