using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP untagged <c>QUOTAROOT</c> response as defined in
    /// RFC 2087. A QUOTAROOT response reports the quota roots associated
    /// with a specific mailbox, allowing the client to determine which quota
    /// domains apply to that mailbox.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>QUOTAROOT</c> response has the following general syntax:
    /// </para>
    /// <code>
    /// * QUOTAROOT &lt;mailbox&gt; &lt;root1&gt; [&lt;root2&gt; ...]
    /// </code>
    /// <para>
    /// The first argument is the mailbox name for which quota information is
    /// being queried.
    /// </para>
    /// <para>
    /// Following the mailbox are one or more quota roots. A quota root identifies
    /// a resource domain for which the server tracks usage and limits. A mailbox
    /// may belong to multiple quota roots, and the server may return them in any
    /// order.
    /// </para>
    /// <para>
    /// The QUOTAROOT response does not itself contain usage or limit values.
    /// Those values are returned separately in <c>QUOTA</c> responses. Clients
    /// typically issue a <c>GETQUOTAROOT</c> command, receive a QUOTAROOT
    /// response listing the applicable roots, and then receive one or more
    /// QUOTA responses providing the actual resource usage for each root.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_QuotaRoot : IMAP_r_u
    {
        private string   m_FolderName = "";
        private string[] m_QuotaRoots;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_QuotaRoot"/> class
        /// using the specified mailbox name and collection of quota roots.
        /// </summary>
        /// <param name="folder">
        /// The mailbox for which the server reports applicable quota roots. The value
        /// must be a non‑empty IMAP <em>astring</em> (atom or quoted string) and is
        /// expected to be already decoded from IMAP UTF‑7.
        /// </param>
        /// <param name="quotaRoots">
        /// The array of quota root names associated with the mailbox. Each root
        /// identifies a resource domain for which usage and limit values may be
        /// reported in subsequent <c>QUOTA</c> responses. The array must not be
        /// <c>null</c>. Servers may legally return zero quota roots.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> or <paramref name="quotaRoots"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string, as a mailbox
        /// name must be specified.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A <c>QUOTAROOT</c> response reports the quota roots that apply to a mailbox.
        /// It does not contain usage or limit values; those are returned separately in
        /// <c>QUOTA</c> responses. Unknown or server‑specific quota roots are preserved
        /// for forward compatibility.
        /// </para>
        /// <para>
        /// The general response format is:
        /// </para>
        /// <code>
        /// * QUOTAROOT &lt;mailbox&gt; &lt;root1&gt; [&lt;root2&gt; ...]
        /// </code>
        /// </remarks>
        public IMAP_r_u_QuotaRoot(string folder,string[] quotaRoots)
        {
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(folder == string.Empty){
                throw new ArgumentException("Argument 'folder' name must be specified.",nameof(folder));
            }
            if(quotaRoots == null){
                throw new ArgumentNullException(nameof(quotaRoots));
            }

            m_FolderName = folder;
            m_QuotaRoots = quotaRoots;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP QUOTAROOT untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. QUOTAROOT responses identify the quota roots
        /// associated with a specific mailbox, as defined in RFC 2087.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a QUOTAROOT untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading mailbox or quota root values,
        /// which may be encoded as literals.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_QuotaRoot"/> instance containing the
        /// mailbox name and its associated quota root list.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the QUOTAROOT response is syntactically invalid, contains
        /// <c>NIL</c> where an astring is required, or violates RFC 2087 structural
        /// rules.
        /// </exception>
        internal async static Task<IMAP_r_u_QuotaRoot> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
            RFC 2087 — QUOTAROOT Response
            (Rewritten for implementation clarity)

            The QUOTAROOT untagged response reports the quota roots associated with a
            specific mailbox. A mailbox may belong to one or more quota roots, and the
            server returns all applicable roots in a single response.

            Response format:

                * QUOTAROOT <mailbox> <root1> [<root2> ...]

            Field definitions:

                <mailbox>
                    The mailbox for which quota information is being queried.
                    May be an atom or a quoted string.
                    IMAP UTF‑7 decoding is required.

                <root>
                    A quota root name. Each root identifies a resource domain for which
                    the server tracks usage and limits. A mailbox may have multiple
                    quota roots. Root names are IMAP astrings (atoms or quoted strings).

            Meaning:
                The QUOTAROOT response does not contain usage or limit values. It only
                identifies which quota roots apply to the mailbox. Actual usage/limit
                values are returned separately in QUOTA responses.

            Example:

                * QUOTAROOT INBOX "" user/ivar

            Meaning:
                The mailbox INBOX belongs to two quota roots:
                    ""          — the empty quota root
                    user/ivar   — a user‑specific quota root

            Notes:
                - QUOTAROOT responses are untagged and typically appear as part of a
                  GETQUOTAROOT command.
                - The server may return multiple quota roots in any order.
                - Unknown or server‑specific quota roots must be preserved for forward
                  compatibility.

            Implementation notes:
                - Decode mailbox and quota roots using IMAP UTF‑7.
                - Parse the mailbox name followed by one or more quota roots.
                - QUOTAROOT may legally return zero roots, though most servers return
                  at least one.
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP QUOTAROOT response (missing *): {imapReader.Text}");
            }

            // "QUOTAROOT"
            if(!string.Equals("QUOTAROOT",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP QUOTAROOT response (missing QUOTAROOT): {imapReader.Text}");
            }

            // Folder
            string? folder = await imapReader.ReadMailboxAsync(cancellationToken);
            if(folder == null){
                throw new ParseException($"Invalid IMAP QUOTAROOT response (expected mailbox-name): {imapReader.Text}");
            }

            // Quota roots
            List<string> quotaRoots = new List<string>();
            while(imapReader.HasMore){
                string? quotaRoot = await imapReader.ReadMailboxAsync(cancellationToken);
                if(quotaRoot != null){
                    quotaRoots.Add(quotaRoot);
                }
            }

            return new IMAP_r_u_QuotaRoot(folder,quotaRoots.ToArray());
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire‑format representation of this
        /// <c>QUOTAROOT</c> response using <see cref="IMAP_Mailbox_Encoding.None"/>
        /// for mailbox and quota‑root encoding.
        /// </summary>
        /// <returns>
        /// A string containing the serialized <c>QUOTAROOT</c> response,
        /// terminated with CRLF, suitable for logging or transmission.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is a convenience overload that formats the response
        /// without applying IMAP UTF‑7 encoding to mailbox names or quota roots.
        /// It is equivalent to calling:
        /// </para>
        /// <code>
        /// ToString(IMAP_Mailbox_Encoding.None)
        /// </code>
        /// <para>
        /// The resulting output follows the syntax defined in RFC 2087:
        /// </para>
        /// <code>
        /// * QUOTAROOT &lt;mailbox&gt; &lt;root1&gt; [&lt;root2&gt; ...]\r\n
        /// </code>
        /// </remarks>
        public override string ToString()
        {
            return ToString(IMAP_Mailbox_Encoding.None);
        }

        /// <summary>
        /// Converts this <c>QUOTAROOT</c> response to its IMAP wire‑format
        /// representation using the specified mailbox encoding.
        /// </summary>
        /// <param name="encoding">
        /// The mailbox encoding to use when serializing the mailbox name and
        /// quota roots. This is typically IMAP UTF‑7 for servers that require
        /// encoded mailbox names.
        /// </param>
        /// <returns>
        /// A string containing the IMAP‑formatted <c>QUOTAROOT</c> response,
        /// terminated with CRLF, suitable for transmission to an IMAP client
        /// or for logging.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The serialized form follows the syntax defined in RFC 2087:
        /// </para>
        /// <code>
        /// * QUOTAROOT &lt;mailbox&gt; &lt;root1&gt; [&lt;root2&gt; ...]\r\n
        /// </code>
        /// <para>
        /// The mailbox name is encoded using the specified
        /// <paramref name="encoding"/>. Each quota root is also encoded and
        /// emitted as a quoted IMAP <em>astring</em>. If the response contains
        /// no quota roots, only the mailbox name is emitted.
        /// </para>
        /// </remarks>
        public override string ToString(IMAP_Mailbox_Encoding encoding)
        {
            // Example:    S: * QUOTAROOT INBOX ""

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* QUOTAROOT " + IMAP_Utils.EncodeMailbox(m_FolderName,encoding));
            foreach(string root in m_QuotaRoots){
                retVal.Append(" " + IMAP_Utils.EncodeMailbox(root,encoding) + "");
            }
            retVal.Append("\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the mailbox name associated with this <c>QUOTAROOT</c> response.
        /// This is the mailbox for which the server reports the set of applicable
        /// quota roots.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In a <c>QUOTAROOT</c> response, the first argument following the
        /// <c>QUOTAROOT</c> keyword is the mailbox name. The mailbox is an IMAP
        /// <em>astring</em> and may be an atom or a quoted string. The value is
        /// decoded from IMAP UTF‑7 before being stored.
        /// </para>
        /// <para>
        /// The general response format is:
        /// </para>
        /// <code>
        /// * QUOTAROOT &lt;mailbox&gt; &lt;root1&gt; [&lt;root2&gt; ...]
        /// </code>
        /// <para>
        /// The mailbox name identifies which mailbox the subsequent quota roots
        /// apply to. The property exposes this value exactly as parsed, after
        /// unquoting and UTF‑7 decoding.
        /// </para>
        /// </remarks>
        public string FolderName
        {
            get{ return m_FolderName; }
        }

        /// <summary>
        /// Gets the collection of quota roots associated with the mailbox named in
        /// this <c>QUOTAROOT</c> response. Each quota root identifies a resource
        /// domain for which the server may report usage and limit values in
        /// subsequent <c>QUOTA</c> responses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In a <c>QUOTAROOT</c> response, all fields following the mailbox name are
        /// quota roots. A mailbox may belong to one or more quota roots, and the
        /// server may return them in any order. Each root is an IMAP <em>astring</em>
        /// and may be an atom or a quoted string. All values are decoded from IMAP
        /// UTF‑7 before being stored.
        /// </para>
        /// <para>
        /// The general response format is:
        /// </para>
        /// <code>
        /// * QUOTAROOT &lt;mailbox&gt; &lt;root1&gt; [&lt;root2&gt; ...]
        /// </code>
        /// <para>
        /// The quota roots listed here do not contain usage or limit values. Those
        /// values are returned separately in <c>QUOTA</c> responses, one per quota
        /// root. Unknown or server‑specific quota roots are preserved for forward
        /// compatibility.
        /// </para>
        /// <para>
        /// Servers may legally return zero quota roots, although most return at
        /// least one. This property exposes the roots exactly as parsed.
        /// </para>
        /// </remarks>
        public string[] QuotaRoots
        {
            get{ return m_QuotaRoots; }
        }

        #endregion
    }
}
