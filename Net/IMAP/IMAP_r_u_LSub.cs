using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a single untagged IMAP <c>LSUB</c> response as defined in
    /// RFC 3501 section 7.2.3. An LSUB response describes a mailbox to which
    /// the user is subscribed, including its attribute list, hierarchy
    /// delimiter, and decoded mailbox name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The LSUB response is structurally identical to the LIST response, but
    /// differs in semantics: it reports only mailboxes that the user is
    /// subscribed to. Each response includes:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     A list of attribute flags (for example <c>\Noselect</c>,
    ///     <c>\Marked</c>, <c>\HasChildren</c>).
    ///   </description></item>
    ///   <item><description>
    ///     A hierarchy delimiter, which is either a single ASCII character or
    ///     <c>NIL</c> when the server does not support hierarchical mailbox
    ///     names.
    ///   </description></item>
    ///   <item><description>
    ///     A mailbox name encoded as a quoted string, which may contain
    ///     modified UTF-7 and must be decoded.
    ///   </description></item>
    /// </list>
    /// <para>
    /// Instances of this class are typically created by the
    /// <c>Parse</c> method, which interprets a raw LSUB response line and
    /// extracts the mailbox components.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_LSub : IMAP_r_u
    {
        private string   m_FolderName        = "";
        private char?    m_Delimiter         = '/';
        private string[] m_pFolderAttributes = new string[0];

        /// <summary>
        /// Initializes a new instance of the IMAP_r_u_LSub class using the mailbox
        /// name, hierarchy delimiter, and attribute list extracted from an IMAP
        /// <c>LSUB</c> response.
        /// </summary>
        /// <param name="folder">
        /// The decoded mailbox name. This value is the modified UTF-7 decoded form of
        /// the quoted mailbox name returned by the server.
        /// </param>
        /// <param name="delimiter">
        /// The hierarchy delimiter returned by the server. This is either a single
        /// ASCII character (for example '/' or '.') when hierarchy is supported, or
        /// <c>null</c> when the server returns <c>NIL</c>, indicating that the mailbox
        /// namespace does not support hierarchical child mailboxes.
        /// </param>
        /// <param name="attributes">
        /// The list of mailbox attributes returned inside the parentheses of the
        /// <c>LSUB</c> response. Examples include \Noselect, \Marked, \Unmarked,
        /// \HasChildren, and \HasNoChildren. The array contains each attribute token
        /// exactly as provided by the server.
        /// </param>
        /// <remarks>
        /// <para>
        /// This constructor is typically used by the <c>Parse</c> method to represent a
        /// single untagged <c>LSUB</c> response as defined in RFC 3501 section 7.2.3.
        /// </para>
        /// <para>
        /// The <paramref name="delimiter"/> parameter uses a nullable character so that
        /// the IMAP <c>NIL</c> delimiter can be represented cleanly.
        /// </para>
        /// </remarks>
        public IMAP_r_u_LSub(string folder,char? delimiter,string[] attributes)
        {
            if(folder == null){
                throw new ArgumentNullException("folder");
            }
            if(folder == string.Empty){
                throw new ArgumentException("Argument 'folder' value must be specified.","folder");
            }

            m_FolderName = folder;
            m_Delimiter  = delimiter;
            if(attributes != null){
                m_pFolderAttributes = attributes;
            }
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP LSUB untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. LSUB responses describe mailboxes to which
        /// the user is subscribed. The response format is structurally identical to
        /// the LIST response defined in RFC 3501 section 7.2.2, with the sole
        /// difference that the response label is <c>LSUB</c> instead of <c>LIST</c>.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of an LSUB untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading mailbox names or other
        /// tokens that may be encoded as literals.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_LSub"/> instance containing the mailbox
        /// name, hierarchy delimiter, and attribute list.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the LSUB response is syntactically invalid or missing required
        /// components.
        /// </exception>
        internal async static Task<IMAP_r_u_LSub> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /* RFC 3501 section 7.2.3 — LSUB Response

               The LSUB response is an untagged server response that describes a mailbox
               to which the user is subscribed. It is returned as part of the LSUB command
               execution. Each LSUB response provides the mailbox attributes, the hierarchy
               delimiter, and the mailbox name.

               LSUB-Response = "*" SP "LSUB" SP "(" [mbox-attr *(SP mbox-attr)] ")" SP
                               quoted-delimiter SP quoted-mailbox-name

               Structure:
                 - Identical to the LIST response structure.
                 - The only difference is semantic: LSUB returns *subscribed* mailboxes,
                   while LIST returns *all* mailboxes matching the pattern.

               Components:
                 - mbox-attr:
                     \Noinferiors   — mailbox cannot have children
                     \Noselect      — mailbox cannot be selected
                     \Marked        — mailbox has new messages
                     \Unmarked      — mailbox has no new messages
                     \HasChildren   — server-defined attribute (RFC 3348)
                     \HasNoChildren — server-defined attribute (RFC 3348)

                 - quoted-delimiter:
                     The hierarchy separator for this mailbox (commonly "/").
                     If the server does not permit hierarchy for this mailbox,
                     the delimiter may be returned as NIL.
                     The delimiter is always a single ASCII character or NIL.

                 - quoted-mailbox-name:
                     The mailbox name, always returned as a quoted string.
                     The name may include hierarchy levels (e.g., "INBOX/Work").
                     The name may contain modified UTF-7 encoding and must be decoded.

               Notes:
                 - LSUB responses are untagged and may appear in any order.
                 - LSUB does not indicate message counts or mailbox status.
                 - LSUB does not select a mailbox; it only describes subscription state.
                 - LSUB may return zero or more responses depending on the pattern.
                 - LSUB responses may include server-specific attributes beyond those
                   defined in RFC 3501.
                 - The structure is byte-for-byte identical to LIST responses except for
                   the response code ("LSUB" instead of "LIST").

               Example:
                 S: * LSUB (\Noselect) "/" "Archive"
                 S: * LSUB () "/" "INBOX"
                 S: * LSUB (\Marked) "/" "INBOX/Work"
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP LSUB response (missing *): {imapReader.Text}");
            }

            // "LSUB"
            if(!string.Equals("LSUB",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP LSUB response (missing LSUB): {imapReader.Text}");
            }

            // Attributes
            string attributes = imapReader.ReadParenthesized();

            // Delimiter
            string? delimiter = await imapReader.ReadStringAsync(cancellationToken); 
            char?   delimiterChar;
            if(delimiter == null){
                delimiterChar = null;
            }
            else if(delimiter.Length != 1){
                throw new ParseException($"Invalid IMAP LSUB response, missing hierarchy delimiter.: {imapReader.Text}");
            }
            else{
                delimiterChar = delimiter[0];
            }

            // Mailbox name
            string? folder = await imapReader.ReadMailboxAsync(cancellationToken); 
            if(folder == null){
                throw new ParseException($"Invalid IMAP LSUB response, missing mailbox-name: {imapReader.Text}");
            }

            return new IMAP_r_u_LSub(folder,delimiterChar,attributes.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries));
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire-format representation of this LSUB response using
        /// <see cref="IMAP_Mailbox_Encoding.None"/>, which leaves the mailbox name
        /// in Unicode without applying modified UTF-7 encoding.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see cref="IMAP_Mailbox_Encoding.None"/> is used, the mailbox name is
        /// emitted exactly as stored in the object, using Unicode characters. This
        /// corresponds to IMAP UTF-8 mode as defined in RFC 6855, where servers that
        /// advertise UTF8=ACCEPT or UTF8=ONLY allow mailbox names to be transmitted
        /// directly in UTF-8 without modified UTF-7 encoding.
        /// </para>
        /// <para>
        /// This overload is intended for debugging, logging, or use with IMAP servers
        /// that support UTF-8 mailbox names. For compatibility with legacy IMAP
        /// servers that require modified UTF-7, use
        /// <see cref="ToString(IMAP_Mailbox_Encoding)"/> with the appropriate encoding.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return ToString(IMAP_Mailbox_Encoding.None);
        }

        /// <summary>
        /// Converts this LSUB response to its IMAP wire-format representation,
        /// including attribute list, hierarchy delimiter, and encoded mailbox name.
        /// </summary>
        /// <param name="encoding">
        /// Specifies how the mailbox name should be encoded. When set to
        /// <see cref="IMAP_Mailbox_Encoding.None"/>, the mailbox name is emitted
        /// in Unicode (UTF-8 on the wire) without applying modified UTF-7 encoding.
        /// </param>
        /// <returns>
        /// A string containing the IMAP <c>LSUB</c> response in protocol format,
        /// terminated with CRLF. The output includes the attribute list, the
        /// hierarchy delimiter (quoted when present, or the atom <c>NIL</c> when
        /// no delimiter exists), and the mailbox name encoded according to the
        /// specified <paramref name="encoding"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The LSUB response format is defined in RFC 3501 section 7.2.3. The
        /// hierarchy delimiter follows the LIST/LSUB grammar:
        /// </para>
        /// <code>
        /// quoted-delimiter = DQUOTE *CHAR DQUOTE / "NIL"
        /// </code>
        /// <para>
        /// This means that delimiter characters are emitted as quoted strings
        /// (for example <c>"/"</c> or <c>"."</c>), while the absence of a delimiter
        /// is represented by the unquoted atom <c>NIL</c>.
        /// </para>
        /// <para>
        /// LSUB responses describe mailboxes to which the user is subscribed.
        /// They do not indicate message counts or structural hierarchy beyond
        /// the presence of the mailbox in the subscription list.
        /// </para>
        /// <para>
        /// The mailbox name is encoded using
        /// <see cref="IMAP_Utils.EncodeMailbox(string, IMAP_Mailbox_Encoding)"/>,
        /// which applies modified UTF-7 when required for compatibility with
        /// legacy IMAP servers.
        /// </para>
        /// </remarks>
        public override string ToString(IMAP_Mailbox_Encoding encoding)
        {
            // Example:    S: * LSUB (\Noselect) "/" ~/Mail/foo

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* LSUB (");
            if(m_pFolderAttributes != null){
                for(int i=0;i<m_pFolderAttributes.Length;i++){
                    if(i > 0){
                        retVal.Append(" ");
                    }
                    retVal.Append(m_pFolderAttributes[i]);
                }
            }
            retVal.Append(") ");
            retVal.Append(m_Delimiter != null ? "\"" + m_Delimiter + "\" " : "NIL ");
            retVal.Append(IMAP_Utils.EncodeMailbox(m_FolderName,encoding));
            retVal.Append("\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the decoded mailbox name returned by the IMAP response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value represents the mailbox name exactly as provided by the server,
        /// after decoding from modified UTF-7. Mailbox names may contain hierarchy
        /// levels separated by the server-defined delimiter (for example "INBOX/Work").
        /// </para>
        /// <para>
        /// This property always contains a valid, non-null string. If the server
        /// returns an empty quoted mailbox name, it is represented as an empty string.
        /// </para>
        /// </remarks>
        public string FolderName
        {
            get{ return m_FolderName; }
        }

        /// <summary>
        /// Gets the hierarchy delimiter returned by the IMAP response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The delimiter indicates how mailbox hierarchy levels are separated. When the
        /// server supports hierarchical mailbox names, this value is a single ASCII
        /// character such as '/' or '.'. 
        /// </para>
        /// <para>
        /// This property always returns a valid character. If the server reports a
        /// <c>NIL</c> delimiter, the constructor must either substitute a default value
        /// or reject the response, since a non-nullable <c>char</c> cannot represent
        /// the absence of a delimiter.
        /// </para>
        /// <para>
        /// The delimiter value is taken exactly as provided by the server when
        /// hierarchy is supported and is not decoded or transformed.
        /// </para>
        /// </remarks>
        public char? Delimiter
        {
            get{ return m_Delimiter; }
        }

        /// <summary>
        /// Gets the list of attribute flags returned by the IMAP response for
        /// this mailbox.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Attribute flags describe server-defined properties of the mailbox. Common
        /// examples include:
        /// </para>
        /// <list type="bullet">
        ///   <item><description><c>\Noselect</c> — the mailbox cannot be selected.</description></item>
        ///   <item><description><c>\Noinferiors</c> — the mailbox cannot have child mailboxes.</description></item>
        ///   <item><description><c>\Marked</c> — the mailbox has new messages.</description></item>
        ///   <item><description><c>\Unmarked</c> — the mailbox has no new messages.</description></item>
        ///   <item><description><c>\HasChildren</c> — the mailbox contains child mailboxes (RFC 3348).</description></item>
        ///   <item><description><c>\HasNoChildren</c> — the mailbox has no child mailboxes (RFC 3348).</description></item>
        /// </list>
        /// <para>
        /// The array contains each attribute exactly as provided by the server, without
        /// modification. If the server returns no attributes, this property contains an
        /// empty array.
        /// </para>
        /// </remarks>
        public string[] FolderAttributes
        {
            get{ return m_pFolderAttributes; }
        }

        #endregion
    }
}
