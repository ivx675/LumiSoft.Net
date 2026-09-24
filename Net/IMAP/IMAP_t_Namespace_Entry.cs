using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a single namespace descriptor returned in an IMAP
    /// <c>NAMESPACE</c> response as defined in RFC 2342. A namespace
    /// entry consists of a prefix and an optional hierarchy delimiter and
    /// describes one logical mailbox namespace advertised by the server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each namespace entry has the form:
    /// </para>
    /// <code>
    /// (&lt;prefix&gt; &lt;delimiter&gt;)
    /// </code>
    /// <para>
    /// The <c>prefix</c> identifies the logical root of the namespace. It may
    /// be an empty string (<c>""</c>) or any server-defined value such as
    /// <c>"INBOX."</c>, <c>"Public/"</c>, or <c>"#shared"</c>. Prefixes may be
    /// quoted strings in the raw NAMESPACE response and may contain
    /// vendor-specific or implementation-specific values. This implementation
    /// preserves the prefix exactly as parsed.
    /// </para>
    /// <para>
    /// The <c>delimiter</c> specifies the hierarchy separator used within the
    /// namespace. Common values are <c>'/'</c> or <c>'.'</c>. If the delimiter
    /// is <c>null</c>, the server has returned <c>NIL</c>, indicating that the
    /// namespace is flat and has no hierarchical structure. In this case,
    /// mailbox names MUST NOT be combined using a separator, and the prefix is
    /// applied directly without inserting any delimiter.
    /// </para>
    /// <para>
    /// Namespace entries appear inside the untagged <c>NAMESPACE</c> response,
    /// which contains three lists: personal namespaces, other-users namespaces,
    /// and shared namespaces. Servers may return multiple entries per category.
    /// </para>
    /// </remarks>
    public class IMAP_t_Namespace_Entry
    {
        private string m_Prefix    = "";
        private char?  m_Delimiter = '/'; 

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Namespace_Entry"/>
        /// class using the specified namespace prefix and hierarchy delimiter.
        /// </summary>
        /// <param name="prefix">
        /// The namespace prefix as returned by the server. The prefix identifies
        /// the logical root of the namespace and may be an empty string (<c>""</c>)
        /// or any server‑defined value such as <c>"INBOX."</c>, <c>"Public/"</c>,
        /// or <c>"#shared"</c>. The value must not be <c>null</c>.
        /// </param>
        /// <param name="delimiter">
        /// The hierarchy delimiter for this namespace. Common values are
        /// <c>'/'</c> or <c>'.'</c>. If the value is <c>null</c>, the server has
        /// returned <c>NIL</c>, indicating that the namespace is flat and has no
        /// hierarchical structure. In this case, mailbox names MUST NOT be
        /// combined using a separator, and the prefix is applied directly without
        /// inserting any delimiter.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="prefix"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Namespace_Entry(string prefix,char? delimiter)
        {
            if(prefix == null){
                throw new ArgumentNullException(nameof(prefix));
            }

            m_Prefix    = prefix;
            m_Delimiter = delimiter;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the namespace prefix associated with this namespace entry.
        /// The prefix identifies the logical root under which the server
        /// exposes mailboxes belonging to this namespace.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Namespace prefixes are defined in RFC 2342 (IMAP4 Namespace
        /// Extension). A prefix is an IMAP string that specifies the base path
        /// for a mailbox namespace. It may be an empty string (<c>""</c>),
        /// meaning the root of the hierarchy, or any server‑defined value such
        /// as <c>"INBOX."</c>, <c>"Public/"</c>, or <c>"#shared"</c>.
        /// </para>
        /// <para>
        /// Prefixes may be quoted strings in the raw NAMESPACE response and may
        /// contain vendor‑specific or implementation‑specific values. This
        /// implementation preserves the prefix exactly as parsed so that clients
        /// can construct mailbox names correctly and without assumptions.
        /// </para>
        /// <para>
        /// When combined with the namespace delimiter (if one exists), the prefix
        /// forms the base path used to construct fully qualified mailbox names.
        /// If the delimiter is <c>NIL</c>, the namespace is flat and the prefix
        /// is applied without inserting a hierarchy separator.
        /// </para>
        /// </remarks>
        public string Prefix
        {
            get{ return m_Prefix; }
        }

        /// <summary>
        /// Gets the hierarchy delimiter associated with this namespace entry.
        /// The delimiter defines how child mailbox names are combined with the
        /// namespace prefix when constructing fully qualified mailbox paths.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Namespace delimiters are defined in RFC 2342 (IMAP4 Namespace
        /// Extension). A delimiter is normally a single character such as
        /// <c>'/'</c> or <c>'.'</c> and indicates the hierarchy separator used
        /// within the namespace. For example, a prefix of <c>"INBOX."</c> with a
        /// delimiter of <c>'.'</c> produces mailbox names such as
        /// <c>"INBOX.Work"</c>.
        /// </para>
        /// <para>
        /// If the delimiter is <c>null</c>, the server has returned <c>NIL</c>
        /// for the delimiter field, meaning that the namespace is flat and has
        /// no hierarchical structure. In this case, mailbox names MUST NOT be
        /// combined using a separator, and the prefix is applied directly without
        /// inserting any delimiter. For example, a prefix of <c>"#shared"</c>
        /// with a <c>null</c> delimiter yields mailbox names such as
        /// <c>"#sharedReports"</c> rather than <c>"#shared/Reports"</c>.
        /// </para>
        /// <para>
        /// Delimiters are preserved exactly as parsed. Unknown or vendor‑specific
        /// delimiter characters are allowed, although <c>null</c> is the only
        /// non‑character value permitted by RFC 2342.
        /// </para>
        /// </remarks>
        public char? Delimiter
        {
            get{ return m_Delimiter; }
        }

        #endregion
    }
}
