using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Represents mailbox message information maintained by the IMAP
    /// session.
    /// </summary>
    /// <remarks>
    /// This class contains metadata used by the IMAP session for
    /// message selection, mailbox synchronization, SEARCH processing,
    /// flag tracking, and generation of IMAP responses.
    /// </remarks>
    public class IMAP_MessageInfo
    {
        private string   m_ID;
        private long     m_UID    = 0;
        private string[] m_pFlags;
        private long     m_Size   = 0;
        private DateTime m_InternalDate;

        /// <summary>
        /// Initializes a new <see cref="IMAP_MessageInfo"/> instance.
        /// </summary>
        /// <param name="id">
        /// Application-defined message identifier.
        /// </param>
        /// <param name="uid">
        /// The IMAP UID assigned to the message.
        /// </param>
        /// <param name="flags">
        /// The message flags.
        /// </param>
        /// <param name="size">
        /// The message size in octets.
        /// </param>
        /// <param name="internalDate">
        /// The IMAP INTERNALDATE value.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="id"/> or
        /// <paramref name="flags"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when one or more arguments contain invalid values.
        /// </exception>
        public IMAP_MessageInfo(string id,long uid,string[] flags,long size,DateTime internalDate)
        {
            if(id == null){
                throw new ArgumentNullException("id");
            }
            if(id == string.Empty){
                throw new ArgumentException("Argument 'id' value must be specified.",nameof(id));
            }
            if(uid < 1){
                throw new ArgumentException("Argument 'uid' value must be >= 1.",nameof(uid));
            }
            if(flags == null){
                throw new ArgumentNullException("flags");
            }

            m_ID           = id;
            m_UID          = uid;
            m_pFlags       = flags;
            m_Size         = size;
            m_InternalDate = internalDate;
        }


        #region method ContainsFlag

        /// <summary>
        /// Determines whether the message contains the specified flag.
        /// </summary>
        /// <param name="flag">
        /// The flag to look for.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the message contains the specified
        /// flag; otherwise, <see langword="false"/>.
        /// </returns>
        public bool ContainsFlag(string flag)
        {
            if(flag == null){
                throw new ArgumentNullException("flag");
            }

            foreach(string f in m_pFlags){
                if(string.Equals(f,flag,StringComparison.InvariantCultureIgnoreCase)){
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region method FlagsEqual

        /// <summary>
        /// Determines whether the specified flags are equal to this
        /// message's current flags.
        /// </summary>
        /// <param name="flags">
        /// The flags to compare against the current message flags.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if both flag collections contain the
        /// same flags; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Flag comparisons are performed case-insensitively and are
        /// independent of flag order.
        /// </remarks>
        public bool FlagsEqual(string[] flags)
        {
            return new HashSet<string>(m_pFlags,StringComparer.OrdinalIgnoreCase).SetEquals(flags);
        }

        #endregion


        #region method UpdateFlags

        /// <summary>
        /// Updates the message flags.
        /// </summary>
        /// <param name="setType">
        /// Specifies how the supplied flags are applied.
        /// </param>
        /// <param name="flags">
        /// The flags to add, remove, or replace.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="flags"/> is
        /// <see langword="null"/>.
        /// </exception>
        internal void UpdateFlags(IMAP_Flags_SetType setType,string[] flags)
        {
            if(flags == null){
                throw new ArgumentNullException("flags");
            }

            if(setType == IMAP_Flags_SetType.Add){
                m_pFlags = IMAP_Utils.MessageFlagsAdd(m_pFlags,flags);
            }
            else if(setType == IMAP_Flags_SetType.Remove){
                m_pFlags = IMAP_Utils.MessageFlagsRemove(m_pFlags,flags);
            }
            else{
                m_pFlags = flags;
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the application-defined message identifier.
        /// </summary>
        public string ID
        {
            get{ return m_ID; }
        }

        /// <summary>
        /// Gets the IMAP UID assigned to the message.
        /// </summary>
        public long UID
        {
            get{ return m_UID; }
        }

        /// <summary>
        /// Gets the message flags.
        /// </summary>
        /// <remarks>
        /// The returned array contains the current IMAP system and
        /// keyword flags associated with the message.
        /// </remarks>
        public string[] Flags
        {
            get{ return m_pFlags; }
        }

        /// <summary>
        /// Gets the message size in bytes.
        /// </summary>
        public long Size
        {
            get{ return m_Size; }
        }

        /// <summary>
        /// Gets the IMAP INTERNALDATE value of the message.
        /// </summary>
        public DateTime InternalDate
        {
            get{ return m_InternalDate; }
        }
        
        #endregion
    }
}
