using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a single untagged IMAP <c>LIST</c> response as defined in
    /// RFC 9051 section 7.2.2. A LIST response describes a mailbox available
    /// on the server, including its attribute list, hierarchy delimiter, and
    /// mailbox name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The LIST response provides structural information about mailboxes
    /// matching the pattern supplied to the LIST command. Each response
    /// includes:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     A list of mailbox attributes (for example <c>\Noselect</c>,
    ///     <c>\Marked</c>, <c>\HasChildren</c>, <c>\HasNoChildren</c>).
    ///   </description></item>
    ///   <item><description>
    ///     A hierarchy delimiter, which is either a single character or
    ///     <c>NIL</c> if no hierarchy delimiter is defined.
    ///   </description></item>
    ///   <item><description>
    ///     A mailbox name represented as an IMAP string. Depending on
    ///     negotiated UTF-8 capabilities, non-ASCII mailbox names may be
    ///     encoded either as UTF-8 or legacy modified UTF-7.
    ///   </description></item>
    /// </list>
    /// <para>
    /// LIST responses describe mailbox hierarchy and mailbox attributes.
    /// They do not include subscription status, message counts, or mailbox
    /// status information.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_List : IMAP_r_u
    {
        private string   m_FolderName        = "";
        private char?    m_Delimiter         = '/';
        private string[] m_pFolderAttributes = new string[0];
                
        /// <summary>
        /// Initializes a new instance of the IMAP_r_u_List class using the mailbox
        /// name, hierarchy delimiter, and attribute list extracted from an IMAP LIST
        /// response.
        /// </summary>
        /// <param name="folder">
        /// The decoded mailbox name. This value is the modified UTF-7 decoded form of
        /// the quoted mailbox name returned by the server.
        /// </param>
        /// <param name="delimiter">
        /// The hierarchy delimiter returned by the server. This is either a single
        /// ASCII character when hierarchy is supported (for example '/' or '.'),
        /// or a <c>null</c> value when the server returns <c>NIL</c>, indicating that
        /// the mailbox does not support hierarchical child mailboxes.
        /// </param>
        /// <param name="attributes">
        /// The list of mailbox attributes returned inside the parentheses of the LIST
        /// response. Examples include \Noselect, \Marked, \Unmarked, \HasChildren, and
        /// \HasNoChildren. The array contains each attribute token exactly as provided
        /// by the server.
        /// </param>
        public IMAP_r_u_List(string folder,char? delimiter,string[] attributes)
        {
            if(folder == null){
                throw new ArgumentNullException("folder");
            }

            m_FolderName = folder;
            m_Delimiter  = delimiter;
            if(attributes != null){
                m_pFolderAttributes = attributes;
            }
        }

        /// <summary>
        /// Initializes a new instance of the IMAP_r_u_List class using only the
        /// hierarchy delimiter. This constructor is used for LIST delimiter-only
        /// responses, such as those returned by the command <c>LIST "" ""</c>.
        /// </summary>
        /// <param name="delimiter">
        /// The hierarchy delimiter returned by the server. This is either a single
        /// ASCII character (for example '/' or '.') when hierarchy is supported, or
        /// <c>null</c> when the server returns <c>NIL</c>, indicating that the mailbox
        /// namespace does not support hierarchical child mailboxes.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if the delimiter value is not valid. A valid delimiter must be either
        /// a single printable ASCII character or <c>null</c> to represent IMAP NIL.
        /// </exception>
        /// <remarks>
        /// <para>
        /// IMAP servers return a delimiter-only LIST response when the client requests
        /// the hierarchy delimiter using <c>LIST "" ""</c>. In this case, no mailbox
        /// name or attribute list is present.
        /// </para>
        /// <para>
        /// For full LIST responses containing attributes and mailbox names, use the
        /// constructor that accepts the folder name, delimiter, and attribute list.
        /// </para>
        /// </remarks>
        internal IMAP_r_u_List(char? delimiter)
        {
            m_Delimiter = delimiter;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP LIST untagged response using the supplied <see cref="_IMAP_Reader"/>.
        /// LIST responses describe a single mailbox and include its attribute set,
        /// hierarchy delimiter, and mailbox name. This method implements the grammar
        /// defined in RFC 3501 section 7.2.2.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a LIST untagged response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading mailbox names or other tokens
        /// that may be encoded as literals.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_List"/> instance containing the mailbox
        /// name, hierarchy delimiter, and attribute list.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the LIST response is syntactically invalid or missing required
        /// components.
        /// </exception>
        internal async static Task<IMAP_r_u_List> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /* RFC 3501 section 7.2.2 — LIST Response

               The LIST response is an untagged server response that describes a mailbox.
               It is returned as part of the LIST command execution. Each LIST response
               provides the mailbox attributes, the hierarchy delimiter, and the mailbox
               name.

               LIST-Response = "*" SP "LIST" SP "(" [mbox-attr *(SP mbox-attr)] ")" SP
                               quoted-delimiter SP quoted-mailbox-name

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

                 - quoted-mailbox-name:
                     The mailbox name, always returned as a quoted string.
                     The name may include hierarchy levels (e.g., "INBOX/Work").

               Notes:
                 - LIST responses are untagged and may appear in any order.
                 - LIST responses do not indicate message counts or mailbox status.
                 - LIST responses do not select a mailbox; they only describe it.
                 - LIST may return zero or more LIST responses depending on the pattern.
                 - LIST responses may include server-specific attributes beyond those
                   defined in RFC 3501.

               Example:
                 S: * LIST (\Noselect) "/" "Archive"
                 S: * LIST () "/" "INBOX"
                 S: * LIST (\Marked) "/" "INBOX/Work"
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP LIST response (missing *): {imapReader.Text}");
            }

            // "LIST"
            if(!string.Equals("LIST",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP LIST response (missing LIST): {imapReader.Text}");
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
                throw new ParseException($"Invalid IMAP LIST response, missing hierarchy delimiter.: {imapReader.Text}");
            }
            else{
                delimiterChar = delimiter[0];
            }

            // Mailbox name
            string? folder = await imapReader.ReadMailboxAsync(cancellationToken);
            if(folder == null){
                throw new ParseException($"Invalid IMAP LIST response, missing mailbox-name: {imapReader.Text}");
            }

            return new IMAP_r_u_List(folder,delimiterChar,attributes.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries));
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire-format representation of this LIST
        /// response with UTF-8 mailbox names allowed.
        /// </summary>
        /// <returns>
        /// A string containing the serialized IMAP <c>LIST</c> response.
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
        /// Returns the IMAP wire-format representation of this LIST
        /// response.
        /// </summary>
        /// <param name="utf8Allowed">
        /// Specifies whether UTF-8 mailbox names are permitted in the
        /// generated IMAP response.
        /// </param>
        /// <returns>
        /// A string containing the IMAP <c>LIST</c> response terminated
        /// with CRLF. The output includes the mailbox attributes,
        /// hierarchy delimiter, and mailbox name.
        /// </returns>
        /// <remarks>
        /// When <paramref name="utf8Allowed"/> is
        /// <see langword="false"/>, mailbox names are encoded as
        /// required for IMAP servers that do not permit UTF-8 mailbox
        /// names in protocol responses.
        /// </remarks>
        public override string ToString(bool utf8Allowed)
        {
            /*
                Example:  S: * LIST (\Noselect) "/" ~/Mail/foo
             
                          C: A101 LIST "" ""
                          S: * LIST (\Noselect) "/" ""
                          S: A101 OK LIST Completed
            */

            // Hierarchy delimiter request.
            if(string.IsNullOrEmpty(m_FolderName)){
                return "* LIST (\\Noselect) " + (m_Delimiter != null ? "\"" + m_Delimiter + "\"" : "NIL") + " \"\"\r\n";
            }
            else{
                StringBuilder retVal = new StringBuilder();
                retVal.Append("* LIST (");
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
                retVal.Append(IMAP_Utils.EncodeMailbox(m_FolderName,utf8Allowed));
                retVal.Append("\r\n");

                return retVal.ToString();
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the decoded mailbox name returned by the IMAP LIST response.
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
        /// Gets the hierarchy delimiter returned by the IMAP LIST response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The delimiter indicates how mailbox hierarchy levels are separated. When the
        /// server supports hierarchical mailbox names, this value is a single ASCII
        /// character such as '/' or '.'. 
        /// </para>
        /// <para>
        /// When the server returns <c>NIL</c> for the delimiter, this property contains
        /// <c>null</c>, meaning that the mailbox namespace does not support child
        /// mailboxes and all mailbox names are considered flat.
        /// </para>
        /// <para>
        /// The delimiter value is taken exactly as provided by the server and is not
        /// decoded or transformed.
        /// </para>
        /// </remarks>
        public char? Delimiter
        {
            get{ return m_Delimiter; }
        }

        /// <summary>
        /// Gets the list of attribute flags returned by the IMAP LIST response for
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
        /// The array contains each attribute exactly as returned by the server, without
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
