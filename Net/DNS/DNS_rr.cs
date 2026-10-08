using System;

namespace LumiSoft.Net.DNS
{
	/// <summary>
	/// Abstract base class for all DNS resource records. Encapsulates the common
	/// fields shared by every RR type, including the owner name, record type, and
	/// time‑to‑live (TTL). Specific RR subclasses (A, AAAA, MX, NS, SOA, SRV, TXT,
	/// etc.) extend this class to provide type‑specific data and behavior.
	/// </summary>
	public abstract class DNS_rr
	{
        private string         m_Name = "";
		private DNS_RecordType m_Type = DNS_RecordType.A;
		private int            m_TTL  = -1;

		/// <summary>
		/// Initializes a new DNS resource record with the specified owner name,
		/// record type, and time‑to‑live (TTL). This constructor is used by all
		/// derived RR classes to establish the common fields defined in RFC 1035.
		/// </summary>
		/// <param name="name">The domain name that owns this resource record.</param>
		/// <param name="recordType">The DNS record type (A, AAAA, MX, NS, SRV, TXT, etc.).</param>
		/// <param name="ttl">Time‑to‑live value in seconds.</param>
		public DNS_rr(string name,DNS_RecordType recordType,int ttl)
		{
            m_Name = name;
			m_Type = recordType;
			m_TTL  = ttl;
        }


        #region Properties Implementation

        /// <summary>
		/// Gets the owner name of this DNS resource record. This is the domain name
		/// to which the record applies, as defined in RFC 1035.
		/// </summary>
        public string Name
        {
            get{ return m_Name; }
        }

        /// <summary>
		/// Gets the DNS record type of this resource record. This identifies the
		/// specific RR format (A, AAAA, MX, NS, SOA, SRV, TXT, etc.) as defined in
		/// RFC 1035 and related extensions.
		/// </summary>
		public DNS_RecordType RecordType
		{
			get{ return m_Type; }
		}

		/// <summary>
		/// Gets the time‑to‑live (TTL) value of this DNS resource record, expressed
		/// in seconds. The TTL defines how long the record may be cached by resolvers
		/// before it must be refreshed, as specified in RFC 1035.
		/// </summary>
		public int TTL
		{
			get{ return m_TTL; }
		}

		#endregion
	}
}
