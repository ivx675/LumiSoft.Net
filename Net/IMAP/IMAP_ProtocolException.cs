using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents a violation of the IMAP protocol or malformed server data
    /// encountered during a IMAP operation.
    /// </summary>
    public class IMAP_ProtocolException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_ProtocolException"/> class
        /// with the specified error message.
        /// </summary>
        /// <param name="message">
        /// A descriptive message explaining the protocol violation.
        /// </param>
        public IMAP_ProtocolException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_ProtocolException"/> class
        /// with the specified error message and inner exception.
        /// </summary>
        /// <param name="message">
        /// A descriptive message explaining the protocol violation.
        /// </param>
        /// <param name="innerException">
        /// The exception that caused this protocol error, if any.
        /// </param>
        public IMAP_ProtocolException(string message,Exception innerException) : base(message,innerException)
        {
        }
    }
}
