using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP <c>STATUS</c> response as defined in
    /// RFC 3501 section 7.2.4. A STATUS response provides selected metadata
    /// about a mailbox without requiring the client to SELECT or EXAMINE it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IMAP <c>STATUS</c> command allows a client to request specific
    /// mailbox metadata items without changing the currently selected mailbox.
    /// The client sends a command of the form:
    /// </para>
    /// <code>
    /// A01 STATUS mailbox-name (MESSAGES UIDNEXT UNSEEN)
    /// </code>
    /// <para>
    /// The server replies with an untagged STATUS response containing exactly
    /// the items requested by the client:
    /// </para>
    /// <code>
    /// * STATUS mailbox-name (MESSAGES 231 UIDNEXT 44292 UNSEEN 8)
    /// </code>
    /// <para>
    /// All STATUS items are optional. Each item is represented in this class
    /// using nullable fields (<c>int?</c>, <c>long?</c>) so that <c>null</c>
    /// indicates that the item was not present in the server's response.
    /// </para>
    /// <para>
    /// Common STATUS items include:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>MESSAGES</c> — Total number of messages.</description></item>
    ///   <item><description><c>RECENT</c> — Messages with the <c>\Recent</c> flag.</description></item>
    ///   <item><description><c>UIDNEXT</c> — Next UID to be assigned.</description></item>
    ///   <item><description><c>UIDVALIDITY</c> — UID validity value of the mailbox.</description></item>
    ///   <item><description><c>UNSEEN</c> — Messages without the <c>\Seen</c> flag.</description></item>
    /// </list>
    /// <para>
    /// This class stores the mailbox name in decoded Unicode form. Encoding
    /// (modified UTF‑7 or UTF‑8) is applied only when generating the wire‑format
    /// response via <see cref="ToString(IMAP_Mailbox_Encoding)"/>.
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
        private int?   m_RecentCount  = null;
        private long?  m_UidNext      = null;
        private long?  m_UidValidity  = null;
        private int?   m_UnseenCount  = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Status"/> class
        /// using the mailbox name and the optional STATUS metadata values returned
        /// by the IMAP server.
        /// </summary>
        /// <param name="folder">
        /// The decoded mailbox name associated with this STATUS response. The value
        /// must be a non-empty string and may originate from a quoted or modified
        /// UTF‑7 encoded name in the raw IMAP response.
        /// </param>
        /// <param name="messagesCount">
        /// The total number of messages in the mailbox (<c>MESSAGES</c>), or
        /// <c>null</c> if the server did not include this item.
        /// </param>
        /// <param name="recentCount">
        /// The number of messages with the <c>\Recent</c> flag (<c>RECENT</c>), or
        /// <c>null</c> if the server did not include this item.
        /// </param>
        /// <param name="uidNext">
        /// The next unique identifier (UID) that will be assigned to the next
        /// message delivered to the mailbox (<c>UIDNEXT</c>), or <c>null</c> if
        /// omitted by the server.
        /// </param>
        /// <param name="folderUidValidity">
        /// The UID validity value of the mailbox (<c>UIDVALIDITY</c>). This acts as
        /// a version identifier for the mailbox’s UID sequence and is commonly used
        /// by clients to determine whether cached UIDs remain valid. May be
        /// <c>null</c> if the server did not include this item.
        /// </param>
        /// <param name="unseenCount">
        /// The number of messages that do not have the <c>\Seen</c> flag
        /// (<c>UNSEEN</c>), or <c>null</c> if omitted by the server.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string.
        /// </exception>
        /// <remarks>
        /// <para>
        /// All STATUS items are optional. IMAP servers include only the items
        /// explicitly requested by the client. Nullable parameters reflect this
        /// behavior directly.
        /// </para>
        /// <para>
        /// This constructor is typically invoked by the STATUS parser after decoding
        /// the mailbox name and extracting the STATUS data items from the server’s
        /// response.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Status(string folder,int? messagesCount,int? recentCount,long? uidNext,long? folderUidValidity,int? unseenCount)
        {
            if(folder == null){
                throw new ArgumentNullException("folder");
            }
            if(folder == string.Empty){
                throw new ArgumentException("Argument 'folder' value must be specified.","folder");
            }

            m_FolderName   = folder;
            m_MessageCount = messagesCount;
            m_RecentCount  = recentCount;
            m_UidNext      = uidNext;
            m_UidValidity  = folderUidValidity;
            m_UnseenCount  = unseenCount;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP STATUS untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. STATUS responses provide selected metadata
        /// about a mailbox without requiring the client to SELECT it, as defined in
        /// RFC 3501 section 7.2.4.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a STATUS untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading the mailbox name, which may
        /// be encoded as a literal.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_Status"/> instance containing the mailbox
        /// name and the available STATUS item values.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the STATUS response is syntactically invalid, contains <c>NIL</c>
        /// where an astring or number is required, or violates RFC 3501 structural
        /// rules.
        /// </exception>
        internal async static Task<IMAP_r_u_Status> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
                IMAP STATUS response (RFC 3501 section 7.2.4)

                The STATUS command returns selected metadata about a mailbox without
                requiring the client to SELECT it. The server sends a single untagged
                STATUS response containing the requested data items.

                    Example:
                        C: A042 STATUS INBOX (MESSAGES UIDNEXT UIDVALIDITY UNSEEN)
                        S: * STATUS INBOX (MESSAGES 17 UIDNEXT 44 UIDVALIDITY 3857529045 UNSEEN 8)
                        S: A042 OK STATUS completed

                Response format:
                    * STATUS <mailbox-name> (<item1> <value1> <item2> <value2> ...)

                Valid STATUS items:
                    MESSAGES     - Total number of messages in the mailbox.
                    RECENT       - Number of messages with the \Recent flag.
                    UIDNEXT      - Next UID that will be assigned to a new message.
                    UIDVALIDITY  - UID validity value for the mailbox.
                    UNSEEN       - Number of messages without the \Seen flag.

                Notes:
                - STATUS never includes mailbox attributes or hierarchy delimiters.
                - Mailbox names may require modified UTF-7 unless UTF8=ACCEPT/ONLY is active.
                - Only the items explicitly requested by the client appear in the response.
                - Values are always integers.
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
            string[] items  = imapReader.ReadParenthesized().Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(items.Length % 2 != 0){
                throw new ParseException($"Invalid STATUS response: {imapReader.Text}");
            }
            for(int i=0;i<items.Length;i+=2){
                try{
                    if(items[i].Equals("MESSAGES",StringComparison.OrdinalIgnoreCase)){
                        messages = Convert.ToInt32(items[i + 1]);
                    }
                    else if(items[i].Equals("RECENT",StringComparison.OrdinalIgnoreCase)){
                        recent = Convert.ToInt32(items[i + 1]);
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
                }
                catch{
                    throw new ParseException($"Invalid STATUS response: {imapReader.Text}");
                }
            }

            return new IMAP_r_u_Status(folder,messages,recent,uidNext,folderUid,unseen);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire‑format representation of this <c>STATUS</c>
        /// response using <see cref="IMAP_Mailbox_Encoding.None"/>, which emits
        /// the mailbox name in Unicode without applying modified UTF‑7 encoding.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This overload is a convenience method that produces a STATUS response
        /// suitable for debugging, logging, or use with IMAP servers that support
        /// UTF‑8 mailbox names (RFC 6855, UTF8=ACCEPT/ONLY). The mailbox name is
        /// encoded exactly as stored in the object without conversion to modified
        /// UTF‑7.
        /// </para>
        /// <para>
        /// For compatibility with legacy IMAP servers that require modified UTF‑7,
        /// use <see cref="ToString(IMAP_Mailbox_Encoding)"/> and specify the desired
        /// encoding explicitly.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return ToString(IMAP_Mailbox_Encoding.None);
        }

        /// <summary>
        /// Builds the IMAP <c>STATUS</c> response in wire format using the specified
        /// mailbox encoding. Only STATUS items that have values are included.
        /// </summary>
        /// <param name="encoding">
        /// The encoding to use for the mailbox name. This is typically modified UTF‑7
        /// for legacy IMAP servers or UTF‑8 when the server advertises UTF8=ACCEPT/ONLY.
        /// </param>
        /// <returns>
        /// A correctly formatted IMAP <c>STATUS</c> response line ending with CRLF.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The IMAP <c>STATUS</c> response reports selected metadata about a mailbox
        /// without requiring the client to SELECT it. The general form is:
        /// </para>
        /// <code>
        /// * STATUS mailbox-name (item1 value1 item2 value2 ...)
        /// </code>
        /// <para>
        /// STATUS items are optional. Only items whose corresponding fields have
        /// values (non‑null) are emitted. This matches the behavior of the IMAP
        /// <c>STATUS</c> command, where the server includes only the items explicitly
        /// requested by the client.
        /// </para>
        /// <para>
        /// Valid STATUS items include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description><c>MESSAGES</c> — Total number of messages.</description></item>
        ///   <item><description><c>RECENT</c> — Messages with the <c>\Recent</c> flag.</description></item>
        ///   <item><description><c>UIDNEXT</c> — Next UID to be assigned.</description></item>
        ///   <item><description><c>UIDVALIDITY</c> — UID validity value of the mailbox.</description></item>
        ///   <item><description><c>UNSEEN</c> — Messages without the <c>\Seen</c> flag.</description></item>
        /// </list>
        /// <para>
        /// The mailbox name is encoded using <see cref="IMAP_Utils.EncodeMailbox"/> and
        /// may require modified UTF‑7 depending on server capabilities.
        /// </para>
        /// </remarks>
        public override string ToString(IMAP_Mailbox_Encoding encoding)
        {
            // Example:    S: * STATUS blurdybloop (MESSAGES 231 UIDNEXT 44292)

            var items = new List<string>();
            if (m_MessageCount.HasValue){
                items.Add($"MESSAGES {m_MessageCount.Value}");
            }
            if (m_RecentCount.HasValue){
                items.Add($"RECENT {m_RecentCount.Value}");
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

            string mailbox     = IMAP_Utils.EncodeMailbox(m_FolderName, encoding);
            string statusItems = string.Join(" ", items);

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
        /// Gets the number of messages in the mailbox that have the <c>\Recent</c>
        /// flag set (<c>RECENT</c>), or <c>null</c> if the server did not include
        /// this item in the <c>STATUS</c> response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>RECENT</c> data item corresponds to the number of messages that
        /// the server considers newly delivered to the mailbox since the last time
        /// it was opened. It is one of the optional fields that may appear in an
        /// IMAP <c>STATUS</c> response (RFC 3501 section 7.2.4).
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

        #endregion
    }
}
