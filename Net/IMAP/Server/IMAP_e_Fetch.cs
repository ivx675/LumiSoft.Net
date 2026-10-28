using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Represents a callback used by <see cref="IMAP_e_Fetch"/> to supply
    /// FETCH data for a message to the IMAP session.
    /// </summary>
    /// <param name="message">
    /// The message for which FETCH data is being supplied.
    /// </param>
    /// <param name="messageStream">
    /// The complete RFC 5322 message stream, or <see langword="null"/>
    /// if the current FETCH request does not require message content.
    /// </param>
    /// <param name="envelope">
    /// The parsed message envelope, or <see langword="null"/> if the
    /// current FETCH request does not require envelope data.
    /// </param>
    /// <param name="bodyStructure">
    /// The parsed MIME body structure, or <see langword="null"/> if the
    /// current FETCH request does not require body structure data.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// This callback is invoked by <see cref="IMAP_e_Fetch.AddDataAsync"/>
    /// whenever a FETCH event handler supplies data for a message. The
    /// IMAP session uses the provided information to generate the
    /// corresponding FETCH response.
    /// </remarks>
    internal delegate Task FetchDataAsyncCallback(
        IMAP_MessageInfo message,
        Stream? messageStream,
        IMAP_t_Envelope? envelope,
        IMAP_t_BodyStructure? bodyStructure
    );

    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.FetchAsync"/>
    /// event.
    /// </summary>
    /// <remarks>
    /// This event allows an application to supply message data for
    /// IMAP FETCH requests.
    /// <para>
    /// The IMAP session determines which information is required to
    /// satisfy the FETCH request and exposes the requirements through
    /// the <see cref="RequiresMessageStream"/>,
    /// <see cref="RequiresEnvelope"/>, and
    /// <see cref="RequiresBodyStructure"/> properties.
    /// </para>
    /// <para>
    /// Event handlers should call <see cref="AddDataAsync"/> for each
    /// message that is to be included in the FETCH response.
    /// </para>
    /// <para>
    /// A handler may supply:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// The complete RFC 5322 message stream.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// A parsed <see cref="IMAP_t_Envelope"/> instance.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// A parsed <see cref="IMAP_t_BodyStructure"/> instance.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Envelope and body structure information may be returned from
    /// application caches to avoid reparsing the message stream for
    /// every FETCH operation.
    /// </para>
    /// <para>
    /// Some FETCH requests require the fetched messages to be marked
    /// with the <c>\Seen</c> flag. This requirement is exposed through
    /// the <see cref="SetSeen"/> property. When the property is
    /// <see langword="true"/>, the event handler is responsible for
    /// persisting the corresponding flag update in the message store.
    /// </para>
    /// </remarks>
    public class IMAP_e_Fetch : EventArgs
    {

        private IMAP_r_ServerStatus    m_pResponse;
        private FetchDataAsyncCallback m_pAddDataCallback;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_Fetch"/> instance containing
        /// information about a FETCH operation.
        /// </summary>
        /// <param name="session">
        /// The IMAP session processing the FETCH command.
        /// </param>
        /// <param name="messages">
        /// The messages included in the FETCH operation.
        /// </param>
        /// <param name="requireMessageStream">
        /// Specifies whether the FETCH request requires access to the
        /// complete message stream.
        /// </param>
        /// <param name="requireEnvelope">
        /// Specifies whether the FETCH request requires envelope data.
        /// </param>
        /// <param name="requireBodyStructure">
        /// Specifies whether the FETCH request requires MIME body structure
        /// data.
        /// </param>
        /// <param name="setSeen">
        /// Specifies whether the FETCH operation requires the fetched
        /// messages to be marked with the <c>\Seen</c> flag.
        /// </param>
        /// <param name="response">
        /// The default server response associated with the FETCH operation.
        /// </param>
        /// <param name="addDataCallback">
        /// Callback invoked when the event handler supplies FETCH data for a
        /// message through <see cref="AddDataAsync"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="messages"/>,
        /// <paramref name="response"/>, or
        /// <paramref name="addDataCallback"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Event handlers should use the requirement flags to determine what
        /// information must be supplied for each message. If
        /// <paramref name="setSeen"/> is <see langword="true"/>, the event
        /// handler is responsible for persisting the corresponding
        /// <c>\Seen</c> flag update in the underlying message store.
        /// </remarks>
        internal IMAP_e_Fetch(
            IMAP_Session session,
            IMAP_MessageInfo[] messages,
            bool requireMessageStream,
            bool requireEnvelope,
            bool requireBodyStructure,
            bool setSeen,
            IMAP_r_ServerStatus response,
            FetchDataAsyncCallback addDataCallback)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(messages == null){
                throw new ArgumentNullException(nameof(messages));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }
            if(addDataCallback == null){
                throw new ArgumentNullException(nameof(addDataCallback));
            }

            Session               = session;
            Messages              = messages;
            m_pResponse           = response;
            RequiresMessageStream = requireMessageStream;
            RequiresEnvelope      = requireEnvelope;
            RequiresBodyStructure = requireBodyStructure;
            SetSeen               = setSeen;
            m_pAddDataCallback    = addDataCallback;
        }


        #region method AddData

        /// <summary>
        /// Adds FETCH data for the specified message.
        /// </summary>
        /// <param name="message">
        /// The message for which FETCH data is being supplied.
        /// </param>
        /// <param name="messageStream">
        /// The complete RFC 5322 message stream, or
        /// <see langword="null"/> if the current FETCH request does not
        /// require message content.
        /// </param>
        /// <param name="envelope">
        /// The parsed message envelope, or
        /// <see langword="null"/> if the current FETCH request does not
        /// require envelope data.
        /// </param>
        /// <param name="bodyStructure">
        /// The parsed MIME body structure, or
        /// <see langword="null"/> if the current FETCH request does not
        /// require body structure data.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="message"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when required FETCH data has not been supplied. For
        /// example, when <see cref="RequiresMessageStream"/>,
        /// <see cref="RequiresEnvelope"/>, or
        /// <see cref="RequiresBodyStructure"/> is
        /// <see langword="true"/> and the corresponding parameter is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Event handlers should call this method once for each message
        /// included in the FETCH response.
        /// <para>
        /// The IMAP session immediately processes the supplied data and
        /// generates the corresponding FETCH response data.
        /// </para>
        /// <para>
        /// If a non-null <paramref name="messageStream"/> is supplied,
        /// the stream must remain valid for the duration of this method
        /// call. When the returned task completes, the IMAP session has
        /// finished reading the stream and the caller may safely dispose
        /// it.
        /// </para>
        /// <para>
        /// When <see cref="SetSeen"/> is <see langword="true"/>, the event
        /// handler should ensure that the supplied message is marked with
        /// the <c>\Seen</c> flag in the underlying message store before
        /// returning from this method.
        /// </para>
        /// </remarks>
        public async Task AddDataAsync(IMAP_MessageInfo message,Stream? messageStream,IMAP_t_Envelope? envelope,IMAP_t_BodyStructure? bodyStructure)
        {
            ArgumentNullException.ThrowIfNull(message);

            if(RequiresMessageStream && messageStream == null){
                throw new InvalidOperationException("Message stream is required.");
            }
            if(messageStream != null && !messageStream.CanRead){
                throw new ArgumentException("Message stream must support reading.",nameof(messageStream));
            }
            if(messageStream != null && !messageStream.CanSeek){
                throw new ArgumentException("Message stream must support seeking.",nameof(messageStream));
            }

            if(RequiresEnvelope && envelope == null){
                throw new InvalidOperationException("Envelope is required.");
            }

            if(RequiresBodyStructure && bodyStructure == null){
                throw new InvalidOperationException("Body structure is required.");
            }

            await m_pAddDataCallback(message,messageStream,envelope,bodyStructure);
        }

        #endregion


        #region Properties impelementation

        /// <summary>
        /// Gets the IMAP session associated with this FETCH operation.
        /// </summary>
        public IMAP_Session Session
        {
            get;
        }

        /// <summary>
        /// Gets or sets the server response for this FETCH operation.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public IMAP_r_ServerStatus Response
        {
            get{ return m_pResponse; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException("value");
                }

                m_pResponse = value; 
            }
        }

        /// <summary>
        /// Gets a value indicating whether the FETCH request requires
        /// access to the complete RFC 5322 message stream.
        /// </summary>
        /// <remarks>
        /// Event handlers must provide a message stream when this
        /// property is <see langword="true"/>.
        /// </remarks>
        public bool RequiresMessageStream
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the FETCH request requires
        /// envelope data.
        /// </summary>
        /// <remarks>
        /// Event handlers should provide an
        /// <see cref="IMAP_t_Envelope"/> instance when this property
        /// is <see langword="true"/>. Implementations may return a
        /// cached envelope to avoid reparsing the message.
        /// </remarks>
        public bool RequiresEnvelope
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the FETCH request requires
        /// MIME body structure data.
        /// </summary>
        /// <remarks>
        /// Event handlers should provide an
        /// <see cref="IMAP_t_BodyStructure"/> instance when this
        /// property is <see langword="true"/>. Implementations may
        /// return a cached body structure to avoid reparsing the
        /// message stream.
        /// </remarks>
        public bool RequiresBodyStructure
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the fetched messages should be
        /// marked with the <c>\Seen</c> flag.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/>, the event handler should persist
        /// the corresponding message state change.
        /// </remarks>
        public bool SetSeen
        {
            get;
        }

        /// <summary>
        /// Gets the messages included in this FETCH operation.
        /// </summary>
        /// <remarks>
        /// The event handler should call <see cref="AddDataAsync"/>
        /// for each message that is to be included in the FETCH
        /// response.
        /// </remarks>
        public IMAP_MessageInfo[] Messages
        {
            get;
        }

        #endregion
    }
}
