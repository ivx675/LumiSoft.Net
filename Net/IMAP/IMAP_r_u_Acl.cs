using LumiSoft.Net.IMAP.Client;
using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP <c>ACL</c> untagged response as defined in RFC 4314.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An ACL response reports the Access Control List entries associated with a
    /// specific mailbox. The response contains the mailbox name and zero or more
    /// identifier‑rights pairs, where each identifier represents a user, group,
    /// or special ACL subject, and each rights string specifies the permissions
    /// granted to that identifier.
    /// </para>
    /// <para>
    /// This class stores the mailbox name exactly as received (after IMAP UTF‑7
    /// decoding) and preserves all ACL entries in the order returned by the server.
    /// No normalization or rights interpretation is performed.
    /// </para>
    /// <para>
    /// Instances of this class are typically created by the ACL parser in response
    /// to a <c>* ACL</c> untagged server message.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Acl : IMAP_r_u
    {
        private string             m_FolderName = "";
        private IMAP_t_Acl_Entry[] m_pEntries;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Acl"/> class using the
        /// specified mailbox name and ACL entry collection.
        /// </summary>
        /// <param name="folderName">
        /// The name of the mailbox to which the ACL entries apply. The value must be a
        /// non‑empty string and is expected to be already decoded from IMAP UTF‑7.
        /// </param>
        /// <param name="entries">
        /// The array of <see cref="IMAP_t_Acl_Entry"/> objects representing the Access
        /// Control List entries returned by the IMAP server. The array must not be
        /// <c>null</c>. If the mailbox has no ACL entries, an empty array should be
        /// provided.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folderName"/> or <paramref name="entries"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folderName"/> is an empty string.
        /// </exception>
        public IMAP_r_u_Acl(string folderName,IMAP_t_Acl_Entry[] entries)
        {
            if(folderName == null){
                throw new ArgumentNullException("folderName");
            }
            if(folderName == string.Empty){
                throw new ArgumentException("Argument 'folderName' value must be specified.","folderName");
            }
            if(entries == null){
                throw new ArgumentNullException("entries");
            }

            m_FolderName = folderName;
            m_pEntries   = entries;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses an IMAP ACL untagged response using the supplied <see cref="_IMAP_Reader"/>.
        /// The ACL response reports all access control entries associated with a mailbox and
        /// is defined by RFC 4314. A single ACL response line contains the mailbox name
        /// followed by zero or more identifier/right pairs.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of an ACL untagged response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading mailbox names or identifiers
        /// that may be encoded as literals.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_Acl"/> instance containing the mailbox name
        /// and all ACL entries returned by the server.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the ACL response is syntactically invalid or missing required fields.
        /// </exception>
        internal async static Task<IMAP_r_u_Acl> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
            RFC 4314 — Section 3.6: ACL Response (Rewritten for implementation clarity)

            The ACL untagged response reports Access Control List information for a
            mailbox. A single ACL response line contains zero or more ACL entries.

            An ACL entry consists of:
                - an identifier (astring: atom / quoted / literal / literal8)
                - a rights string (the complete set of rights granted to that identifier)

            GETACL returns exactly one ACL response line containing all ACL entries
            for the requested mailbox.

            Response format:

                ACL <mailbox> <identifier> <rights> [<identifier> <rights> ...]

            Field definitions:
                <mailbox>    - Mailbox name to which the ACL applies.
                <identifier> - ACL subject (user, group, or special identifier).
                               May be an atom, quoted string, literal, or literal8.
                <rights>     - String containing all rights granted to the identifier.

            A single ACL response line may contain zero or more ACL entries. Servers
            must include every ACL entry for the mailbox in this single response line.

            Example:

                ACL INBOX ivar lrwstipekxa shareduser lrwstipe anonymous r

            ACL entries in this example:
                identifier "ivar"        → lrwstipekxa
                identifier "shareduser"  → lrwstipe
                identifier "anonymous"   → r

            Error behavior:
                - ACL responses are sent only when the ACL-related command succeeds.
                - If the command returns NO or BAD, no ACL response is sent.

            Security rules:
                - Servers must not reveal ACL entries to users lacking the "a" (admin)
                  right for the mailbox.
                - If the user lacks permission to view ACLs, the server must not send
                  an ACL response.

            Implementation notes:
                - Normalize rights before returning them.
                - Return mailbox names exactly as stored.
                - Omit ACL entries for identifiers with no rights.
                - Preserve identifier encoding (quoted, literal, literal8) exactly as
                  received or stored.
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP ACL response (missing *): {imapReader.Text}");
            }

            // "ACL"
            if(!string.Equals("ACL",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP ACL response (missing ACL): {imapReader.Text}");
            }

            // Folder
            string? folder = await imapReader.ReadMailboxAsync(cancellationToken);
            if(folder == null){
                throw new ParseException($"Invalid IMAP ACL response (expected mailbox-name): {imapReader.Text}");
            }

            // Acl entries
            List<IMAP_t_Acl_Entry> entries = new List<IMAP_t_Acl_Entry>();
            while(imapReader.HasMore){
                string? identifier = await imapReader.ReadStringAsync(cancellationToken);
                if(identifier == null){
                    throw new ParseException($"Invalid IMAP ACL response (expected identifier): {imapReader.Text}");
                }

                if(!imapReader.HasMore){
                    throw new ParseException($"Invalid IMAP ACL response (expected rights): {imapReader.Text}");
                }
                string rights = await imapReader.ReadStringAsync(cancellationToken) ?? "";

                entries.Add(new IMAP_t_Acl_Entry(identifier,rights));
            }

            return new IMAP_r_u_Acl(folder,entries.ToArray());
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>ACL</c> untagged response line for this ACL object
        /// using <see cref="IMAP_Mailbox_Encoding.None"/> for mailbox name encoding.
        /// </summary>
        /// <returns>
        /// A string containing the serialized ACL response in IMAP protocol format,
        /// beginning with <c>* ACL</c>, followed by the mailbox name and one or more
        /// quoted identifier‑rights pairs, terminated with CRLF.
        /// </returns>
        public override string ToString()
        {
            return ToString(IMAP_Mailbox_Encoding.None);
        }

        /// <summary>
        /// Generates an IMAP <c>ACL</c> untagged response line representing this ACL object.
        /// </summary>
        /// <param name="encoding">
        /// The mailbox name encoding to use when serializing the folder name
        /// (typically IMAP UTF‑7).
        /// </param>
        /// <returns>
        /// A string containing the serialized ACL response in IMAP protocol format,
        /// beginning with <c>* ACL</c>, followed by the encoded mailbox name and
        /// one or more quoted identifier‑rights pairs, terminated with CRLF.
        /// </returns>
        public override string ToString(IMAP_Mailbox_Encoding encoding)
        {
            // Example:    S: * ACL INBOX Fred rwipslda test rwipslda

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* ACL ");
            retVal.Append(IMAP_Utils.EncodeMailbox(m_FolderName,encoding));
            foreach(IMAP_t_Acl_Entry e in m_pEntries){
                retVal.Append(" \"" + e.Identifier + "\" " + e.Rights + "");
            }
            retVal.Append("\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the name of the mailbox for which this ACL response applies.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The folder name is stored exactly as received from the IMAP server and
        /// represents the mailbox whose Access Control List entries are contained
        /// in this response.
        /// </para>
        /// <para>
        /// The value is already decoded from IMAP UTF‑7 and unquoted, so it can be
        /// used directly for display or further processing.
        /// </para>
        /// </remarks>
        public string FolderName
        {
            get{ return m_FolderName; }
        }

        /// <summary>
        /// Gets the collection of Access Control List (ACL) entries associated with
        /// the mailbox specified by this ACL response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each entry contains an identifier (user, group, or special ACL subject)
        /// and the rights granted to that identifier. The entries are returned in the
        /// same order as provided by the IMAP server.
        /// </para>
        /// <para>
        /// The array is never <c>null</c>. If the mailbox has no ACL entries, the
        /// array will be empty.
        /// </para>
        /// </remarks>
        public IMAP_t_Acl_Entry[] Entires
        {
            get{ return m_pEntries; }
        }

        #endregion
    }
}
