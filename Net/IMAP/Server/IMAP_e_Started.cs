using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides event data for the <c>Started</c> event raised when a new
    /// IMAP session is established, before the server sends its initial
    /// greeting response to the client.
    /// <para>
    /// The event supplies both the newly created <see cref="IMAP_Session"/>
    /// and the server-generated <see cref="IMAP_r_u_ServerStatus"/> that
    /// will be transmitted as the IMAP greeting. Event handlers may inspect
    /// or replace the response to customize the greeting before it is sent.
    /// </para>
    /// </summary>
    public class IMAP_e_Started : EventArgs
    {
        private IMAP_Session          m_pSession;
        private IMAP_r_u_ServerStatus m_pResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_e_Started"/> class,
        /// providing event data for the IMAP <c>Started</c> event raised when a
        /// new IMAP session is established, before the server sends its initial
        /// greeting response to the client.
        /// <para>
        /// The event supplies both the newly created <see cref="IMAP_Session"/>
        /// and the server-generated <see cref="IMAP_r_u_ServerStatus"/> that
        /// will be transmitted as the IMAP greeting. Event handlers may inspect
        /// or replace the response before it is sent.
        /// </para>
        /// </summary>
        /// <param name="session">
        /// The <see cref="IMAP_Session"/> associated with the incoming IMAP
        /// connection.
        /// </param>
        /// <param name="response">
        /// The initial <see cref="IMAP_r_u_ServerStatus"/> that will be sent to
        /// the client as the IMAP greeting. Handlers may modify or replace this
        /// value before transmission.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="session"/> or <paramref name="response"/>
        /// is <c>null</c>.
        /// </exception>

        internal IMAP_e_Started(IMAP_Session session,IMAP_r_u_ServerStatus response)
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
        /// Gets the <see cref="IMAP_Session"/> associated with the newly
        /// established IMAP connection.
        /// <para>
        /// The session object encapsulates the state and behavior of the IMAP
        /// connection, including authentication, mailbox selection, message
        /// access, and command processing for the duration of the client session.
        /// </para>
        /// </summary>
        /// <value>
        /// The active <see cref="IMAP_Session"/> instance representing the
        /// current IMAP connection.
        /// </value>

        public IMAP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_u_ServerStatus"/> that will be
        /// sent to the client as the initial IMAP server greeting.
        /// <para>
        /// The greeting is transmitted immediately after the TCP connection is
        /// established and typically consists of an IMAP status response such as
        /// <c>OK</c>, <c>NO</c>, or <c>BAD</c>, accompanied by human-readable
        /// text and optional response codes. Event handlers may replace this
        /// response to customize the greeting before it is delivered to the
        /// client.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a <c>null</c> value is assigned.
        /// </exception>

        public IMAP_r_u_ServerStatus Response
        {
            get{ return m_pResponse; }

            set{ m_pResponse = value; }
        }

        #endregion
    }
}
