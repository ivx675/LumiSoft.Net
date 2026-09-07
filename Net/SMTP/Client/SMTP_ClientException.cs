using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Client
{
    /// <summary>
    /// Represents an exception raised during an SMTP client operation.  
    /// The exception encapsulates the SMTP reply that triggered the failure,
    /// allowing callers to inspect the reply code, enhanced status code,
    /// and any diagnostic text returned by the remote endpoint.
    /// </summary>
    public class SMTP_ClientException : Exception
    {
        private SMTP_ServerResponse m_pResponse;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_ClientException"/> class
        /// using the specified SMTP reply.
        /// </summary>
        /// <param name="response">
        /// The SMTP reply associated with the failure.  
        /// Must contain at least one reply line.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        public SMTP_ClientException(SMTP_ServerResponse response)
        {
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pResponse = response;
        }


        #region Properties Implementation

        /// <summary>
        /// Gets the SMTP reply associated with the exception.  
        /// The reply provides the full set of reply lines returned by the
        /// remote endpoint, including the final reply code and any enhanced
        /// status code that describes the nature of the failure.
        /// </summary>
        public SMTP_ServerResponse ServerResponse
        {
            get { return m_pResponse; }
        }

        #endregion
    }
}
