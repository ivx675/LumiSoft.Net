using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3 
{
    /// <summary>
    /// Represents a violation of the POP3 protocol or malformed server data
    /// encountered during a POP3 operation.
    /// </summary>
    public class POP3_ProtocolException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="POP3_ProtocolException"/> class
        /// with the specified error message.
        /// </summary>
        /// <param name="message">
        /// A descriptive message explaining the protocol violation.
        /// </param>
        public POP3_ProtocolException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="POP3_ProtocolException"/> class
        /// with the specified error message and inner exception.
        /// </summary>
        /// <param name="message">
        /// A descriptive message explaining the protocol violation.
        /// </param>
        /// <param name="innerException">
        /// The exception that caused this protocol error, if any.
        /// </param>
        public POP3_ProtocolException(string message,Exception innerException) : base(message,innerException)
        {
        }
    }

}
