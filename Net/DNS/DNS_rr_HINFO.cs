using System;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
	/// <summary>
	/// DNS HINFO (Host Information) resource record. Specifies the CPU type and operating
	/// system of a host, as defined in RFC 1035.
	/// </summary>
	public class DNS_rr_HINFO : DNS_rr
	{
		private string m_CPU = "";
		private string m_OS  = "";

		/// <summary>
		/// Initializes a new instance of the HINFO resource record, defining the CPU type
		/// and operating system of the host as specified in RFC 1035.
		/// </summary>
		/// <param name="name">DNS domain name that owns this resource record.</param>
		/// <param name="cpu">CPU type of the host.</param>
		/// <param name="os">Operating system of the host.</param>
		/// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_HINFO(string name,string cpu,string os,int ttl) : base(name,DNS_RecordType.HINFO,ttl)
		{
			m_CPU = cpu;
			m_OS  = os;
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
        internal static DNS_rr_HINFO Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            /* RFC 1035 3.3.2. HINFO RDATA format

			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			/                      CPU                      /
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			/                       OS                      /
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			
			CPU     A <character-string> which specifies the CPU type.

			OS      A <character-string> which specifies the operating
					system type.
					
					Standard values for CPU and OS can be found in [RFC-1010].

			*/

			// CPU
			string cpu = DNS_Client.ReadCharacterString(reply,ref offset);

			// OS
			string os = DNS_Client.ReadCharacterString(reply,ref offset);

			return new DNS_rr_HINFO(name,cpu,os,ttl);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the HINFO resource record.
        /// The format includes the owner name, record type, CPU value,
        /// OS value, and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; HINFO &lt;cpu&gt; &lt;os&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} HINFO {m_CPU} {m_OS} (TTL={TTL})";
		}

		#endregion


        #region Properties Implementation

        /// <summary>
		/// Gets the CPU type of the host as specified in this HINFO resource record.
		/// </summary>
		public string CPU
		{
			get{ return m_CPU; }
		}

		/// <summary>
		/// Gets the operating system of the host as specified in this HINFO resource record.
		/// </summary>
		public string OS
		{
			get{ return m_OS; }
		}
        
		#endregion
	}
}
