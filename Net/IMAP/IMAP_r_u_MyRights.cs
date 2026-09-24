using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP <c>MYRIGHTS</c> untagged response as defined in
    /// RFC 4314. The response reports the set of rights that are currently
    /// in effect for the authenticated user for a specific mailbox.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A MYRIGHTS response provides the effective rights that the server grants to
    /// the authenticated user for a given mailbox. Unlike <c>LISTRIGHTS</c>, which
    /// reports the rights that may be granted to an identifier, MYRIGHTS reports
    /// only the rights that are actually active at the time of the response.
    /// </para>
    /// <para>
    /// The response format is:
    /// </para>
    /// <code>
    /// * MYRIGHTS &lt;mailbox&gt; &lt;rights&gt;
    /// </code>
    /// </remarks>
    public class IMAP_r_u_MyRights : IMAP_r_u
    {
        private string m_FolderName = "";
        private string m_pRights    = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_MyRights"/> class
        /// using the specified mailbox name and effective rights string.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name for which the server reports the authenticated user's
        /// effective rights. The value must be a non‑empty string and is expected to
        /// already be decoded from IMAP UTF‑7 and unquoted.
        /// </param>
        /// <param name="rights">
        /// The set of effective rights granted to the authenticated user for the
        /// specified mailbox. The value may be <c>null</c>, in which case an empty
        /// rights string is used. Rights are stored exactly as provided and may be
        /// empty if the user has no rights for the mailbox.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string.
        /// </exception>
        public IMAP_r_u_MyRights(string folder,string rights)
        {
            if(folder == null){
                throw new ArgumentNullException("folder");
            }
            if(folder == string.Empty){
                throw new ArgumentException("Argument 'folder' value must be specified.","folder");
            }

            m_FolderName = folder;
            
            m_pRights = rights ?? "";
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP MYRIGHTS untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. MYRIGHTS responses report the set of rights
        /// that the authenticated user currently possesses for a specific mailbox,
        /// as defined in RFC 4314 section 3.8.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a MYRIGHTS untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading mailbox names or other
        /// astring values that may be encoded as literals.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_MyRights"/> instance containing the
        /// mailbox name and the effective rights string.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the MYRIGHTS response is syntactically invalid or missing
        /// required components.
        /// </exception>
        internal async static Task<IMAP_r_u_MyRights> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
            RFC 4314 — Section 3.8: MYRIGHTS Response
            (Rewritten for implementation clarity)

            The MYRIGHTS untagged response reports the set of rights that the *authenticated
            user* currently has for a given mailbox. Unlike LISTRIGHTS, which reports
            possible rights, MYRIGHTS reports the rights that are actually in effect.

            Response format:

                MYRIGHTS <mailbox> <rights>

            Field definitions:
                <mailbox> - The mailbox name for which the server reports the user's
                            effective rights. May be an atom or quoted string.
                            IMAP UTF‑7 decoding is required.

                <rights>  - A string containing zero or more rights characters that the
                            authenticated user currently possesses for the mailbox.
                            Each character corresponds to a single IMAP ACL right as
                            defined in RFC 4314 (e.g., l, r, s, t, w, i, p, e, k, x, a).

            Example:

                * MYRIGHTS INBOX lrstipekx

            Meaning:
                - The authenticated user has the rights: l, r, s, t, i, p, e, k, x.
                - The user does not have the 'a' (admin) right.

            Notes:
                - MYRIGHTS reports *effective* rights, not possible rights.
                - Servers must not reveal rights for mailboxes the user cannot access.
                - The rights string may be empty if the user has no rights at all.
                - Rights are returned as a single concatenated string, not separate tokens.
                - MYRIGHTS does not include required/optional rights; only effective rights.

            Implementation notes:
                - Decode mailbox names using IMAP UTF‑7.
                - The rights field may be an empty string.
                - Rights are returned as a single atom; do not split unless your API prefers
                  an array representation.
                - MYRIGHTS responses always contain exactly two fields after the tag:
                    MYRIGHTS <mailbox> <rights>
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP MYRIGHTS response (missing *): {imapReader.Text}");
            }

            // "MYRIGHTS"
            if(!string.Equals("MYRIGHTS",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP MYRIGHTS response (missing MYRIGHTS): {imapReader.Text}");
            }

            // Folder
            string? folder = await imapReader.ReadMailboxAsync(cancellationToken);
            if(folder == null){
                throw new ParseException($"Invalid IMAP MYRIGHTS response (expected mailbox-name): {imapReader.Text}");
            }

            // rights
            string rights = await imapReader.ReadStringAsync(cancellationToken) ?? "";
            // must be single token
            if(imapReader.HasMore){
                throw new ParseException($"Invalid IMAP MYRIGHTS response (rights must be a single token): {imapReader.Text}");
            }

            return new IMAP_r_u_MyRights(folder,rights);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire‑format representation of this MYRIGHTS response
        /// using <see cref="IMAP_Mailbox_Encoding.None"/> for mailbox encoding.
        /// </summary>
        /// <returns>
        /// A string containing the serialized MYRIGHTS untagged response in IMAP
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
        /// The returned string includes the mailbox name and the effective rights
        /// granted to the authenticated user, formatted according to RFC 4314.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return ToString(IMAP_Mailbox_Encoding.None);
        }

        /// <summary>
        /// Converts this MYRIGHTS response to its IMAP wire‑format representation.
        /// </summary>
        /// <param name="encoding">
        /// The mailbox encoding to use when serializing the folder name. This controls
        /// how the mailbox is emitted (typically IMAP UTF‑7) in the resulting protocol
        /// string.
        /// </param>
        /// <returns>
        /// A string containing the IMAP MYRIGHTS untagged response in protocol format,
        /// including the mailbox name and the effective rights granted to the
        /// authenticated user.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The output format follows RFC 4314 and has the form:
        /// </para>
        /// <code>
        /// * MYRIGHTS &lt;mailbox&gt; &lt;rights&gt;
        /// </code>
        /// <para>
        /// The mailbox name is encoded using the specified <paramref name="encoding"/>.
        /// Rights are serialized exactly as stored in the object. If the rights string
        /// is empty, it is emitted as a quoted empty string (<c>""</c>) because an empty
        /// atom is not valid in IMAP. Otherwise, rights are emitted as a bare atom
        /// without surrounding quotes.
        /// </para>
        /// </remarks>
        public override string ToString(IMAP_Mailbox_Encoding encoding)
        {
            // Example: S: * MYRIGHTS INBOX rwiptsldaex

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* MYRIGHTS ");
            retVal.Append(IMAP_Utils.EncodeMailbox(m_FolderName, encoding));
            retVal.Append(" ");

            // Rights:
            // - Empty rights must be serialized as "" (quoted empty string)
            // - Non-empty rights must be serialized as a bare atom (no quotes)
            if (string.IsNullOrEmpty(m_pRights)){
                retVal.Append("\"\"");
            }
            else {
                retVal.Append(m_pRights);
            }

            retVal.Append("\r\n");

            return retVal.ToString();
        }


        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the name of the mailbox for which the server reports the effective
        /// rights of the authenticated user in this MYRIGHTS response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The folder name identifies the mailbox whose access rights are being
        /// reported. The value is returned after IMAP UTF‑7 decoding and removal of
        /// any surrounding quotes, preserving the exact mailbox name supplied by the
        /// server.
        /// </para>
        /// <para>
        /// MYRIGHTS responses always contain a mailbox name, and this property is
        /// therefore never <c>null</c>. If the server omits the mailbox name or sends
        /// an invalid token, the parser throws a <see cref="ParseException"/>.
        /// </para>
        /// <para>
        /// The rights reported in a MYRIGHTS response apply only to the authenticated
        /// user and represent the rights currently in effect, not the rights that may
        /// be granted. Use <c>LISTRIGHTS</c> to obtain the set of rights that may be
        /// granted to an identifier.
        /// </para>
        /// </remarks>
        public string FolderName
        {
            get{ return m_FolderName; }
        }

        /// <summary>
        /// Gets the set of effective rights that the authenticated user currently
        /// possesses for the mailbox referenced by this MYRIGHTS response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The rights string contains zero or more IMAP ACL rights characters as
        /// defined in RFC 4314 (for example: <c>l</c>, <c>r</c>, <c>s</c>,
        /// <c>t</c>, <c>w</c>, <c>i</c>, <c>p</c>, <c>e</c>, <c>k</c>, <c>x</c>,
        /// <c>a</c>). Each character represents a single permission granted to the
        /// authenticated user.
        /// </para>
        /// <para>
        /// MYRIGHTS reports the rights that are <em>currently in effect</em> for the
        /// authenticated user, not the rights that may be granted. Use
        /// <c>LISTRIGHTS</c> to obtain the set of rights that may be granted to an
        /// identifier, or <c>ACL</c> to retrieve rights assigned to other users.
        /// </para>
        /// <para>
        /// The value is returned exactly as provided by the server, after removal of
        /// surrounding quotes when present. The string may be empty if the user has
        /// no rights for the mailbox. The property is never <c>null</c>.
        /// </para>
        /// </remarks>
        public string Rights
        {
            get{ return m_pRights; }
        }

        /// <summary>
        /// Gets whether the authenticated user has the 'l' (lookup) right.
        /// </summary>
        /// <remarks>
        /// Lookup allows the user to test for the existence of the mailbox.
        /// </remarks>
        public bool CanLookup => m_pRights.Contains('l');

        /// <summary>
        /// Gets whether the authenticated user has the 'r' (read) right.
        /// </summary>
        /// <remarks>
        /// Read allows the user to fetch message content.
        /// </remarks>
        public bool CanRead => m_pRights.Contains('r');

        /// <summary>
        /// Gets whether the authenticated user has the 's' (keep seen state) right.
        /// </summary>
        /// <remarks>
        /// Allows the user to set or clear the \Seen flag.
        /// </remarks>
        public bool CanSetSeen => m_pRights.Contains('s');

        /// <summary>
        /// Gets whether the authenticated user has the 't' (delete messages) right.
        /// </summary>
        /// <remarks>
        /// Allows the user to mark messages as \Deleted.
        /// </remarks>
        public bool CanDeleteMessages => m_pRights.Contains('t');

        /// <summary>
        /// Gets whether the authenticated user has the 'w' (write flags) right.
        /// </summary>
        /// <remarks>
        /// Allows modification of message flags other than \Seen and \Deleted.
        /// </remarks>
        public bool CanWriteFlags => m_pRights.Contains('w');

        /// <summary>
        /// Gets whether the authenticated user has the 'i' (insert) right.
        /// </summary>
        /// <remarks>
        /// Allows the user to APPEND new messages to the mailbox.
        /// </remarks>
        public bool CanInsert => m_pRights.Contains('i');

        /// <summary>
        /// Gets whether the authenticated user has the 'e' (expunge) right.
        /// </summary>
        /// <remarks>
        /// Allows the user to permanently remove messages marked as \Deleted.
        /// </remarks>
        public bool CanExpunge => m_pRights.Contains('e');

        /// <summary>
        /// Gets whether the authenticated user has the 'k' (create mailbox) right.
        /// </summary>
        /// <remarks>
        /// Allows creation of new child mailboxes.
        /// </remarks>
        public bool CanCreateMailbox => m_pRights.Contains('k');

        /// <summary>
        /// Gets whether the authenticated user has the 'x' (delete mailbox) right.
        /// </summary>
        /// <remarks>
        /// Allows deletion of the mailbox.
        /// </remarks>
        public bool CanDeleteMailbox => m_pRights.Contains('x');

        /// <summary>
        /// Gets whether the authenticated user has the 'a' (administer ACL) right.
        /// </summary>
        /// <remarks>
        /// Allows modification of ACLs for the mailbox.
        /// </remarks>
        public bool CanAdminister => m_pRights.Contains('a');

        #endregion
    }
}
