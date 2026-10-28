using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.GetAclAsync"/> event.
    /// This event allows the application to supply the access control list
    /// (ACL) for a mailbox.
    /// </summary>
    public class IMAP_e_GetAcl : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_u_Acl?       m_pAclResponse;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_Folder;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_GetAcl"/> instance containing
        /// the mailbox name supplied by the client and the response object
        /// used to return the command result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the GETACL command.
        /// </param>
        /// <param name="folder">
        /// The mailbox for which access control information was requested.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_GetAcl(IMAP_Session session,string folder,IMAP_r_ServerStatus response)
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
        /// Gets or sets the ACL response that will be returned to the client.
        /// </summary>
        /// <remarks>
        /// The ACL response contains the access rights associated with the
        /// requested mailbox. A successful GETACL operation should provide
        /// a value for this property.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public IMAP_r_u_Acl? AclResponse
        {
            get{ return m_pAclResponse; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }

                m_pAclResponse = value; 
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the GETACL command.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public IMAP_r_ServerStatus Response
        {
            get{ return m_pResponse; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }

                m_pResponse = value; 
            }
        }

        /// <summary>
        /// Gets the mailbox for which access control information was requested.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        #endregion
    }
}
