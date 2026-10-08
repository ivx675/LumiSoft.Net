using System;

using LumiSoft.Net.DNS.Client;

namespace LumiSoft.Net.DNS
{
	/// <summary>
    /// DNS TXT (Text) resource record. Stores arbitrary human‑readable or
    /// machine‑interpretable text associated with a domain name, commonly used
    /// for SPF, DKIM, DMARC, and other metadata.
    /// </summary>
	[Serializable]
	public class DNS_rr_TXT : DNS_rr
	{
		private string m_Text = "";

		/// <summary>
        /// Initializes a new instance of the TXT resource record, storing arbitrary
        /// text associated with the specified domain name, as defined in RFC 1035.
        /// </summary>
        /// <param name="name">DNS domain name that owns this resource record.</param>
        /// <param name="text">
        /// The text string contained in the TXT record. May include human‑readable
        /// information or machine‑interpretable metadata such as SPF, DKIM, or DMARC.
        /// </param>
        /// <param name="ttl">Time to live value in seconds.</param>
		public DNS_rr_TXT(string name,string text,int ttl) : base(name,DNS_RecordType.TXT,ttl)
		{
			m_Text = text;
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
        internal static DNS_rr_TXT Parse(string name,byte[] reply,ref int offset,int rdLength,int ttl)
        {
            // TXT RR

            string text = DNS_Client.ReadCharacterString(reply,ref offset);

			return new DNS_rr_TXT(name,text,ttl);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a textual representation of the TXT resource record.
        /// The format includes the owner name, record type, text value,
        /// and the TTL value.
        /// </summary>
        /// <returns>
        /// A string in the form: "&lt;name&gt; TXT &lt;text&gt; (TTL=&lt;ttl&gt;)".
        /// </returns>
        public override string ToString()
		{
			return $"{Name} TXT {m_Text} (TTL={TTL})";
		}

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets the text string contained in this TXT resource record. TXT records
        /// store arbitrary human‑readable or machine‑interpretable data, commonly used
        /// for SPF, DKIM, DMARC, and other domain metadata.
        /// </summary>
		public string Text
		{
			get{ return m_Text; }
		}

		#endregion
	}
}
