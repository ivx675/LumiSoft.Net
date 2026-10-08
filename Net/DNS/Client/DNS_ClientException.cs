using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.DNS;

namespace LumiSoft.Net.DNS.Client
{
    /// <summary>
    /// DNS client exception.
    /// </summary>
    public class DNS_ClientException : Exception
    {
        private DNS_ResponseCode m_RCode;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="rcode">DNS server returned error code.</param>
        public DNS_ClientException(DNS_ResponseCode rcode) : base("Dns error: " + rcode + ".")
        {
            m_RCode = rcode;
        }


        #region Properties implementation

        /// <summary>
        /// Gets DNS server returned error code.
        /// </summary>
        public DNS_ResponseCode ErrorCode
        {
            get{ return m_RCode; }
        }

        #endregion

    }
}
