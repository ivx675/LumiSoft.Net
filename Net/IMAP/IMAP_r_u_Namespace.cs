using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP <c>NAMESPACE</c> response as defined in
    /// RFC 2342. A NAMESPACE response describes the mailbox namespace
    /// structure available to the authenticated user, including personal,
    /// other‑users, and shared mailbox hierarchies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IMAP <c>NAMESPACE</c> command allows a client to discover how
    /// mailbox names should be constructed for different namespace categories.
    /// Each namespace category is represented by a list of namespace
    /// descriptors, where each descriptor contains a prefix and a hierarchy
    /// delimiter. These values are used by clients to build fully qualified
    /// mailbox names.
    /// </para>
    /// <para>
    /// A server may return multiple namespace descriptors per category, or
    /// <c>NIL</c> if no namespaces of that type exist. This class preserves
    /// all namespace descriptors exactly as returned by the server.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Namespace : IMAP_r_u
    {
        private IMAP_t_Namespace_Entry[] m_pPersonal;
        private IMAP_t_Namespace_Entry[] m_pOtherUsers;
        private IMAP_t_Namespace_Entry[] m_pShared;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Namespace"/> class
        /// using the personal, other‑users, and shared namespace lists returned by
        /// the IMAP server.
        /// </summary>
        /// <param name="personal">
        /// The list of personal namespace entries. This corresponds to the first
        /// namespace list in the IMAP <c>NAMESPACE</c> response and describes the
        /// mailbox hierarchy belonging to the authenticated user. Must not be
        /// <c>null</c>.
        /// </param>
        /// <param name="otherUsers">
        /// The list of other‑users namespace entries. This corresponds to the
        /// second namespace list in the IMAP <c>NAMESPACE</c> response and describes
        /// mailbox hierarchies belonging to other users that may be accessible
        /// depending on server permissions. Must not be <c>null</c>.
        /// </param>
        /// <param name="shared">
        /// The list of shared namespace entries. This corresponds to the third
        /// namespace list in the IMAP <c>NAMESPACE</c> response and describes
        /// mailbox hierarchies that are accessible to multiple users, such as
        /// public folders or shared mailbox trees. Must not be <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of the provided namespace lists is <c>null</c>.
        /// </exception>
        public IMAP_r_u_Namespace(IMAP_t_Namespace_Entry[] personal,IMAP_t_Namespace_Entry[] otherUsers,IMAP_t_Namespace_Entry[] shared)
        {
            if(personal == null){
                throw new ArgumentNullException(nameof(personal));
            }
            if(otherUsers == null){
                throw new ArgumentNullException(nameof(otherUsers));
            }
            if(shared == null){
                throw new ArgumentNullException(nameof(shared));
            }

            m_pPersonal   = personal;
            m_pOtherUsers = otherUsers;
            m_pShared     = shared;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP NAMESPACE untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. NAMESPACE responses describe the mailbox
        /// namespace structure supported by the server, as defined in RFC 2342.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a NAMESPACE untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading prefix, delimiter, or
        /// extension values that may be encoded as literals.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_Namespace"/> instance containing the
        /// personal, other-users, and shared namespace lists.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the NAMESPACE response is syntactically invalid, contains an
        /// illegal delimiter, includes <c>NIL</c> where an astring is required, or
        /// violates RFC 2342 structural rules.
        /// </exception>
        internal async static Task<IMAP_r_u_Namespace> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
            RFC 2342 — IMAP4 Namespace Extension
            ------------------------------------

            The NAMESPACE command allows a client to discover the server’s mailbox
            namespace structure, including personal mailboxes, other users’ mailboxes,
            and shared mailboxes. The server returns an untagged NAMESPACE response
            containing three namespace lists:

                * NAMESPACE <personal> <other-users> <shared>

            Each namespace list is either:
                - a parenthesized list of namespace descriptors, or
                - NIL (meaning no namespaces of that type exist)

            Namespace descriptor format:

                (<prefix> <delimiter> <extensions...>)

            <prefix>
                A string (astring) identifying the root of the namespace. This may be an
                empty string ("") meaning the root of the hierarchy. Prefixes may be
                atoms, quoted strings, or literals.

            <delimiter>
                The hierarchy separator used within that namespace. This is typically "/"
                or ".", but may be NIL if the namespace has no hierarchical structure.
                The delimiter MUST be a quoted string or NIL; literals are not permitted.

            <extensions>
                Zero or more astring values providing server-defined namespace metadata.
                Extensions are syntactically valid but not used by any known IMAP server.
                Clients should parse and preserve them but need not interpret them.

            Example:

                S: * NAMESPACE (("" "/")) (("~" "/")) NIL

            Meaning:
                - Personal namespace: root "" with delimiter "/"
                - Other users’ namespace: "~" with delimiter "/"
                - Shared namespace: none
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP NAMESPACE response (missing *): {imapReader.Text}");
            }

            // "NAMESPACE"
            if(!string.Equals("NAMESPACE",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP NAMESPACE response (missing NAMESPACE): {imapReader.Text}");
            }
            
            // Personal namespaces
            List<IMAP_t_Namespace_Entry> personal = new List<IMAP_t_Namespace_Entry>();
            if(imapReader.PeekIs('(')){
                // Consume '('
                imapReader.ReadChar();

                while(imapReader.PeekIs('(')){
                    // Consume '('
                    imapReader.ReadChar();

                    // Prefix
                    string? prefix = await imapReader.ReadStringAsync(cancellationToken);
                    if(prefix == null){
                        throw new ParseException($"Invalid IMAP NAMESPACE response, missing prefix: {imapReader.Text}");
                    }

                    // Delimiter
                    string? delimiter = await imapReader.ReadStringAsync(cancellationToken);
                    char?   delimiterChar;
                    if(delimiter == null){
                        delimiterChar = null;
                    }
                    else if(delimiter.Length != 1){
                        throw new ParseException($"Invalid IMAP NAMESPACE response, missing hierarchy delimiter.: {imapReader.Text}");
                    }
                    else{
                        delimiterChar = delimiter[0];
                    }

                    personal.Add(new IMAP_t_Namespace_Entry(prefix,delimiterChar));

                    // Skip extentions, if any.
                    while(!imapReader.PeekIs(')')){
                        await imapReader.ReadStringAsync(cancellationToken);
                    }

                    // Consume ')'
                    imapReader.ReadChar();
                }

                // Consume ')'
                imapReader.ReadChar();
            }
            // NIL
            else{
                if(!string.Equals("NIL",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                     throw new ParseException($"Invalid IMAP NAMESPACE response: {imapReader.Text}");
                }
            }

            // Other users namespaces
            List<IMAP_t_Namespace_Entry> other = new List<IMAP_t_Namespace_Entry>();
            if(imapReader.PeekIs('(')){
                // Consume '('
                imapReader.ReadChar();

                while(imapReader.PeekIs('(')){
                    // Consume '('
                    imapReader.ReadChar();

                    // Prefix
                    string? prefix = await imapReader.ReadStringAsync(cancellationToken);
                    if(prefix == null){
                        throw new ParseException($"Invalid IMAP NAMESPACE response, missing prefix: {imapReader.Text}");
                    }

                    // Delimiter
                    string? delimiter = await imapReader.ReadStringAsync(cancellationToken);
                    char?   delimiterChar;
                    if(delimiter == null){
                        delimiterChar = null;
                    }
                    else if(delimiter.Length != 1){
                        throw new ParseException($"Invalid IMAP NAMESPACE response, missing hierarchy delimiter.: {imapReader.Text}");
                    }
                    else{
                        delimiterChar = delimiter[0];
                    }

                    other.Add(new IMAP_t_Namespace_Entry(prefix,delimiterChar));

                    // Skip extentions, if any.
                    while(!imapReader.PeekIs(')')){
                        await imapReader.ReadStringAsync(cancellationToken);
                    }

                    // Consume ')'
                    imapReader.ReadChar();
                }

                // Consume ')'
                imapReader.ReadChar();
            }
            // NIL
            else{
                if(!string.Equals("NIL",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                     throw new ParseException($"Invalid IMAP NAMESPACE response: {imapReader.Text}");
                }
            }

            // Shared namespaces
            List<IMAP_t_Namespace_Entry> shared = new List<IMAP_t_Namespace_Entry>();
            if(imapReader.PeekIs('(')){
                // Consume '('
                imapReader.ReadChar();

                while(imapReader.PeekIs('(')){
                    // Consume '('
                    imapReader.ReadChar();

                    // Prefix
                    string? prefix = await imapReader.ReadStringAsync(cancellationToken);
                    if(prefix == null){
                        throw new ParseException($"Invalid IMAP NAMESPACE response, missing prefix: {imapReader.Text}");
                    }

                    // Delimiter
                    string? delimiter = await imapReader.ReadStringAsync(cancellationToken);
                    char?   delimiterChar;
                    if(delimiter == null){
                        delimiterChar = null;
                    }
                    else if(delimiter.Length != 1){
                        throw new ParseException($"Invalid IMAP NAMESPACE response, missing hierarchy delimiter.: {imapReader.Text}");
                    }
                    else{
                        delimiterChar = delimiter[0];
                    }

                    shared.Add(new IMAP_t_Namespace_Entry(prefix,delimiterChar));

                    // Skip extentions, if any.
                    while(!imapReader.PeekIs(')')){
                        await imapReader.ReadStringAsync(cancellationToken);
                    }

                    // Consume ')'
                    imapReader.ReadChar();
                }

                // Consume ')'
                imapReader.ReadChar();
            }
            // NIL
            else{
                if(!string.Equals("NIL",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                     throw new ParseException($"Invalid IMAP NAMESPACE response: {imapReader.Text}");
                }
            }

            return new IMAP_r_u_Namespace(personal.ToArray(),other.ToArray(),shared.ToArray());
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>NAMESPACE</c> response string representing the
        /// personal, other‑users, and shared namespaces contained in this
        /// <see cref="IMAP_r_u_Namespace"/> instance.
        /// </summary>
        /// <returns>
        /// A properly formatted IMAP <c>NAMESPACE</c> response line ending with
        /// CRLF. The output follows the syntax defined in RFC 2342 and
        /// includes three namespace lists in the form:
        /// <para/>
        /// <c>* NAMESPACE (personal) (other-users) (shared)</c>
        /// <para/>
        /// Each namespace list is either:
        /// <list type="bullet">
        /// <item><description>
        /// A parenthesized list of namespace descriptors, where each descriptor
        /// has the form <c>("prefix" "delimiter")</c>.
        /// </description></item>
        /// <item><description>
        /// <c>NIL</c>, indicating that no namespaces of that type exist.
        /// </description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>
        /// Prefixes are always emitted as quoted strings. Delimiters are emitted
        /// as quoted single‑character strings unless the delimiter is <c>NIL</c>,
        /// in which case the literal <c>NIL</c> is written.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            // Example: S: * NAMESPACE (("" "/")) NIL (("Public Folders/" "/"))

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* NAMESPACE ");

            // Personal
            if(m_pPersonal.Length > 0){
                retVal.Append("(");
                for(int i = 0; i < m_pPersonal.Length; i++){
                    if(i > 0){
                        retVal.Append(" ");
                    }

                    string prefix = m_pPersonal[i].Prefix.Replace("\"","\\\"");
                    string delimiter = m_pPersonal[i].Delimiter == null
                        ? "NIL"
                        : "\"" + m_pPersonal[i].Delimiter + "\"";

                    retVal.Append("(\"" + prefix + "\" " + delimiter + ")");
                }
                retVal.Append(")");
            }
            else{
                retVal.Append("NIL");
            }

            retVal.Append(" ");

            // OtherUsers
            if(m_pOtherUsers.Length > 0){
                retVal.Append("(");
                for(int i = 0; i < m_pOtherUsers.Length; i++){
                    if(i > 0){
                        retVal.Append(" ");
                    }

                    string prefix = m_pOtherUsers[i].Prefix.Replace("\"","\\\"");
                    string delimiter = m_pOtherUsers[i].Delimiter == null
                        ? "NIL"
                        : "\"" + m_pOtherUsers[i].Delimiter + "\"";

                    retVal.Append("(\"" + prefix + "\" " + delimiter + ")");
                }
                retVal.Append(")");
            }
            else{
                retVal.Append("NIL");
            }

            retVal.Append(" ");

            // Shared
            if(m_pShared.Length > 0){
                retVal.Append("(");
                for(int i = 0; i < m_pShared.Length; i++){
                    if(i > 0){
                        retVal.Append(" ");
                    }

                    string prefix = m_pShared[i].Prefix.Replace("\"","\\\"");
                    string delimiter = m_pShared[i].Delimiter == null
                        ? "NIL"
                        : "\"" + m_pShared[i].Delimiter + "\"";

                    retVal.Append("(\"" + prefix + "\" " + delimiter + ")");
                }
                retVal.Append(")");
            }
            else{
                retVal.Append("NIL");
            }

            retVal.Append("\r\n");

            return retVal.ToString();
        }


        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the list of personal namespace entries returned by the server.
        /// Personal namespaces describe the mailbox hierarchy belonging to the
        /// authenticated user, such as the root namespace or the user's private
        /// mailbox tree.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Personal namespaces correspond to the first namespace list in the
        /// IMAP <c>NAMESPACE</c> response as defined in RFC 2342. Each entry
        /// contains a prefix and a hierarchy delimiter that together define how
        /// mailbox names are constructed within the user's personal namespace.
        /// </para>
        /// <para>
        /// If the server returns <c>NIL</c> for the personal namespace list,
        /// this property contains an empty array.
        /// </para>
        /// </remarks>
        public IMAP_t_Namespace_Entry[] Personal
        {
            get{ return m_pPersonal; }
        }

        /// <summary>
        /// Gets the list of other‑users namespace entries returned by the server.
        /// Other‑users namespaces describe mailbox hierarchies that belong to
        /// other authenticated users and may be accessible depending on server
        /// permissions and access control rules.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Other‑users namespaces correspond to the second namespace list in the
        /// IMAP <c>NAMESPACE</c> response as defined in RFC 2342. Each entry
        /// contains a prefix and a hierarchy delimiter that together define how
        /// mailbox names are constructed within another user's namespace.
        /// </para>
        /// <para>
        /// If the server returns <c>NIL</c> for the other‑users namespace list,
        /// this property contains an empty array.
        /// </para>
        /// </remarks>
        public IMAP_t_Namespace_Entry[] OtherUsers
        {
            get{ return m_pOtherUsers; }
        }

        /// <summary>
        /// Gets the list of shared namespace entries returned by the server.
        /// Shared namespaces describe mailbox hierarchies that are accessible
        /// to multiple users, such as public folders or server‑defined shared
        /// mailbox trees.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Shared namespaces correspond to the third namespace list in the
        /// IMAP <c>NAMESPACE</c> response as defined in RFC 2342. Each entry
        /// contains a prefix and a hierarchy delimiter that together define how
        /// mailbox names are constructed within the shared namespace.
        /// </para>
        /// <para>
        /// If the server returns <c>NIL</c> for the shared namespace list,
        /// this property contains an empty array.
        /// </para>
        /// </remarks>
        public IMAP_t_Namespace_Entry[] Shared
        {
            get{ return m_pShared; }
        }

        #endregion
    }
}
