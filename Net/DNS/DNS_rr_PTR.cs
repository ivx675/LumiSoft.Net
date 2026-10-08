using System;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
	/// <summary>
    /// DNS PTR (Pointer) resource record. Maps an IP address to a domain name,
    /// providing reverse DNS lookup information as defined in RFC 1035.
    /// </summary>
	[Serializable]
	public class DNS_rr_PTR : DNS_rr
	{
		private string m_DomainName = "";

		/// <summary>
        /// Initializes a new instance of the PTR (Pointer) resource record, mapping an
        /// IP address to a domain name for reverse DNS lookup as defined in RFC 1035.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="domainName">The domain name to which the IP address resolves.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_PTR(string name,string domainName,int ttl) : base(name,DNS_RecordType.PTR,ttl)
		{
			m_DomainName = domainName;
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
        internal static DNS_rr_PTR Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            string? domainName = DNS_Client.ReadQName(reply,ref offset);
			if(domainName != null){
			    return new DNS_rr_PTR(name,domainName,ttl);
            }
            else{
                throw new ParseException("Invalid PTR resource record data.");
            }
        }

        #endregion


        #region ovrride method ToString

        /// <summary>
        /// Returns a textual representation of the PTR resource record.
        /// The format includes the owner name, record type, domain name,
        /// and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; PTR &lt;domainname&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} PTR {m_DomainName} (TTL={TTL})";
		}

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets the domain name to which the IP address resolves in this PTR resource
        /// record, providing reverse DNS lookup information as defined in RFC 1035.
        /// </summary>
		public string DomainName
		{
			get{ return m_DomainName; }
		}

		#endregion

	}
}
