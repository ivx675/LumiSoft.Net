using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server 
{
    /// <summary>
    /// Provides data for the <c>MessageStoringCancelAsync</c> event, which is raised
    /// when the SMTP session aborts the message‑storing operation before completion.
    /// Cancellation may occur if the client closes the connection, the message size
    /// exceeds the server's configured limit, a DATA line exceeds the permitted
    /// length, or the session times out during message reception.
    /// </summary>
    /// <remarks>
    /// This event is raised after the server has determined that the message cannot
    /// be accepted but before returning the final SMTP response to the client.
    /// 
    /// The <see cref="Stream"/> property contains any message data that was received
    /// prior to cancellation. Depending on the failure condition, the stream may
    /// contain partial content, truncated data, or discarded bytes. The message is
    /// not delivered and does not enter normal processing.
    /// 
    /// Applications may use this event for logging, diagnostics, or inspection of
    /// the partial message content.
    /// </remarks>
    public class SMTP_e_MessageStoringCancel : EventArgs
    {
        private SMTP_Session m_pSession;
        private Stream       m_pStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_e_MessageStoringCancel"/> class
        /// using the specified SMTP session and the stream containing the message data
        /// that was received before the storing operation was cancelled.
        /// </summary>
        /// <param name="session">
        /// The SMTP session associated with the cancelled message‑storing operation.
        /// </param>
        /// <param name="stream">
        /// The stream containing any message data that was received prior to cancellation.
        /// This may contain partial or discarded content depending on the reason for the
        /// cancellation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> or <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        public SMTP_e_MessageStoringCancel(SMTP_Session session,Stream stream)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            m_pSession  = session;
            m_pStream   = stream;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the active <see cref="SMTP_Session"/> associated with the command
        /// that raised this event.  
        /// Provides access to session‑level information such as connection state,
        /// security status, local endpoint, and server capabilities.
        /// </summary>
        /// <value>
        /// The <see cref="SMTP_Session"/> instance that is currently processing
        /// the SMTP command.
        /// </value>
        public SMTP_Session Session
        {
            get{ return m_pSession; }
        }
        
        /// <summary>
        /// Gets the stream containing any message data that was received before the
        /// message‑storing operation was cancelled.
        /// </summary>
        /// <remarks>
        /// The stream may contain partial message content or discarded data depending
        /// on the reason for cancellation (for example, a DATA line exceeding the
        /// permitted length, a BDAT chunk violation, or a server‑side policy rejection).
        /// 
        /// The stream is provided for diagnostic, logging, or inspection purposes.
        /// It does not represent a complete or valid message.
        /// </remarks>
        public Stream Stream
        {
            get { return m_pStream; }
        }

        #endregion
    }
}
