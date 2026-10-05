using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server 
{
    /// <summary>
    /// Provides data for the <c>MessageStoringBeginAsync</c> event, which is raised
    /// when the SMTP session begins receiving message content via the DATA or BDAT
    /// command. The event allows the application to supply a writable stream where
    /// the incoming message bytes will be stored.
    /// </summary>
    /// <remarks>
    /// The event is raised once per message, before any DATA body lines or BDAT
    /// chunks are processed. The application may assign a custom writable stream
    /// to <see cref="StoreStream"/> to control how the message is stored.
    ///
    /// If the application does not assign a stream, the server will either allocate
    /// a default hybrid memory/file stream or reject the command, depending on the
    /// configured storage policy. BDAT requires a storage sink from the first chunk,
    /// therefore omitting a stream does not prevent the server from accepting BDAT
    /// chunks; the server may internally route them to a discard sink if the message
    /// is to be rejected at BDAT LAST.
    /// </remarks>
    public class SMTP_e_MessageStoringBegin : EventArgs
    {
        private SMTP_Session m_pSession;
        private Stream?      m_pStoreStream = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_e_MessageStoringBegin"/> class
        /// using the specified SMTP session.
        /// </summary>
        /// <param name="session">
        /// The SMTP session associated with the message‑storing operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> is <c>null</c>.
        /// </exception>
        public SMTP_e_MessageStoringBegin(SMTP_Session session)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }

            m_pSession = session;
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
        /// Gets or sets the writable stream into which the incoming message content
        /// will be stored. The application may assign any writable <see cref="Stream"/>
        /// instance (memory, file, database, etc.).
        /// </summary>
        /// <remarks>
        /// This property must not be set to <c>null</c>. If the application does not
        /// assign a stream during <c>MessageStoringBeginAsync</c>, the server will
        /// automatically allocate a default hybrid memory/file stream to ensure
        /// correct handling of both DATA and BDAT transports.
        ///
        /// BDAT requires a storage sink from the first chunk, therefore rejecting
        /// the command when no stream is provided is not permitted by RFC 3030.
        /// The server will always allocate a fallback stream if none is supplied.
        /// </remarks>
        public Stream? StoreStream
        {
            get { return m_pStoreStream; }

            set { 
                if(value == null){
                    throw new ArgumentNullException("value");
                }

                m_pStoreStream = value; 
            }
        }

        #endregion
    }
}
