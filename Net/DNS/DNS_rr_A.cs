using System;
using System.Net;

namespace LumiSoft.Net.DNS
{
	/// <summary>
	/// DNS A (Address) resource record. Represents an IPv4 address mapping for a host name.
	/// </summary>
	[Serializable]
	public class DNS_rr_A : DNS_rr
	{
		private IPAddress m_IP;

		/// <summary>
		/// Initializes a new instance of the A resource record, defining an IPv4 address
		/// for the specified host name.
		/// </summary>
		/// <param name="name">DNS domain name that owns this resource record.</param>
		/// <param name="ip">IPv4 address associated with the host.</param>
		/// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_A(string name,IPAddress ip,int ttl) : base(name,DNS_RecordType.A,ttl)
		{
			m_IP = ip;
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
        internal static DNS_rr_A Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            // IPv4 = byte byte byte byte

			byte[] ip = new byte[rdLength];
			Array.Copy(reply,offset,ip,0,rdLength);
            offset += rdLength;
            
			return new DNS_rr_A(name,new IPAddress(ip),ttl);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the A resource record.
        /// The format includes the owner name, record type, IPv4 address,
        /// and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; A &lt;ip&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} A {m_IP} (TTL={TTL})";
		}

		#endregion


		#region Properties Implementation

		/// <summary>
		/// Gets host IP address.
		/// </summary>
		public IPAddress IP
		{
			get{ return m_IP; }
		}

		#endregion

	}
}
