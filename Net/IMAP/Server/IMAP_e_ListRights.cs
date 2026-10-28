using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.ListRightsAsync"/>
    /// event. This event allows the application to provide information
    /// about the rights that may be granted to an identifier for a mailbox.
    /// </summary>
    public class IMAP_e_ListRights : EventArgs
    {
        private IMAP_Session         m_pSession;
        private IMAP_r_u_ListRights? m_pListRightsResponse = null;
        private IMAP_r_ServerStatus  m_pResponse;
        private string               m_Folder;
        private string               m_Identifier;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_ListRights"/> instance
        /// containing the mailbox and identifier supplied by the client,
        /// together with the response object used to return the command
        /// result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the LISTRIGHTS command.
        /// </param>
        /// <param name="folder">
        /// The mailbox whose grantable rights are being queried.
        /// </param>
        /// <param name="identifier">
        /// The identifier whose grantable rights are being queried. This
        /// is typically a user or group name.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>,
        /// <paramref name="identifier"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_ListRights(IMAP_Session session,string folder,string identifier,IMAP_r_ServerStatus response)
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
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession   = session;
            m_Folder     = folder;
            m_Identifier = identifier;
            m_pResponse  = response;
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
        /// Gets or sets the LISTRIGHTS response that will be returned to
        /// the client.
        /// </summary>
        /// <remarks>
        /// The LISTRIGHTS response describes which rights may be granted to
        /// the specified identifier for the mailbox. A successful LISTRIGHTS
        /// operation should provide a value for this property.
        /// </remarks>
        public IMAP_r_u_ListRights? ListRightsResponse
        {
            get{ return m_pListRightsResponse; }

            set{ m_pListRightsResponse = value; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the LISTRIGHTS command.
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
        /// Gets the mailbox whose grantable rights are being queried.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets the identifier whose grantable rights are being queried.
        /// This is typically a user or group name.
        /// </summary>
        public string Identifier
        {
            get{ return m_Identifier; }
        }

        #endregion
    }
}
