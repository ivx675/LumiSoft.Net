using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// Provides information about an IMAP LSUB command. This event-args
    /// class is used with <see cref="IMAP_Session.LSubAsync"/> to retrieve
    /// subscribed mailboxes matching the reference name and mailbox pattern
    /// supplied by the client.
    /// </summary>
    public class IMAP_e_LSub : EventArgs
    {
        private IMAP_Session        m_pSession;
        private string              m_FolderReferenceName;
        private string              m_FolderFilter;
        private List<IMAP_r_u_LSub> m_pFolders;
        private IMAP_r_ServerStatus m_pResponse;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_e_LSub"/> class.
        /// </summary>
        /// <param name="session">
        /// The IMAP session that is processing the LSUB command.
        /// </param>
        /// <param name="referenceName">
        /// The reference name supplied by the client.
        /// </param>
        /// <param name="folderFilter">
        /// The mailbox pattern used to match subscribed mailboxes.
        /// </param>
        /// <param name="response">
        /// The response that will be returned to the client.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>,
        /// <paramref name="referenceName"/>,
        /// <paramref name="folderFilter"/>, or
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        internal IMAP_e_LSub(IMAP_Session session,string referenceName,string folderFilter,IMAP_r_ServerStatus response)
        {
            m_pSession            = session;
            m_FolderReferenceName = referenceName;
            m_FolderFilter        = folderFilter;
            m_pResponse           = response;

            m_pFolders = new List<IMAP_r_u_LSub>();
        }


        #region method IsMatch

        /// <summary>
        /// Tests whether the specified folder path matches the reference name and filter rule 
        /// supplied in the IMAP LSUB command.
        /// </summary>
        /// <param name="folderPath">The full path of the folder to test (e.g., "INBOX/Drafts").</param>
        /// <param name="hierarchyDelimiter">The hierarchy separator character (default is '/').</param>
        /// <returns>True if the folder matches the filter criteria; otherwise, false.</returns>
        public bool IsMatch(string folderPath,char hierarchyDelimiter = '/')
        {
            if(string.IsNullOrEmpty(folderPath)){
                return false;
            }

            // 1. Combine Reference Name and Filter Pattern per RFC rules
            string reference = m_FolderReferenceName ?? string.Empty;
            string filter = m_FolderFilter ?? string.Empty;
            string combined;

            if(!string.IsNullOrEmpty(reference)){
                if(!reference.EndsWith(hierarchyDelimiter.ToString()) && !filter.StartsWith(hierarchyDelimiter.ToString())){
                    combined = reference + hierarchyDelimiter + filter;
                }
                else{
                    combined = reference + filter;
                }
            }
            else{
                combined = filter;
            }

            // 2. Convert IMAP wildcard pattern (* and %) to a regular expression
            string escaped = Regex.Escape(combined)
                .Replace(@"\*", ".*")                       // '*' matches zero or more characters across hierarchy levels
                .Replace(@"\%", "[^" + hierarchyDelimiter + "]*");   // '%' matches zero or more characters EXCEPT the delimiter

            string regexPattern = "^" + escaped + "$";

            // 3. Evaluate match (case-insensitive)
            return Regex.IsMatch(folderPath, regexPattern,RegexOptions.IgnoreCase);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP session associated with this event.
        /// </summary>
        public IMAP_Session Session
        {
            get { return m_pSession; }
        }

        /// <summary>
        /// Gets the reference name supplied with the IMAP LSUB command.
        /// </summary>
        /// <remarks>
        /// An empty string indicates that no reference name was specified.
        /// </remarks>
        public string FolderReferenceName
        {
            get{ return m_FolderReferenceName; }
        }

        /// <summary>
        /// Gets the mailbox name filter supplied with the IMAP LIST command.
        /// </summary>
        /// <remarks>
        /// The character "*" matches zero or more characters. The character "%"
        /// is similar to "*", but does not match a hierarchy delimiter. If "%"
        /// appears as the last character of the mailbox name argument, matching
        /// hierarchy levels are also returned.
        /// </remarks>
        public string FolderFilter
        {
            get{ return m_FolderFilter; }
        }

        /// <summary>
        /// Gets the collection of folders that will be returned to the client.
        /// Event handlers should populate this collection with all folders that
        /// match the supplied filter criteria.
        /// </summary>
        public List<IMAP_r_u_LSub> Folders
        {
            get{ return m_pFolders; }
        }

        /// <summary>
        /// Gets or sets the IMAP server response describing the outcome of the
        /// LSUB command.
        /// </summary>
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
