using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// Provides data for the MAIL FROM event raised during an SMTP transaction.
    /// </summary>
    /// <remarks>
    /// This event is triggered when the client issues a MAIL FROM command.
    /// It exposes the active SMTP session, the parsed MAIL FROM envelope
    /// (including reverse-path and mail-parameters), and the server response
    /// object used to accept or reject the command.
    /// </remarks>
    public class SMTP_e_MailFrom : EventArgs
    {
        private SMTP_Session        m_pSession;
        private SMTP_t_MailFrom     m_pMailFrom;
        private SMTP_ServerResponse m_pResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_e_MailFrom"/> event.
        /// This event is raised when the client issues a MAIL FROM command and
        /// provides the session context, the parsed MAIL FROM envelope data, and
        /// the response object used to accept or reject the command.
        /// </summary>
        /// <param name="session">
        /// The SMTP session associated with the MAIL FROM command.
        /// </param>
        /// <param name="from">
        /// The parsed MAIL FROM envelope, including reverse-path and any
        /// mail-parameters (SIZE, BODY, AUTH, RET, ENVID, etc.).
        /// </param>
        /// <param name="response">
        /// The server response object used to indicate success or failure
        /// (e.g., 250, 501, 503, 555).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="session"/>, <paramref name="from"/>, or
        /// <paramref name="response"/> is null.
        /// </exception>
        public SMTP_e_MailFrom(SMTP_Session session,SMTP_t_MailFrom from,SMTP_ServerResponse response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(from == null){
                throw new ArgumentNullException(nameof(from));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_pMailFrom = from;
            m_pResponse = response;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the active <see cref="SMTP_Session"/> associated with the
        /// MAIL FROM command that raised this event.
        /// </summary>
        /// <value>
        /// The <see cref="SMTP_Session"/> instance providing connection state,
        /// client information, and server capabilities for the current SMTP
        /// transaction.
        /// </value>
        public SMTP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets the parsed <see cref="SMTP_t_MailFrom"/> envelope associated with
        /// the MAIL FROM command that raised this event.
        /// </summary>
        /// <value>
        /// The <see cref="SMTP_t_MailFrom"/> instance containing the reverse-path
        /// and any MAIL FROM parameters (SIZE, BODY, AUTH, RET, ENVID, etc.)
        /// supplied by the client.
        /// </value>
        public SMTP_t_MailFrom MailFrom
        {
            get{ return m_pMailFrom; }
        }

        /// <summary>
        /// Gets or sets the <see cref="SMTP_ServerResponse"/> associated with the
        /// MAIL FROM command that raised this event.
        /// </summary>
        /// <value>
        /// The <see cref="SMTP_ServerResponse"/> instance used to indicate the
        /// server's reply to the MAIL FROM command (e.g., 250, 501, 503, 555).
        /// </value>
        /// <exception cref="ArgumentNullException">
        /// Thrown when attempting to assign a <c>null</c> value.
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
