using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server 
{
    /// <summary>
    /// Provides information about an IMAP DELETE command. This event-args
    /// class is used with <see cref="IMAP_Session.DeleteAsync"/> to process
    /// mailbox deletion requests before a response is returned to the client.
    /// </summary>
    public class IMAP_e_Delete : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_Folder;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_e_Delete"/> class.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the DELETE command.
        /// </param>
        /// <param name="folder">
        /// The name of the mailbox the client requested to delete.
        /// </param>
        /// <param name="response">
        /// The response that will be returned to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>, <paramref name="folder"/>,
        /// or <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Delete(IMAP_Session session,string folder,IMAP_r_ServerStatus response)
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
            m_pResponse = response;
            m_Folder    = folder;
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
        /// Gets or sets the IMAP server response describing the outcome of the
        /// DELETE command.
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
        /// Gets the name of the mailbox the client requested to delete.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        #endregion
    }
}
