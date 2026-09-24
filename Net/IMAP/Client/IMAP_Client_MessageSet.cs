using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Client 
{
    /// <summary>
    /// Represents a collection of IMAP messages retrieved through a single
    /// <c>FETCH</c> or <c>UID FETCH</c> operation. A message set provides access
    /// to prefetched message metadata, supports lazy retrieval of additional
    /// metadata, and exposes convenience methods for per‑message IMAP operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances of <see cref="IMAP_Client_MessageSet"/> are created internally by
    /// <see cref="IMAP_Client_SelectedFolder"/> when a <c>FETCH</c> request is
    /// executed. Each untagged <c>FETCH</c> response contributes one message to
    /// the set, which is stored in a UID‑keyed dictionary. The set represents a
    /// static snapshot of the mailbox state at the time of retrieval.
    /// </para>
    /// <para>
    /// A message set may be disposed independently when the caller is finished
    /// with it, or it may be disposed automatically when the parent
    /// <see cref="IMAP_Client_SelectedFolder"/> is disposed. Disposing the set
    /// releases all contained <see cref="IMAP_Client_Message"/> instances and
    /// marks the set as unusable. After disposal, any attempt to enumerate or
    /// access messages will result in an <see cref="ObjectDisposedException"/>.
    /// </para>
    /// <para>
    /// The message set itself does not perform any IMAP communication. All network
    /// operations—such as copying, moving, flag manipulation, or header retrieval—
    /// are delegated to the owning <see cref="IMAP_Client"/> instance through the
    /// individual <see cref="IMAP_Client_Message"/> objects.
    /// </para>
    /// <para>
    /// Enumeration reflects the order in which messages were added during the
    /// <c>FETCH</c> operation. This ordering is stable for the lifetime of the set
    /// but is not guaranteed to correspond to IMAP message sequence numbers or any
    /// server‑defined ordering.
    /// </para>
    /// </remarks>
    public class IMAP_Client_MessageSet : IEnumerable<IMAP_Client_Message>
    {
        private bool                                 m_IsDisposed = false;
        private IMAP_Client                          m_pImap;
        private IMAP_Client_SelectedFolder           m_pSelectedFolder;
        private Dictionary<long,IMAP_Client_Message> m_pMessages;

        /// <summary>
        /// Initializes a new <see cref="IMAP_Client_MessageSet"/> instance bound to the
        /// specified IMAP client and selected folder. The message set represents a
        /// collection of messages retrieved through a single <c>FETCH</c> operation.
        /// </summary>
        /// <param name="imap">
        /// The owning <see cref="IMAP_Client"/> instance used for all IMAP operations
        /// performed on messages contained in this set.
        /// </param>
        /// <param name="selectedFolder">
        /// The <see cref="IMAP_Client_SelectedFolder"/> to which this message set
        /// belongs. The folder tracks the lifetime of the set and disposes it when
        /// the folder itself is disposed.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imap"/> or <paramref name="selectedFolder"/> is
        /// <c>null</c>.
        /// </exception>
        internal IMAP_Client_MessageSet(IMAP_Client imap,IMAP_Client_SelectedFolder selectedFolder)
        {
            m_pImap           = imap;
            m_pSelectedFolder = selectedFolder;

            m_pMessages = new Dictionary<long,IMAP_Client_Message>();
        }

        #region method Dispose

        /// <summary>
        /// Releases all resources associated with this <c>MessageSet</c> and the
        /// <see cref="IMAP_Client_Message"/> instances it contains.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When this method is called, the set is marked as disposed and each
        /// message stored in the internal collection is also disposed. After
        /// disposal, the set can no longer be enumerated or queried, and any
        /// attempt to access members such as <see cref="GetMessage(long)"/>,
        /// <see cref="Count"/>, or <see cref="GetEnumerator()"/> will result in an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// <para>
        /// Disposing the set does not communicate with the IMAP server; it only
        /// releases local resources associated with the cached message objects.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            if(m_IsDisposed){
                return;
            }

            m_IsDisposed = true;
            
            foreach(IMAP_Client_Message message in m_pMessages.Values){
                message.Dispose();
            }
            m_pMessages.Clear();
            m_pSelectedFolder.RemoveMessageSet(this);
        }
        
        #endregion


        #region method GetMessage

        /// <summary>
        /// Retrieves the message associated with the specified UID from this
        /// <c>MessageSet</c>.
        /// </summary>
        /// <param name="uid">
        /// The unique identifier (UID) of the message to retrieve.
        /// </param>
        /// <returns>
        /// The <see cref="IMAP_Client_Message"/> instance matching the specified
        /// UID, or <c>null</c> if the set does not contain a message with that UID.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <c>MessageSet</c> instance has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Lookup is performed using the internal UID‑keyed dictionary, providing
        /// constant‑time access. Only messages that were included when the set was
        /// constructed are available through this method.
        /// </para>
        /// <para>
        /// The method does not trigger any server communication; it simply returns
        /// the cached message object if present.
        /// </para>
        /// </remarks>
        public IMAP_Client_Message? GetMessage(long uid)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            m_pMessages.TryGetValue(uid, out var msg);
            return msg;
        }

        #endregion

        #region method GetEnumerator

        /// <summary>
        /// Returns an enumerator that iterates through all messages contained in
        /// this <c>MessageSet</c>.
        /// </summary>
        /// <returns>
        /// An enumerator over the <see cref="IMAP_Client_Message"/> instances stored
        /// in this set.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <c>MessageSet</c> instance has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Enumeration is performed over the values of the internal UID‑keyed
        /// dictionary. The order of iteration reflects the dictionary’s value
        /// enumeration order, which is not guaranteed to correspond to IMAP
        /// sequence numbers or any server‑defined ordering.
        /// </para>
        /// <para>
        /// This method does not communicate with the server; it simply exposes the
        /// messages currently held in the set.
        /// </para>
        /// </remarks>
        public IEnumerator<IMAP_Client_Message> GetEnumerator()
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            return m_pMessages.Values.GetEnumerator();
        }

        /// <summary>
        /// Returns a non‑generic enumerator that iterates through all messages
        /// contained in this <c>MessageSet</c>.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerator"/> instance for iterating over the messages
        /// in this set.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method provides the non‑generic <see cref="IEnumerable"/> interface
        /// required by the .NET enumeration pattern. It simply delegates to the
        /// generic <see cref="GetEnumerator()"/> implementation.
        /// </para>
        /// <para>
        /// The returned enumerator reflects the same iteration order as the generic
        /// enumerator and does not perform any additional processing.
        /// </para>
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion


        #region method Add

        internal void Add(
            long uid,
            long size,
            DateTime internalDate,
            string[] flags,
            IMAP_t_Envelope? envelope,
            IMAP_t_BodyStructure? bodyStructure,
            string[]? gmailLabels,
            long? gmailMsgId,
            long? gmailThrId)
        {
            m_pMessages.Add(uid,new IMAP_Client_Message(m_pImap,uid,size,internalDate,flags,envelope,bodyStructure,gmailLabels,gmailMsgId,gmailThrId));
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this <c>MessageSet</c> instance has been
        /// disposed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <c>true</c>, the object has released its internal resources and can no
        /// longer be used. Any attempt to enumerate the set or retrieve messages through
        /// methods such as <see cref="GetMessage(long)"/> or <see cref="GetEnumerator()"/>
        /// will result in an <see cref="ObjectDisposedException"/>.
        /// </para>
        /// <para>
        /// This property allows callers to check the disposal state before performing
        /// operations that require access to the underlying message collection.
        /// </para>
        /// </remarks>
        public bool IsDisposed
        {
            get { return m_IsDisposed; }
        }

        /// <summary>
        /// Gets the number of messages contained in this <c>MessageSet</c>.
        /// </summary>
        /// <returns>
        /// The total number of <see cref="IMAP_Client_Message"/> instances stored
        /// in this set.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <c>MessageSet</c> instance has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The value reflects the number of messages present at the time the set
        /// was constructed. The count does not automatically update based on
        /// server‑side mailbox changes.
        /// </para>
        /// <para>
        /// Accessing this property does not trigger any IMAP communication; it
        /// simply returns the size of the internal UID‑keyed dictionary.
        /// </para>
        /// </remarks>
        public int Count
        {
            get { 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pMessages.Count;
            }
        }

        /// <summary>
        /// Gets the message at the specified zero‑based index within this
        /// <c>MessageSet</c>.
        /// </summary>
        /// <param name="index">
        /// The zero‑based index of the message to retrieve.
        /// </param>
        /// <returns>
        /// The <see cref="IMAP_Client_Message"/> instance at the specified index.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this <c>MessageSet</c> instance has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="index"/> is less than zero or greater than or
        /// equal to <see cref="Count"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Index‑based access reflects the insertion order of messages in this
        /// <c>MessageSet</c>. This order corresponds to the order in which messages
        /// were added during the bulk <c>FETCH</c> operation.
        /// </para>
        /// <para>
        /// Accessing this property does not trigger any IMAP communication; it
        /// simply returns the cached message object at the specified position.
        /// </para>
        /// </remarks>
        public IMAP_Client_Message this[int index]
        {
            get
            {
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                if(index < 0 || index >= m_pMessages.Count){
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                
                int i = 0;
                foreach(var msg in m_pMessages.Values){
                    if (i == index){
                        return msg;
                    }
                    i++;
                }

                // Should never happen
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        #endregion
    }
}
