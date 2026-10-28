using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.StoreAsync"/>
    /// event. This event allows the application to modify message flags
    /// for one or more messages in the selected mailbox.
    /// </summary>
    public class IMAP_e_Store : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_Folder;
        private IMAP_MessageInfo[]  m_pMessages;
        private IMAP_Flags_SetType  m_SetType   = IMAP_Flags_SetType.Replace;
        private string[]            m_pFlags;
 
        /// <summary>
        /// Initializes a new <see cref="IMAP_e_Store"/> instance containing
        /// information about the STORE operation requested by the client.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the STORE command.
        /// </param>
        /// <param name="folder">
        /// The mailbox containing the messages to be updated.
        /// </param>
        /// <param name="flagsSetType">
        /// Specifies whether the supplied flags replace, add to, or remove
        /// existing message flags.
        /// </param>
        /// <param name="flags">
        /// The flags supplied by the client.
        /// </param>
        /// <param name="messages">
        /// The messages affected by the STORE operation.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>,
        /// <paramref name="flags"/>,
        /// <paramref name="messages"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Store(IMAP_Session session,string folder,IMAP_Flags_SetType flagsSetType,string[] flags,IMAP_MessageInfo[] messages,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(flags == null){
                throw new ArgumentNullException(nameof(flags));
            }
            if(messages == null){
                throw new ArgumentNullException(nameof(messages));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_pResponse = response;
            m_Folder    = folder;
            m_pMessages = messages;
            m_SetType   = flagsSetType;
            m_pFlags    = flags;
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
        /// Gets or sets the IMAP server response describing the outcome
        /// of the STORE operation.
        /// </summary>
        /// <remarks>
        /// Event handlers should assign an appropriate OK or NO response.
        /// When a successful response is returned, the IMAP server
        /// automatically generates any required FETCH responses and updates
        /// the selected mailbox state.
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
        /// Gets the mailbox containing the messages being updated.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets the flag modification mode requested by the client.
        /// </summary>
        /// <remarks>
        /// Specifies whether the supplied flags replace existing flags,
        /// are added to existing flags, or are removed from existing flags.
        /// </remarks>
        public IMAP_Flags_SetType FlagsSetType
        {
            get{ return m_SetType; }
        }

        /// <summary>
        /// Gets the flags supplied by the client.
        /// </summary>
        /// <remarks>
        /// The interpretation of these flags depends on the value of
        /// <see cref="FlagsSetType"/>.
        /// </remarks>
        public string[] Flags
        {
            get{ return m_pFlags; }
        }

        /// <summary>
        /// Gets the messages affected by the STORE operation.
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
