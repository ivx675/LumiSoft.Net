using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// Provides event data for the <c>StartedAsync</c> event raised when a new
    /// SMTP session is established, before the server sends its initial
    /// greeting banner to the client.  
    /// Contains the session object and the server-generated greeting
    /// <see cref="SMTP_ServerResponse"/> that event handlers may inspect or
    /// replace.
    /// </summary>
    public class SMTP_e_Started : EventArgs
    {
        private SMTP_Session        m_pSession;
        private SMTP_ServerResponse m_pResponse;

        /// <summary>
        /// Provides event data for the <c>StartedAsync</c> event raised when a new
        /// SMTP session is created, before the server sends its initial greeting
        /// banner to the client.
        /// </summary>
        /// <param name="session">
        /// The newly created <see cref="SMTP_Session"/> associated with the
        /// incoming connection.
        /// </param>
        /// <param name="response">
        /// The initial <see cref="SMTP_ServerResponse"/> that will be sent as the
        /// server greeting. Event handlers may modify this value before it is
        /// transmitted.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="session"/> or <paramref name="response"/> is
        /// <c>null</c>.
        /// </exception>
        public SMTP_e_Started(SMTP_Session session,SMTP_ServerResponse response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_pResponse = response;
        }


        #region Properties impelemntation

        /// <summary>
        /// Gets the <see cref="SMTP_Session"/> associated with the newly
        /// established connection.  
        /// This session is created before the server sends its initial greeting
        /// banner to the client.
        /// </summary>
        /// <value>
        /// The <see cref="SMTP_Session"/> instance representing the active
        /// connection.
        /// </value>
        public SMTP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets or sets the <see cref="SMTP_ServerResponse"/> that will be sent
        /// to the client as the initial server greeting banner.  
        /// Event handlers may replace this value to customize the greeting before
        /// it is transmitted.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a <c>null</c> value is assigned.
        /// </exception>
        public SMTP_ServerResponse Response
        {
            get{ return m_pResponse; }

            set{
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }

                m_pResponse = value;
            }
        }

        #endregion
    }
}
