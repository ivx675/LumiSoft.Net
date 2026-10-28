using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.NamespaceAsync"/>
    /// event. This event allows the application to customize the
    /// NAMESPACE response returned to the client.
    /// </summary>
    public class IMAP_e_Namespace : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_u_Namespace  m_pNamespaceResponse;
        private IMAP_r_ServerStatus m_pResponse;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_Namespace"/> instance
        /// containing the session context and the response object used
        /// to return the command result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the NAMESPACE command.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Namespace(IMAP_Session session,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession = session;
            m_pResponse = response;
            
            m_pNamespaceResponse = new IMAP_r_u_Namespace([new IMAP_t_Namespace_Entry("",'/')],[],[]);
        }


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP session associated with this event. The session provides access
        /// to connection state, authentication status, and server-side context required
        /// to process the current IMAP command.
        /// </summary>
        public IMAP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_u_Namespace"/> response that
        /// will be returned to the client.
        /// </summary>
        /// <remarks>
        /// The default value represents a single personal namespace with an
        /// empty prefix and '/' hierarchy delimiter, and no other-users or
        /// shared namespaces.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public IMAP_r_u_Namespace NamespaceResponse
        {
            get{ return m_pNamespaceResponse; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException("value");
                }

                m_pNamespaceResponse = value; 
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the NAMESPACE command.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public IMAP_r_ServerStatus Response
        {
            get{ return m_pResponse; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException("value");
                }

                m_pResponse = value; 
            }
        }

        #endregion
    }
}
