using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents the <c>TCP-info</c> value of a <c>Received</c> header
    /// field as defined in RFC 5321 section 4.4.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TCP-info        = address-literal /
    ///                   ( Domain FWS address-literal )
    ///
    /// address-literal = "[" ( IPv4-address-literal /
    ///                         IPv6-address-literal /
    ///                         General-address-literal ) "]"
    /// </code>
    /// </remarks>
    public class Mail_t_TcpInfo
    {
        private IPAddress m_pIP;
        private string?   m_HostName = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_TcpInfo"/>
        /// class.
        /// </summary>
        /// <param name="ip">
        /// IP address.
        /// </param>
        /// <param name="hostName">
        /// Host name, or <see langword="null"/> if not specified.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="ip"/> is
        /// <see langword="null"/>.
        /// </exception>
        public Mail_t_TcpInfo(IPAddress ip,string? hostName)
        {
            if(ip == null){
                throw new ArgumentNullException(nameof(ip));
            }

            m_pIP      = ip;
            m_HostName = hostName;
        }


        #region method ToString

        /// <summary>
        /// Returns the TCP-info value as a string.
        /// </summary>
        /// <returns>
        /// Returns the TCP-info value as a string.
        /// </returns>
        public override string ToString()
        {
            if(string.IsNullOrEmpty(m_HostName)){
                return "["  + m_pIP.ToString() + "]";
            }
            else{
                return m_HostName + " [" + m_pIP.ToString() + "]";
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IP address.
        /// </summary>
        public IPAddress IP
        {
            get{ return m_pIP; }
        }

        /// <summary>
        /// Gets the host name.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null"/> if a host name was not specified.
        /// </remarks>
        public string? HostName
        {
            get{ return m_HostName; }
        }

        #endregion
    }
}
