using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an IMAP untagged <c>QUOTA</c> response as defined in
    /// RFC 2087. A QUOTA response reports the current usage and maximum
    /// allowed usage for one or more quota resources within a specific quota
    /// root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The QUOTA response has the following general syntax:
    /// </para>
    /// <code>
    /// * QUOTA &lt;quotaroot&gt; (&lt;resource&gt; &lt;usage&gt; &lt;limit&gt; ...)
    /// </code>
    /// <para>
    /// The <c>quotaroot</c> identifies the resource domain for which quota
    /// information is being reported. It is an IMAP <em>astring</em> and may be
    /// an atom or a quoted string.
    /// </para>
    /// <para>
    /// Each resource triplet consists of:
    /// </para>
    /// <code>
    /// &lt;resource&gt; &lt;usage&gt; &lt;limit&gt;
    /// </code>
    /// <para>
    /// Resource names are case‑insensitive atoms such as <c>STORAGE</c> or
    /// <c>MESSAGE</c>. The <c>usage</c> field specifies the current consumption
    /// of the resource (kilobytes for <c>STORAGE</c>, message count for
    /// <c>MESSAGE</c>). The <c>limit</c> field specifies the maximum allowed
    /// usage using the same units.
    /// </para>
    /// <para>
    /// This class provides strongly‑typed access to the quota root and the
    /// collection of resource entries, as well as serialization back into the
    /// IMAP QUOTA response format.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Quota : IMAP_r_u
    {
        private string               m_QuotaRoot = "";
        private IMAP_t_Quota_Entry[] m_pEntries;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Quota"/> class using
        /// the specified quota root and collection of quota resource entries.
        /// </summary>
        /// <param name="quotaRoot">
        /// The quota root associated with this QUOTA response. The quota root identifies
        /// the resource domain for which usage and limit values are reported.
        /// </param>
        /// <param name="entries">
        /// The array of quota resource entries reported for the quota root. Each entry
        /// corresponds to a <c>&lt;resource&gt; &lt;usage&gt; &lt;limit&gt;</c> triplet as
        /// defined in RFC 2087. The array must not be <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="quotaRoot"/> or <paramref name="entries"/> is
        /// <c>null</c>.
        /// </exception>
        public IMAP_r_u_Quota(string quotaRoot,IMAP_t_Quota_Entry[] entries)
        {
            if(quotaRoot == null){
                throw new ArgumentNullException("quotaRootName");
            }
            if(entries == null){
                throw new ArgumentNullException("entries");
            }

            m_QuotaRoot = quotaRoot;
            m_pEntries  = entries;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP QUOTA untagged response using the supplied
        /// <see cref="_IMAP_Reader"/>. QUOTA responses report the current usage and
        /// limits of server-defined quota resources for a specific quota root, as
        /// defined in RFC 2087.
        /// </summary>
        /// <param name="imapReader">
        /// The lexical IMAP reader positioned at the start of a QUOTA untagged
        /// response.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used when reading the quota root, which may be
        /// encoded as a literal.
        /// </param>
        /// <returns>
        /// A fully parsed <see cref="IMAP_r_u_Quota"/> instance containing the quota
        /// root and its associated resource usage entries.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="imapReader"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the QUOTA response is syntactically invalid, contains an illegal
        /// resource name, includes <c>NIL</c> where an astring is required, or violates
        /// RFC 2087 structural rules.
        /// </exception>
        internal async static Task<IMAP_r_u_Quota> Parse(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            if(imapReader == null){
                throw new ArgumentNullException(nameof(imapReader));
            }

            /*
            RFC 2087 — QUOTA Response
            (Rewritten for implementation clarity)

            The QUOTA untagged response reports the current resource usage and limits for
            a specific quota root. A quota root is a named resource domain (often a mailbox
            hierarchy) for which the server tracks usage.

            Response format:

                * QUOTA <quotaroot> (<resource> <usage> <limit> ...)

            Field definitions:
                <quotaroot>  - The name of the quota root. May be an atom or quoted string.
                               IMAP UTF‑7 decoding is required.

                <resource>   - A resource name such as "STORAGE" or "MESSAGE".
                               Resource names are case-insensitive atoms.

                <usage>      - The current usage of the resource. For STORAGE, usage is
                               measured in kilobytes. For MESSAGE, usage is the number of
                               messages.

                <limit>      - The maximum allowed usage for the resource. Same units as
                               <usage>.

            Multiple resource triplets may appear in a single QUOTA response.

            Examples:

                * QUOTA "" (STORAGE 512 1024)
                * QUOTA user/ivar (MESSAGE 120 500 STORAGE 2048 4096)

            Meaning:
                - The first example reports that the empty quota root has used 512 KB out
                  of a 1024 KB limit.
                - The second example reports two resources:
                    MESSAGE: 120 messages used out of a 500 message limit
                    STORAGE: 2048 KB used out of a 4096 KB limit

            Notes:
                - QUOTA responses are untagged and may appear as part of a GETQUOTA or
                  GETQUOTAROOT command.
                - The server may return multiple resource triplets in any order.
                - Resource names are extensible; servers may define additional resource
                  types beyond STORAGE and MESSAGE.

            Implementation notes:
                - Decode the quota root using IMAP UTF‑7.
                - Parse the parenthesized list of resource triplets.
                - Each triplet consists of: <resource> <usage> <limit>.
                - Usage and limit values are integers.
                - Preserve unknown resource names for forward compatibility.
            */

            // *
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException($"Invalid IMAP QUOTA response (missing *): {imapReader.Text}");
            }

            // "QUOTA"
            if(!string.Equals("QUOTA",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP QUOTA response (missing QUOTA): {imapReader.Text}");
            }

            // quotaroot
            string? quotaroot = await imapReader.ReadMailboxAsync(cancellationToken);
            if(quotaroot == null){
                throw new ParseException($"Invalid IMAP QUOTA response (missing quotaroot): {imapReader.Text}");
            } 

            // Quota entries
            string[] items = imapReader.ReadParenthesized().Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(items.Length % 3 != 0){
                throw new ParseException($"Invalid QUOTA response: {imapReader.Text}");
            }
            List<IMAP_t_Quota_Entry> entries = new List<IMAP_t_Quota_Entry>();
            for(int i=0;i<items.Length;i+=3){
                try{
                    entries.Add(new IMAP_t_Quota_Entry(items[i],Convert.ToInt64(items[i + 1]),Convert.ToInt64(items[i + 2])));
                }
                catch{
                    throw new ParseException($"Invalid QUOTA response: {imapReader.Text}");
                }
            }

            return new IMAP_r_u_Quota(quotaroot,entries.ToArray());
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP wire-format representation of this
        /// <c>QUOTA</c> response with UTF-8 quota roots allowed.
        /// </summary>
        /// <returns>
        /// A string containing the serialized IMAP
        /// <c>QUOTA</c> response.
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
        /// <c>QUOTA</c> response.
        /// </summary>
        /// <param name="utf8Allowed">
        /// Specifies whether UTF-8 quota roots are permitted in the
        /// generated IMAP response.
        /// </param>
        /// <returns>
        /// A string containing the IMAP <c>QUOTA</c> response in
        /// protocol format, terminated with CRLF.
        /// </returns>
        /// <remarks>
        /// The output format follows RFC 2087:
        /// <code>
        /// * QUOTA &lt;quotaroot&gt;
        ///   (&lt;resource&gt; &lt;usage&gt; &lt;limit&gt; ...)
        /// </code>
        /// <para>
        /// Each quota entry is emitted as a resource triplet consisting
        /// of the resource name, current usage, and maximum permitted
        /// usage.
        /// </para>
        /// <para>
        /// When <paramref name="utf8Allowed"/> is
        /// <see langword="false"/>, quota roots are encoded as required
        /// for IMAP servers that do not permit UTF-8 mailbox names in
        /// protocol responses.
        /// </para>
        /// </remarks>
        public override string ToString(bool utf8Allowed)
        {
            // Example:    S: * QUOTA "" (STORAGE 10 512)

            StringBuilder retVal = new StringBuilder();
            retVal.Append("* QUOTA " + IMAP_Utils.EncodeMailbox(m_QuotaRoot,utf8Allowed) + " (");
            for(int i=0;i<m_pEntries.Length;i++){
                if(i > 0){
                    retVal.Append(" ");
                }
                retVal.Append(m_pEntries[i].ResourceName + " " + m_pEntries[i].Usage + " " + m_pEntries[i].Limit);
            }
            retVal.Append(")\r\n");

            return retVal.ToString();
        }

        #endregion


        #region Properties impelemntation

        /// <summary>
        /// Gets the quota root associated with this QUOTA response. The quota root
        /// identifies the resource domain for which the server reports usage and
        /// limit values.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The quota root is an IMAP <em>astring</em> as defined in RFC 2087.
        /// It may be an atom or a quoted string and is decoded from IMAP UTF‑7
        /// before being stored. A quota root typically corresponds to a mailbox
        /// hierarchy or other server‑defined resource domain.
        /// </para>
        /// <para>
        /// QUOTA responses have the general form:
        /// </para>
        /// <code>
        /// * QUOTA &lt;quotaroot&gt; (&lt;resource&gt; &lt;usage&gt; &lt;limit&gt; ...)
        /// </code>
        /// <para>
        /// The <c>quotaroot</c> value in this property is the first field following
        /// the <c>QUOTA</c> keyword and uniquely identifies the quota domain for
        /// all resource entries contained in the response.
        /// </para>
        /// </remarks>
        public string QuotaRoot
        {
            get{ return m_QuotaRoot; }
        }

        /// <summary>
        /// Gets the collection of quota resource entries reported in this QUOTA
        /// response. Each entry contains a resource name, usage value, and limit
        /// value as defined in RFC 2087.
        /// </summary>
        public IMAP_t_Quota_Entry[] ResourceEntries
        {
            get { return m_pEntries; }
        }

        #endregion
    }
}
