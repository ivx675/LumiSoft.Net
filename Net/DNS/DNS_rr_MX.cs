using System;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
	/// <summary>
    /// DNS MX (Mail Exchange) resource record. Specifies a mail server responsible
    /// for accepting email for the domain, together with its preference value.
    /// </summary>
	[Serializable]
	public class DNS_rr_MX : DNS_rr,IComparable
	{
		private int    m_Preference = 0;
		private string m_Host       = "";

		/// <summary>
        /// Initializes a new instance of the MX resource record, defining a mail exchange
        /// host and its preference value for the specified domain.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="preference">Preference value; lower numbers indicate higher priority.</param>
        /// <param name="host">Mail exchange host name.</param>
        /// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_MX(string name,int preference,string host,int ttl) : base(name,DNS_RecordType.MX,ttl)
		{
			m_Preference = preference;
			m_Host       = host;
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
        internal static DNS_rr_MX Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            /* RFC 1035	3.3.9. MX RDATA format

			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			|                  PREFERENCE                   |
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			/                   EXCHANGE                    /
			/                                               /
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

			where:

			PREFERENCE      
				A 16 bit integer which specifies the preference given to
				this RR among others at the same owner.  Lower values
                are preferred.

			EXCHANGE 
			    A <domain-name> which specifies a host willing to act as
                a mail exchange for the owner name. 
			*/

			int pref = reply[offset++] << 8 | reply[offset++];
		    
            string? host = DNS_Client.ReadQName(reply,ref offset);
            if(host != null){
                return new DNS_rr_MX(name,pref,host,ttl);
            }
            else{
                throw new ParseException("Invalid MX resource record data.");
            }
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the MX resource record.
        /// The format includes the owner name, record type, preference value,
        /// mail exchange host, and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; MX &lt;preference&gt; &lt;host&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} MX {m_Preference} {m_Host} (TTL={TTL})";
		}

        #endregion

        #region IComparable Implementation

        /// <summary>
        /// Compares the current instance with another object of the same type. 
        /// </summary>
        /// <param name="obj">An object to compare with this instance. </param>
        /// <returns>Returns 0 if two objects are equal, returns negative value if this object is less,
        /// returns positive value if this object is grater.</returns>
        public int CompareTo(object? obj)
        {
            if(obj == null){
                throw new ArgumentNullException("obj");
            }
            if(!(obj is DNS_rr_MX)){
                throw new ArgumentException("Argument obj is not MX_Record !");
            }

            DNS_rr_MX mx = (DNS_rr_MX)obj;
            if(this.Preference > mx.Preference){
                return 1;
            }
            else if(this.Preference < mx.Preference){
                return -1;
            }
            else{
                return 0;
            }
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets the preference value of this MX resource record. Lower values indicate
        /// higher priority when selecting a mail server for the domain.
        /// </summary>
		public int Preference
		{
			get{ return m_Preference; }
		}

		/// <summary>
        /// Gets the mail exchange host specified by this MX resource record.
        /// </summary>
		public string Host
		{
			get{ return m_Host; }
		}

		#endregion

	}
}
