using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a single quota resource entry in an IMAP <c>QUOTA</c> response,
    /// as defined in RFC 2087. Each entry reports the current usage and the
    /// maximum allowed usage for a specific quota resource within a quota root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A quota entry corresponds to one resource triplet in the QUOTA response:
    /// </para>
    /// <code>
    /// &lt;resource&gt; &lt;usage&gt; &lt;limit&gt;
    /// </code>
    /// <para>
    /// The <c>resource</c> field identifies the quota resource being tracked.
    /// Common resource names include <c>STORAGE</c> (kilobytes of space used) and
    /// <c>MESSAGE</c> (number of messages), but servers may define additional
    /// resource types. Resource names are case‑insensitive IMAP atoms and are
    /// preserved exactly as provided by the server.
    /// </para>
    /// <para>
    /// The <c>usage</c> field specifies the current consumption of the resource.
    /// For <c>STORAGE</c>, usage is measured in kilobytes; for <c>MESSAGE</c>,
    /// usage is the number of messages. The <c>limit</c> field specifies the
    /// maximum allowed usage for the resource, using the same units as
    /// <c>usage</c>.
    /// </para>
    /// </remarks>
    public class IMAP_t_Quota_Entry
    {
        private string m_ResourceName = "";
        private long   m_Usage        = 0;
        private long   m_Limit        = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Quota_Entry"/> class
        /// using the specified resource name, usage value, and limit value.
        /// </summary>
        /// <param name="resourceName">
        /// The name of the quota resource being reported (for example,
        /// <c>STORAGE</c> or <c>MESSAGE</c>). Resource names are case‑insensitive
        /// IMAP atoms and must be a non‑empty string.
        /// </param>
        /// <param name="usage">
        /// The current usage of the resource, as defined in RFC 2087. For
        /// <c>STORAGE</c>, usage is measured in kilobytes; for <c>MESSAGE</c>,
        /// usage is the number of messages.
        /// </param>
        /// <param name="limit">
        /// The maximum allowed usage for the resource. The interpretation matches
        /// the resource type: kilobytes for <c>STORAGE</c>, message count for
        /// <c>MESSAGE</c>. The limit is always an integer.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="resourceName"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="resourceName"/> is an empty string.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A quota entry represents one resource triplet in a QUOTA response:
        /// </para>
        /// <code>
        /// &lt;resource&gt; &lt;usage&gt; &lt;limit&gt;
        /// </code>
        /// </remarks>
        public IMAP_t_Quota_Entry(string resourceName,long usage,long limit)
        {
            if(resourceName == null){
                throw new ArgumentNullException("resourceName");
            }
            if(resourceName == string.Empty){
                throw new ArgumentException("Argument 'resourceName' value must be specified.","resourceName");
            }

            m_ResourceName = resourceName;
            m_Usage        = usage;
            m_Limit        = limit;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the name of the quota resource reported in this QUOTA entry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A quota resource identifies the type of usage being tracked for a quota
        /// root, as defined in RFC 2087. Common resource names include
        /// <c>STORAGE</c> (kilobytes of space used) and <c>MESSAGE</c> (number of
        /// messages stored), but servers may define additional resource types.
        /// </para>
        /// <para>
        /// Resource names are case‑insensitive IMAP atoms and are returned exactly as
        /// provided by the server. The parser preserves unknown or non‑standard
        /// resource names for forward compatibility.
        /// </para>
        /// <para>
        /// Each QUOTA entry consists of:
        /// </para>
        /// <code>
        /// &lt;resource&gt; &lt;usage&gt; &lt;limit&gt;
        /// </code>
        /// <para>
        /// where <c>resource</c> is this property, <c>usage</c> is the current usage
        /// value, and <c>limit</c> is the maximum allowed usage for the resource.
        /// </para>
        /// </remarks>
        public string ResourceName
        {
            get{ return m_ResourceName;}
        }

        /// <summary>
        /// Gets the current usage value for this quota resource, as reported by the
        /// server in the QUOTA response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The usage value represents the amount of the resource currently consumed
        /// within the quota root, as defined in RFC 2087. The interpretation of
        /// the value depends on the resource type:
        /// </para>
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///     For the <c>STORAGE</c> resource, usage is measured in kilobytes.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     For the <c>MESSAGE</c> resource, usage is the number of messages.
        ///     </description>
        ///   </item>
        /// </list>
        /// <para>
        /// Usage is always an integer and is returned exactly as provided by the
        /// server. The value reflects the current state of the quota at the time the
        /// QUOTA response was generated.
        /// </para>
        /// <para>
        /// Each quota entry consists of:
        /// </para>
        /// <code>
        /// &lt;resource&gt; &lt;usage&gt; &lt;limit&gt;
        /// </code>
        /// <para>
        /// where this property corresponds to the <c>usage</c> field.
        /// </para>
        /// </remarks>
        public long Usage
        {
            get{ return m_Usage; }
        }

        /// <summary>
        /// Gets the maximum allowed usage for this quota resource, as reported by the
        /// server in the QUOTA response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The limit value represents the upper bound of resource consumption permitted
        /// within the quota root, as defined in RFC 2087. The interpretation of
        /// the value depends on the resource type:
        /// </para>
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///     For the <c>STORAGE</c> resource, the limit is measured in kilobytes.
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///     For the <c>MESSAGE</c> resource, the limit is the maximum number of
        ///     messages allowed.
        ///     </description>
        ///   </item>
        /// </list>
        /// <para>
        /// The limit is always an integer and is returned exactly as provided by the
        /// server. It reflects the quota configuration in effect at the time the
        /// QUOTA response was generated.
        /// </para>
        /// <para>
        /// Each quota entry consists of:
        /// </para>
        /// <code>
        /// &lt;resource&gt; &lt;usage&gt; &lt;limit&gt;
        /// </code>
        /// <para>
        /// where this property corresponds to the <c>limit</c> field.
        /// </para>
        /// </remarks>
        public long Limit
        {
            get{ return m_Limit; }
        }

        #endregion
    }
}
