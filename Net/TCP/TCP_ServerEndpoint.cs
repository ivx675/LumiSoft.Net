using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace LumiSoft.Net.TCP 
{
    /// <summary>
    /// Represents a TCP server listening endpoint, including the advertised hostname,
    /// bind address, port, TLS mode, and optional TLS certificate. This class defines
    /// how a TCP server accepts incoming connections and how TLS is applied to them.
    /// </summary>
    /// <remarks>
    /// A <see cref="TCP_ServerEndpoint"/> describes the server-side network endpoint
    /// configuration. The <see cref="HostName"/> property specifies the hostname
    /// advertised to clients in protocol greetings, while <see cref="IPEndPoint"/>
    /// defines the actual IP address and port the server binds to. TLS behavior is
    /// controlled by <see cref="TCP_ServerTlsMode"/>, and a certificate is required
    /// when TLS is enabled.
    /// </remarks>
    public class TCP_ServerEndpoint 
    {
        private string            m_HostName     = "";  
        private IPEndPoint        m_pEndPoint;
        private TCP_ServerTlsMode m_TlsMode      = TCP_ServerTlsMode.None;
        private X509Certificate2? m_pCertificate = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="TCP_ServerEndpoint"/> class.
        /// Defines a TCP listening endpoint including advertised hostname, bind address,
        /// port, TLS mode, and optional TLS certificate.
        /// </summary>
        /// <param name="hostName">
        /// The hostname the server advertises to connecting clients. This value is used
        /// in protocol greetings (such as SMTP, POP3, IMAP, FTP) and does not need to
        /// match the IP address the server binds to.
        /// </param>
        /// <param name="ip">
        /// The IP address the server binds to for incoming TCP connections.
        /// </param>
        /// <param name="port">
        /// The TCP port the server listens on. Valid values are 0–65535. Port 0 allows
        /// the operating system to select an available ephemeral port.
        /// </param>
        /// <param name="tlsMode">
        /// Specifies how TLS is applied to incoming connections. TLS modes that require
        /// encryption (Implicit or Explicit) also require a valid certificate.
        /// </param>
        /// <param name="certificate">
        /// The X.509 certificate used for TLS. This value must be provided when
        /// <paramref name="tlsMode"/> is <see cref="TCP_ServerTlsMode.Implicit"/> or
        /// <see cref="TCP_ServerTlsMode.Explicit"/>. It may be null when TLS is disabled.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="hostName"/> is empty or when TLS is requested but
        /// no certificate is provided.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="ip"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="port"/> is outside the valid range of 0–65535.
        /// </exception>
        public TCP_ServerEndpoint(string hostName,IPAddress ip,int port,TCP_ServerTlsMode tlsMode,X509Certificate2? certificate)
        {
            if(string.IsNullOrEmpty(hostName)) {
                throw new ArgumentException("Argument 'hostName' value must be specified.",nameof(hostName));
            }
            if(ip == null) {
                throw new ArgumentNullException(nameof(ip));
            }
            if(port < 0 || port > 65535){
                throw new ArgumentOutOfRangeException(nameof(port),"Port value must be between 0 and 65535.");
            }
            if((tlsMode == TCP_ServerTlsMode.Implicit || tlsMode == TCP_ServerTlsMode.Explicit) && certificate == null){
                throw new ArgumentException("TLS requested, but argument 'certificate' is not provided.",nameof(certificate));
            }

            m_HostName     = hostName;
            m_pEndPoint    = new IPEndPoint(ip,port);
            m_TlsMode      = tlsMode;
            m_pCertificate = certificate;            
        }


        #region Properties Implementation

        /// <summary>
        /// Gets the hostname that the server advertises to connecting clients.
        /// This value is used in protocol greetings (e.g. SMTP EHLO/HELO, POP3, IMAP)
        /// and does not need to match the IP address or DNS name the server binds to.
        /// </summary>
        public string HostName
        {
            get{ return m_HostName; }
        }

        /// <summary>
        /// Gets the full TCP listening endpoint (IP address and port) the server binds to.
        /// This represents the actual network endpoint used for accepting incoming
        /// connections and is independent of the advertised hostname sent in protocol
        /// greetings.
        /// </summary>
        public IPEndPoint IPEndPoint
        {
            get{ return m_pEndPoint; }
        }

        /// <summary>
        /// Gets the IP address the server binds to for incoming TCP connections.
        /// This is part of the listening endpoint and may differ from the advertised
        /// hostname used in protocol greetings.
        /// </summary>
        public IPAddress IP
        {
            get{ return m_pEndPoint.Address; }
        }

        /// <summary>
        /// Gets the TCP port the server binds to for incoming connections.
        /// This value is part of the listening endpoint and may differ from
        /// protocol-specific default ports (e.g. 25/587 for SMTP, 110 for POP3).
        /// </summary>
        public int Port
        {
            get{ return m_pEndPoint.Port; }
        }

        /// <summary>
        /// Gets the TLS mode the server uses for this endpoint. This specifies whether
        /// the connection is unencrypted, uses implicit TLS from the first byte, or
        /// starts unencrypted and is upgraded to TLS using a protocol-specific command.
        /// </summary>
        public TCP_ServerTlsMode TlsMode
        {
            get{ return m_TlsMode; }
        }

        /// <summary>
        /// Gets the X.509 certificate used for TLS on this server endpoint. The value
        /// is required when the TLS mode is Implicit or Explicit, and is null when
        /// TLS is not enabled for the endpoint.
        /// </summary>
        public X509Certificate2? Certificate
        {
            get{ return m_pCertificate; }
        }

        #endregion
    }
}
