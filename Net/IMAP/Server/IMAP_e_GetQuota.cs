using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.GetQuotaAsync"/> event.
    /// This event allows the application to supply quota information for the
    /// specified quota root.
    /// </summary>
    public class IMAP_e_GetQuota : EventArgs
    {
        private IMAP_Session         m_pSession;
        private List<IMAP_r_u_Quota> m_pQuotaResponses;
        private IMAP_r_ServerStatus  m_pResponse;
        private string               m_QuotaRoot;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_GetQuota"/> instance containing
        /// the quota root specified by the client and the response object used
        /// to return the command result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the GETQUOTA command.
        /// </param>
        /// <param name="quotaRoot">
        /// The quota root whose resource usage and limits were requested.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="quotaRoot"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_GetQuota(IMAP_Session session,string quotaRoot,IMAP_r_ServerStatus response)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }
            if(quotaRoot == null){
                throw new ArgumentNullException(nameof(quotaRoot));
            }

            m_pSession  = session;
            m_QuotaRoot = quotaRoot;
            m_pResponse = response;

            m_pQuotaResponses = new List<IMAP_r_u_Quota>();
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
        /// Gets the quota root whose resource usage and limits were requested.
        /// </summary>
        public string QuotaRoot
        {
            get{ return m_QuotaRoot; }
        }

        /// <summary>
        /// Gets the collection of QUOTA responses that will be returned to the
        /// client. Event handlers should populate this collection with the
        /// resource usage and limit information associated with the requested
        /// quota root.
        /// </summary>
        public List<IMAP_r_u_Quota> QuotaResponses
        {
            get{ return m_pQuotaResponses; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the GETQUOTA command.
        /// </summary>
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

        #endregion
    }
}
