using LumiSoft.Net.SMTP;
using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3.Client
{    
    /// <summary>
    /// Represents an exception raised during a POP3 client operation.
    /// The exception encapsulates the POP3 server response that caused
    /// the failure, allowing callers to inspect the status indicator,
    /// optional response code, and any diagnostic text returned by the server.
    /// </summary>
    public class POP3_ClientException : Exception
    {
        private POP3_ServerResponse m_pResponse;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="POP3_ClientException"/> class
        /// using the specified POP3 server response.
        /// </summary>
        /// <param name="response">
        /// The POP3 server response associated with the failure.  
        /// Must contain a valid POP3 status line beginning with <c>+OK</c> or <c>-ERR</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        public POP3_ClientException(POP3_ServerResponse response)
        {
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pResponse = response;
        }


        #region Properties Implementation

        /// <summary>
        /// Gets the POP3 server response associated with the exception.
        /// The response provides access to the status indicator (<c>+OK</c> or <c>-ERR</c>),
        /// any optional POP3 response code (RESP-CODES), and the server's diagnostic text.
        /// </summary>
        public POP3_ServerResponse ServerResponse
        {
            get { return m_pResponse; }
        }

        #endregion

    }
}
