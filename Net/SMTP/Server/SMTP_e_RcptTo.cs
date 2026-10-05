using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// Provides data for the RCPT TO event raised during an SMTP transaction.
    /// </summary>
    /// <remarks>
    /// This event is triggered when the client issues a RCPT TO command.
    /// It exposes the active SMTP session, the parsed recipient envelope
    /// (including forward-path and any DSN rcpt-parameters such as NOTIFY
    /// or ORCPT), and the server response object used to accept or reject
    /// the recipient. Event handlers may inspect or modify the response
    /// before it is sent back to the client.
    /// </remarks>
    public class SMTP_e_RcptTo : EventArgs
    {
        private SMTP_Session        m_pSession;
        private SMTP_t_RcptTo       m_pRcptTo;
        private SMTP_ServerResponse m_pResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_e_RcptTo"/> event.
        /// This event is raised when the client issues a RCPT TO command and
        /// provides the session context, the parsed recipient envelope data,
        /// and the response object used to accept or reject the recipient.
        /// </summary>
        /// <param name="session">
        /// The SMTP session associated with the RCPT TO command.
        /// </param>
        /// <param name="to">
        /// The parsed RCPT TO envelope, including the forward-path mailbox
        /// and any DSN-related rcpt-parameters (NOTIFY, ORCPT).
        /// </param>
        /// <param name="response">
        /// The server response object used to indicate success or failure
        /// (e.g., 250, 450, 451, 550, 551, 553, 555).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="session"/>, <paramref name="to"/>, or
        /// <paramref name="response"/> is null.
        /// </exception>
        public SMTP_e_RcptTo(SMTP_Session session,SMTP_t_RcptTo to,SMTP_ServerResponse response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(to == null){
                throw new ArgumentNullException(nameof(to));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_pRcptTo   = to;
            m_pResponse = response;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the <see cref="SMTP_Session"/> associated with this event.
        /// The session object provides access to connection state, client
        /// commands, authentication status, and other per‑session data used
        /// during SMTP command processing.
        /// </summary>
        public SMTP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets the <see cref="SMTP_t_RcptTo"/> envelope recipient associated
        /// with this RCPT TO event. The object contains the forward‑path mailbox
        /// and any DSN‑related rcpt‑parameters (such as NOTIFY or ORCPT) parsed
        /// from the RCPT TO command.
        /// </summary>
        public SMTP_t_RcptTo RcptTo
        {
            get{ return m_pRcptTo; }
        }

        /// <summary>
        /// Gets or sets the <see cref="SMTP_ServerResponse"/> associated with
        /// this RCPT TO event. The response object determines whether the
        /// recipient is accepted or rejected and allows event handlers to
        /// modify the server's reply (for example: 250, 450, 451, 550, 551,
        /// 553, or 555) before it is sent back to the client.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown if a null value is assigned to the property.
        /// </exception>
        public SMTP_ServerResponse Response
        {
            get{ return m_pResponse; }

            set{
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }

                m_pResponse = value;
            }
        }

        #endregion
    }
}
