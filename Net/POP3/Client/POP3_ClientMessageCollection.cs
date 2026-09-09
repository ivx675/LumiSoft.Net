using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3.Client
{
    /// <summary>
    /// Represents a cached collection of POP3 message metadata associated with a
    /// <see cref="POP3_Client"/> instance.  
    /// Each entry corresponds to a single POP3 message and contains only metadata
    /// retrieved via the <c>LIST</c> and (when supported) <c>UIDL</c> commands.
    /// Message bodies are not stored in this collection; they must be retrieved
    /// separately using <c>RETR</c> or <c>TOP</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The collection is created internally by <see cref="POP3_Client"/> when the
    /// message list is loaded using <see cref="POP3_Client.LoadMessages"/> or
    /// <see cref="POP3_Client.LoadMessagesAsync(System.Threading.CancellationToken)"/>.
    /// It is not intended to be instantiated directly by user code.
    /// </para>
    /// <para>
    /// Messages are assigned sequential 1‑based POP3 message numbers in the order
    /// returned by the server's <c>LIST</c> command.  
    /// These numbers remain stable for the duration of the POP3 session.
    /// </para>
    /// <para>
    /// The collection implements <see cref="IEnumerable"/> to allow iteration over
    /// all messages.  
    /// Messages marked for deletion (via <c>DELE</c>) remain present in the
    /// collection until the POP3 session ends with <c>QUIT</c>, because POP3
    /// deletions are applied only during the UPDATE state.
    /// </para>
    /// <para>
    /// The collection and all contained <see cref="POP3_ClientMessage"/> instances
    /// are disposed when the POP3 client disconnects or when the collection's
    /// <see cref="Dispose"/> method is called.
    /// Accessing the collection after disposal raises an
    /// <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    public class POP3_ClientMessageCollection : IEnumerable,IDisposable
    {
        private POP3_Client              m_pPop3Client;
        private List<POP3_ClientMessage> m_pMessages;
        private bool                     m_IsDisposed  = false;

        /// <summary>
        /// Initializes a new <see cref="POP3_ClientMessageCollection"/> instance
        /// associated with the specified <see cref="POP3_Client"/>.  
        /// This constructor is internal because message collections are created
        /// exclusively by <see cref="POP3_Client"/> when the POP3 message list is
        /// loaded using <c>LIST</c> and (when supported) <c>UIDL</c>.
        /// </summary>
        /// <param name="pop3">
        /// The owning <see cref="POP3_Client"/> instance that provides POP3 session
        /// context for all messages in the collection.
        /// </param>
        /// <remarks>
        /// <para>
        /// The collection stores only POP3 message metadata (size and optional
        /// unique-id). Message bodies are not retrieved or cached here; they must be
        /// accessed using <c>RETR</c> or <c>TOP</c> on the owning client.
        /// </para>
        /// <para>
        /// Each message added to the collection is assigned a sequential 1-based POP3
        /// message number corresponding to its position in the server's <c>LIST</c>
        /// response.
        /// </para>
        /// </remarks>
        internal POP3_ClientMessageCollection(POP3_Client pop3)
        {
            m_pPop3Client = pop3;

            m_pMessages = new List<POP3_ClientMessage>();
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public void Dispose()
        {
            if(m_IsDisposed){
                return;
            }
            m_IsDisposed = true;

            // Release messages.
            foreach(POP3_ClientMessage message in m_pMessages){
                message.Dispose();
            }
        }

        #endregion


        #region method Add

        /// <summary>
        /// Adds a new POP3 message entry to the collection using sequential 1‑based
        /// POP3 message numbering.  
        /// The entry contains only message metadata (size and optional unique‑id)
        /// retrieved from the server's <c>LIST</c> and <c>UIDL</c> commands.
        /// </summary>
        /// <param name="size">
        /// The message size in bytes as reported by the POP3 <c>LIST</c> command.
        /// </param>
        /// <param name="uniqueId">
        /// The POP3 unique‑id (UIDL) associated with the message, or <c>null</c>
        /// if the server does not support UIDL or if the UIDL list could not be
        /// matched to the <c>LIST</c> results.
        /// </param>
        /// <remarks>
        /// <para>
        /// Messages are assigned sequential 1‑based POP3 message numbers in the
        /// order returned by the server.  
        /// These numbers remain stable for the duration of the POP3 session.
        /// </para>
        /// <para>
        /// This method is internal because message entries are created exclusively
        /// by <see cref="POP3_Client"/> when loading the message list.
        /// </para>
        /// </remarks>
        internal void Add(int size,string? uniqueId)
        {
            m_pMessages.Add(new POP3_ClientMessage(m_pPop3Client,m_pMessages.Count + 1,size,uniqueId));
        }

        #endregion


        #region interface IEnumerator

		/// <summary>
        /// Returns an enumerator that iterates through all POP3 message metadata
        /// entries contained in the collection.  
        /// The enumerator exposes messages in the same order they were added,
        /// corresponding to the server's <c>LIST</c> response.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerator"/> instance that can be used to iterate over
        /// the <see cref="POP3_ClientMessage"/> objects in the collection.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the collection has been disposed.  
        /// Once disposed, the collection and all contained messages are no longer
        /// accessible.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Enumeration provides access only to POP3 message metadata (size and
        /// optional unique‑id). Message bodies must be retrieved separately using
        /// <c>RETR</c> or <c>TOP</c> on the owning <see cref="POP3_Client"/>.
        /// </para>
        /// <para>
        /// Messages marked for deletion remain visible in the enumerator because
        /// POP3 deletions are applied only when the client sends <c>QUIT</c>
        /// and enters the UPDATE state.
        /// </para>
        /// </remarks>
		public IEnumerator GetEnumerator()
		{
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

			return m_pMessages.GetEnumerator();
		}

		#endregion

        #region Properties Implementation

        /// <summary>
        /// Gets the total size, in bytes, of all messages contained in the
        /// collection.  
        /// The value includes messages that have been marked for deletion,
        /// because POP3 deletions are not applied until the client sends
        /// the <c>QUIT</c> command and the session enters the UPDATE state.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the collection has been disposed.  
        /// Once disposed, the collection and all contained messages are no
        /// longer accessible.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The size reported for each message corresponds to the value returned
        /// by the POP3 <c>LIST</c> command.  
        /// This property does not retrieve message bodies and does not perform
        /// any additional network operations.
        /// </para>
        /// <para>
        /// The total reflects only the cached metadata stored in the collection.
        /// Message bodies must be retrieved separately using <c>RETR</c> or
        /// <c>TOP</c> on the owning <see cref="POP3_Client"/>.
        /// </para>
        /// </remarks>
        public long TotalSize
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                long size = 0;
                foreach(POP3_ClientMessage message in m_pMessages){
                    size += message.Size;
                }

                return size; 
            }
        }

        /// <summary>
        /// Gets the number of messages contained in the collection.  
        /// Messages that have been marked for deletion are still included,
        /// because POP3 deletions are not applied until the client sends
        /// the <c>QUIT</c> command and the session enters the UPDATE state.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the collection has been disposed.  
        /// Once disposed, the collection and all contained messages are no
        /// longer accessible.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The count reflects only the cached metadata stored in the collection.
        /// Message bodies are not retrieved or cached here; they must be accessed
        /// using <c>RETR</c> or <c>TOP</c> on the owning <see cref="POP3_Client"/>.
        /// </para>
        /// <para>
        /// The number of messages corresponds to the entries returned by the
        /// server's <c>LIST</c> command and remains stable for the duration of
        /// the POP3 session.
        /// </para>
        /// </remarks>
        public int Count
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pMessages.Count; 
            }
        }

        /// <summary>
        /// Retrieves the <see cref="POP3_ClientMessage"/> at the specified zero‑based
        /// index within the collection.  
        /// The index corresponds to the position of the message in the server's
        /// <c>LIST</c> response and remains stable for the duration of the POP3 session.
        /// </summary>
        /// <param name="index">
        /// The zero‑based index of the message to retrieve.  
        /// Valid values range from <c>0</c> to <c>Count - 1</c>.
        /// </param>
        /// <returns>
        /// The <see cref="POP3_ClientMessage"/> at the specified index.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the collection has been disposed.  
        /// Once disposed, the collection and all contained messages are no longer
        /// accessible.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="index"/> is less than <c>0</c> or greater than or
        /// equal to <see cref="Count"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This indexer provides access only to POP3 message metadata (size and
        /// optional unique‑id). Message bodies must be retrieved separately using
        /// <c>RETR</c> or <c>TOP</c> on the owning <see cref="POP3_Client"/>.
        /// </para>
        /// <para>
        /// Messages marked for deletion remain accessible through this indexer because
        /// POP3 deletions are applied only when the client sends <c>QUIT</c> and enters
        /// the UPDATE state.
        /// </para>
        /// </remarks>
        public POP3_ClientMessage this[int index]
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(index < 0 || index > m_pMessages.Count){
                    throw new ArgumentOutOfRangeException();
                }

                return m_pMessages[index]; 
            }
        }

        /// <summary>
        /// Retrieves the <see cref="POP3_ClientMessage"/> that has the specified
        /// POP3 unique‑id (UIDL).  
        /// Returns <c>null</c> if no message in the collection matches the provided
        /// unique‑id.
        /// </summary>
        /// <param name="uniqueId">
        /// The POP3 unique‑id value to search for.  
        /// This value corresponds to the identifier returned by the server's
        /// <c>UIDL</c> command.
        /// </param>
        /// <returns>
        /// The matching <see cref="POP3_ClientMessage"/>, or <c>null</c> if no such
        /// message exists.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the collection has been disposed.  
        /// Once disposed, the collection and all contained messages are no longer
        /// accessible.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown if the POP3 server does not support the <c>UIDL</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// UIDL values are stable across POP3 sessions and uniquely identify messages
        /// on the server.  
        /// This indexer performs a linear search through the cached message metadata
        /// and does not issue any network operations.
        /// </para>
        /// <para>
        /// If the server does not support UIDL, or if the message list was loaded
        /// without UIDL information, this indexer cannot be used.
        /// </para>
        /// </remarks>
        public POP3_ClientMessage? this[string uniqueId]
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!m_pPop3Client.SupportsUidl){
                    throw new NotSupportedException();
                }

                foreach(POP3_ClientMessage message in m_pMessages){
                    if(message.UniqueId == uniqueId){
                        return message;
                    }
                }

                return null; 
            }
        }

        #endregion

    }
}
