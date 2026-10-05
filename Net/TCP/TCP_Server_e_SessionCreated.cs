using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.TCP
{
    /// <summary>
    /// Provides data for the <c>SessionCreatedAsync</c>
    /// events. This event argument contains the newly created TCP session instance.
    /// </summary>
    /// <typeparam name="T">
    /// The session type used by the server. Must derive from
    /// <see cref="TCP_ServerSession"/> and have a parameterless constructor.
    /// </typeparam>
    public class TCP_Server_e_SessionCreated<T> : EventArgs where T : TCP_ServerSession,new()
    {
        private T m_pSession;

        /// <summary>
        /// Initializes a new instance of the <see cref="TCP_Server_e_SessionCreated{T}"/>
        /// event argument class using the specified session.
        /// </summary>
        /// <param name="session">
        /// The newly created TCP session associated with this event.
        /// </param>
        internal TCP_Server_e_SessionCreated(T session)
        {
            m_pSession = session;
        }


        #region Properties Implementation

        /// <summary>
        /// Gets the TCP session associated with this event.
        /// </summary>
        public T Session
        {
            get{ return m_pSession; }
        }

        #endregion

    }
}
