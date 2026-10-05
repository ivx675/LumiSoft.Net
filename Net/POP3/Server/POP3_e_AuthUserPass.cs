using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3.Server
{
    /// <summary>
    /// Provides the USER/PASS credentials supplied by the client during POP3 authentication.
    /// This event‑args class is used with <see cref="POP3_Session.AuthUserPassAsync"/> to perform
    /// USER/PASS validation before the session enters the TRANSACTION state.
    /// </summary>
    public class POP3_e_AuthUserPass : EventArgs
    {
        private POP3_Session        m_pSession;
        private string              m_UserName        = "";
        private string              m_Password        = "";
        private POP3_ServerResponse m_pResponse;

        /// <summary>
        /// Initializes a new <see cref="POP3_e_AuthUserPass"/> instance containing the
        /// USER/PASS credentials supplied by the client during POP3 authentication, along
        /// with the associated session context and server response object.
        /// </summary>
        /// <param name="session">
        /// The POP3 session in which the USER/PASS authentication attempt is occurring.
        /// This value must not be null.
        /// </param>
        /// <param name="userName">
        /// The user name provided in the USER command. This value must not be null or empty.
        /// </param>
        /// <param name="password">
        /// The password provided in the PASS command. This value must not be null or empty.
        /// </param>
        /// <param name="response">
        /// The server response object used to send the authentication result back to the
        /// client. This value must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> or <paramref name="response"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="userName"/> or <paramref name="password"/> is null or empty.
        /// </exception>
        internal POP3_e_AuthUserPass(POP3_Session session,string userName,string password,POP3_ServerResponse response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(string.IsNullOrEmpty(userName)){
                throw new ArgumentException("Argument 'userName' is null or empty.",nameof(userName));
            }
            if(string.IsNullOrEmpty(password)){
                throw new ArgumentException("Argument 'password' is null or empty.",nameof(password));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_UserName  = userName;
            m_Password  = password;
            m_pResponse = response;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the POP3 session associated with this event. The session provides access
        /// to connection state, authentication status, and server-side context required
        /// to process the current POP3 command.
        /// </summary>
        public POP3_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets the user name supplied during USER/PASS authentication for this POP3 session.
        /// </summary>
        public string UserName
        {
            get{ return m_UserName; }
        }

        /// <summary>
        /// Gets the password supplied during USER/PASS authentication for this POP3 session.
        /// </summary>
        public string Password
        {
            get{ return m_Password; }
        }

        /// <summary>
        /// Gets or sets the <see cref="POP3_ServerResponse"/> that will be returned to
        /// the client for this USER/PASS authentication attempt. Event handlers must
        /// assign a successful response (for example, <c>+OK Authenticated successfully</c>)
        /// if authentication is to succeed; otherwise the server will treat the attempt
        /// as failed and remain in the AUTHORIZATION state.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
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
