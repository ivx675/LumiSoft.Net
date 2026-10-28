using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.ExpungeAsync"/>
    /// event. This event allows the application to permanently remove
    /// messages from a mailbox as a result of an EXPUNGE or UID EXPUNGE
    /// command.
    /// </summary>
    public class IMAP_e_Expunge : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private bool                m_IsUidExpunge;
        private IMAP_t_SeqSet?      m_pSeqSet;
        private string              m_Folder;
        private IMAP_MessageInfo[]  m_pMessages;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_Expunge"/> instance containing
        /// information about the EXPUNGE operation requested by the client.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the EXPUNGE command.
        /// </param>
        /// <param name="isUidExpunge">
        /// Specifies whether the operation originated from a UID EXPUNGE
        /// command. When <see langword="true"/>, only messages matching the
        /// supplied UID sequence set may be removed.
        /// </param>
        /// <param name="seqSet">
        /// The UID sequence set supplied by the client. This value is
        /// <see langword="null"/> when processing a standard EXPUNGE
        /// command and is populated only for UID EXPUNGE operations.
        /// </param>
        /// <param name="folder">
        /// The mailbox from which messages will be permanently removed.
        /// </param>
        /// <param name="messages">
        /// The messages selected for expunging.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>,
        /// <paramref name="messages"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Expunge(IMAP_Session session,bool isUidExpunge,IMAP_t_SeqSet? seqSet,string folder,IMAP_MessageInfo[] messages,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(messages == null){
                throw new ArgumentNullException(nameof(messages));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }
            
            m_pSession     = session;
            m_pResponse    = response;
            m_IsUidExpunge = isUidExpunge;
            m_pSeqSet      = seqSet;
            m_Folder       = folder;
            m_pMessages    = messages;
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
        /// Gets or sets the IMAP server response describing the outcome
        /// of the EXPUNGE operation.
        /// </summary>
        /// <remarks>
        /// Event handlers should assign an appropriate OK or NO response.
        /// When a successful response is returned, the IMAP server generates
        /// the required EXPUNGE responses and updates the mailbox state
        /// automatically.
        /// </remarks>
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
        /// Gets whether the operation originated from a UID EXPUNGE command.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/>, the client issued a UID EXPUNGE
        /// command and only messages matching the supplied UID sequence set
        /// may be removed. When <see langword="false"/>, the operation
        /// originated from a standard EXPUNGE command.
        /// </remarks>
        public bool IsUidExpunge
        {
            get { return m_IsUidExpunge; }
        }

        /// <summary>
        /// Gets the UID sequence set supplied by the client.
        /// </summary>
        /// <remarks>
        /// This property is <see langword="null"/> for a standard EXPUNGE
        /// command.
        /// <para>
        /// When <see cref="IsUidExpunge"/> is <see langword="true"/>, the
        /// value contains the UID sequence set supplied to the UID EXPUNGE
        /// command.
        /// </para>
        /// <para>
        /// The messages resolved from this UID sequence set are available
        /// through the <see cref="Messages"/> property.
        /// </para>
        /// </remarks>
        public IMAP_t_SeqSet? SequenceSet
        {
            get{ return m_pSeqSet; }
        }

        /// <summary>
        /// Gets the mailbox from which messages will be permanently removed.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets the messages selected for expunging.
        /// </summary>
        /// <remarks>
        /// The messages are resolved by the IMAP server before the event is
        /// raised and contain the cached metadata available to the current
        /// session, including the application-defined message identifier,
        /// UID, flags, size, and INTERNALDATE.
        /// </remarks>
        public IMAP_MessageInfo[] Messages
        {
            get{ return m_pMessages; }
        }

        #endregion
    }
}
