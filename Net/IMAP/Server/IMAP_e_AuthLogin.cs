using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides the credentials supplied by the client during an IMAP LOGIN command.
    /// This event-args class is used with <see cref="IMAP_Session.AuthLoginAsync"/>
    /// to validate user credentials before the session enters the authenticated state.
    /// </summary>
    public class IMAP_e_AuthLogin : EventArgs
    {
        private IMAP_Session        m_pSession;
        private string              m_User      = "";
        private string              m_Password  = "";
        private IMAP_r_ServerStatus m_pResponse;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_AuthLogin"/> instance containing the
        /// credentials supplied by the client in an IMAP LOGIN command, along with the
        /// associated session context and server response object.
        /// </summary>
        /// <param name="session">
        /// The IMAP session in which the LOGIN command is being processed.
        /// This value must not be null.
        /// </param>
        /// <param name="user">
        /// The user name supplied by the client in the LOGIN command.
        /// This value must not be null.
        /// </param>
        /// <param name="password">
        /// The password supplied by the client in the LOGIN command.
        /// This value must not be null.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the LOGIN result to the client.
        /// This value must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>, <paramref name="user"/>,
        /// <paramref name="password"/>, or <paramref name="response"/> is null.
        /// </exception>
        internal IMAP_e_AuthLogin(IMAP_Session session,string user,string password,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(user == null){
                throw new ArgumentNullException(nameof(user));
            }
            if(password == null){
                throw new ArgumentNullException(nameof(password));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_User      = user;
            m_Password  = password;
            m_pResponse = response;
        }


        #region Properties implementataion

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
        /// Gets the user name supplied in the IMAP LOGIN command for this session.
        /// </summary>
        public string UserName
        {
            get{ return m_User; }
        }

        /// <summary>
        /// Gets the password supplied in the IMAP LOGIN command for this session.
        /// </summary>
        public string Password
        {
            get{ return m_Password; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be returned to
        /// the client for this LOGIN command. Event handlers must assign a successful
        /// response when the supplied credentials are valid; otherwise the server will
        /// treat the login attempt as failed and the session will remain in the
        /// non-authenticated state.
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

        #endregion
    }
}
