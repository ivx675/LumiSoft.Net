using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides information about an IMAP RENAME command. This event-args
    /// class is used with <see cref="IMAP_Session.RenameAsync"/> to process
    /// mailbox rename requests before a response is returned to the client.
    /// </summary>
    public class IMAP_e_Rename : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_CurrentFolder;
        private string              m_NewFolder;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_e_Rename"/> class.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the RENAME command.
        /// </param>
        /// <param name="currentFolder">
        /// The name of the existing mailbox to be renamed.
        /// </param>
        /// <param name="newFolder">
        /// The new name to assign to the mailbox.
        /// </param>
        /// <param name="response">
        /// The response that will be returned to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="currentFolder"/>,
        /// <paramref name="newFolder"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Rename(IMAP_Session session,string currentFolder,string newFolder,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(currentFolder == null){
                throw new ArgumentNullException("currentFolder");
            }
            if(newFolder == null){
                throw new ArgumentNullException("newFolder");
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession      = session;
            m_CurrentFolder = currentFolder;
            m_NewFolder     = newFolder;
            m_pResponse     = response;
        }


        #region Properties impalementation

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
        /// RENAME command.
        /// </summary>
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
        /// Gets the name of the mailbox the client requested to rename.
        /// </summary>
        public string CurrentFolder
        {
            get{ return m_CurrentFolder; }
        }

        /// <summary>
        /// Gets the new name requested for the mailbox.
        /// </summary>
        public string NewFolder
        {
            get{ return m_NewFolder; }
        }

        #endregion
    }
}
