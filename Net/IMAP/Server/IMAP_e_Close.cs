using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server 
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.CloseAsync"/> event.
    /// This event allows the application to process a CLOSE command and,
    /// when appropriate, permanently remove messages marked with the
    /// \Deleted flag from the selected mailbox before returning to the
    /// authenticated state.
    /// </summary>
    public class IMAP_e_Close : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_Folder;
        private bool                m_IsReadOnly;
        private IMAP_MessageInfo[]  m_pDeletedMessages;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_e_Close"/> class.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the CLOSE command.
        /// </param>
        /// <param name="folder">
        /// The name of the mailbox the client requested to close.
        /// </param>
        /// <param name="isReadOnly">
        /// Specifies whether the mailbox is currently open in read-only mode.
        /// A value of <see langword="true"/> indicates that the mailbox was
        /// opened by an EXAMINE command or is otherwise read-only. In this
        /// case, CLOSE must not permanently remove messages marked with the
        /// \Deleted flag.
        /// </param>
        /// <param name="deletedMessages">
        /// The messages currently marked with the <c>\Deleted</c> flag that
        /// should be permanently removed when processing a writable CLOSE
        /// command.
        /// </param>
        /// <param name="response">
        /// The response that will be returned to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>,
        /// <paramref name="deletedMessages"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Close(IMAP_Session session,string folder,bool isReadOnly,IMAP_MessageInfo[] deletedMessages,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(deletedMessages == null){
                throw new ArgumentNullException(nameof(deletedMessages));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }
         
            m_pSession         = session;
            m_pResponse        = response;
            m_Folder           = folder;
            m_IsReadOnly       = isReadOnly;
            m_pDeletedMessages = deletedMessages;
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
        /// Gets or sets the IMAP server response describing the outcome of
        /// the CLOSE command.
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
        /// Gets the name of the mailbox being closed.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets whether the mailbox was opened in read-only mode.
        /// </summary>
        /// <remarks>
        /// When true, the mailbox was opened by EXAMINE or otherwise selected
        /// read-only, and CLOSE must not remove messages marked with the
        /// \Deleted flag.
        /// </remarks>
        public bool IsReadOnly
        {
            get {  return m_IsReadOnly; }
        }

        /// <summary>
        /// Gets the messages currently marked with the <c>\Deleted</c> flag
        /// in the selected mailbox.
        /// </summary>
        /// <remarks>
        /// For writable mailboxes, these messages should be permanently
        /// removed when processing the CLOSE command. For mailboxes opened
        /// in read-only mode, no messages should be removed.
        /// <para>
        /// Each <see cref="IMAP_MessageInfo"/> contains the message's
        /// application-defined identifier, UID, flags, and other cached
        /// metadata available to the current IMAP session.
        /// </para>
        /// </remarks>
        public IMAP_MessageInfo[] DeletedMessages
        {
            get{ return m_pDeletedMessages; }
        }

        #endregion
    }
}
