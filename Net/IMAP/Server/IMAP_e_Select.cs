using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.SelectAsync"/> event.
    /// This event allows the application to select a mailbox and provide
    /// the mailbox state information required by the IMAP SELECT command.
    /// </summary>
    public class IMAP_e_Select : EventArgs
    {
        private IMAP_Session           m_pSession;
        private IMAP_r_ServerStatus    m_pResponse;
        private string                 m_Folder;
        private int                    m_UidValidity = 0;
        private string[]               m_pFlags;
        private string[]               m_pPermanentFlags;
        private List<IMAP_MessageInfo> m_pMessagesInfo = [];

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_Select"/> instance containing
        /// the mailbox name supplied by the client and the response object
        /// used to return the command result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the SELECT command.
        /// </param>
        /// <param name="folder">
        /// The mailbox that the client requested to select.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Select(IMAP_Session session,string folder,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }
         
            m_pSession  = session;
            m_pResponse = response;
            m_Folder    = folder;


            // Add default falgs.
            m_pFlags = new string[]{"\\Answered","\\Flagged","\\Deleted","\\Seen","\\Draft"};
            m_pPermanentFlags = new string[]{"\\Answered","\\Flagged","\\Deleted","\\Seen","\\Draft"};
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
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the SELECT command.
        /// </summary>
        /// <remarks>
        /// Event handlers must assign a successful response when the mailbox
        /// can be selected. A successful response should normally contain
        /// either a <see cref="IMAP_t_orc_ReadWrite"/> or
        /// <see cref="IMAP_t_orc_ReadOnly"/> response code indicating the
        /// access mode of the selected mailbox.
        ///
        /// If the mailbox cannot be selected, for example because it does not
        /// exist or the user lacks sufficient access rights, an appropriate
        /// failure response should be assigned.
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
        /// Gets the mailbox that the client requested to select.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets or sets the mailbox version associated with the selected
        /// mailbox.
        /// </summary>
        /// <remarks>
        /// The value is used by the IMAP session to determine whether the
        /// mailbox state has changed since the last synchronization.
        /// <para>
        /// The interpretation of the version value is
        /// implementation-specific. The IMAP session treats the value as
        /// opaque and compares it only for equality.
        /// </para>
        /// <para>
        /// The value is typically initialized during SELECT or EXAMINE and
        /// updated after successful mailbox synchronization.
        /// </para>
        /// </remarks>
        public long Version
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the UIDVALIDITY value of the selected mailbox.
        /// </summary>
        /// <remarks>
        /// UIDVALIDITY is used by clients to determine whether previously
        /// cached message UIDs remain valid.
        /// </remarks>
        public int UidValidity
        {
            get{ return m_UidValidity; }

            set{ m_UidValidity = value; }
        }

        /// <summary>
        /// Gets or sets the flags defined for the selected mailbox.
        /// </summary>
        /// <remarks>
        /// The default value contains the standard system flags:
        /// <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// <c>\Seen</c>, and <c>\Draft</c>.
        /// </remarks>
        public string[] Flags
        {
            get{ return m_pFlags; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }

                m_pFlags = value; 
            }
        }

        /// <summary>
        /// Gets or sets the message flags that may be permanently modified
        /// by the client in the selected mailbox.
        /// </summary>
        /// <remarks>
        /// The default value contains the standard system flags:
        /// <c>\Answered</c>, <c>\Flagged</c>, <c>\Deleted</c>,
        /// <c>\Seen</c>, and <c>\Draft</c>.
        /// </remarks>
        public string[] PermanentFlags
        {
            get{ return m_pPermanentFlags; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }

                m_pPermanentFlags = value; 
            }
        }

        /// <remarks>
        /// Event handlers must populate this collection with metadata for all
        /// messages currently contained in the mailbox.
        /// <para>
        /// The server caches this information for the lifetime of the selected
        /// mailbox and uses it to service subsequent mailbox operations such
        /// as FETCH, SEARCH, and sequence number to UID mapping without
        /// repeatedly querying the underlying message store.
        /// </para>
        /// </remarks>
        public List<IMAP_MessageInfo> MessagesInfo
        {
            get{ return m_pMessagesInfo; }
        }


        /// <summary>
        /// Gets the predicted next UID value for the selected mailbox.
        /// </summary>
        /// <remarks>
        /// The value is calculated from the highest UID present in
        /// <see cref="MessagesInfo"/> plus one.
        /// </remarks>
        internal long UidNext
        {
            get{ 
                long maxUID = 0;
                foreach(IMAP_MessageInfo m in m_pMessagesInfo){
                    if(m.UID > maxUID){
                        maxUID = m.UID;
                    }
                }
                maxUID++;
                
                return maxUID; 
            }
        }

        #endregion
    }
}
