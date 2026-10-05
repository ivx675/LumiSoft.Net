using LumiSoft.Net.SMTP;
using LumiSoft.Net.SMTP.Server;
using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3.Server
{
    /// <summary>
    /// Provides event data for the <c>StartedAsync</c> event raised when a new
    /// POP3 session is established, before the server sends its initial
    /// greeting banner to the client.
    /// <para>
    /// The event supplies both the newly created <see cref="POP3_Session"/>
    /// and the server-generated <see cref="POP3_ServerResponse"/> that will
    /// be transmitted as the POP3 greeting. Event handlers may inspect or
    /// replace the response to customize the greeting before it is sent.
    /// </para>
    /// </summary>
    public class POP3_e_Started : EventArgs
    {
        private POP3_Session        m_pSession;
        private POP3_ServerResponse m_pResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="POP3_e_Started"/> class,
        /// providing event data for the POP3 <c>StartedAsync</c> event raised when a
        /// new POP3 session is established, before the server sends its initial
        /// greeting banner to the client.
        /// <para>
        /// The event supplies both the newly created <see cref="POP3_Session"/>
        /// and the server-generated <see cref="POP3_ServerResponse"/> that will
        /// be transmitted as the POP3 greeting. Event handlers may inspect or
        /// replace the response before it is sent.
        /// </para>
        /// </summary>
        /// <param name="session">
        /// The <see cref="POP3_Session"/> associated with the incoming POP3
        /// connection.
        /// </param>
        /// <param name="response">
        /// The initial <see cref="POP3_ServerResponse"/> that will be sent to
        /// the client as the POP3 greeting. Handlers may modify or replace this
        /// value before transmission.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="session"/> or <paramref name="response"/>
        /// is <c>null</c>.
        /// </exception>
        internal POP3_e_Started(POP3_Session session,POP3_ServerResponse response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_pResponse = response;
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
        /// Gets or sets the <see cref="POP3_ServerResponse"/> that will be sent
        /// to the client as the initial POP3 server greeting.
        /// <para>
        /// The greeting is transmitted immediately after the TCP connection is
        /// established and consists of a POP3 status indicator (<c>+OK</c> or
        /// <c>-ERR</c>) followed by optional textual information. Event handlers
        /// may replace this response to customize the greeting before it is
        /// delivered to the client.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a <c>null</c> value is assigned.
        /// </exception>
        public POP3_ServerResponse Response
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
