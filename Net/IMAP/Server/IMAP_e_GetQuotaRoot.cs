using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.GetQuotaRootAsync"/>
    /// event. This event allows the application to supply quota root
    /// information and quota usage details for a mailbox.
    /// </summary>
    public class IMAP_e_GetQuotaRoot : EventArgs
    {
        private IMAP_Session          m_pSession;
        private IMAP_r_u_QuotaRoot?   m_pQuotaRootResponse;
        private List<IMAP_r_u_Quota>  m_pQuotaResponses;
        private IMAP_r_ServerStatus   m_pResponse;
        private string                m_Folder;

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_GetQuotaRoot"/> instance
        /// containing the mailbox name supplied by the client and the
        /// response object used to return the command result.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the GETQUOTAROOT command.
        /// </param>
        /// <param name="folder">
        /// The mailbox for which quota root information was requested.
        /// </param>
        /// <param name="response">
        /// The server response object used to return the result to the
        /// client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="folder"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_GetQuotaRoot(IMAP_Session session,string folder,IMAP_r_ServerStatus response)
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
            m_Folder    = folder;
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
        /// Gets or sets the QUOTAROOT response that will be returned to the client.
        /// </summary>
        /// <remarks>
        /// The QUOTAROOT response identifies the quota roots associated with the
        /// requested mailbox. For each quota root listed in this response, a
        /// corresponding <see cref="IMAP_r_u_Quota"/> entry should be added to
        /// <see cref="QuotaResponses"/>.
        /// </remarks>
        public IMAP_r_u_QuotaRoot? QuotaRootResponse
        {
            get{ return m_pQuotaRootResponse; }

            set{ m_pQuotaRootResponse = value; }
        }

        /// <summary>
        /// Gets the collection of QUOTA responses that will be returned to the client.
        /// </summary>
        /// <remarks>
        /// A QUOTA response should be supplied for each quota root listed in
        /// <see cref="QuotaRootResponse"/>.
        /// </remarks>
        public List<IMAP_r_u_Quota> QuotaResponses
        {
            get{ return m_pQuotaResponses; }
        }

        /// <summary>
        /// Gets or sets the <see cref="IMAP_r_ServerStatus"/> that will be
        /// returned to the client for the GETQUOTAROOT command.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public IMAP_r_ServerStatus Response
        {
            get{ return m_pResponse; }

            set{ 
                if(value == null){
                    throw new ArgumentNullException("value");
                }

                m_pResponse = value; 
            }
        }

        /// <summary>
        /// Gets the mailbox for which quota root information was requested.
        /// </summary>
        public string Folder
        {
            get{ return m_Folder; }
        }

        #endregion
    }
}
