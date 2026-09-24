using LumiSoft.Net.IMAP.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP <c>LISTRIGHTS</c> untagged response as defined in
    /// RFC 4314. The response reports the rights that may be granted to a
    /// specific identifier for a given mailbox.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A LISTRIGHTS response provides the set of rights that the server allows to
    /// be granted to an ACL identifier (user, group, or special subject) for a
    /// particular mailbox. It does <em>not</em> report the rights currently
    /// assigned; use <c>GETACL</c> or <c>MYRIGHTS</c> to obtain active rights.
    /// </para>
    /// <para>
    /// The response format is:
    /// </para>
    /// <code>
    /// * LISTRIGHTS &lt;mailbox&gt; &lt;identifier&gt; &lt;required&gt; [ &lt;optional&gt; ... ]
    /// </code>
    /// </remarks>
    public class IMAP_r_u_ListRights : IMAP_r_u
    {
        private string   m_FolderName     = "";
        private string   m_Identifier     = "";
        private string   m_RequiredRights = "";
        private string[] m_OptionalRights = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_ListRights"/> class
        /// using the specified mailbox name, identifier, required rights, and optional
        /// rights set.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox for which the LISTRIGHTS information applies.
        /// The value must be a non‑empty string and is expected to be already
        /// decoded from IMAP UTF‑7.
        /// </param>
        /// <param name="identifier">
        /// The ACL identifier (user, group, or special ACL subject) for which the
        /// server reports the rights that may be granted. The value must be a
        /// non‑empty string and is returned exactly as parsed from the server
        /// response.
        /// </param>
        /// <param name="requiredRights">
        /// The set of rights that MUST be granted if any rights are granted to the
        /// identifier. This value may be an empty string, as permitted by RFC 4314,
        /// but must not be <c>null</c>.
        /// </param>
        /// <param name="optionalRights">
        /// The set of rights that MAY be granted to the identifier. Each element is a
        /// single rights character as defined in RFC 4314. The array must not be
        /// <c>null</c>. If the server provides no optional rights, an empty array should
        /// be supplied.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/>, <paramref name="identifier"/>,
        /// <paramref name="requiredRights"/>, or <paramref name="optionalRights"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="identifier"/> is an
        /// empty string.
        /// </exception>
        public IMAP_r_u_ListRights(string folder,string identifier,string requiredRights,string[] optionalRights)
        {
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(folder == string.Empty){
                throw new ArgumentException("Argument 'folder' name must be specified.",nameof(folder));
            }
            if(identifier == null){
                throw new ArgumentNullException(nameof(identifier));
            }
            if(identifier == string.Empty){
                throw new ArgumentException("Argument 'identifier' name must be specified.",nameof(identifier));
            }            
            if(requiredRights == null){
                throw new ArgumentNullException(nameof(identifier));
            }            
            if(optionalRights == null){
                throw new ArgumentNullException(nameof(identifier));
            }

            m_FolderName     = folder;
            m_Identifier     = identifier;
            m_RequiredRights = requiredRights;
            m_OptionalRights = optionalRights;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses an IMAP LISTRIGHTS untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. LISTRIGHTS responses describe the set of
        /// rights that may be granted to a specific identifier for a given mailbox,
        /// as defined in RFC 4314 section 3.7.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a LISTRIGHTS untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading mailbox names or other
        /// astring values that may be encoded as literals.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_ListRights"/> instance containing the
        /// mailbox name, identifier, required rights, and optional rights.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the LISTRIGHTS response is syntactically invalid or missing
        /// required components.
        /// </exception>
        internal async static Task<IMAP_r_u_ListRights> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
            RFC 4314 — Section 3.7: LISTRIGHTS Response
            (Rewritten for implementation clarity)

            The LISTRIGHTS untagged response reports the set of rights that may be
            granted to a specific identifier for a given mailbox. It does not report
            the rights currently granted; it reports the *possible* rights and the
            required rights.

            Response format:

                LISTRIGHTS <mailbox> <identifier> <required> <optional...>

            Field definitions:
                <mailbox>    - Mailbox name for which rights are being queried.
                <identifier> - ACL subject (user, group, or special identifier).
                               May be an atom or quoted string. Servers rarely use
                               literals for identifiers in LISTRIGHTS.
                <required>   - Rights that MUST be granted if any rights are granted.
                               This is typically empty ("") for most servers.
                <optional>   - Zero or more rights characters, each representing a
                               right that MAY be granted to the identifier.

            Example:

                LISTRIGHTS INBOX ivar "" l r s t w i p e k x a

            Meaning:
                - Identifier "ivar" has no required rights.
                - The optional rights that may be granted are:
                    l r s t w i p e k x a

            Notes:
                - Servers must not reveal LISTRIGHTS information to users lacking the
                  "a" (admin) right for the mailbox.
                - The required rights string may be empty.
                - Optional rights are returned as individual atoms, not a single
                  concatenated string.

            Implementation notes:
                - Decode mailbox names using IMAP UTF‑7.
                - Required rights may be an empty string.
                - Optional rights are returned as separate tokens; concatenate them
                  only if your API prefers a single string.
                - LISTRIGHTS responses always contain at least three fields:
                    LISTRIGHTS <mailbox> <identifier> <required>
                  Optional rights follow after <required>.
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP LISTRIGHTS response (missing *): {imapReader.Text}");
            }

            // "LISTRIGHTS"
            if(!string.Equals("LISTRIGHTS",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP LISTRIGHTS response (missing LISTRIGHTS): {imapReader.Text}");
            }

            // Folder
            string? folder = await imapReader.ReadMailboxAsync(cancellationToken);
            if(folder == null){
                throw new ParseException($"Invalid IMAP LISTRIGHTS response (expected mailbox-name): {imapReader.Text}");
            }

            // Identifier
            string? identifier = await imapReader.ReadStringAsync(cancellationToken);
            if(identifier == null){
                throw new ParseException($"Invalid IMAP LISTRIGHTS response (expected user/gruop identifier): {imapReader.Text}");
            }

            // required
            string reqRights = await imapReader.ReadMailboxAsync(cancellationToken) ?? "";

            // optional
            List<string> optionalRights = new List<string>();
            while(imapReader.HasMore){
                optionalRights.Add(imapReader.ReadAtom());
            }        

            return new IMAP_r_u_ListRights(folder,identifier,reqRights,optionalRights.ToArray());
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire‑format representation of this LISTRIGHTS response
        /// using <see cref="IMAP_Mailbox_Encoding.None"/> for mailbox encoding.
        /// </summary>
        /// <returns>
        /// A string containing the serialized LISTRIGHTS untagged response in IMAP
        /// protocol format.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This overload is a convenience method that calls
        /// <see cref="ToString(IMAP_Mailbox_Encoding)"/> with
        /// <see cref="IMAP_Mailbox_Encoding.None"/>. It produces the same output as
        /// the encoding‑aware version, except that mailbox names are emitted without
        /// IMAP UTF‑7 encoding.
        /// </para>
        /// <para>
        /// The returned string includes the mailbox name, identifier, required rights,
        /// and any optional rights, formatted according to RFC 4314.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return ToString(IMAP_Mailbox_Encoding.None);
        }

        /// <summary>
        /// Converts this LISTRIGHTS response to its IMAP wire‑format representation.
        /// </summary>
        /// <param name="encoding">
        /// The mailbox encoding to use when serializing the folder name. The value
        /// determines how the mailbox is encoded (typically IMAP UTF‑7) before being
        /// written to the output string.
        /// </param>
        /// <returns>
        /// A string containing the IMAP LISTRIGHTS untagged response in protocol
        /// format, including the mailbox name, identifier, required rights, and any
        /// optional rights returned by the server.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The output format follows RFC 4314 and has the form:
        /// </para>
        /// <code>
        /// * LISTRIGHTS &lt;mailbox&gt; &lt;identifier&gt; &lt;required&gt; [ &lt;optional&gt; ... ]
        /// </code>
        /// <para>
        /// Optional rights are emitted as individual IMAP atoms separated by spaces.
        /// If no optional rights are present, the response omits the optional rights
        /// section entirely. Required rights may be an empty string.
        /// </para>
        /// <para>
        /// This method does not perform any normalization or interpretation of rights.
        /// All values are serialized exactly as stored in the object.
        /// </para>
        /// </remarks>
        public override string ToString(IMAP_Mailbox_Encoding encoding)
        {
            // Example: S: * LISTRIGHTS ~/Mail/saved smith "" l r swicdkxte

            StringBuilder retVal = new StringBuilder();

            retVal.Append("* LISTRIGHTS ");
            retVal.Append(IMAP_Utils.EncodeMailbox(m_FolderName, encoding));
            retVal.Append(" ");
            retVal.Append(m_Identifier);
            retVal.Append(" ");
            retVal.Append(m_RequiredRights);

            // Optional rights: zero or more atoms
            if (m_OptionalRights.Length > 0){
                retVal.Append(" ");
                retVal.Append(string.Join(" ", m_OptionalRights));
            }

            retVal.Append("\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties impelementation

        /// <summary>
        /// Gets the name of the mailbox associated with this LISTRIGHTS response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The folder name identifies the mailbox for which the server reports the
        /// rights that may be granted to a specific identifier. The value is returned
        /// exactly as provided by the server, after IMAP UTF‑7 decoding and removal of
        /// any surrounding quotes.
        /// </para>
        /// <para>
        /// The folder name is never <c>null</c>. If the server sends an empty or
        /// syntactically invalid mailbox name, the parser will throw a
        /// <see cref="ParseException"/>.
        /// </para>
        /// </remarks>
        public string FolderName
        {
            get{ return m_FolderName; }
        }

        /// <summary>
        /// Gets the ACL identifier to which the rights in this LISTRIGHTS response apply.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The identifier represents the ACL subject for which the server reports the
        /// rights that may be granted. It may be a user name, group name, or a special
        /// IMAP ACL identifier such as <c>anonymous</c> or <c>owner</c>.
        /// </para>
        /// <para>
        /// The value is returned exactly as parsed from the server response. If the
        /// server encoded the identifier as an IMAP literal, the literal payload is
        /// read and decoded before being stored. Quoted identifiers are unquoted, and
        /// atom identifiers are preserved as‑is.
        /// </para>
        /// <para>
        /// The identifier is never <c>null</c>. If the server omits the identifier or
        /// provides an invalid literal header, the parser throws a
        /// <see cref="ParseException"/>.
        /// </para>
        /// </remarks>
        public string Identifier
        {
            get{ return m_Identifier; }
        }

        /// <summary>
        /// Gets the set of rights that the server reports as <em>required</em> for the
        /// identifier in this LISTRIGHTS response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Required rights are those that MUST be granted if any rights are granted to
        /// the identifier. In most IMAP server implementations this value is an empty
        /// string, because few servers define rights that are inherently mandatory.
        /// </para>
        /// <para>
        /// The value is returned exactly as parsed from the server response. It may be:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>An empty string (<c>""</c>), which is the most common case.</description></item>
        ///   <item><description>A quoted empty string (<c>""</c>) in the raw response, which is normalized to an empty string.</description></item>
        ///   <item><description>A non‑empty rights string, if the server defines mandatory rights for the identifier.</description></item>
        /// </list>
        /// <para>
        /// The property is never <c>null</c>. If the server omits the required rights
        /// field entirely, the parser substitutes an empty string as permitted by
        /// RFC 4314.
        /// </para>
        /// </remarks>
        public string RequiredRights
        {
            get{ return m_RequiredRights; }
        }

        /// <summary>
        /// Gets the set of optional rights that the server reports as rights which
        /// <em>may</em> be granted to the identifier in this LISTRIGHTS response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Optional rights represent the complete set of rights that the server allows
        /// to be granted to the identifier for the specified mailbox. Each element in
        /// the array is a single rights character, such as <c>l</c>, <c>r</c>, <c>w</c>,
        /// or <c>i</c>, as defined in RFC 4314.
        /// </para>
        /// <para>
        /// The LISTRIGHTS response may contain zero or more optional rights. If the
        /// server provides no optional rights, this property returns an empty array.
        /// The array is never <c>null</c>.
        /// </para>
        /// <para>
        /// Optional rights are returned exactly as parsed from the server response.
        /// They are not concatenated or normalized; each rights token is preserved in
        /// the order provided by the server.
        /// </para>
        /// </remarks>
        public string[] OptionalRights
        {
            get{ return m_OptionalRights; }
        }

        #endregion
    }
}
