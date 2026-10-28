using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.AppendCompleteAsync"/>
    /// event. This event is raised after the APPEND message literal has
    /// been completely received from the client. Event handlers may
    /// finalize message storage, update mailbox metadata, and set the
    /// final IMAP response that will be returned to the client.
    /// </summary>
    public class IMAP_e_AppendComplete : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private string              m_Folder;
        private string[]            m_pFlags;
        private DateTime?           m_Date      = DateTime.MinValue;
        private int                 m_Size      = 0;
        private Stream              m_pStream;      

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_AppendComplete"/> instance
        /// containing information about the completed APPEND operation.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the APPEND command.
        /// </param>
        /// <param name="folder">
        /// The destination mailbox to which the message was appended.
        /// </param>
        /// <param name="flags">
        /// The flags requested for the appended message.
        /// </param>
        /// <param name="date">
        /// The INTERNALDATE specified by the client, or
        /// <see langword="null"/> if the APPEND command did not specify one.
        /// </param>
        /// <param name="size">
        /// The size, in bytes, of the received message literal.
        /// </param>
        /// <param name="stream">
        /// The stream containing the received message data.
        /// </param>
        /// <param name="response">
        /// The server response that will be returned to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>,
        /// <paramref name="flags"/>,
        /// <paramref name="stream"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_AppendComplete(IMAP_Session session,string folder,string[] flags,DateTime? date,int size,Stream stream,IMAP_r_ServerStatus response)
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
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession  = session;
            m_Folder    = folder;
            m_pFlags    = flags;
            m_Date      = date;
            m_Size      = size;
            m_pStream   = stream;
            m_pResponse = response;
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
        /// Gets or sets the IMAP response that will be returned to the client
        /// when the APPEND command completes.
        /// </summary>
        /// <remarks>
        /// Event handlers are responsible for setting the final response for
        /// the APPEND operation. For successful APPEND operations, servers
        /// supporting the UIDPLUS extension (RFC 4315) should include an
        /// <see cref="IMAP_t_orc_AppendUid"/> optional response code in the
        /// final OK response.
        /// </remarks>
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

        /// <summary>
        /// Gets the destination mailbox to which the message was appended.
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
        /// Gets the INTERNALDATE specified by the client.
        /// </summary>
        /// <remarks>
        /// A value of <see langword="null"/> indicates that the APPEND command
        /// did not specify an INTERNALDATE.
        /// </remarks>
        public DateTime? InternalDate
        {
            get{ return m_Date; }
        }

        /// <summary>
        /// Gets the size, in bytes, of the received message literal.
        /// </summary>
        public int Size
        {
            get{ return m_Size; }
        }

        /// <summary>
        /// Gets the stream containing the received message data.
        /// </summary>
        /// <remarks>
        /// The stream position and lifetime are controlled by the APPEND
        /// implementation. Event handlers may use this stream to finalize
        /// message storage, perform validation, or extract message metadata.
        /// </remarks>
        public Stream Stream
        {
            get{ return m_pStream; }
        }

        #endregion
    }
}
