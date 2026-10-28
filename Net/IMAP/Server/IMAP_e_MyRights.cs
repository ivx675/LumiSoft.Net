using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.MyRightsAsync"/>
    /// event. This event allows the application to supply the effective
    /// access rights of the authenticated user for a mailbox.
    /// </summary>
    public class IMAP_e_MyRights : EventArgs
    {
        private IMAP_Session         m_pSession;
        private IMAP_r_u_MyRights?   m_pMyRightsResponse = null;
        private IMAP_r_ServerStatus  m_pResponse;
        private string               m_Folder;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_MyRights"/> instance
        /// containing the mailbox name supplied by the client and the
        /// response object used to return the command result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the MYRIGHTS command.
        /// </param>
        /// <param name="folder">
        /// The mailbox whose effective access rights are being queried.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_MyRights(IMAP_Session session,string folder,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_Folder    = folder;
            m_pResponse = response;
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
        /// Gets or sets the MYRIGHTS response that will be returned to the
        /// client.
        /// </summary>
        /// <remarks>
        /// The MYRIGHTS response contains the effective rights of the
        /// authenticated user for the requested mailbox. A successful
        /// MYRIGHTS operation should provide a value for this property.
        /// </remarks>
        public IMAP_r_u_MyRights? MyRightsResponse
        {
            get{ return m_pMyRightsResponse; }

            set{ m_pMyRightsResponse = value; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the MYRIGHTS command.
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

        /// <summary>
        /// Gets the mailbox whose effective access rights are being queried.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        #endregion
    }
}
