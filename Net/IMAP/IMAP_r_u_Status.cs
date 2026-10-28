using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP <c>STATUS</c> response as defined by
    /// RFC 9051. A STATUS response provides selected metadata about a
    /// mailbox without requiring the client to SELECT or EXAMINE it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IMAP <c>STATUS</c> command allows a client to request specific
    /// mailbox metadata items without changing the currently selected
    /// mailbox. The client sends a command of the form:
    /// </para>
    /// <code>
    /// A01 STATUS mailbox-name (MESSAGES UIDNEXT UNSEEN)
    /// </code>
    /// <para>
    /// The server replies with an untagged STATUS response containing the
    /// requested status data items:
    /// </para>
    /// <code>
    /// * STATUS mailbox-name (MESSAGES 231 UIDNEXT 44292 UNSEEN 8)
    /// </code>
    /// <para>
    /// All STATUS data items are optional. Each item is represented in this
    /// class using nullable values so that <c>null</c> indicates that the
    /// corresponding item was not present in the server response.
    /// </para>
    /// <para>
    /// IMAP4rev2 defines the following STATUS data items:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>MESSAGES</c> - Total number of messages in the mailbox.</description></item>
    ///   <item><description><c>UIDNEXT</c> - Next UID to be assigned.</description></item>
    ///   <item><description><c>UIDVALIDITY</c> - UID validity value of the mailbox.</description></item>
    ///   <item><description><c>UNSEEN</c> - Number of messages without the <c>\Seen</c> flag.</description></item>
    ///   <item><description><c>DELETED</c> - Number of messages with the <c>\Deleted</c> flag.</description></item>
    ///   <item><description><c>SIZE</c> - Total size, in bytes, of all messages in the mailbox.</description></item>
    /// </list>
    /// <para>
    /// For compatibility with IMAP4rev1 servers, the obsolete
    /// <c>RECENT</c> status data item may also be present.
    /// </para>
    /// <para>
    /// This class derives from <see cref="IMAP_r_u"/>, the base type for all
    /// untagged IMAP responses.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Status : IMAP_r_u
    {
        private string m_FolderName   = "";
        private int?   m_MessageCount = null;
        private long?  m_UidNext      = null;
        private long?  m_UidValidity  = null;
        private int?   m_UnseenCount  = null;
        private int?   m_DeletedCount = null;
        private long?  m_Size         = null;
        private int?   m_RecentCount  = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Status"/> class
        /// using the mailbox name and the optional STATUS data items returned by
        /// the IMAP server.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name associated with this STATUS response.
        /// </param>
        /// <param name="messagesCount">
        /// The total number of messages in the mailbox (<c>MESSAGES</c>), or
        /// <c>null</c> if the server did not include this item.
        /// </param>
        /// <param name="uidNext">
        /// The next unique identifier (UID) that will be assigned to a new
        /// message in the mailbox (<c>UIDNEXT</c>), or <c>null</c> if omitted by
        /// the server.
        /// </param>
        /// <param name="folderUidValidity">
        /// The UID validity value of the mailbox (<c>UIDVALIDITY</c>). May be
        /// <c>null</c> if the server did not include this item.
        /// </param>
        /// <param name="unseenCount">
        /// The number of messages that do not have the <c>\Seen</c> flag set
        /// (<c>UNSEEN</c>), or <c>null</c> if omitted by the server.
        /// </param>
        /// <param name="deletedCount">
        /// The number of messages that have the <c>\Deleted</c> flag set
        /// (<c>DELETED</c>), or <c>null</c> if omitted by the server.
        /// </param>
        /// <param name="size">
        /// The total size, in bytes, of all messages in the mailbox
        /// (<c>SIZE</c>), or <c>null</c> if omitted by the server.
        /// </param>
        /// <param name="recentCount">
        /// The number of messages that have the <c>\Recent</c> flag set
        /// (<c>RECENT</c>), or <c>null</c> if the server did not include this
        /// item.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string.
        /// </exception>
        /// <remarks>
        /// <para>
        /// All STATUS data items are optional. IMAP servers include only the
        /// items explicitly requested by the client. Nullable parameters reflect
        /// this behavior directly.
        /// </para>
        /// <para>
        /// The <c>DELETED</c> and <c>SIZE</c> status data items were introduced
        /// by IMAP4rev2 (RFC 9051). The <c>RECENT</c> status data item is
        /// obsolete in IMAP4rev2 but may still be returned by servers for
        /// compatibility with IMAP4rev1 clients.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Status(string folder,int? messagesCount,long? uidNext,long? folderUidValidity,int? unseenCount,int? deletedCount,long? size,int? recentCount)
        {
            if(folder == null){
                throw new ArgumentNullException("folder");
            }
            if(folder == string.Empty){
                throw new ArgumentException("Argument 'folder' value must be specified.","folder");
            }

            m_FolderName   = folder;
            m_MessageCount = messagesCount;
            m_UidNext      = uidNext;
            m_UidValidity  = folderUidValidity;
            m_UnseenCount  = unseenCount;
            m_DeletedCount = deletedCount;
            m_Size         = size;
            m_RecentCount  = recentCount;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP STATUS untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. STATUS responses provide selected metadata
        /// about a mailbox without requiring the client to SELECT or EXAMINE it,
        /// as defined by RFC 9051.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a STATUS untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading the mailbox name, which
        /// may be encoded as a literal.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_Status"/> instance containing the
        /// mailbox name and the STATUS data items present in the response.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the STATUS response is syntactically invalid, contains
        /// <c>NIL</c> where a mailbox name or numeric status value is required,
        /// or otherwise violates the STATUS response grammar.
        /// </exception>
        /// <remarks>
        /// STATUS responses may contain any combination of the defined status
        /// data items, including <c>MESSAGES</c>, <c>UIDNEXT</c>,
        /// <c>UIDVALIDITY</c>, <c>UNSEEN</c>, <c>DELETED</c>, and
        /// <c>SIZE</c>. For compatibility with IMAP4rev1 servers, the obsolete
        /// <c>RECENT</c> status data item may also be present.
        /// </remarks>
        internal async static Task<IMAP_r_u_Status> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
                IMAP STATUS response (RFC 9051 section 7.2.4)

                The STATUS command returns selected metadata about a mailbox without
                requiring the client to SELECT it. The server sends a single untagged
                STATUS response containing the requested data items.

                    Example:
                        C: A042 STATUS INBOX (MESSAGES UIDNEXT UIDVALIDITY UNSEEN)
                        S: * STATUS INBOX (MESSAGES 17 UIDNEXT 44 UIDVALIDITY 3857529045 UNSEEN 8)
                        S: A042 OK STATUS completed

                Response format:
                    * STATUS <mailbox-name>
                        (<item1> <value1> <item2> <value2> ...)

                IMAP4rev2 STATUS items:
                    MESSAGES
                        Total number of messages in the mailbox.

                    UIDNEXT
                        Next UID that will be assigned to a new message.

                    UIDVALIDITY
                        UID validity value for the mailbox.

                    UNSEEN
                        Number of messages without the \Seen flag.

                    DELETED
                        Number of messages with the \Deleted flag.

                    SIZE
                        Total size, in bytes, of all messages in the mailbox.

                Legacy IMAP4rev1 STATUS item:
                    RECENT
                        Number of messages with the \Recent flag.
                        Obsolete in IMAP4rev2 but may still be returned for
                        compatibility with older clients.

                Notes:
                - STATUS never includes mailbox attributes or hierarchy delimiters.
                - Mailbox names may require modified UTF-7 unless UTF8=ACCEPT or
                  UTF8=ONLY is active.
                - Only the items explicitly requested by the client appear in the
                  response.
                - All STATUS values are non-negative integers.
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP STATUS response (missing *): {imapReader.Text}");
            }

            // "STATUS"
            if(!string.Equals("STATUS",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP STATUS response (missing STATUS): {imapReader.Text}");
            }
            
            // Folder
            string? folder = await imapReader.ReadMailboxAsync(cancellationToken);
            if(folder == null){
                throw new ParseException($"Invalid IMAP STATUS response (expected mailbox-name): {imapReader.Text}");
            }

            // Status items
            int?  messages  = null;
            int?  recent    = null;
            long? uidNext   = null;
            long? folderUid = null;
            int?  unseen    = null;
            int?  deleted   = null;
            int?  size      = null;
            string[] items  = imapReader.ReadParenthesized().Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(items.Length % 2 != 0){
                throw new ParseException($"Invalid STATUS response: {imapReader.Text}");
            }
            for(int i=0;i<items.Length;i+=2){
                try{
                    if(items[i].Equals("MESSAGES",StringComparison.OrdinalIgnoreCase)){
                        messages = Convert.ToInt32(items[i + 1]);
                    }
                    else if(items[i].Equals("UIDNEXT",StringComparison.OrdinalIgnoreCase)){
                        uidNext = Convert.ToInt64(items[i + 1]);
                    }
                    else if(items[i].Equals("UIDVALIDITY",StringComparison.OrdinalIgnoreCase)){
                        folderUid = Convert.ToInt64(items[i + 1]);
                    }
                    else if(items[i].Equals("UNSEEN",StringComparison.OrdinalIgnoreCase)){
                        unseen = Convert.ToInt32(items[i + 1]);
                    }
                    else if(items[i].Equals("DELETED",StringComparison.OrdinalIgnoreCase)){
                        deleted = Convert.ToInt32(items[i + 1]);
                    }
                    else if(items[i].Equals("SIZE",StringComparison.OrdinalIgnoreCase)){
                        size = Convert.ToInt32(items[i + 1]);
                    }
                    else if(items[i].Equals("RECENT",StringComparison.OrdinalIgnoreCase)){
                        recent = Convert.ToInt32(items[i + 1]);
                    }
                }
                catch{
                    throw new ParseException($"Invalid STATUS response: {imapReader.Text}");
                }
            }

            return new IMAP_r_u_Status(folder,messages,uidNext,folderUid,unseen,deleted,size,recent);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire-format representation of this
        /// <c>STATUS</c> response with UTF-8 mailbox names allowed.
        /// </summary>
        /// <returns>
        /// A string containing the serialized IMAP
        /// <c>STATUS</c> response.
        /// </returns>
        /// <remarks>
        /// This method is equivalent to calling
        /// <see cref="ToString(bool)"/> with
        /// <see langword="true"/>.
        /// </remarks>
        public override string ToString()
        {
            return ToString(true);
        }

        /// <summary>
        /// Returns the IMAP wire-format representation of this
        /// <c>STATUS</c> response.
        /// </summary>
        /// <param name="utf8Allowed">
        /// Specifies whether UTF-8 mailbox names are permitted in the
        /// generated IMAP response.
        /// </param>
        /// <returns>
        /// A string containing the IMAP <c>STATUS</c> response in
        /// protocol format, terminated with CRLF.
        /// </returns>
        /// <remarks>
        /// The output format follows RFC 9051:
        /// <code>
        /// * STATUS mailbox
        ///   (item1 value1 item2 value2 ...)
        /// </code>
        /// <para>
        /// Only status data items that have values are included in the
        /// generated response.
        /// </para>
        /// <para>
        /// The following status data items may be emitted:
        /// <c>MESSAGES</c>, <c>UIDNEXT</c>,
        /// <c>UIDVALIDITY</c>, <c>UNSEEN</c>,
        /// <c>DELETED</c>, <c>SIZE</c>, and
        /// <c>RECENT</c>.
        /// </para>
        /// <para>
        /// When <paramref name="utf8Allowed"/> is
        /// <see langword="false"/>, mailbox names are encoded as
        /// required for IMAP servers that do not permit UTF-8 mailbox
        /// names in protocol responses.
        /// </para>
        /// </remarks>
        public override string ToString(bool utf8Allowed)
        {
            // Example:    S: * STATUS blurdybloop (MESSAGES 231 UIDNEXT 44292)

            var items = new List<string>();
            if (m_MessageCount.HasValue){
                items.Add($"MESSAGES {m_MessageCount.Value}");
            }
            if (m_UidNext.HasValue){
                items.Add($"UIDNEXT {m_UidNext.Value}");
            }
            if (m_UidValidity.HasValue){
                items.Add($"UIDVALIDITY {m_UidValidity.Value}");
            }
            if (m_UnseenCount.HasValue){
                items.Add($"UNSEEN {m_UnseenCount.Value}");
            }            
            if (m_DeletedCount.HasValue){
                items.Add($"DELETED {m_DeletedCount.Value}");
            }            
            if (m_Size.HasValue){
                items.Add($"SIZE {m_Size.Value}");
            }
            if (m_RecentCount.HasValue){
                items.Add($"RECENT {m_RecentCount.Value}");
            }

            string mailbox     = IMAP_Utils.EncodeMailbox(m_FolderName,utf8Allowed);
            string statusItems = string.Join(" ",items);

            return $"* STATUS {mailbox} ({statusItems})\r\n";
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the decoded mailbox name associated with this IMAP response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The mailbox name may be quoted or encoded using modified UTF-7 in the
        /// raw IMAP response. It is decoded during parsing so that
        /// <see cref="FolderName"/> always returns the canonical Unicode form.
        /// </para>
        /// <para>
        /// IMAP mailbox names do not include hierarchy delimiters or attribute
        /// flags; those are provided separately in LIST/LSUB responses.
        /// </para>
        /// </remarks>
        public string FolderName
        {
            get{ return m_FolderName; }
        }

        /// <summary>
        /// Gets the total number of messages in the mailbox (<c>MESSAGES</c>),
        /// or <c>null</c> if the server did not include this item in the
        /// <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>MESSAGES</c> data item represents the total number of messages
        /// currently stored in the mailbox. It is one of the optional fields that
        /// may appear in an IMAP <c>STATUS</c> response (RFC 3501 section 7.2.4).
        /// </para>
        /// <para>
        /// IMAP servers include only the STATUS items explicitly requested by the
        /// client. When the server omits <c>MESSAGES</c>, this property will be
        /// <c>null</c>.
        /// </para>
        /// </remarks>
        public int? MessagesCount
        {
            get{ return m_MessageCount; }
        }

        /// <summary>
        /// Gets the next unique identifier (UID) that the IMAP server will assign
        /// to the next message delivered to the mailbox (<c>UIDNEXT</c>), or
        /// <c>null</c> if the server did not include this item in the
        /// <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>UIDNEXT</c> data item represents the UID that will be assigned to
        /// the next message added to the mailbox. It is one of the optional fields
        /// that may appear in an IMAP <c>STATUS</c> response (RFC 3501 section 7.2.4).
        /// </para>
        /// <para>
        /// IMAP servers include only the STATUS items explicitly requested by the
        /// client. When the server omits <c>UIDNEXT</c>, this property will be
        /// <c>null</c>.
        /// </para>
        /// <para>
        /// <c>UIDNEXT</c> does not indicate whether a new message has already arrived.
        /// It only specifies the UID that will be used for the next message added.
        /// The value increases monotonically for the lifetime of the mailbox.
        /// </para>
        /// </remarks>
        public long? UidNext
        {
            get{ return m_UidNext; }
        }

        /// <summary>
        /// Gets the UID validity value of the mailbox (<c>UIDVALIDITY</c>), or
        /// <c>null</c> if the server did not include this item in the
        /// <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>UIDVALIDITY</c> data item is a monotonically increasing value
        /// assigned by the IMAP server to indicate the stability of the mailbox’s
        /// UID sequence. When the mailbox is recreated or its UID sequence becomes
        /// invalid, the server assigns a new UIDVALIDITY value.
        /// </para>
        /// <para>
        /// Clients use UIDVALIDITY to determine whether previously cached UIDs
        /// remain valid. If the UIDVALIDITY value changes, all cached UIDs for the
        /// mailbox must be discarded.
        /// </para>
        /// <para>
        /// UIDVALIDITY is optional in a <c>STATUS</c> response. IMAP servers include
        /// only the items explicitly requested by the client. When omitted, this
        /// property will be <c>null</c>.
        /// </para>
        /// </remarks>
        public long? FolderUidValidity
        {
            get{ return m_UidValidity; }
        }

        /// <summary>
        /// Gets the number of messages in the mailbox that do not have the
        /// <c>\Seen</c> flag set (<c>UNSEEN</c>), or <c>null</c> if the server
        /// did not include this item in the <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>UNSEEN</c> data item represents the count of messages that the
        /// server considers unread. It is one of the optional fields that may
        /// appear in an IMAP <c>STATUS</c> response (RFC 3501 section 7.2.4).
        /// </para>
        /// <para>
        /// IMAP servers include only the STATUS items explicitly requested by the
        /// client. When the server omits <c>UNSEEN</c>, this property will be
        /// <c>null</c>.
        /// </para>
        /// <para>
        /// Unlike <c>RECENT</c>, which indicates newly delivered messages since the
        /// last mailbox open, <c>UNSEEN</c> reflects the current number of messages
        /// lacking the <c>\Seen</c> flag, regardless of when they arrived.
        /// </para>
        /// </remarks>
        public int? UnseenCount
        {
            get{ return m_UnseenCount; }
        }

        /// <summary>
        /// Gets the number of messages in the mailbox that have the
        /// <c>\Deleted</c> flag set (<c>DELETED</c>), or <c>null</c> if the
        /// server did not include this item in the <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>DELETED</c> status data item represents the number of messages
        /// currently marked with the <c>\Deleted</c> flag in the mailbox.
        /// </para>
        /// <para>
        /// The <c>DELETED</c> status data item was introduced by IMAP4rev2
        /// (RFC 9051).
        /// </para>
        /// <para>
        /// IMAP servers include only the STATUS items explicitly requested by the
        /// client. When the server omits <c>DELETED</c>, this property will be
        /// <c>null</c>.
        /// </para>
        /// </remarks>
        public int? DeletedCount
        {
            get{ return m_DeletedCount; }
        }

        /// <summary>
        /// Gets the total size, in bytes, of all messages in the mailbox
        /// (<c>SIZE</c>), or <c>null</c> if the server did not include this item
        /// in the <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>SIZE</c> status data item represents the total size, in bytes,
        /// of all messages contained in the mailbox.
        /// </para>
        /// <para>
        /// The <c>SIZE</c> status data item was introduced by IMAP4rev2
        /// (RFC 9051).
        /// </para>
        /// <para>
        /// IMAP servers include only the STATUS items explicitly requested by the
        /// client. When the server omits <c>SIZE</c>, this property will be
        /// <c>null</c>.
        /// </para>
        /// </remarks>
        public long? Size
        {
            get{ return m_Size; }
        }

        /// <summary>
        /// Gets the number of messages in the mailbox that have the <c>\Recent</c>
        /// flag set (<c>RECENT</c>), or <c>null</c> if the server did not include
        /// this item in the <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>RECENT</c> status data item represents the number of messages
        /// that have the <c>\Recent</c> flag set in the mailbox. This status item
        /// was defined by IMAP4rev1 (RFC 3501) and may still be returned by
        /// servers for compatibility with older clients.
        /// </para>
        /// <para>
        /// The <c>RECENT</c> status data item and the <c>\Recent</c> flag are
        /// obsolete in IMAP4rev2 (RFC 9051).
        /// </para>
        /// <para>
        /// IMAP servers include only the STATUS items explicitly requested by the
        /// client. When the server omits <c>RECENT</c>, this property will be
        /// <c>null</c>.
        /// </para>
        /// </remarks>
        public int? RecentCount
        {
            get{ return m_RecentCount;}
        }

        #endregion
    }
}
