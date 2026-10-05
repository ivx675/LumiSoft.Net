using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LumiSoft.Net.POP3.Server
{
    /// <summary>
    /// Provides event data for the <c>GetMessageStreamAsync</c> POP3 server
    /// event. A <see cref="POP3_e_GetMessageStream"/> instance is created when
    /// the server requires a readable RFC‑822 message stream for a specific
    /// message, typically during handling of the <c>RETR</c> or <c>TOP</c>
    /// commands.
    /// <para>
    /// The <see cref="Message"/> property identifies the server‑side message
    /// descriptor, while the event handler is responsible for assigning a
    /// readable stream to <see cref="MessageStream"/>. The POP3 server will
    /// consume the stream according to command semantics: <c>RETR</c> reads the
    /// entire message, whereas <c>TOP</c> reads headers and a limited number of
    /// body lines.
    /// </para>
    /// <para>
    /// If the message does not exist or cannot be opened, the handler must
    /// leave <see cref="MessageStream"/> set to <c>null</c>. In this case the
    /// server will return an appropriate <c>-ERR</c> response to the client.
    /// </para>
    /// </summary>
    public class POP3_e_GetMessageStream : EventArgs
    {
        private POP3_Session       m_pSession;
        private POP3_ServerMessage m_pMessage;
        private Stream?            m_pStream   = null;

        /// <summary>
        /// Initializes a new <see cref="POP3_e_GetMessageStream"/> instance for the
        /// specified POP3 session and message. This event‑args object is used when
        /// the server requires a readable RFC‑822 message stream, typically in
        /// response to <c>RETR</c> or <c>TOP</c> commands.
        /// <para>
        /// The session identifies the active POP3 transaction, while the
        /// <see cref="POP3_ServerMessage"/> instance provides the metadata needed
        /// to locate and open the message content. The event handler is expected
        /// to supply a stream containing the full message; the POP3 server will
        /// read from the stream according to command semantics.
        /// </para>
        /// </summary>
        /// <param name="session">
        /// The POP3 session for which the message stream is being requested.
        /// This value must not be null.
        /// </param>
        /// <param name="message">
        /// The server‑side message descriptor identifying the message whose
        /// content should be streamed. This value must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> or <paramref name="message"/>
        /// is null.
        /// </exception>
        internal POP3_e_GetMessageStream(POP3_Session session,POP3_ServerMessage message)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(message == null){
                throw new ArgumentNullException(nameof(message));
            }

            m_pSession = session;
            m_pMessage = message;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the <see cref="POP3_ServerMessage"/> instance that describes the
        /// message for which a stream is being requested. This object contains the
        /// server‑side metadata associated with the message, such as its sequence
        /// number, size, and storage reference.
        /// <para>
        /// The POP3 server uses this descriptor to locate and open the underlying
        /// RFC‑822 message content when handling <c>RETR</c> or <c>TOP</c> commands.
        /// The event handler assigned to <c>GetMessageStreamAsync</c> is expected
        /// to use this metadata to provide a readable stream containing the full
        /// message.
        /// </para>
        /// </summary>
        public POP3_ServerMessage Message
        {
            get{ return m_pMessage; }
        }

        /// <summary>
        /// Gets or sets the stream that provides the RFC‑822 message content for
        /// the requested POP3 message. The event handler assigned to
        /// <c>GetMessageStreamAsync</c> should supply a readable stream containing
        /// the full message (headers followed by body) when the message exists.
        /// <para>
        /// If the message does not exist or cannot be opened, the handler must
        /// leave this property set to <c>null</c>. In that case the POP3 server
        /// will return an appropriate <c>-ERR</c> response to the client.
        /// </para>
        /// <para>
        /// The stream must contain the message in canonical CRLF format. The POP3
        /// server performs dot‑stuffing and multi‑line termination when sending
        /// data to the client. The server will dispose the stream once message
        /// transmission is complete.
        /// </para>
        /// </summary>
        public Stream? MessageStream
        {
            get{ return m_pStream; }

            set{ m_pStream = value; }
        }

        #endregion
    }
}
