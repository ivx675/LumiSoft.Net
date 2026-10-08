using System;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
	/// <summary>
	/// DNS SOA (Start of Authority) resource record. Defines the authoritative
	/// information for a DNS zone, including the primary name server, the
	/// responsible party, and zone‑control parameters such as serial number,
	/// refresh, retry, expire, and minimum TTL, as specified in RFC 1035.
	/// </summary>
	[Serializable]
	public class DNS_rr_SOA : DNS_rr
	{
		private string m_NameServer = "";
		private string m_AdminEmail = "";
		private long   m_Serial     = 0;
		private long   m_Refresh    = 0;
		private long   m_Retry      = 0;
		private long   m_Expire     = 0;
		private long   m_Minimum    = 0;
		
		/// <summary>
		/// Initializes a new instance of the SOA (Start of Authority) resource record,
		/// defining the authoritative zone parameters including the primary name server,
		/// responsible party, serial number, and timing values as specified in RFC 1035.
		/// </summary>
		/// <param name="name">DNS domain name that owns this resource record.</param>
		/// <param name="nameServer">Primary authoritative name server for the zone.</param>
		/// <param name="adminEmail">Email address of the zone administrator, in DNS SOA format.</param>
		/// <param name="serial">Zone serial number used for synchronization between name servers.</param>
		/// <param name="refresh">Time in seconds before secondary servers should check for updates.</param>
		/// <param name="retry">Time in seconds before retrying a failed zone transfer.</param>
		/// <param name="expire">Time in seconds after which zone data is considered invalid.</param>
		/// <param name="minimum">Minimum TTL value applied to negative responses.</param>
		/// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_SOA(string name,string nameServer,string adminEmail,long serial,long refresh,long retry,long expire,long minimum,int ttl) : base(name,DNS_RecordType.SOA,ttl)
		{
			m_NameServer = nameServer;
			m_AdminEmail = adminEmail;
			m_Serial     = serial;
			m_Refresh    = refresh;
			m_Retry      = retry;
			m_Expire     = expire;
			m_Minimum    = minimum;
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
        internal static DNS_rr_SOA Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            /* RFC 1035 3.3.13. SOA RDATA format

				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
				/                     MNAME                     /
				/                                               /
				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
				/                     RNAME                     /
				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
				|                    SERIAL                     |
				|                                               |
				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
				|                    REFRESH                    |
				|                                               |
				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
				|                     RETRY                     |
				|                                               |
				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
				|                    EXPIRE                     |
				|                                               |
				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
				|                    MINIMUM                    |
				|                                               |
				+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

			where:

			MNAME           The <domain-name> of the name server that was the
							original or primary source of data for this zone.

			RNAME           A <domain-name> which specifies the mailbox of the
							person responsible for this zone.

			SERIAL          The unsigned 32 bit version number of the original copy
							of the zone.  Zone transfers preserve this value.  This
							value wraps and should be compared using sequence space
							arithmetic.

			REFRESH         A 32 bit time interval before the zone should be
							refreshed.

			RETRY           A 32 bit time interval that should elapse before a
							failed refresh should be retried.

			EXPIRE          A 32 bit time value that specifies the upper limit on
							the time interval that can elapse before the zone is no
							longer authoritative.
							
			MINIMUM         The unsigned 32 bit minimum TTL field that should be
							exported with any RR from this zone.
			*/

			//---- Parse record -------------------------------------------------------------//
			// MNAME
			string? nameserver = DNS_Client.ReadQName(reply,ref offset);			
			if(nameserver == null){
				throw new ParseException("Invalid SOA resource record data.");
			}

			// RNAME
			string? adminMailBox = DNS_Client.ReadQName(reply,ref offset);	
			if(adminMailBox == null){
				throw new ParseException("Invalid SOA resource record data.");
			}			
			char[] adminMailBoxAr = adminMailBox.ToCharArray();
			for(int i=0;i<adminMailBoxAr.Length;i++){			
				if(adminMailBoxAr[i] == '.'){
					adminMailBoxAr[i] = '@';
					break;
				}
			}
			adminMailBox = new string(adminMailBoxAr);

			// SERIAL
			long serial = reply[offset++] << 24 | reply[offset++] << 16 | reply[offset++] << 8 | reply[offset++];

			// REFRESH
			long refresh = reply[offset++] << 24 | reply[offset++] << 16 | reply[offset++] << 8 | reply[offset++];

			// RETRY
			long retry = reply[offset++] << 24 | reply[offset++] << 16 | reply[offset++] << 8 | reply[offset++];

			// EXPIRE
			long expire = reply[offset++] << 24 | reply[offset++] << 16 | reply[offset++] << 8 | reply[offset++];

			// MINIMUM
			long minimum = reply[offset++] << 24 | reply[offset++] << 16 | reply[offset++] << 8 | reply[offset++];
			//--------------------------------------------------------------------------------//

			return new DNS_rr_SOA(name,nameserver,adminMailBox,serial,refresh,retry,expire,minimum,ttl);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the SOA resource record.
        /// The format includes the owner name, record type, primary name server,
        /// administrator email, serial number, refresh, retry, expire, minimum TTL,
        /// and the TTL value of the record.
        /// </summary>
        /// <returns>
        /// A string in the form:
        /// "&lt;name&gt; SOA &lt;nameserver&gt; &lt;adminemail&gt; &lt;serial&gt; &lt;refresh&gt; &lt;retry&gt; &lt;expire&gt; &lt;minimum&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} SOA {m_NameServer} {m_AdminEmail} {m_Serial} {m_Refresh} {m_Retry} {m_Expire} {m_Minimum} (TTL={TTL})";
		}

		#endregion


        #region Properties Implementation

        /// <summary>
		/// Gets the primary authoritative name server for the DNS zone as specified
		/// by this SOA resource record.
		/// </summary>
		public string NameServer
		{
			get{ return m_NameServer; }
		}

		/// <summary>
		/// Gets the email address of the zone administrator as specified by this SOA
		/// resource record. The address follows the DNS SOA format, where the first
		/// dot in the local part represents the '@' symbol.
		/// </summary>
		public string AdminEmail
		{
			get{ return m_AdminEmail; }
		}

		/// <summary>
		/// Gets the zone serial number used by this SOA resource record. Secondary
		/// name servers compare this value to determine whether the zone data has
		/// changed and a zone transfer is required.
		/// </summary>
		public long Serial
		{
			get{ return m_Serial; }
		}

		/// <summary>
		/// Gets the refresh interval, in seconds, specified by this SOA resource
		/// record. Secondary name servers wait this amount of time before checking
		/// the primary server to determine whether the zone serial number has changed.
		/// </summary>
		public long Refresh
		{
			get{ return m_Refresh; }
		}

		/// <summary>
		/// Gets the retry interval, in seconds, specified by this SOA resource record.
		/// If a secondary name server fails to contact the primary during a refresh
		/// attempt, it waits this amount of time before trying again.
		/// </summary>
		public long Retry
		{
			get{ return m_Retry; }
		}

		/// <summary>
		/// Gets the expire interval, in seconds, specified by this SOA resource record.
		/// If a secondary name server cannot reach the primary for this entire period,
		/// the zone data is considered invalid and must no longer be served.
		/// </summary>
		public long Expire
		{
			get{ return m_Expire; }
		}

		/// <summary>
		/// Gets the minimum TTL value, in seconds, specified by this SOA resource
		/// record. This value is used primarily for negative caching, defining how
		/// long resolvers may cache NXDOMAIN or other negative responses.
		/// </summary>
		public long Minimum
		{
			get{ return m_Minimum; }
		}

		#endregion

	}
}
