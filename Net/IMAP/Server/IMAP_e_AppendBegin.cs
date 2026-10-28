using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.AppendBeginAsync"/>
    /// event. This event is raised after a valid APPEND command has been
    /// parsed but before the message literal is read from the client.
    /// Event handlers are expected to provide a writable destination stream
    /// for the incoming message or reject the APPEND operation by setting
    /// <see cref="RejectResponse"/>.
    /// </summary>
    public class IMAP_e_AppendBegin : EventArgs
    {
        private IMAP_Session         m_pSession;
        private IMAP_r_ServerStatus? m_pResponse;
        private string               m_Folder;
        private string[]             m_pFlags;
        private DateTime?            m_Date      = DateTime.MinValue;
        private int                  m_Size      = 0;
        private Stream?              m_pStream   = null;      

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_AppendBegin"/> instance
        /// containing the APPEND command parameters supplied by the client.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the APPEND command.
        /// </param>
        /// <param name="folder">
        /// The destination mailbox to which the message will be appended.
        /// </param>
        /// <param name="flags">
        /// The message flags specified by the client.
        /// </param>
        /// <param name="date">
        /// The requested INTERNALDATE value. A value of
        /// <see cref="DateTime.MinValue"/> indicates that the client did
        /// not supply an INTERNALDATE.
        /// </param>
        /// <param name="size">
        /// The size, in bytes, of the incoming message literal.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>, or
        /// <paramref name="flags"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_AppendBegin(IMAP_Session session,string folder,string[] flags,DateTime? date,int size)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(flags == null){
                throw new ArgumentNullException(nameof(flags));
            }

            m_pSession = session;
            m_Folder   = folder;
            m_pFlags   = flags;
            m_Date     = date;
            m_Size     = size;
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
        /// Gets or sets the IMAP response that should be returned to the
        /// client instead of continuing the APPEND operation.
        /// </summary>
        /// <remarks>
        /// Setting this property causes the APPEND operation to be rejected.
        /// Typical responses include mailbox access denied, mailbox not
        /// found, quota exceeded, or other application-specific validation
        /// failures discovered before the message literal is received.
        /// </remarks>
        public IMAP_r_ServerStatus? RejectResponse
        {
            get{ return m_pResponse; }

            set{ m_pResponse = value; }
        }

        /// <summary>
        /// Gets the destination mailbox to which the message will be
        /// appended.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets the flags requested for the appended message.
        /// </summary>
        public string[] Flags
        {
            get{ return m_pFlags; }
        }

        /// <summary>
        /// Gets the INTERNALDATE requested by the client.
        /// </summary>
        /// <remarks>
        /// A value of <see langword="null"/> indicates that the APPEND
        /// command did not specify an INTERNALDATE.
        /// </remarks>
        public DateTime? InternalDate
        {
            get{ return m_Date; }
        }

        /// <summary>
        /// Gets the size, in bytes, of the message literal that will be
        /// transmitted by the client.
        /// </summary>
        public int Size
        {
            get{ return m_Size; }
        }

        /// <summary>
        /// Gets or sets the destination stream into which the APPEND message
        /// literal will be written.
        /// </summary>
        /// <remarks>
        /// A writable stream must be supplied in order for the APPEND operation
        /// to store the incoming message. The IMAP server writes the message
        /// data directly to this stream as it is received from the client.
        /// </remarks>
        public Stream? Stream
        {
            get{ return m_pStream; }

            set{ m_pStream = value; }
        }

        #endregion
    }
}
