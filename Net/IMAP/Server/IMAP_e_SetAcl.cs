using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.SetAclAsync"/> event.
    /// This event allows the application to modify the access control list
    /// (ACL) of a mailbox for a specific identifier.
    /// </summary>
    public class IMAP_e_SetAcl : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_Folder;
        private string              m_Identifier;
        private string              m_Rights;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_SetAcl"/> instance containing
        /// the mailbox, identifier, and rights modification supplied by the
        /// client, together with the response object used to return the
        /// command result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the SETACL command.
        /// </param>
        /// <param name="folder">
        /// The mailbox whose access control list is being modified.
        /// </param>
        /// <param name="identifier">
        /// The identifier whose rights are being modified. This is typically
        /// a user or group name.
        /// </param>
        /// <param name="rights">
        /// The rights modification string supplied by the client. The value
        /// may add, remove, or replace rights depending on whether it is
        /// prefixed with '+' or '-'.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>,
        /// <paramref name="identifier"/>,
        /// <paramref name="rights"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_SetAcl(IMAP_Session session,string folder,string identifier,string rights,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(identifier == null){
                throw new ArgumentNullException(nameof(identifier));
            }
            if(rights == null){
                throw new ArgumentNullException(nameof(rights));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_Folder     = folder;
            m_Identifier = identifier;
            m_Rights     = rights;
            m_pResponse  = response;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP session associated with this event. The session provides
        /// access to connection state, authentication status, and server-side
        /// context required to process the current IMAP command.
        /// </summary>
        public IMAP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the SETACL command.
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
        /// Gets the mailbox whose access control list is being modified.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets the identifier whose rights are being modified. This is
        /// typically a user or group name.
        /// </summary>
        public string Identifier
        {
            get{ return m_Identifier; }
        }

        /// <summary>
        /// Gets the rights modification string supplied by the client.
        /// </summary>
        /// <remarks>
        /// A leading '+' indicates that the specified rights should be added
        /// to the identifier's existing rights. A leading '-' indicates that
        /// the specified rights should be removed. If neither prefix is
        /// present, the supplied rights replace any existing rights.
        /// </remarks>
        public string Rights
        {
            get{ return m_Rights; }
        }

        #endregion
    }
}
