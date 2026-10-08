using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
    /// <summary>
    /// DNS NAPTR (Naming Authority Pointer) resource record. Defines rewrite rules used
    /// for URI, ENUM, and other applications, including order, preference, flags,
    /// services, a regular expression, and a replacement domain name.
    /// </summary>
    [Serializable]
    public class DNS_rr_NAPTR : DNS_rr
    {
        private int    m_Order       = 0;
        private int    m_Preference  = 0;
        private string m_Flags       = "";
        private string m_Services    = "";
        private string m_Regexp      = "";
        private string m_Replacement = "";

        /// <summary>
        /// Initializes a new instance of the NAPTR (Naming Authority Pointer) resource record,
        /// defining a rewrite rule consisting of order, preference, flags, services, a regular
        /// expression, and a replacement domain name as specified in RFC 3403.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="order">Processing order for NAPTR records; lower values are evaluated first.</param>
        /// <param name="preference">Preference among records with equal order; lower values indicate higher priority.</param>
        /// <param name="flags">Flags controlling how the rewrite rule is interpreted.</param>
        /// <param name="services">Service parameters associated with this rule.</param>
        /// <param name="regexp">Regular expression used to transform the original domain name.</param>
        /// <param name="replacement">Replacement domain name used when the regular expression is empty.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
        public DNS_rr_NAPTR(string name,int order,int preference,string flags,string services,string regexp,string replacement,int ttl) : base(name,DNS_RecordType.NAPTR,ttl)
        {
            m_Order       = order;
            m_Preference  = preference;
            m_Flags       = flags;
            m_Services    = services;
            m_Regexp      = regexp;
            m_Replacement = replacement;
        }


        #region static method Parse

        /// <summary>
        /// Parses resource record from reply data.
        /// </summary>
        /// <param name="name">DNS domain name that owns a resource record.</param>
        /// <param name="reply">DNS server reply data.</param>
        /// <param name="offset">Current offset in reply data.</param>
        /// <param name="rdLength">Resource record data length.</param>
        /// <param name="ttl">Time to live in seconds.</param>
        internal static DNS_rr_NAPTR Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            /* RFC 3403.
                The packet format for the NAPTR record is as follows
                                               1  1  1  1  1  1
                 0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                |                     ORDER                     |
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                |                   PREFERENCE                  |
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                     FLAGS                     /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                   SERVICES                    /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                    REGEXP                     /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
                /                  REPLACEMENT                  /
                /                                               /
                +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            */

            int order = reply[offset++] << 8 | reply[offset++];

            int preference = reply[offset++] << 8 | reply[offset++];

            string flags = DNS_Client.ReadCharacterString(reply,ref offset);

            string services = DNS_Client.ReadCharacterString(reply,ref offset);

            string regexp = DNS_Client.ReadCharacterString(reply,ref offset);
            
            string? replacement = DNS_Client.ReadQName(reply,ref offset);
            if(replacement == null){
                throw new ParseException("Invalid NAPTR resource record data.");
            }

            return new DNS_rr_NAPTR(name,order,preference,flags,services,regexp,replacement,ttl);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the NAPTR resource record.
        /// The format includes the owner name, record type, order, preference,
        /// flags, services, regular expression, replacement value, and the TTL.
        /// </summary>
        /// <returns>
        /// A string in the form:
        /// "&lt;name&gt; NAPTR &lt;order&gt; &lt;preference&gt; &lt;flags&gt; &lt;services&gt; &lt;regexp&gt; &lt;replacement&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
        {
            return $"{Name} NAPTR {m_Order} {m_Preference} {m_Flags} {m_Services} {m_Regexp} {m_Replacement} (TTL={TTL})";
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets the order value of this NAPTR resource record. Lower values are processed
        /// before higher values when evaluating rewrite rules.
        /// </summary>
        public int Order
        {
            get{ return m_Order; }
        }

        /// <summary>
        /// Gets the preference value of this NAPTR resource record. Among records with
        /// the same order value, lower preference values indicate higher priority.
        /// </summary>
        public int Preference
        {
            get{ return m_Preference; }
        }

        /// <summary>
        /// Gets the flags that control how this NAPTR rewrite rule is interpreted, as
        /// defined in RFC 3403.
        /// </summary>
        public string Flags
        {
            get{ return m_Flags; }
        }

        /// <summary>
        /// Gets the service parameters associated with this NAPTR rewrite rule, as
        /// defined in RFC 3403.
        /// </summary>
        public string Services
        {
            get{ return m_Services; }
        }

        /// <summary>
        /// Gets the regular expression used by this NAPTR rewrite rule to transform the
        /// original domain name, as defined in RFC 3403.
        /// </summary>
        public string Regexp
        {
            get{ return m_Regexp; }
        }

        /// <summary>
        /// Gets the replacement domain name used by this NAPTR rewrite rule when the
        /// regular expression field is empty, as defined in RFC 3403.
        /// </summary>
        public string Replacement
        {
            get{ return m_Replacement; }
        }

        #endregion

    }
}
