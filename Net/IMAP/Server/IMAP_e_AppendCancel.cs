using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server 
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.AppendCancelAsync"/>
    /// event. This event is raised when an APPEND operation is aborted
    /// before completion and the message cannot be stored successfully.
    /// Event handlers may use this event to release resources and remove
    /// any temporary data created during the APPEND operation.
    /// </summary>
    public class IMAP_e_AppendCancel : EventArgs
    {
        private IMAP_Session m_pSession;
        private Stream       m_pStream;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_AppendCancel"/> instance.
        /// </summary>
        /// <param name="session">
        /// The IMAP session associated with the APPEND operation.
        /// </param>
        /// <param name="stream">
        /// The stream that was being used to receive the appended message.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_AppendCancel(IMAP_Session session,Stream stream)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            m_pSession = session;
            m_pStream  = stream;
        }


        #region Properties implementation

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
        /// Gets the stream that was used to receive the message being
        /// appended.
        /// </summary>
        /// <remarks>
        /// The stream may contain a partially received message if the APPEND
        /// operation was cancelled before completion. Event handlers may use
        /// this event to perform cleanup of temporary resources associated
        /// with the stream.
        /// </remarks>
        public Stream Stream
        {
            get{ return m_pStream; }
        }

        #endregion
    }
}
