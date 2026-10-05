using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Principal;

namespace LumiSoft.Net.POP3.Server
{
    /// <summary>
    /// This class provides data for <see cref="POP3_Session.LoadMessagesInfoAsync"/> event.
    /// </summary>
    public class POP3_e_LoadMessagesInfo : EventArgs
    {
        private POP3_Session             m_pSession;
        private List<POP3_ServerMessage> m_pMessages;

        /// <summary>
        /// Initializes a new <see cref="POP3_e_LoadMessagesInfo"/> instance for the
        /// specified POP3 session. This event‑args object is used when the server
        /// requests message metadata (sizes and UIDLs) after successful authentication,
        /// before entering the TRANSACTION state.
        /// </summary>
        /// <param name="session">
        /// The POP3 session for which message metadata will be loaded. This value
        /// must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> is null.
        /// </exception>
        internal POP3_e_LoadMessagesInfo(POP3_Session session)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }

            m_pSession  = session;
            m_pMessages = new List<POP3_ServerMessage>();
        }


        #region Properties implementation

        /// <summary>
        /// Gets the <see cref="POP3_Session"/> associated with the newly
        /// established POP3 connection.
        /// <para>
        /// The session object encapsulates the state and behavior of the POP3
        /// transaction, including authentication, command processing, and
        /// mailbox interaction for the duration of the client connection.
        /// </para>
        /// </summary>
        /// <value>
        /// The active <see cref="POP3_Session"/> instance representing the
        /// current POP3 control connection.
        /// </value>
        public POP3_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets the collection of messages for the authenticated POP3 session. The event
        /// handler for <see cref="POP3_Session.LoadMessagesInfoAsync"/> must populate this list with
        /// all messages in the user's mailbox, including their size and UIDL values.
        /// </summary>
        public List<POP3_ServerMessage> Messages
        {
            get{ return m_pMessages; }
        }

        #endregion
    }
}
