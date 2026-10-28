using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.CopyAsync"/>
    /// event. This event allows the application to copy one or more
    /// messages from a source mailbox to a destination mailbox.
    /// </summary>
    public class IMAP_e_Copy : EventArgs
    {
        private IMAP_Session        m_pSession;
        private IMAP_r_ServerStatus m_pResponse;
        private bool                m_IsUidCopy;
        private IMAP_t_SeqSet       m_pSeqSet;
        private string              m_SourceFolder;
        private string              m_TargetFolder;
        private IMAP_MessageInfo[]  m_pMessages;

         /// <summary>
        /// Initializes a new <see cref="IMAP_e_Copy"/> instance containing
        /// information about the COPY operation requested by the client.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the COPY command.
        /// </param>
        /// <param name="isUidCopy">
        /// Specifies whether the operation originated from a UID COPY
        /// command. When <see langword="true"/>, the supplied sequence
        /// set is interpreted as UIDs rather than message sequence
        /// numbers.
        /// </param>
        /// <param name="seqSet">
        /// The sequence set supplied by the client.
        /// </param>
        /// <param name="sourceFolder">
        /// The mailbox containing the source messages.
        /// </param>
        /// <param name="targetFolder">
        /// The destination mailbox to which the messages will be copied.
        /// </param>
        /// <param name="messagesInfo">
        /// The messages resolved from the supplied sequence set.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="seqSet"/>,
        /// <paramref name="sourceFolder"/>,
        /// <paramref name="targetFolder"/>,
        /// <paramref name="messagesInfo"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Copy(IMAP_Session session,bool isUidCopy,IMAP_t_SeqSet seqSet,string sourceFolder,string targetFolder,IMAP_MessageInfo[] messagesInfo,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }
            if(sourceFolder == null){
                throw new ArgumentNullException(nameof(sourceFolder));
            }
            if(targetFolder == null){
                throw new ArgumentNullException(nameof(targetFolder));
            }
            if(messagesInfo == null){
                throw new ArgumentNullException(nameof(messagesInfo));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession     = session;
            m_pResponse    = response;
            m_IsUidCopy    = isUidCopy;
            m_pSeqSet      = seqSet;
            m_SourceFolder = sourceFolder;
            m_TargetFolder = targetFolder;
            m_pMessages    = messagesInfo;
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
        /// Gets or sets the IMAP server response describing the outcome
        /// of the COPY operation.
        /// </summary>
        /// <remarks>
        /// Event handlers should assign an appropriate OK or NO response.
        /// Servers supporting the UIDPLUS extension (RFC 4315) may include
        /// a <see cref="IMAP_t_orc_CopyUid"/> optional response code in a
        /// successful response.
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
        /// Gets whether the operation originated from a UID COPY command.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/>, the client issued a UID COPY command
        /// and the supplied sequence set was interpreted as message UIDs.
        /// When <see langword="false"/>, the client issued a COPY command
        /// and the sequence set was interpreted as message sequence numbers.
        /// </remarks>
        public bool IsUidCopy 
        {
            get { return m_IsUidCopy; }
        }

        /// <summary>
        /// Gets the sequence set supplied by the client.
        /// </summary>
        /// <remarks>
        /// The interpretation of the sequence set depends on the value of
        /// <see cref="IsUidCopy"/>.
        /// <para>
        /// When <see cref="IsUidCopy"/> is <see langword="false"/>, the
        /// sequence set identifies messages by sequence number as specified
        /// by the COPY command.
        /// </para>
        /// <para>
        /// When <see cref="IsUidCopy"/> is <see langword="true"/>, the
        /// sequence set identifies messages by UID as specified by the UID
        /// COPY command.
        /// </para>
        /// <para>
        /// The messages resolved from this sequence set are available through
        /// the <see cref="Messages"/> property.
        /// </para>
        /// </remarks>
        public IMAP_t_SeqSet SequenceSet
        {
            get{ return m_pSeqSet; }
        }

        /// <summary>
        /// Gets the mailbox containing the source messages.
        /// </summary>
        public string SourceFolder
        {
            get{ return m_SourceFolder; }
        }

        /// <summary>
        /// Gets the destination mailbox to which the messages will be
        /// copied.
        /// </summary>
        public string TargetFolder
        {
            get{ return m_TargetFolder; }
        }

        /// <summary>
        /// Gets the messages selected for copying.
        /// </summary>
        /// <remarks>
        /// The messages are resolved from the supplied sequence set and
        /// contain the cached metadata available to the current IMAP
        /// session, including the application-defined message identifier,
        /// UID, flags, size, and INTERNALDATE.
        /// </remarks>
        public IMAP_MessageInfo[] Messages
        {
            get{ return m_pMessages; }
        }

        #endregion
    }
}
