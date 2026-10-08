using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
    /// <summary>
    /// DNS SRV (Service Locator) resource record. Specifies the location of a
    /// service within a domain, including its priority, weight, port, and target
    /// host, as defined in RFC 2782.
    /// </summary>
    [Serializable]
    public class DNS_rr_SRV : DNS_rr
    {
        private int    m_Priority = 1;
        private int    m_Weight   = 1;
        private int    m_Port     = 0;
        private string m_Target   = "";

        /// <summary>
        /// Initializes a new instance of the SRV (Service Locator) resource record,
        /// specifying the service priority, weight, port, and target host as defined
        /// in RFC 2782.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="priority">
        /// The priority of the target host. Lower values indicate higher preference.
        /// </param>
        /// <param name="weight">
        /// Relative weight for records with the same priority. Higher values increase
        /// the likelihood of selection.
        /// </param>
        /// <param name="port">The TCP or UDP port on which the service is running.</param>
        /// <param name="target">The target host providing the service.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
        public DNS_rr_SRV(string name,int priority,int weight,int port,string target,int ttl) : base(name,DNS_RecordType.SRV,ttl)
        {
            m_Priority = priority;
            m_Weight   = weight;
            m_Port     = port;
            m_Target   = target;
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
        internal static DNS_rr_SRV Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            // Priority Weight Port Target
            
            // Priority
            int priority  = reply[offset++] << 8 | reply[offset++];

            // Weight
            int weight  = reply[offset++] << 8 | reply[offset++];

            // Port
            int port  = reply[offset++] << 8 | reply[offset++];

            // Target
            string? target = DNS_Client.ReadQName(reply,ref offset);
            if(target == null){
				throw new ParseException("Invalid SRV resource record data.");
			}

            return new DNS_rr_SRV(name,priority,weight,port,target,ttl);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the SRV resource record.
        /// The format includes the owner name, record type, priority, weight,
        /// port, target host, and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form:
        /// "&lt;name&gt; SRV &lt;priority&gt; &lt;weight&gt; &lt;port&gt; &lt;target&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
        {
            return $"{Name} SRV {m_Priority} {m_Weight} {m_Port} {m_Target} (TTL={TTL})";
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets the priority value of this SRV resource record. Lower values indicate
        /// higher preference when selecting the target host for the service, as defined
        /// in RFC 2782.
        /// </summary>
        public int Priority
        {
            get{ return m_Priority; }
        }

        /// <summary>
        /// Gets the weight value of this SRV resource record. When multiple targets
        /// share the same priority, the weight determines the relative likelihood of
        /// selecting this record, with higher values increasing its probability as
        /// defined in RFC 2782.
        /// </summary>
        public int Weight
        {
            get{ return m_Weight; }
        }

        /// <summary>
        /// Gets the TCP or UDP port number on which the advertised service is running,
        /// as specified by this SRV resource record.
        /// </summary>
        public int Port
        {
            get{ return m_Port; }
        }

        /// <summary>
        /// Gets the target host specified by this SRV resource record. This host
        /// provides the advertised service and may be selected based on the record's
        /// priority and weight values, as defined in RFC 2782.
        /// </summary>
        public string Target
        {
            get{ return m_Target; }
        }

        #endregion

    }
}
