using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace LumiSoft.Net.IMAP.Client
{
    /// <summary>
    /// Represents an IMAP mailbox that has been successfully selected via the
    /// <c>SELECT</c> or <c>EXAMINE</c> command. The object exposes all server‑reported
    /// mailbox metadata (such as <c>UIDVALIDITY</c>, <c>FLAGS</c>, <c>PERMANENTFLAGS</c>,
    /// <c>UIDNEXT</c>, <c>UNSEEN</c>, <c>EXISTS</c>, and <c>RECENT</c>) and provides
    /// methods for creating message sets within the selected mailbox.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A selected folder represents the server’s current view of a mailbox. All
    /// metadata is populated from the untagged responses returned during the
    /// <c>SELECT</c> or <c>EXAMINE</c> command and updated automatically through the
    /// IMAP client's global untagged‑response handler as additional <c>EXISTS</c>,
    /// <c>RECENT</c>, or <c>EXPUNGE</c> notifications arrive.
    /// </para>
    /// <para>
    /// The folder maintains a collection of <see cref="IMAP_Client_MessageSet"/>
    /// instances created during the session. Each message set is a static snapshot
    /// of the mailbox state at the time it was fetched and remains valid until the
    /// folder or message-set is disposed.
    /// </para>
    /// <para>
    /// The object does not perform any IMAP communication on its own. All network
    /// operations are delegated to the owning <see cref="IMAP_Client"/> instance.
    /// </para>
    /// <para>
    /// Once <see cref="Dispose"/> is called, the folder becomes invalid and all
    /// message sets it contains are disposed. Any further attempt to access mailbox
    /// metadata or create new message sets will result in an
    /// <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    public class IMAP_Client_SelectedFolder
    {
        private bool                         m_IsDisposed          = false;
        private IMAP_Client                  m_pImap;
        private string                       m_Name                = "";
        private long?                        m_UidValidity         = null;
        private string[]                     m_pFlags              = new string[0];
        private string[]                     m_pPermanentFlags     = new string[0];
        private bool                         m_IsReadOnly          = false;
        private long?                        m_UidNext             = null;
        private int?                         m_FirstUnseen         = null;
        private int                          m_MessagesCount       = 0;
        private int                          m_RecentMessagesCount = 0;
        private List<IMAP_Client_MessageSet> m_pMessageSets;

        /// <summary>
        /// Initializes a new <see cref="IMAP_Client_SelectedFolder"/> instance
        /// representing the mailbox selected via the IMAP <c>SELECT</c> or
        /// <c>EXAMINE</c> command.
        /// </summary>
        /// <param name="imap">
        /// The owning <see cref="IMAP_Client"/> instance that manages all IMAP
        /// communication for this folder.
        /// </param>
        /// <param name="name">
        /// The canonical name of the selected mailbox, provided exactly as returned
        /// by the server. Must not be <c>null</c> or empty.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imap"/> or <paramref name="name"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="name"/> is an empty string.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This constructor is invoked internally by the IMAP client when a mailbox
        /// is successfully selected. It stores the folder name and prepares internal
        /// structures used to track message sets created during the session.
        /// </para>
        /// <para>
        /// No IMAP communication occurs during construction. All mailbox metadata
        /// such as <c>UIDVALIDITY</c>, <c>FLAGS</c>, <c>PERMANENTFLAGS</c>,
        /// <c>UIDNEXT</c>, <c>UNSEEN</c>, <c>EXISTS</c>, and <c>RECENT</c> is applied
        /// later through the client's global untagged response handler.
        /// </para>
        /// <para>
        /// The selected folder remains valid until <see cref="Dispose"/> is called.
        /// After disposal, all operations such as creating message sets or accessing
        /// folder metadata will throw an <see cref="ObjectDisposedException"/>.
        /// </para>
        /// </remarks>
        internal IMAP_Client_SelectedFolder(IMAP_Client imap,string name)
        {
            if(imap == null){
                throw new ArgumentNullException("imap");
            }
            if(name == null){
                throw new ArgumentNullException("name");
            }
            if(name == string.Empty){
                throw new ArgumentException("The argument 'name' value must be specified.","name");
            }

            m_pImap = imap;
            m_Name  = name;

            m_pMessageSets = new List<IMAP_Client_MessageSet>();
        }

        #region method Dispose

        /// <summary>
        /// Releases all resources.
        /// </summary>
        internal void Dispose()
        {
            if(m_IsDisposed){
                return;
            }

            m_IsDisposed = true;

            foreach(IMAP_Client_MessageSet m in m_pMessageSets){
                m.Dispose();
            }
            m_pMessageSets.Clear();
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns this object as human readable string.
        /// </summary>
        /// <returns>Returns this object as human readable string.</returns>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            retVal.AppendLine("Name: "                + this.Name);
            retVal.AppendLine("UidValidity: "         + this.UidValidity);
            retVal.AppendLine("Flags: "               + StringArrayToString(this.Flags));
            retVal.AppendLine("PermanentFlags: "      + StringArrayToString(this.PermanentFlags));
            retVal.AppendLine("IsReadOnly: "          + this.IsReadOnly);
            retVal.AppendLine("UidNext: "             + this.UidNext);
            retVal.AppendLine("FirstUnseen: "         + this.FirstUnseen);
            retVal.AppendLine("MessagesCount: "       + this.MessagesCount);
            retVal.AppendLine("RecentMessagesCount: " + this.RecentMessagesCount);

            return retVal.ToString();
        }

        #endregion

        #region metod CreateMessageSet

        /// <summary>
        /// Synchronously creates a new <see cref="IMAP_Client_MessageSet"/> by
        /// performing an IMAP <c>FETCH</c> operation over the specified sequence set.
        /// This method is a blocking wrapper around
        /// <see cref="CreateMessageSetAsync(bool,IMAP_t_SeqSet,bool,bool,bool,CancellationToken)"/>
        /// and executes the asynchronous operation using the IMAP client's configured
        /// timeout.
        /// </summary>
        /// <param name="uidSeqet">
        /// If <c>true</c>, the <paramref name="seqSet"/> is interpreted as a UID
        /// sequence set and the operation uses <c>UID FETCH</c>.  
        /// If <c>false</c>, the sequence set is interpreted as message sequence
        /// numbers (MSNs) and the operation uses <c>FETCH</c>.
        /// </param>
        /// <param name="seqSet">
        /// The IMAP sequence set identifying the messages to retrieve.
        /// </param>
        /// <param name="preFetchEnvelope">
        /// If <c>true</c>, the <c>ENVELOPE</c> data item is included in the
        /// <c>FETCH</c> request and stored directly in the resulting message objects.
        /// </param>
        /// <param name="preFetchBodyStructure">
        /// If <c>true</c>, the <c>BODYSTRUCTURE</c> data item is included in the
        /// <c>FETCH</c> request and stored directly in the resulting message objects.
        /// </param>
        /// <param name="preFetchGmail">
        /// If <c>true</c>, Gmail‑specific extensions (<c>X-GM-LABELS</c>,
        /// <c>X-GM-MSGID</c>, <c>X-GM-THRID</c>) are included in the <c>FETCH</c>
        /// request and stored directly in the resulting message objects.
        /// </param>
        /// <returns>
        /// A newly constructed <see cref="IMAP_Client_MessageSet"/> containing the
        /// messages returned by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this selected folder instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the <c>FETCH</c> request or violates IMAP
        /// protocol semantics.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks until the underlying asynchronous operation completes.
        /// All IMAP protocol validation, response parsing, and message‑object
        /// construction are performed by the asynchronous implementation.
        /// </para>
        /// <para>
        /// The returned message set is tracked by the selected folder and will be
        /// disposed automatically when the folder is disposed.
        /// </para>
        /// </remarks>
        public IMAP_Client_MessageSet CreateMessageSet(
            bool uidSeqet,
            IMAP_t_SeqSet seqSet,
            bool preFetchEnvelope,
            bool preFetchBodyStructure,
            bool preFetchGmail)
        {
            using var cts = new CancellationTokenSource(m_pImap.Timeout);

            return CreateMessageSetAsync(uidSeqet,seqSet,preFetchEnvelope,preFetchBodyStructure,preFetchGmail,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method CreateMessageSetAsync

        /// <summary>
        /// Asynchronously creates a new <see cref="IMAP_Client_MessageSet"/> by
        /// performing an IMAP <c>FETCH</c> or <c>UID FETCH</c> operation over the
        /// specified sequence set.
        /// </summary>
        /// <param name="uidSeqSet">
        /// If <c>true</c>, the <paramref name="seqSet"/> is interpreted as a UID
        /// sequence set and the operation uses <c>UID FETCH</c>.  
        /// If <c>false</c>, the sequence set is interpreted as message sequence
        /// numbers (MSNs) and the operation uses <c>FETCH</c>.
        /// </param>
        /// <param name="seqSet">
        /// The IMAP sequence set identifying the messages to retrieve.
        /// </param>
        /// <param name="preFetchEnvelope">
        /// If <c>true</c>, the <c>ENVELOPE</c> data item is included in the
        /// <c>FETCH</c> request and stored directly in the resulting message objects.
        /// </param>
        /// <param name="preFetchBodyStructure">
        /// If <c>true</c>, the <c>BODYSTRUCTURE</c> data item is included in the
        /// <c>FETCH</c> request and stored directly in the resulting message objects.
        /// </param>
        /// <param name="preFetchGmail">
        /// If <c>true</c>, Gmail‑specific extensions (<c>X-GM-LABELS</c>,
        /// <c>X-GM-MSGID</c>, <c>X-GM-THRID</c>) are included in the <c>FETCH</c>
        /// request and stored directly in the resulting message objects.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A newly constructed <see cref="IMAP_Client_MessageSet"/> containing the
        /// messages returned by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this selected folder instance has been disposed.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown if the server rejects the <c>FETCH</c> request or violates IMAP
        /// protocol semantics.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The method constructs a new <see cref="IMAP_Client_MessageSet"/> and
        /// populates it using an event‑driven callback that processes each untagged
        /// <c>FETCH</c> response returned by the server.
        /// </para>
        /// <para>
        /// Only the data items explicitly requested via the <paramref name="preFetchEnvelope"/>,
        /// <paramref name="preFetchBodyStructure"/>, and <paramref name="preFetchGmail"/>
        /// parameters are included in the <c>FETCH</c> request. Any metadata not
        /// prefetched remains <c>null</c> in the resulting message objects until
        /// explicitly retrieved via their lazy‑fetch methods.
        /// </para>
        /// <para>
        /// The completed message set is added to the selected folder’s internal
        /// tracking list and will be disposed automatically when the folder is
        /// disposed.
        /// </para>
        /// <para>
        /// No IMAP communication occurs after the <c>FETCH</c> operation completes.
        /// The returned message set is a static snapshot of the mailbox state at the
        /// time of retrieval.
        /// </para>
        /// </remarks>
        public async ValueTask<IMAP_Client_MessageSet> CreateMessageSetAsync(
            bool uidSeqSet,
            IMAP_t_SeqSet seqSet,
            bool preFetchEnvelope,
            bool preFetchBodyStructure,
            bool preFetchGmail,
            CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            IMAP_Client_MessageSet msgSet = new IMAP_Client_MessageSet(m_pImap,this);            

            EventHandler<IMAP_r_u_Fetch> responseCallback = delegate(object? sender,IMAP_r_u_Fetch e){
                long                  uid           = e.Uid?.Uid ?? 0;
                long                  size          = e.Rfc822Size?.Size ?? 0;
                DateTime              internalDate  = e.InternalDate?.Date ?? DateTime.MinValue;
                string[]              flags         = e.Flags?.Flags ?? [];
                IMAP_t_Envelope?      envelope      = e.Envelope?.Envelope;
                IMAP_t_BodyStructure? bosyStructure = e.BodyStructure?.BodyStructure;
                string[]?             gmailLabels   = e.X_GM_LABELS?.Labels;
                long?                 gmailMsgId    = e.X_GM_MSGID?.MsgId;
                long?                 gmailThrId    = e.X_GM_THRID?.ThreadId;

                if(uid > 0){
                    msgSet.Add(uid,size,internalDate,flags,envelope,bosyStructure,gmailLabels,gmailMsgId,gmailThrId);
                }
            };

            List<IMAP_t_Fetch_i> dataItems = new List<IMAP_t_Fetch_i>();
            dataItems.Add(new IMAP_t_Fetch_i_Uid());
            dataItems.Add(new IMAP_t_Fetch_i_Rfc822Size());
            dataItems.Add(new IMAP_t_Fetch_i_Flags());
            dataItems.Add(new IMAP_t_Fetch_i_InternalDate());
            if(preFetchEnvelope){
                dataItems.Add(new IMAP_t_Fetch_i_Envelope());
            }
            if(preFetchBodyStructure){
                dataItems.Add(new IMAP_t_Fetch_i_BodyStructure());
            }
            if(preFetchGmail){
                dataItems.Add(new IMAP_t_Fetch_i_xGmailLabels());
                dataItems.Add(new IMAP_t_Fetch_i_xGmailMsgId());
                dataItems.Add(new IMAP_t_Fetch_i_xGmailThrId());
            }

            if(uidSeqSet){
                await m_pImap.MessagesFetchUidAsync(seqSet,dataItems.ToArray(),responseCallback,null,cancellationToken);
            }
            else{
                await m_pImap.MessagesFetchAsync(seqSet,dataItems.ToArray(),responseCallback,null,cancellationToken);
            }

            m_pMessageSets.Add(msgSet);
                        
            return msgSet;
        }

        #endregion


        #region method RemoveMessageSet

        /// <summary>
        /// Removes specified message-set from message sets list.
        /// </summary>
        /// <param name="msgSet">Message-set to remove.</param>
        internal void RemoveMessageSet(IMAP_Client_MessageSet msgSet)
        {
            if(!this.IsDisposed){
                m_pMessageSets.Remove(msgSet);
            }
        }

        #endregion

        #region method SetUidValidity

        /// <summary>
        /// Sets UidValidity property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetUidValidity(long value)
        {
            m_UidValidity = value;
        }

        #endregion

        #region method SetFlags

        /// <summary>
        /// Sets Flags property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetFlags(string[] value)
        {
            m_pFlags = value;
        }

        #endregion

        #region method SetPermanentFlags

        /// <summary>
        /// Sets PermanentFlags property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetPermanentFlags(string[] value)
        {
            m_pPermanentFlags = value;
        }

        #endregion

        #region method SetReadOnly

        /// <summary>
        /// Sets IsReadOnly property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetReadOnly(bool value)
        {
            m_IsReadOnly = value;
        }

        #endregion

        #region method SetUidNext

        /// <summary>
        /// Sets UidNext property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetUidNext(long value)
        {
            m_UidNext = value;
        }

        #endregion

        #region method SetFirstUnseen

        /// <summary>
        /// Sets FirstUnseen property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetFirstUnseen(int value)
        {
            m_FirstUnseen = value;
        }

        #endregion

        #region method SetMessagesCount

        /// <summary>
        /// Sets MessagesCount property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetMessagesCount(int value)
        {
            m_MessagesCount = value;
        }

        #endregion

        #region method SetRecentMessagesCount

        /// <summary>
        /// Sets RecentMessagesCount property value.
        /// </summary>
        /// <param name="value">Value to set.</param>
        internal void SetRecentMessagesCount(int value)
        {
            m_RecentMessagesCount = value;
        }

        #endregion


        #region method StringArrayToString

        /// <summary>
        /// Coneverts string array to comma separated value.
        /// </summary>
        /// <param name="value">String array.</param>
        /// <returns>Returns string array as comma separated value.</returns>
        private string StringArrayToString(string[] value)
        {
            StringBuilder retVal = new StringBuilder();

            for(int i=0;i<value.Length;i++){
                // Last item.
                if(i == (value.Length - 1)){
                    retVal.Append(value[i]);
                }
                else{
                    retVal.Append(value[i] + ",");
                }
            }

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this <c>IMAP_Client_SelectedFolder</c>
        /// instance has been disposed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <c>true</c>, the folder has released all of its internal resources,
        /// including any <see cref="IMAP_Client_MessageSet"/> instances created during
        /// the session. After disposal, the folder can no longer be used, and any
        /// attempt to access mailbox metadata or create new message sets will result
        /// in an <see cref="ObjectDisposedException"/>.
        /// </para>
        /// <para>
        /// This property allows callers to check the disposal state before performing
        /// operations that require access to the selected mailbox.
        /// </para>
        /// </remarks>
        public bool IsDisposed
        {
            get { return m_IsDisposed; }
        }

        /// <summary>
        /// Gets the canonical name of the currently selected IMAP mailbox.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The mailbox name is the exact string used when the folder was selected
        /// via the <c>SELECT</c> or <c>EXAMINE</c> command. It is stored in decoded
        /// Unicode form, regardless of how the server represented it in the IMAP
        /// response (quoted, literal, or modified UTF‑7).
        /// </para>
        /// <para>
        /// The value does not change during the lifetime of the selected folder
        /// unless a new mailbox is selected. It does not include hierarchy delimiters
        /// or any attribute flags; those are provided separately by the server.
        /// </para>
        /// </remarks>
        public string Name
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_Name; 
            }
        }

        /// <summary>
        /// Gets the <c>UIDVALIDITY</c> value of the selected mailbox, or <c>null</c>
        /// if the server has not reported it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>UIDVALIDITY</c> value is assigned by the IMAP server to indicate
        /// the stability of message UIDs within the mailbox. When a mailbox is
        /// recreated or its UID sequence becomes invalid, the server changes this
        /// value to signal that previously cached UIDs must be discarded.
        /// </para>
        /// <para>
        /// Although RFC 3501 requires servers to send <c>UIDVALIDITY</c> during
        /// <c>SELECT</c> or <c>EXAMINE</c>, some non‑compliant or legacy servers may
        /// omit it. In such cases, the property remains <c>null</c> until explicitly
        /// set by the client’s untagged‑response handler.
        /// </para>
        /// <para>
        /// Accessing this property after the folder has been disposed will throw an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this folder instance has been disposed.
        /// </exception>
        public long? UidValidity
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_UidValidity; 
            }
        }

        /// <summary>
        /// Gets the list of system and server‑defined flags applicable to the
        /// selected mailbox.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>FLAGS</c> response returned during the <c>SELECT</c> or
        /// <c>EXAMINE</c> command describes all message flags that the server
        /// recognizes for the mailbox. This includes the standard IMAP system
        /// flags such as:
        /// <c>\Seen</c>, <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// <c>\Draft</c>, and <c>\Recent</c>.
        /// </para>
        /// <para>
        /// Servers may also define additional implementation‑specific flags.
        /// These are included in the array exactly as reported by the server.
        /// The presence of a flag in this list does not necessarily mean that
        /// the client is permitted to set it permanently; writable flags are
        /// indicated separately via the <c>PERMANENTFLAGS</c> response.
        /// </para>
        /// <para>
        /// The values are provided in the order returned by the server and are
        /// not modified or normalized by the client.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public string[] Flags
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pFlags; 
            }
        }

        /// <summary>
        /// Gets the list of message flags that the server allows to be set
        /// permanently within the selected mailbox.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>PERMANENTFLAGS</c> response is returned by the server during the
        /// <c>SELECT</c> or <c>EXAMINE</c> command. It indicates which flags may be
        /// stored persistently on messages in the mailbox. Typical writable flags
        /// include the standard IMAP system flags such as:
        /// <c>\Seen</c>, <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// <c>\Draft</c>.
        /// </para>
        /// <para>
        /// Servers may also advertise implementation‑specific permanent flags.
        /// These values are included exactly as reported by the server. If the
        /// special flag <c>\*</c> is present, it indicates that the server permits
        /// clients to create new user‑defined flags.
        /// </para>
        /// <para>
        /// Flags listed in <see cref="Flags"/> but not present in
        /// <c>PermanentFlags</c> are read‑only for this mailbox and cannot be
        /// changed permanently by the client.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public string[] PermanentFlags
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_pPermanentFlags; 
            }
        }

        /// <summary>
        /// Gets a value indicating whether the selected mailbox is opened in
        /// read‑only mode.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The read‑only state is determined by the server’s tagged completion
        /// response to the <c>SELECT</c> or <c>EXAMINE</c> command. If the server
        /// returns <c>[READ-ONLY]</c>, the mailbox cannot be modified and commands
        /// that alter message state (such as <c>STORE</c> or <c>COPY</c> with
        /// flag changes) are not permitted.
        /// </para>
        /// <para>
        /// A <c>[READ-WRITE]</c> response indicates that the mailbox allows
        /// permanent state changes. In this case, message flags, keywords, and
        /// other persistent attributes may be updated by the client.
        /// </para>
        /// <para>
        /// The value reflects only the access mode granted for the current
        /// selection. It does not indicate server‑side ACLs or long‑term mailbox
        /// permissions.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public bool IsReadOnly
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_IsReadOnly; 
            }
        }

        /// <summary>
        /// Gets the <c>UIDNEXT</c> value of the selected mailbox, or <c>null</c>
        /// if the server has not reported it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>UIDNEXT</c> value represents the UID that the IMAP server will
        /// assign to the next message delivered to the mailbox. It is normally
        /// provided during the <c>SELECT</c> or <c>EXAMINE</c> command.
        /// </para>
        /// <para>
        /// Although most modern IMAP servers report <c>UIDNEXT</c>, the IMAP
        /// specification defines it as a <c>SHOULD</c> rather than a <c>MUST</c>.
        /// As a result, some legacy or non‑compliant servers may omit the value,
        /// and the property will remain <c>null</c> until explicitly set by the
        /// client's untagged‑response handler.
        /// </para>
        /// <para>
        /// Accessing this property after the folder has been disposed will throw an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this folder instance has been disposed.
        /// </exception>
        public long? UidNext
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_UidNext; 
            }
        }

        /// <summary>
        /// Gets the message sequence number of the first unseen message in the
        /// selected mailbox, if reported by the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is taken from the server’s <c>OK [UNSEEN n]</c> response,
        /// returned during <c>SELECT</c> or <c>EXAMINE</c>. It indicates the
        /// lowest‑numbered message that does not have the <c>\Seen</c> flag.
        /// </para>
        /// <para>
        /// If the server does not send an <c>UNSEEN</c> response, the property is
        /// <c>null</c>. This means only that the server did not report a first
        /// unseen message; clients may still determine unseen messages by examining
        /// individual message flags.
        /// </para>
        /// <para>
        /// The value is a message sequence number (MSN), not a UID.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public int? FirstUnseen
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_FirstUnseen; 
            }
        }

        /// <summary>
        /// Gets the total number of messages currently present in the selected
        /// mailbox.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value corresponds to the server’s untagged <c>* EXISTS</c> response,
        /// returned during the <c>SELECT</c> or <c>EXAMINE</c> command. It represents
        /// the number of messages in the mailbox at the moment the folder was
        /// selected.
        /// </para>
        /// <para>
        /// The count may change during the session if new messages arrive or existing
        /// messages are expunged. Such changes are reported by additional untagged
        /// <c>EXISTS</c> or <c>EXPUNGE</c> responses, which update this property
        /// automatically through the client’s global untagged‑response handler.
        /// </para>
        /// <para>
        /// The value is a message sequence number count (MSN‑based), not a UID count.
        /// It reflects the current ordering and numbering of messages within the
        /// selected mailbox.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public int MessagesCount
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_MessagesCount; 
            }
        }

        /// <summary>
        /// Gets the number of messages in the selected mailbox that have the
        /// <c>\Recent</c> flag set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value corresponds to the server’s untagged <c>* RECENT</c> response,
        /// returned during the <c>SELECT</c> or <c>EXAMINE</c> command. It indicates
        /// how many messages the server considers newly delivered since the last
        /// time the mailbox was opened by any IMAP session.
        /// </para>
        /// <para>
        /// The <c>\Recent</c> flag is maintained by the server and is not persistent.
        /// Once a mailbox is opened, the server may clear the flag from messages or
        /// adjust the count. For this reason, <c>RECENT</c> is not a reliable indicator
        /// of new mail for long‑term synchronization; clients typically use UIDs
        /// instead.
        /// </para>
        /// <para>
        /// The count may change during the session if the server sends additional
        /// untagged <c>RECENT</c> responses. Such updates are applied automatically
        /// through the client’s global untagged‑response handler.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this message instance has been disposed.
        /// </exception>
        public int RecentMessagesCount
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                return m_RecentMessagesCount; 
            }
        }

        #endregion
    }
}
