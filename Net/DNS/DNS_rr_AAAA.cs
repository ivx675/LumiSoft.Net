using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace LumiSoft.Net.DNS
{
    /// <summary>
    /// DNS AAAA (IPv6 Address) resource record. Represents an IPv6 address mapping for a host name.
    /// </summary>
    public class DNS_rr_AAAA : DNS_rr
    {
        private IPAddress m_IP;

        /// <summary>
        /// Initializes a new instance of the AAAA resource record, defining an IPv6 address
        /// for the specified host name.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="ip">IPv6 address associated with the host.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
        public DNS_rr_AAAA(string name,IPAddress ip,int ttl) : base(name,DNS_RecordType.AAAA,ttl)
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
        internal static DNS_rr_AAAA Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            // IPv6 = 16xbyte
			byte[] ip = new byte[rdLength];
			Array.Copy(reply,offset,ip,0,rdLength);
            offset += rdLength;
	
			return new DNS_rr_AAAA(name,new IPAddress(ip),ttl);	
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the AAAA resource record.
        /// The format includes the owner name, record type, IPv6 address,
        /// and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; AAAA &lt;ip&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
        {
            return $"{Name} AAAA {m_IP} (TTL={TTL})";
        }

        #endregion


        #region Properties Implementation

        /// <summary>
		/// Gets host IPv6 address.
		/// </summary>
		public IPAddress IP
		{
			get{ return m_IP; }
		}

        #endregion

    }
}
