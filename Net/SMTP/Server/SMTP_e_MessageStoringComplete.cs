using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server 
{
    /// <summary>
    /// Provides data for the <c>MessageStoringCompletedAsync</c> event, which is
    /// raised when the SMTP session has finished receiving all message content
    /// via the DATA or BDAT command. The event supplies the completed message
    /// stream and allows the application to override the final SMTP response
    /// returned to the client.
    /// </summary>
    /// <remarks>
    /// This event is raised after the terminating DATA period (<c>.</c>) or the
    /// final BDAT chunk marked with <c>LAST</c>. At this point the message stream
    /// contains the full message data and is ready for inspection, storage, or
    /// further processing.
    ///
    /// The <see cref="Response"/> property contains the server's default final
    /// reply (typically <c>250 OK</c>), but event handlers may replace it with a
    /// custom <see cref="SMTP_ServerResponse"/> instance to reject the message or
    /// to provide alternative success or failure text.
    /// </remarks>
    public class SMTP_e_MessageStoringComplete : EventArgs
    {
        private SMTP_Session        m_pSession;
        private Stream              m_pStream;
        private SMTP_ServerResponse m_pResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_e_MessageStoringComplete"/> class
        /// using the specified SMTP session, the completed message stream, and the final
        /// server response returned to the client after DATA or BDAT processing.
        /// </summary>
        /// <param name="session">
        /// The SMTP session associated with the completed message‑storing operation.
        /// </param>
        /// <param name="stream">
        /// The stream containing the full message data that was received via DATA or BDAT.
        /// </param>
        /// <param name="response">
        /// The final SMTP server response that was sent to the client upon successful
        /// completion of the message‑storing operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>, <paramref name="stream"/>, or
        /// <paramref name="response"/> is <c>null</c>.
        /// </exception>
        public SMTP_e_MessageStoringComplete(SMTP_Session session,Stream stream,SMTP_ServerResponse response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_pStream   = stream;
            m_pResponse = response;
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
        /// Gets the stream containing the complete message data that was received
        /// via the DATA or BDAT command.
        /// </summary>
        /// <remarks>
        /// The stream is fully populated at the time this event is raised. The
        /// application may read from the stream to inspect, store, or process the
        /// received message. The stream remains owned by the SMTP server and will
        /// be disposed after the event handler returns unless the application
        /// explicitly takes ownership.
        /// </remarks>
        public Stream Stream
        {
            get { return m_pStream; }
        }

        /// <summary>
        /// Gets or sets the SMTP server response that will be sent to the client
        /// after the message‑storing operation completes. Applications may modify
        /// this value to reject the message or to provide a custom success or
        /// failure response.
        /// </summary>
        /// <remarks>
        /// The initial value reflects the server's default response for the DATA or
        /// BDAT transaction (typically <c>250 OK</c>). Event handlers may replace
        /// this response with any valid <see cref="SMTP_ServerResponse"/> instance
        /// to override the final status returned to the client.
        /// 
        /// Assigning <c>null</c> is not permitted.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when attempting to assign <c>null</c> to this property.
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
