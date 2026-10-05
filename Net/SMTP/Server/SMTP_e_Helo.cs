using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// Provides event data for the <c>HeloAsync</c> event raised when the
    /// session processes a <c>HELO</c> command.  
    /// Contains the client‑supplied domain string and the server’s initial
    /// <see cref="SMTP_ServerResponse"/> that the event handler may inspect
    /// or replace.
    /// </summary>
    public class SMTP_e_Helo : EventArgs
    {
        private SMTP_Session        m_pSession;
        private string              m_Domain   = "";
        private SMTP_ServerResponse m_pResponse;

        /// <summary>
        /// Provides data for the <c>HeloAsync</c> event raised when the session
        /// processes a <c>HELO</c> command.  
        /// Contains the client-supplied domain string and the server’s initial
        /// <see cref="SMTP_ServerResponse"/> that the event handler may inspect
        /// or replace.
        /// </summary>
        /// <param name="session">
        /// The active SMTP session that received the <c>HELO</c> command.
        /// </param>
        /// <param name="domain">
        /// The domain or address-literal supplied by the client in the
        /// <c>HELO</c> command.
        /// </param>
        /// <param name="response">
        /// The server-generated <c>250</c> reply line acknowledging the
        /// <c>HELO</c> command. Event handlers may modify this response
        /// before it is sent to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="session"/> or <paramref name="response"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="domain"/> is <c>null</c> or empty.
        /// </exception>
        public SMTP_e_Helo(SMTP_Session session,string domain,SMTP_ServerResponse response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(string.IsNullOrEmpty(domain)){
                throw new ArgumentException("Argument 'domain' value must be sepcified.","domain");
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_Domain    = domain;
            m_pResponse = response;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the active <see cref="SMTP_Session"/> associated with the command
        /// that raised this event.  
        /// Provides access to session‑level information such as connection state,
        /// security status, local endpoint, and server capabilities.
        /// </summary>
        /// <value>
        /// The <see cref="SMTP_Session"/> instance that is currently processing
        /// the SMTP command.
        /// </value>
        public SMTP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets the domain or address-literal supplied by the client in the
        ///  <c>HELO</c> command.  
        /// This value represents the client’s self‑reported identity and may not
        /// correspond to a verified DNS hostname.
        /// </summary>
        public string Domain
        {
            get{ return m_Domain; }
        }

        /// <summary>
        /// Gets or sets the <see cref="SMTP_ServerResponse"/> that will be sent
        /// to the client for the associated SMTP command.  
        /// Event handlers may replace this value to modify the server’s reply
        /// before it is transmitted.
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
