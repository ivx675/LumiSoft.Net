using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.POP3.Server
{
    /// <summary>
    /// Represents a single message in the POP3 mailbox, including its UIDL value,
    /// size in bytes, deletion state, and optional user-defined tag data. Instances
    /// of this class are created by the POP3 server when loading message metadata
    /// after authentication and are used by STAT, LIST, UIDL, RETR, TOP, and DELE
    /// commands during the TRANSACTION state.
    /// </summary>
    public class POP3_ServerMessage
    {
        private int     m_SequenceNumber      = -1;
        private string  m_UID                 = "";
        private int     m_Size                = 0;
        private bool    m_IsMarkedForDeletion = false;
        private object? m_pTag                = null;

        /// <summary>
        /// Initializes a new <see cref="POP3_ServerMessage"/> instance with the specified
        /// UIDL value and message size. This overload creates a message without any
        /// user-defined tag data.
        /// </summary>
        /// <param name="uid">
        /// The POP3 unique identifier (UIDL) for the message. This value must not be
        /// null or empty.
        /// </param>
        /// <param name="size">
        /// The message size in bytes as reported by the POP3 <c>LIST</c> command.
        /// Must be greater than or equal to zero.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="uid"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="uid"/> is empty or when <paramref name="size"/>
        /// is less than zero.
        /// </exception>
        public POP3_ServerMessage(string uid,int size) : this(uid,size,null)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="POP3_ServerMessage"/> instance with the specified
        /// UIDL value, message size, and optional user-defined tag.
        /// </summary>
        /// <param name="uid">
        /// The POP3 unique identifier (UIDL) for the message. This value must not be
        /// null or empty.
        /// </param>
        /// <param name="size">
        /// The message size in bytes as reported by the POP3 <c>LIST</c> command.
        /// Must be greater than or equal to zero.
        /// </param>
        /// <param name="tag">
        /// Optional user-defined data associated with the message.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="uid"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="uid"/> is empty or when <paramref name="size"/>
        /// is less than zero.
        /// </exception>
        public POP3_ServerMessage(string uid,int size,object? tag)
        {
            if(uid == null){
                throw new ArgumentNullException("uid");
            }
            if(uid == string.Empty){
                throw new ArgumentException("Argument 'uid' value must be specified.");
            }
            if(size < 0){
                throw new ArgumentException("Argument 'size' value must be >= 0.");
            }

            m_UID  = uid;
            m_Size = size;
            m_pTag = tag;
        }


        #region method SetIsMarkedForDeletion

        /// <summary>
        /// Sets IsMarkedForDeletion proerty value.
        /// </summary>
        /// <param name="value">Value.</param>
        internal void SetIsMarkedForDeletion(bool value)
        {
            m_IsMarkedForDeletion = value;
        }

        #endregion


        #region Properties implemnetation
                
        /// <summary>
        /// Gets the POP3 unique identifier (UIDL) value for this message. The UIDL is a
        /// server-assigned stable identifier used by POP3 clients to track messages
        /// across sessions.
        /// </summary>
        public string UID
        {
            get{ return m_UID; }
        }

        /// <summary>
        /// Gets the message size in bytes as reported by the POP3 <c>LIST</c> command.
        /// This value is provided when constructing the <see cref="POP3_ServerMessage"/>
        /// instance and is used by STAT, LIST, RETR, and TOP commands.
        /// </summary>
        public int Size
        {
            get{ return m_Size; }
        }

        /// <summary>
        /// Gets a value indicating whether this message has been marked for deletion
        /// during the current POP3 session. A message is marked when the DELE command
        /// is issued and is actually removed only when the session ends with a successful
        /// QUIT command.
        /// </summary>
        public bool IsMarkedForDeletion
        {
            get{ return m_IsMarkedForDeletion; }
        }

        /// <summary>
        /// Gets or sets optional user-defined data associated with this message. The POP3
        /// server does not use this value internally; it is provided for applications that
        /// need to attach custom metadata to <see cref="POP3_ServerMessage"/> instances.
        /// </summary>
        public object? Tag
        {
            get{ return m_pTag; }

            set{ m_pTag = value; }
        }


        /// <summary>
        /// Gets message 1 based sequence number.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        internal int SequenceNumber
        {
            get{ return m_SequenceNumber; }

            set{ m_SequenceNumber = value; }
        }

        #endregion
    }
}
