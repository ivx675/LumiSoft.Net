using System;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
	/// <summary>
    /// DNS CNAME (Canonical Name) resource record. Specifies an alias that maps a domain name
    /// to its canonical (true) name.
    /// </summary>
	[Serializable]
	public class DNS_rr_CNAME : DNS_rr
	{
		private string m_Alias = "";

		/// <summary>
        /// Initializes a new instance of the CNAME resource record, defining an alias
        /// that maps the owner name to its canonical (true) name.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="alias">Canonical name to which the owner name resolves.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_CNAME(string name,string alias,int ttl) : base(name,DNS_RecordType.CNAME,ttl)
		{
			m_Alias = alias;
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
        internal static DNS_rr_CNAME Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            string? alias = DNS_Client.ReadQName(reply,ref offset);			
			if(alias != null){			
				return new DNS_rr_CNAME(name,alias,ttl);
			}
            else{
                throw new ParseException("Invalid CNAME resource record data.");
            }
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the CNAME resource record.
        /// The format includes the owner name, record type, alias target,
        /// and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; CNAME &lt;alias&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} CNAME {m_Alias} (TTL={TTL})";
		}

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets the canonical name to which the owner name of this CNAME record resolves.
        /// </summary>
		public string Alias
		{
			get{ return m_Alias; }
		}

		#endregion

	}
}
