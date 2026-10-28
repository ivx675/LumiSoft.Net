using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Server 
{
    /// <summary>
    /// Provides data for the <see cref="IMAP_Session.FolderSyncAsync"/>
    /// event.
    /// </summary>
    /// <remarks>
    /// This event allows an application to synchronize the current
    /// mailbox state with the IMAP session.
    /// <para>
    /// If the mailbox version differs from
    /// <see cref="KnownVersion"/>, the event handler should provide
    /// the current mailbox UID set through <see cref="Uids"/> and
    /// message information through <see cref="Messages"/>.
    /// </para>
    /// <para>
    /// The IMAP session uses the supplied information to detect
    /// message additions, removals, and flag changes and generates
    /// the appropriate IMAP mailbox update responses.
    /// </para>
    /// </remarks>
    public class IMAP_e_FolderSync 
    {
        private IMAP_MessageInfo[] m_pMessages = [];

        /// <summary>
        /// Initializes a new <see cref="IMAP_e_FolderSync"/> instance.
        /// </summary>
        /// <param name="session">
        /// The IMAP session requesting mailbox synchronization.
        /// </param>
        /// <param name="folder">
        /// The mailbox to synchronize.
        /// </param>
        /// <param name="version">
        /// The mailbox version currently known to the IMAP session.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> or
        /// <paramref name="folder"/> is <see langword="null"/>.
        /// </exception>
        public IMAP_e_FolderSync(IMAP_Session session,string folder,long version)
        {
            if(session == null){
                throw new ArgumentNullException(nameof(session));
            }            
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }

            Session      = session;
            Folder       = folder;
            KnownVersion = version;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP session requesting mailbox synchronization.
        /// </summary>
        public IMAP_Session Session
        {
            get;
        }

        /// <summary>
        /// Gets the mailbox being synchronized.
        /// </summary>
        public string Folder
        {
            get;
        }

        /// <summary>
        /// Gets or sets the complete set of message UIDs currently
        /// contained in the mailbox.
        /// </summary>
        /// <remarks>
        /// The IMAP session uses this information to detect message
        /// additions and removals during mailbox synchronization.
        /// <para>
        /// This property must contain all existing message UIDs in the
        /// mailbox when <see cref="Version"/> differs from
        /// <see cref="KnownVersion"/>.
        /// </para>
        /// <para>
        /// The <see cref="Messages"/> property may contain either the
        /// complete mailbox snapshot or only messages that have been
        /// added or modified since the previous synchronization.
        /// </para>
        /// </remarks>
        public long[] Uids
        {
            get;
            set;
        } = [];

        /// <summary>
        /// Gets the mailbox version known to the IMAP session before the
        /// current synchronization operation.
        /// </summary>
        /// <remarks>
        /// The IMAP session compares this value with
        /// <see cref="Version"/> to determine whether the mailbox state
        /// has changed.
        /// <para>
        /// If the values are equal, the mailbox is generally considered
        /// unchanged and the current mailbox state typically does not need
        /// to be supplied.
        /// </para>
        /// <para>
        /// The interpretation of the version value is
        /// implementation-specific. It is treated as an opaque value and
        /// is compared only for equality.
        /// </para>
        /// </remarks>
        public long KnownVersion
        {
            get;
        }

        /// <summary>
        /// Gets or sets the current mailbox version.
        /// </summary>
        /// <remarks>
        /// The version value is used by the IMAP session to determine
        /// whether the mailbox state has changed since the previous
        /// synchronization.
        /// <para>
        /// The value must change whenever mailbox state changes in a way
        /// that may affect IMAP clients, such as message additions,
        /// removals, or flag updates.
        /// </para>
        /// <para>
        /// The format of the version is implementation-specific. The
        /// IMAP session compares version values for equality and does
        /// not interpret their contents.
        /// </para>
        /// </remarks>
        public long Version
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets message information used to update the IMAP
        /// session mailbox cache.
        /// </summary>
        /// <remarks>
        /// This property may contain either the complete mailbox
        /// snapshot or only messages that have been added or modified
        /// since the previous synchronization.
        /// <para>
        /// When <see cref="Version"/> differs from
        /// <see cref="KnownVersion"/>, the IMAP session updates its
        /// cached message information from the supplied values and
        /// generates any required mailbox update responses.
        /// </para>
        /// <para>
        /// Messages omitted from this property are assumed to be
        /// unchanged. Message removals are detected using the
        /// <see cref="Uids"/> property.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a null value is assigned.
        /// </exception>
        public IMAP_MessageInfo[] Messages
        {
            get{ return m_pMessages; }

            set{ m_pMessages = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        #endregion

    }
}
