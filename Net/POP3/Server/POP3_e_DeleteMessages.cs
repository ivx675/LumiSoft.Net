using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3.Server
{
    /// <summary>
    /// Provides event data for the <c>DeleteMessagesAsync</c> event, containing
    /// the POP3 session context and the set of messages that the hosting
    /// application must delete when the POP3 session enters the UPDATE state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// During the TRANSACTION state, messages may be marked for deletion via <c>DELE</c>
    /// commands. When the client issues a successful <c>QUIT</c> command, the
    /// server transitions to the UPDATE state and raises the
    /// <c>DeleteMessagesAsync</c> event.
    /// </para>
    /// </remarks>
    public class POP3_e_DeleteMessages : EventArgs
    {
        private POP3_Session         m_pSession;
        private POP3_ServerMessage[] m_pMessages;

        /// <summary>
        /// Initializes a new instance of the <see cref="POP3_e_DeleteMessages"/> class,
        /// providing the POP3 session context and the set of messages that must be
        /// deleted by the hosting application when the session enters the UPDATE state.
        /// </summary>
        /// <param name="session">
        /// The POP3 session that is finalizing with a successful <c>QUIT</c> command.
        /// This context allows the event handler to access session‑specific information
        /// if needed during the deletion process.
        /// </param>
        /// <param name="messages">
        /// The collection of messages that were marked for deletion during the
        /// TRANSACTION state. The hosting application is responsible for deleting
        /// these messages from its storage backend (filesystem, database, etc.).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> or <paramref name="messages"/> is null.
        /// </exception>
        internal POP3_e_DeleteMessages(POP3_Session session,POP3_ServerMessage[] messages)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(messages == null){
                throw new ArgumentNullException(nameof(messages));
            }

            m_pSession  = session;
            m_pMessages = messages;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the POP3 session associated with this deletion request.  
        /// </summary>
        /// <remarks>
        /// This property provides the context of the POP3 session that is finalizing
        /// with a successful <c>QUIT</c> command. Event handlers may use this session
        /// object to access connection information, logging context, or other
        /// session‑specific data when performing message deletion.
        /// </remarks>
        public POP3_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets the collection of messages that were marked for deletion during the
        /// POP3 session's TRANSACTION state.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These messages represent the complete deletion set accumulated from all
        /// <c>DELE</c> commands issued by the client.
        /// </para>
        /// <para>
        /// The hosting application is responsible for removing these messages from
        /// its underlying storage (filesystem, database, cloud, etc.) before the
        /// POP3 session is finalized.
        /// </para>
        /// </remarks>
        public POP3_ServerMessage[] Messages
        {
            get{ return m_pMessages; }
        }

        #endregion
    }
}
