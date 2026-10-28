using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server 
{
    /// <summary>
    /// Provides information about an IMAP CREATE command. This event-args
    /// class is used with <see cref="IMAP_Session.CreateAsync"/> to process
    /// mailbox creation requests before a response is returned to the client.
    /// </summary>
    public class IMAP_e_Create : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_Folder;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_e_Create"/> class.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the CREATE command.
        /// </param>
        /// <param name="folder">
        /// The name of the mailbox requested by the client.
        /// </param>
        /// <param name="response">
        /// The response object used to report the result of the CREATE
        /// operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>, or <paramref name="response"/> is
        /// <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Create(IMAP_Session session,string folder,IMAP_r_ServerStatus response)
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
        /// Gets or sets the response that will be returned to the client when
        /// processing of the CREATE command completes.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when attempting to set the value to <see langword="null"/>.
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
        /// Gets the name of the mailbox specified by the client in the
        /// CREATE command.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        #endregion
    }
}
