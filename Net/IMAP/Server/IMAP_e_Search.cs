using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.SearchAsync"/>
    /// event. This event allows the application to search a mailbox
    /// using the supplied SEARCH criteria and return matching
    /// message UIDs.
    /// </summary>
    public class IMAP_e_Search : EventArgs
    {
        private IMAP_Session        m_pSession;
        private string              m_Folder;
        private IMAP_r_ServerStatus m_pResponse;
        private IMAP_t_Search_Key   m_pCriteria;
        private long[]              m_pMatchedUids = [];

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_Search"/> instance containing
        /// information about the SEARCH operation requested by the client.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the SEARCH command.
        /// </param>
        /// <param name="folder">
        /// The mailbox being searched.
        /// </param>
        /// <param name="criteria">
        /// The parsed SEARCH criteria supplied by the client.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>,
        /// <paramref name="criteria"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_Search(IMAP_Session session,string folder,IMAP_t_Search_Key criteria,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(criteria == null){
                throw new ArgumentNullException(nameof(criteria));
            }
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            m_pSession    = session;
            m_Folder      = folder;
            m_pResponse   = response;
            m_pCriteria   = criteria;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP session associated with this event. The session provides
        /// access to connection state, authentication status, and server-side
        /// context required to process the current IMAP command.
        /// </summary>
        public IMAP_Session Session
        {
            get{ return m_pSession; }
        }

        /// <summary>
        /// Gets or sets the IMAP server response describing the outcome
        /// of the SEARCH operation.
        /// </summary>
        /// <remarks>
        /// Event handlers should assign an appropriate OK or NO response.
        /// Matching messages should be returned through the
        /// <see cref="MatchedUids"/> property.
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
        /// Gets the mailbox being searched.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        /// <summary>
        /// Gets the parsed SEARCH criteria supplied by the client.
        /// </summary>
        /// <remarks>
        /// The search criteria are represented as a tree of
        /// <see cref="IMAP_t_Search_Key"/> instances corresponding to the
        /// SEARCH expression requested by the client.
        /// </remarks>
        public IMAP_t_Search_Key Criteria
        {
            get{ return m_pCriteria; }
        }

        /// <summary>
        /// Gets or sets the UIDs of the messages matching the search
        /// criteria.
        /// </summary>
        /// <remarks>
        /// Event handlers should populate this property with the UIDs of
        /// all matching messages in the searched mailbox.
        /// <para>
        /// The IMAP session automatically converts these UIDs to message
        /// sequence numbers when processing a SEARCH command. For UID
        /// SEARCH commands, the UIDs are returned directly to the client.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public long[] MatchedUids
        {
            get{ return m_pMatchedUids; }
            set{ m_pMatchedUids = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        #endregion
    }
}
