using System;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
	/// <summary>
    /// DNS NS (Name Server) resource record. Specifies an authoritative name server
    /// for the domain, identifying the host responsible for answering DNS queries
    /// for that zone.
    /// </summary>
	[Serializable]
	public class DNS_rr_NS : DNS_rr
	{
		private string m_NameServer = "";

		/// <summary>
        /// Initializes a new instance of the NS (Name Server) resource record, specifying
        /// the authoritative name server for the given domain.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="nameServer">Host name of the authoritative name server for the domain.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_NS(string name,string nameServer,int ttl) : base(name,DNS_RecordType.NS,ttl)
		{
			m_NameServer = nameServer;
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
        internal static DNS_rr_NS Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            // Name server name

			string? server = DNS_Client.ReadQName(reply,ref offset);			
			if(server != null){			
				return new DNS_rr_NS(name,server,ttl);
			}
            else{
                throw new ParseException("Invalid NS resource record data.");
            }
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the NS resource record.
        /// The format includes the owner name, record type, name server,
        /// and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; NS &lt;nameserver&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} NS {m_NameServer} (TTL={TTL})";
		}

		#endregion


        #region Properties Implementation

        /// <summary>
        /// Gets the host name of the authoritative name server specified by this
        /// NS resource record.
        /// </summary>
		public string NameServer
		{
			get{ return m_NameServer; }
		}

		#endregion

	}
}
