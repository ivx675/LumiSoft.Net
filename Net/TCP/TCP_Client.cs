using LumiSoft.Net.IO;
using LumiSoft.Net.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace LumiSoft.Net.TCP
{    
    /// <summary>
    /// This class implements generic TCP client.
    /// </summary>
    public class TCP_Client : TCP_Session
    {
        private bool                                 m_IsDisposed           = false;
        private bool                                 m_IsConnected          = false;
        private string                               m_ID                   = "";
        private DateTime                             m_ConnectTime;
        private IPEndPoint?                          m_pLocalEP             = null;
        private IPEndPoint?                          m_pRemoteEP            = null;
        private bool                                 m_IsSecure             = false;
        private SmartStream?                         m_pTcpStream           = null;
        private Logger?                              m_pLogger              = null;
        private RemoteCertificateValidationCallback? m_pCertificateCallback = null;
        private int                                  m_Timeout              = 61000;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TCP_Client()
        {
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used. This method is thread-safe.
        /// </summary>
        public override void Dispose()
        {
            lock(this){
                if(m_IsDisposed){
                    return;
                }
                try{
                    Disconnect();
                }
                catch{
                }
                m_IsDisposed = true;
            }
        }

        #endregion

                
        #region method Connect

        /// <summary>
        /// Synchronously establishes a TCP connection to the specified host.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind before connecting. When provided, its
        /// <see cref="AddressFamily"/> overrides the <paramref name="addressFamily"/>
        /// parameter.
        /// </param>
        /// <param name="host">The remote host name to resolve and connect to.</param>
        /// <param name="port">The remote TCP port.</param>
        /// <param name="addressFamily">
        /// Desired address family when <paramref name="localEP"/> is not specified.
        /// Use <see cref="AddressFamily.Unspecified"/> to allow both IPv4 and IPv6.
        /// </param>
        /// <param name="ssl">Indicates whether SSL/TLS should be negotiated.</param>
        /// <param name="sslOptions">Optional SSL/TLS configuration settings.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="host"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when DNS resolution succeeds but no usable addresses remain after
        /// filtering.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous convenience wrapper around the asynchronous
        /// <c>ConnectAsync</c> implementation. It resolves the host name, applies
        /// address‑family filtering rules, and then blocks the calling thread until the
        /// connection attempt completes.
        /// </para>
        /// <para>
        /// Because this method blocks, it should not be used on thread‑sensitive
        /// contexts such as UI threads or high‑throughput server loops. Prefer the
        /// asynchronous overload whenever possible.
        /// </para>
        /// </remarks>
        public void Connect(IPEndPoint? localEP,string host,int port,AddressFamily addressFamily,bool ssl,SslClientAuthenticationOptions? sslOptions)
        {
            using var cts = new CancellationTokenSource(m_Timeout);

            ConnectAsync(localEP,host,port,addressFamily,ssl,sslOptions,cts.Token).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Establishes a TCP connection to the specified remote endpoints and optionally
        /// upgrades the connection to SSL.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the socket to. If specified, all remote addresses
        /// must match the local endpoint's <see cref="AddressFamily"/>.
        /// </param>
        /// <param name="addresses">
        /// An array of remote IP addresses to attempt connecting to. The method tries each
        /// address in order until a connection succeeds. Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="port">
        /// The remote port number. Must be in the range 1–65535.
        /// </param>
        /// <param name="ssl">
        /// If <c>true</c>, the established TCP connection is upgraded to SSL using the
        /// provided <paramref name="sslOptions"/>.
        /// </param>
        /// <param name="sslOptions">
        /// SSL client authentication options used when <paramref name="ssl"/> is <c>true</c>.
        /// Ignored when <paramref name="ssl"/> is <c>false</c>.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is already connected, or if the local endpoint's address
        /// family does not match the remote addresses.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="addresses"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="port"/> is outside the valid range 1–65535.
        /// </exception>
        /// <exception cref="SocketException">
        /// Thrown if the TCP connection attempt fails for all provided addresses.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the SSL upgrade fails when <paramref name="ssl"/> is <c>true</c>.
        /// </exception>
        public void Connect(IPEndPoint? localEP,IPAddress[] addresses,int port,bool ssl,SslClientAuthenticationOptions? sslOptions)
        {
            using var cts = new CancellationTokenSource(m_Timeout);

            ConnectAsync(localEP,addresses,port,ssl,sslOptions,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ConnectAsync

        /// <summary>
        /// Resolves the specified host name to a set of IP addresses, optionally filters
        /// them based on the provided local endpoint or desired address family, and then
        /// initiates an asynchronous TCP connection attempt using the resulting address list.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When a <paramref name="localEP"/> is supplied, its <see cref="AddressFamily"/>
        /// takes precedence and all DNS results are filtered to match it. This ensures that
        /// the connection uses the same protocol family as the bound local endpoint.
        /// </para>
        /// <para>
        /// If <paramref name="localEP"/> is <c>null</c>, the method applies the
        /// <paramref name="addressFamily"/> filter only when it is explicitly set to
        /// <see cref="AddressFamily.InterNetwork"/> (IPv4) or
        /// <see cref="AddressFamily.InterNetworkV6"/> (IPv6). When
        /// <see cref="AddressFamily.Unspecified"/> is provided, no filtering is performed
        /// and all DNS results are used.
        /// </para>
        /// <para>
        /// After filtering, the method fails fast if no usable addresses remain, preventing
        /// ambiguous or delayed fallback behavior.
        /// </para>
        /// </remarks>
        /// <param name="localEP">
        /// Optional local endpoint to bind before connecting. If specified, its address
        /// family determines which DNS results are considered valid.
        /// </param>
        /// <param name="host">The remote host name to resolve.</param>
        /// <param name="port">The remote TCP port to connect to.</param>
        /// <param name="addressFamily">
        /// The desired address family when <paramref name="localEP"/> is not provided.
        /// Use <see cref="AddressFamily.Unspecified"/> to allow both IPv4 and IPv6.
        /// </param>
        /// <param name="ssl">Indicates whether SSL/TLS should be negotiated after connecting.</param>
        /// <param name="sslOptions">Optional SSL/TLS configuration settings.</param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous connection operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="host"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when DNS resolution succeeds but no addresses remain after filtering.
        /// </exception>
        public ValueTask ConnectAsync(
            IPEndPoint? localEP,
            string host,
            int port,
            AddressFamily addressFamily,
            bool ssl,SslClientAuthenticationOptions? sslOptions,
            CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(this.IsConnected){
                throw new InvalidOperationException("The client is already connected.");
            }
            ArgumentNullException.ThrowIfNull(host);

            IPAddress[] addresses = System.Net.Dns.GetHostAddresses(host);
            // If a local endpoint is provided, its address family overrides everything.
            if(localEP != null){
                addresses = addresses.Where(ip => ip.AddressFamily == localEP.AddressFamily).ToArray();
            }
            // Otherwise apply addressFamily only when explicitly IPv4 or IPv6.
            else if(addressFamily == AddressFamily.InterNetwork){
                addresses = addresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToArray();
            }
            else if(addressFamily == AddressFamily.InterNetworkV6){
                addresses = addresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
            }

            LogAddText($"DNS lookup for host '{host}' returned: " + string.Join(", ", addresses));

            // Fail fast if nothing remains.
            if(addresses.Length == 0){
                throw new ArgumentException($"No {addressFamily} addresses found for host '{host}'.",nameof(host));
            }

            return ConnectAsync(localEP,addresses,port,ssl,sslOptions,cancellationToken);            
        }

        /// <summary>
        /// Establishes a TCP connection to the specified remote endpoints and optionally
        /// upgrades the connection to SSL. This method performs address‑family validation,
        /// socket binding, multi‑address fallback connection, and secure‑stream initialization.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the socket to. If specified, all remote addresses
        /// must match the local endpoint's <see cref="AddressFamily"/>.
        /// </param>
        /// <param name="addresses">
        /// An array of remote IP addresses to attempt connecting to. The method tries each
        /// address in order until a connection succeeds. Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="port">
        /// The remote port number. Must be in the range 1–65535.
        /// </param>
        /// <param name="ssl">
        /// If <c>true</c>, the established TCP connection is upgraded to SSL using the
        /// provided <paramref name="sslOptions"/>.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration.  
        /// If <c>null</c>, the method applies a permissive legacy‑compatible configuration:
        /// <list type="bullet">
        /// <item><description>TLS 1.2 only</description></item>
        /// <item><description>Certificate revocation checking disabled</description></item>
        /// <item><description>Renegotiation allowed</description></item>
        /// <item><description>ALPN disabled</description></item>
        /// <item><description>No client certificates</description></item>
        /// <item><description>Certificate validation callback that accepts all certificates</description></item>
        /// </list>
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is already connected, or if the local endpoint's address
        /// family does not match the remote addresses.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="addresses"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="addresses"/> is empty and therefore provides no valid
        /// remote endpoints to attempt connecting to.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="port"/> is outside the valid range 1–65535.
        /// </exception>
        /// <exception cref="SocketException">
        /// Thrown if the TCP connection attempt fails for all provided addresses.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the SSL upgrade fails when <paramref name="ssl"/> is <c>true</c>.
        /// </exception>
        public async ValueTask ConnectAsync(
            IPEndPoint? localEP,
            IPAddress[] addresses,
            int port,
            bool ssl,
            SslClientAuthenticationOptions? sslOptions,
            CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(this.IsConnected){
                throw new InvalidOperationException("The client is already connected.");
            }
            if(m_pLocalEP != null){
                AddressFamily localFamily = m_pLocalEP.AddressFamily;
                foreach(var ip in addresses){
                    if(ip.AddressFamily != localFamily){
                        throw new InvalidOperationException(
                            $"Local endpoint is {localFamily}, but remote address {ip} is {ip.AddressFamily}. " +
                            "Address families do not match."
                        );
                    }
                }
            }
            ArgumentNullException.ThrowIfNull(addresses);
            if(addresses.Length == 0){
                throw new ArgumentException("The addresses array must contain at least one IP address.",nameof(addresses));
            }
            if(port <= 0 || port > 65535){
                throw new ArgumentOutOfRangeException(nameof(port),"Port must be between 1 and 65535.");
            }

            Socket? socket = null;
            try{
                socket = new Socket(AddressFamily.InterNetworkV6,SocketType.Stream,ProtocolType.Tcp){ 
                    DualMode = true,
                    ReceiveTimeout = m_Timeout,
                    SendTimeout = m_Timeout
                };

                // Bind socket to the specified end point.
                if(m_pLocalEP != null){
                    socket.Bind(m_pLocalEP);
                }

                LogAddText("Connecting " + string.Join(" -> ",addresses.Select(a => a.ToString() + ":" + port)) + ".");

                await socket.ConnectAsync(addresses,port,cancellationToken);
                m_IsConnected = true;
                m_pLocalEP  = (IPEndPoint)socket.LocalEndPoint!;
                m_pRemoteEP = (IPEndPoint)socket.RemoteEndPoint!;

                if(ssl){
                    LogAddText("Switching to secure connection (SSL).");
                    await SwitchToSecureAsync(sslOptions,cancellationToken);
                    LogAddText("Secure connection established (SSL).");
                }
                else{
                    m_pTcpStream = new SmartStream(new NetworkStream(socket,true),true);
                }

                await OnConnectedAsync(cancellationToken);
            }
            catch{
                socket?.Dispose();
                throw;
            }
        }

        #endregion

        #region method ConnectSocks5

        /// <summary>
        /// Establishes a TCP connection through a SOCKS5 proxy, resolving the proxy server from a
        /// hostname and performing the operation synchronously. This overload performs DNS resolution
        /// on <paramref name="proxyHost"/> and filters the resulting IP addresses based on either the
        /// provided <paramref name="localEP"/> or the specified <paramref name="addressFamily"/>.
        /// Optional SOCKS5 username/password authentication is supported.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying socket to. If provided, its address family
        /// determines which resolved proxy addresses are eligible for connection.
        /// </param>
        /// <param name="proxyHost">
        /// The hostname of the SOCKS5 proxy server. DNS resolution is performed to obtain one or more
        /// IP addresses for connection.
        /// </param>
        /// <param name="proxyPort">
        /// The TCP port of the SOCKS5 proxy server. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// The preferred address family (IPv4 or IPv6) to use when <paramref name="localEP"/> is not
        /// specified. If <paramref name="localEP"/> is provided, this parameter is ignored.
        /// </param>
        /// <param name="username">
        /// Optional SOCKS5 authentication username. If both <paramref name="username"/> and
        /// <paramref name="password"/> are provided, SOCKS5 username/password authentication (RFC 1929)
        /// will be performed.
        /// </param>
        /// <param name="password">
        /// Optional SOCKS5 authentication password. Used together with <paramref name="username"/> to
        /// perform SOCKS5 username/password authentication when required by the proxy.
        /// </param>
        /// <param name="targetHost">
        /// The destination host name to connect to through the SOCKS5 tunnel. This value is sent to the
        /// proxy as part of the SOCKS5 CONNECT request.
        /// </param>
        /// <param name="targetPort">
        /// The destination TCP port to connect to through the tunnel. Must be between 1 and 65535.
        /// </param>
        /// <remarks>
        /// <para>
        /// DNS resolution is performed on <paramref name="proxyHost"/>. If <paramref name="localEP"/> is
        /// provided, only addresses matching its address family are used. Otherwise, the
        /// <paramref name="addressFamily"/> parameter determines whether IPv4 or IPv6 addresses are
        /// selected.
        /// </para>
        /// <para>
        /// If <paramref name="username"/> and <paramref name="password"/> are provided, SOCKS5
        /// username/password authentication (RFC 1929) is attempted. If the proxy requires
        /// authentication and the credentials are missing or invalid, the connection will fail.
        /// </para>
        /// <para>
        /// Once the SOCKS5 CONNECT command succeeds, a raw TCP tunnel is established to
        /// <paramref name="targetHost"/>:<paramref name="targetPort"/>. Protocols such as SMTP, IMAP,
        /// POP3, FTP, SSH, and TLS/STARTTLS can be used normally through the tunnel.
        /// </para>
        /// </remarks>
        public void ConnectSocks5(
            IPEndPoint? localEP,
            string proxyHost,
            int proxyPort,
            AddressFamily addressFamily,
            string? username,
            string? password,
            string targetHost,
            int targetPort)
        {
            using var cts = new CancellationTokenSource(m_Timeout);

            ConnectSocks5Async(null,proxyHost,proxyPort,addressFamily,username,password,targetHost,targetPort,cts.Token).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Establishes a SOCKS5 tunnel to the specified target host and port using the
        /// provided proxy server.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying socket to. If null, the system
        /// will select an appropriate local address automatically.
        /// </param>
        /// <param name="proxyAddresses">
        /// One or more IP addresses of the SOCKS5 proxy server. At least one address must
        /// be provided.
        /// </param>
        /// <param name="proxyPort">
        /// The TCP port of the SOCKS5 proxy server. Must be between 1 and 65535.
        /// </param>
        /// <param name="username">
        /// Optional username for SOCKS5 username/password authentication. If both
        /// <paramref name="username"/> and <paramref name="password"/> are null, the
        /// client will request "no authentication" from the proxy.
        /// </param>
        /// <param name="password">
        /// Optional password for SOCKS5 username/password authentication.
        /// </param>
        /// <param name="targetHost">
        /// The destination host name to connect to through the SOCKS5 tunnel. This value
        /// is sent to the proxy as part of the CONNECT request.
        /// </param>
        /// <param name="targetPort">
        /// The destination TCP port to connect to through the SOCKS5 tunnel. Must be
        /// between 1 and 65535.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="proxyAddresses"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="proxyAddresses"/> is empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="proxyPort"/> or <paramref name="targetPort"/> is
        /// outside the valid range of 1–65535.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is already connected.
        /// </exception>
        public void ConnectSocks5(
            IPEndPoint? localEP,
            IPAddress[] proxyAddresses,
            int proxyPort,
            string? username,
            string? password,
            string targetHost,
            int targetPort)
        {
            using var cts = new CancellationTokenSource(m_Timeout);

            ConnectSocks5Async(null,proxyAddresses,proxyPort,username,password,targetHost,targetPort,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ConnectSocks5Async

        /// <summary>
        /// Establishes a TCP connection through a SOCKS5 proxy, resolving the proxy server from a
        /// hostname and performing the operation synchronously. This overload performs DNS resolution
        /// on <paramref name="proxyHost"/> and filters the resulting IP addresses based on either the
        /// provided <paramref name="localEP"/> or the specified <paramref name="addressFamily"/>.
        /// Optional SOCKS5 username/password authentication is supported.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying socket to. If provided, its address family
        /// determines which resolved proxy addresses are eligible for connection.
        /// </param>
        /// <param name="proxyHost">
        /// The hostname of the SOCKS5 proxy server. DNS resolution is performed to obtain one or more
        /// IP addresses for connection.
        /// </param>
        /// <param name="proxyPort">
        /// The TCP port of the SOCKS5 proxy server. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// The preferred address family (IPv4 or IPv6) to use when <paramref name="localEP"/> is not
        /// specified. If <paramref name="localEP"/> is provided, this parameter is ignored.
        /// </param>
        /// <param name="username">
        /// Optional SOCKS5 authentication username. If both <paramref name="username"/> and
        /// <paramref name="password"/> are provided, SOCKS5 username/password authentication (RFC 1929)
        /// will be performed.
        /// </param>
        /// <param name="password">
        /// Optional SOCKS5 authentication password. Used together with <paramref name="username"/> to
        /// perform SOCKS5 username/password authentication when required by the proxy.
        /// </param>
        /// <param name="targetHost">
        /// The destination host name to connect to through the SOCKS5 tunnel. This value is sent to the
        /// proxy as part of the SOCKS5 CONNECT request.
        /// </param>
        /// <param name="targetPort">
        /// The destination TCP port to connect to through the tunnel. Must be between 1 and 65535.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <remarks>
        /// <para>
        /// DNS resolution is performed on <paramref name="proxyHost"/>. If <paramref name="localEP"/> is
        /// provided, only addresses matching its address family are used. Otherwise, the
        /// <paramref name="addressFamily"/> parameter determines whether IPv4 or IPv6 addresses are
        /// selected.
        /// </para>
        /// <para>
        /// If <paramref name="username"/> and <paramref name="password"/> are provided, SOCKS5
        /// username/password authentication (RFC 1929) is attempted. If the proxy requires
        /// authentication and the credentials are missing or invalid, the connection will fail.
        /// </para>
        /// <para>
        /// Once the SOCKS5 CONNECT command succeeds, a raw TCP tunnel is established to
        /// <paramref name="targetHost"/>:<paramref name="targetPort"/>. Protocols such as SMTP, IMAP,
        /// POP3, FTP, SSH, and TLS/STARTTLS can be used normally through the tunnel.
        /// </para>
        /// </remarks>
        public ValueTask ConnectSocks5Async(
            IPEndPoint? localEP,
            string proxyHost,
            int proxyPort,
            AddressFamily addressFamily,
            string? username,
            string? password,
            string targetHost,
            int targetPort,
            CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(this.IsConnected){
                throw new InvalidOperationException("The client is already connected.");
            }
            ArgumentNullException.ThrowIfNull(proxyHost);

            IPAddress[] addresses = System.Net.Dns.GetHostAddresses(proxyHost);
            // If a local endpoint is provided, its address family overrides everything.
            if(localEP != null){
                addresses = addresses.Where(ip => ip.AddressFamily == localEP.AddressFamily).ToArray();
            }
            // Otherwise apply addressFamily only when explicitly IPv4 or IPv6.
            else if(addressFamily == AddressFamily.InterNetwork){
                addresses = addresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToArray();
            }
            else if(addressFamily == AddressFamily.InterNetworkV6){
                addresses = addresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
            }

            LogAddText($"DNS lookup for host '{proxyHost}' returned: " + string.Join(", ", addresses));

            // Fail fast if nothing remains.
            if(addresses.Length == 0){
                throw new ArgumentException($"No {addressFamily} addresses found for host '{proxyHost}'.",nameof(proxyHost));
            }

            return ConnectSocks5Async(localEP,addresses,proxyPort,username,password,targetHost,targetPort,cancellationToken);
        }

        /// <summary>
        /// Establishes a TCP connection to a remote server through a SOCKS5 proxy,
        /// performing the full SOCKS5 handshake, optional username/password
        /// authentication, and the CONNECT command. The proxy resolves the target
        /// hostname and opens the outbound connection on behalf of the client.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method connects to the specified SOCKS5 proxy using the provided
        /// <paramref name="proxyAddresses"/> and <paramref name="proxyPort"/>.
        /// After establishing the TCP connection, it performs the SOCKS5 greeting,
        /// negotiates an authentication method, optionally executes RFC 1929
        /// username/password authentication, and finally issues the SOCKS5 CONNECT
        /// command for <paramref name="targetHost"/> and <paramref name="targetPort"/>.
        /// </para>
        /// <para>
        /// When the CONNECT request uses a domain name (ATYP = 0x03), the SOCKS5 proxy
        /// performs DNS resolution. The client does not resolve <paramref name="targetHost"/>
        /// locally. This allows the proxy to handle DNS lookups according to its own
        /// network environment, privacy policies, or routing rules.
        /// </para>
        /// <para>
        /// After the proxy successfully establishes the outbound connection and then invokes <see cref="OnConnectedAsync"/>
        /// to signal that the tunnel is fully established.
        /// </para>
        /// </remarks>
        /// <param name="localEP">
        /// Optional local endpoint to bind before connecting to the SOCKS5 proxy.
        /// If specified, its <see cref="AddressFamily"/> must match the proxy address family.
        /// </param>
        /// <param name="proxyAddresses">
        /// One or more IP addresses of the SOCKS5 proxy. The method attempts connection
        /// in the order provided.
        /// </param>
        /// <param name="proxyPort">The TCP port of the SOCKS5 proxy.</param>
        /// <param name="username">
        /// Optional username for SOCKS5 username/password authentication (RFC 1929).
        /// </param>
        /// <param name="password">
        /// Optional password for SOCKS5 username/password authentication (RFC 1929).
        /// </param>
        /// <param name="targetHost">
        /// The hostname of the final destination server. This value is sent to the proxy
        /// as a domain name, and the proxy performs DNS resolution.
        /// </param>
        /// <param name="targetPort">The TCP port of the final destination server.</param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous SOCKS5 connection and handshake process.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="proxyAddresses"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="proxyAddresses"/> contains no usable addresses.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="proxyPort"/> is outside the valid range (1–65535).
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the SOCKS5 proxy rejects authentication, fails the CONNECT command,
        /// or returns an invalid protocol response.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is already connected.
        /// </exception>
        public async ValueTask ConnectSocks5Async(
            IPEndPoint? localEP,
            IPAddress[] proxyAddresses,
            int proxyPort,
            string? username,
            string? password,
            string targetHost,
            int targetPort,
            CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(GetType().Name);
            }
            if(IsConnected){
                throw new InvalidOperationException("The client is already connected.");
            }
            ArgumentNullException.ThrowIfNull(proxyAddresses);
            if(proxyAddresses.Length == 0){
                throw new ArgumentException("Proxy address list must contain at least one IP address.", nameof(proxyAddresses));
            }
            if(proxyPort <= 0 || proxyPort > 65535){
                throw new ArgumentOutOfRangeException(nameof(proxyPort), "Port must be between 1 and 65535.");
            }

            Socket? socket = null;

            try{
                socket = new Socket(AddressFamily.InterNetworkV6,SocketType.Stream,ProtocolType.Tcp){
                    DualMode = true,
                    ReceiveTimeout = m_Timeout,
                    SendTimeout = m_Timeout
                };

                if(localEP != null){
                    socket.Bind(localEP);
                }

                LogAddText("Connecting to SOCKS5 proxy " + string.Join(" -> ", proxyAddresses.Select(a => a.ToString())) + $":{proxyPort}");

                await socket.ConnectAsync(proxyAddresses,proxyPort,cancellationToken);

                // Wrap socket
                m_pTcpStream = new SmartStream(new NetworkStream(socket,true),true);

                LogAddText("SOCKS5: sending greeting.");

                // Greeting
                if(username != null && password != null){
                    await m_pTcpStream.WriteAsync(new byte[] { 0x05, 0x02, 0x00, 0x02 },cancellationToken);
                }
                else{
                    await m_pTcpStream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 },cancellationToken);
                }

                await m_pTcpStream.FlushAsync(cancellationToken);

                LogAddText("SOCKS5: waiting for method selection reply.");

                byte[] methodReply = new byte[2];
                await m_pTcpStream.ReadExactlyAsync(methodReply,0,2,cancellationToken);

                if(methodReply[0] != 0x05){
                    throw new IOException("SOCKS5: invalid version in method reply.");
                }

                byte selectedMethod = methodReply[1];
                LogAddText($"SOCKS5: server selected method 0x{selectedMethod:X2}.");

                if(selectedMethod == 0xFF){
                    throw new IOException("SOCKS5: no acceptable authentication methods.");
                }

                // Username/password auth
                if(selectedMethod == 0x02){
                    if(username == null || password == null){
                        throw new IOException("SOCKS5: proxy requires username/password.");
                    }

                    LogAddText("SOCKS5: performing username/password authentication.");

                    byte[] uname = Encoding.UTF8.GetBytes(username);
                    byte[] pass = Encoding.UTF8.GetBytes(password);

                    byte[] authReq = new byte[3 + uname.Length + pass.Length];
                    authReq[0] = 0x01;
                    authReq[1] = (byte)uname.Length;
                    Buffer.BlockCopy(uname, 0, authReq, 2, uname.Length);
                    authReq[2 + uname.Length] = (byte)pass.Length;
                    Buffer.BlockCopy(pass, 0, authReq, 3 + uname.Length, pass.Length);

                    await m_pTcpStream.WriteAsync(authReq,cancellationToken);
                    await m_pTcpStream.FlushAsync(cancellationToken);

                    byte[] authReply = new byte[2];
                    await m_pTcpStream.ReadExactlyAsync(authReply, 0, 2,cancellationToken);

                    if(authReply[1] != 0x00){
                        throw new IOException("SOCKS5: authentication failed.");
                    }

                    LogAddText("SOCKS5: authentication successful.");
                }

                // CONNECT command
                LogAddText($"SOCKS5: sending CONNECT {targetHost}:{targetPort}.");

                byte[] hostBytes = Encoding.UTF8.GetBytes(targetHost);
                byte[] connectReq = new byte[7 + hostBytes.Length];

                connectReq[0] = 0x05;
                connectReq[1] = 0x01;
                connectReq[2] = 0x00;
                connectReq[3] = 0x03;
                connectReq[4] = (byte)hostBytes.Length;

                Buffer.BlockCopy(hostBytes, 0, connectReq, 5, hostBytes.Length);

                connectReq[5 + hostBytes.Length] = (byte)(targetPort >> 8);
                connectReq[6 + hostBytes.Length] = (byte)(targetPort & 0xFF);

                await m_pTcpStream.WriteAsync(connectReq,cancellationToken);
                await m_pTcpStream.FlushAsync(cancellationToken);

                LogAddText("SOCKS5: waiting for CONNECT reply.");

                byte[] replyHead = new byte[4];
                await m_pTcpStream.ReadExactlyAsync(replyHead, 0, 4,cancellationToken);

                byte atyp = replyHead[3];

                if(replyHead[1] != 0x00){
                    throw new IOException($"SOCKS5: CONNECT failed, code 0x{replyHead[1]:X2}.");
                }

                int addrLen;
                if(atyp == 0x01){        // IPv4                
                    addrLen = 4;
                }
                else if (atyp == 0x03){   // Domain
                    byte[] len = new byte[1];
                    await m_pTcpStream.ReadExactlyAsync(len, 0, 1,cancellationToken);
                    addrLen = len[0];
                }
                else if (atyp == 0x04){   // IPv6
                    addrLen = 16;
                }
                else{
                    throw new IOException("SOCKS5: invalid ATYP.");
                }

                // Read address
                byte[] addr = new byte[addrLen];
                await m_pTcpStream.ReadExactlyAsync(addr, 0, addrLen,cancellationToken);

                // Read port
                byte[] portBytes = new byte[2];
                await m_pTcpStream.ReadExactlyAsync(portBytes, 0, 2,cancellationToken);

                // Now we are truly connected
                m_IsConnected = true;
                m_pLocalEP = (IPEndPoint)socket.LocalEndPoint!;
                m_pRemoteEP = new IPEndPoint(IPAddress.None, targetPort); // SOCKS hides real remote EP

                await OnConnectedAsync(cancellationToken);
            }
            catch{
                socket?.Dispose();
                throw;
            }
        }

        #endregion

        #region method ConnectHttpConnect

        /// <summary>
        /// Establishes a TCP tunnel through an HTTP or HTTPS proxy using the HTTP CONNECT method,
        /// resolving the proxy server from a hostname and performing the operation synchronously.
        /// This overload performs DNS resolution on <paramref name="proxyHost"/> and filters the
        /// resulting IP addresses based on either the provided <paramref name="localEP"/> or the
        /// specified <paramref name="addressFamily"/>.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying socket to. If provided, its address
        /// family determines which resolved proxy addresses are eligible for connection.
        /// </param>
        /// <param name="proxyHost">
        /// The hostname of the proxy server. DNS resolution is performed to obtain one or more
        /// IP addresses for connection.
        /// </param>
        /// <param name="proxyPort">
        /// The TCP port of the proxy server. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// The preferred address family (IPv4 or IPv6) to use when <paramref name="localEP"/> is
        /// not specified. If <paramref name="localEP"/> is provided, this parameter is ignored.
        /// </param>
        /// <param name="userName">
        /// Optional proxy authentication username. If both <paramref name="userName"/> and
        /// <paramref name="password"/> are provided, a <c>Proxy-Authorization: Basic</c> header
        /// will be included in the CONNECT request.
        /// </param>
        /// <param name="password">
        /// Optional proxy authentication password. Used together with <paramref name="userName"/>
        /// to generate the <c>Proxy-Authorization</c> header for proxies requiring Basic authentication.
        /// </param>
        /// <param name="targetHost">
        /// The destination host name to connect to through the HTTP CONNECT tunnel. This value
        /// is sent to the proxy as part of the CONNECT request.
        /// </param>
        /// <param name="targetPort">
        /// The destination TCP port to connect to through the tunnel. Must be between 1 and 65535.
        /// </param>
        /// <param name="sslToProxy">
        /// If true, the connection to the proxy will be wrapped in TLS using <see cref="SslStream"/>
        /// before sending the CONNECT request. If false, the CONNECT request is sent over a plaintext
        /// TCP connection.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used when <paramref name="sslToProxy"/> is true.
        /// If null, reasonable defaults will be applied. The <see cref="SslClientAuthenticationOptions"/>
        /// must specify a valid <see cref="SslClientAuthenticationOptions.TargetHost"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// DNS resolution is performed on <paramref name="proxyHost"/>. If <paramref name="localEP"/>
        /// is provided, only addresses matching its address family are used. Otherwise, the
        /// <paramref name="addressFamily"/> parameter determines whether IPv4 or IPv6 addresses
        /// are selected.
        /// </para>
        /// <para>
        /// When <paramref name="sslToProxy"/> is true, the proxy connection is protected with TLS.
        /// The CONNECT request is sent inside the encrypted TLS session. After the proxy returns
        /// <c>200 Connection Established</c>, the proxy stops interpreting data and a raw TCP tunnel
        /// is created inside the TLS session.
        /// </para>
        /// <para>
        /// When <paramref name="sslToProxy"/> is false, the CONNECT request is sent over plaintext
        /// TCP. After the proxy returns <c>200 Connection Established</c>, the proxy stops interpreting
        /// data and a raw TCP tunnel is created over the plaintext connection.
        /// </para>
        /// <para>
        /// If <paramref name="userName"/> and <paramref name="password"/> are provided, the method
        /// includes a <c>Proxy-Authorization: Basic</c> header in the CONNECT request. This is the
        /// standard authentication mechanism for HTTP proxies requiring credentials.
        /// </para>
        /// <para>
        /// In all cases, once the tunnel is established, the underlying stream behaves exactly as if
        /// it were directly connected to <paramref name="targetHost"/>:<paramref name="targetPort"/>.
        /// Protocols such as SMTP, IMAP, POP3, and TLS/STARTTLS can be used normally through the tunnel.
        /// </para>
        /// </remarks>
        public void ConnectHttpConnect(
            IPEndPoint? localEP,
            string proxyHost,
            int proxyPort,
            AddressFamily addressFamily,
            string userName,
            string password,
            string targetHost,
            int targetPort,
            bool sslToProxy,
            SslClientAuthenticationOptions? sslOptions)
        {
            using var cts = new CancellationTokenSource(m_Timeout);

            ConnectHttpConnectAsync(localEP,proxyHost,proxyPort,addressFamily,userName,password,targetHost,targetPort,sslToProxy,sslOptions,cts.Token)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Establishes a TCP tunnel through an HTTP or HTTPS proxy using the HTTP CONNECT method,
        /// performing the operation synchronously. This allows creating end‑to‑end TCP connections
        /// (e.g., SMTP, IMAP, POP3, TLS) through a proxy server.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying socket to. If null, the system will
        /// select an appropriate local address automatically.
        /// </param>
        /// <param name="proxyAddresses">
        /// One or more IP addresses of the proxy server. At least one address must be provided.
        /// </param>
        /// <param name="proxyPort">
        /// The TCP port of the proxy server. Must be between 1 and 65535.
        /// </param>
        /// <param name="userName">
        /// Optional proxy authentication username. If both <paramref name="userName"/> and
        /// <paramref name="password"/> are provided, a <c>Proxy-Authorization: Basic</c> header
        /// will be included in the CONNECT request.
        /// </param>
        /// <param name="password">
        /// Optional proxy authentication password. Used together with <paramref name="userName"/>
        /// to generate the <c>Proxy-Authorization</c> header for proxies requiring Basic authentication.
        /// </param>
        /// <param name="targetHost">
        /// The destination host name to connect to through the HTTP CONNECT tunnel. This value
        /// is sent to the proxy as part of the CONNECT request.
        /// </param>
        /// <param name="targetPort">
        /// The destination TCP port to connect to through the tunnel. Must be between 1 and 65535.
        /// </param>
        /// <param name="sslToProxy">
        /// If true, the connection to the proxy will be wrapped in TLS using <see cref="SslStream"/>
        /// before sending the CONNECT request. If false, the CONNECT request is sent over a plaintext
        /// TCP connection.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used when <paramref name="sslToProxy"/> is true.
        /// If null, reasonable defaults will be applied. The <see cref="SslClientAuthenticationOptions"/>
        /// must specify a valid <see cref="SslClientAuthenticationOptions.TargetHost"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// When <paramref name="sslToProxy"/> is true, the proxy connection is protected with TLS.
        /// The CONNECT request is sent inside the encrypted TLS session. After the proxy returns
        /// <c>200 Connection Established</c>, the proxy stops interpreting data and a raw TCP tunnel
        /// is created inside the TLS session.
        /// </para>
        /// <para>
        /// When <paramref name="sslToProxy"/> is false, the CONNECT request is sent over plaintext
        /// TCP. After the proxy returns <c>200 Connection Established</c>, the proxy stops interpreting
        /// data and a raw TCP tunnel is created over the plaintext connection.
        /// </para>
        /// <para>
        /// If <paramref name="userName"/> and <paramref name="password"/> are provided, the method
        /// includes a <c>Proxy-Authorization: Basic</c> header in the CONNECT request. This is the
        /// standard authentication mechanism for HTTP proxies requiring credentials.
        /// </para>
        /// <para>
        /// In all cases, once the tunnel is established, the underlying stream behaves exactly as if
        /// it were directly connected to <paramref name="targetHost"/>:<paramref name="targetPort"/>.
        /// Protocols such as SMTP, IMAP, POP3, and TLS/STARTTLS can be used normally through the tunnel.
        /// </para>
        /// </remarks>
        public void ConnectHttpConnect(
            IPEndPoint? localEP,
            IPAddress[] proxyAddresses,
            int proxyPort,
            string userName,
            string password,
            string targetHost,
            int targetPort,
            bool sslToProxy,
            SslClientAuthenticationOptions? sslOptions)
        {
            ConnectHttpConnectAsync(localEP,proxyAddresses,proxyPort,userName,password,targetHost,targetPort,sslToProxy,sslOptions)
                .GetAwaiter().GetResult();
        }

        #endregion

        #region method ConnectHttpConnectAsync

        /// <summary>
        /// Establishes a TCP tunnel through an HTTP or HTTPS proxy using the HTTP CONNECT method,
        /// resolving the proxy server from a hostname. This overload performs DNS resolution on
        /// <paramref name="proxyHost"/> and filters the resulting IP addresses based on either
        /// the provided <paramref name="localEP"/> or the specified <paramref name="addressFamily"/>.
        /// Optional proxy authentication is supported.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying socket to. If provided, its address
        /// family determines which resolved proxy addresses are eligible for connection.
        /// </param>
        /// <param name="proxyHost">
        /// The hostname of the proxy server. DNS resolution is performed to obtain one or more
        /// IP addresses for connection.
        /// </param>
        /// <param name="proxyPort">
        /// The TCP port of the proxy server. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// The preferred address family (IPv4 or IPv6) to use when <paramref name="localEP"/> is
        /// not specified. If <paramref name="localEP"/> is provided, this parameter is ignored.
        /// </param>
        /// <param name="userName">
        /// Optional proxy authentication username. If both <paramref name="userName"/> and
        /// <paramref name="password"/> are provided, a <c>Proxy-Authorization: Basic</c> header
        /// will be included in the CONNECT request.
        /// </param>
        /// <param name="password">
        /// Optional proxy authentication password. Used together with <paramref name="userName"/>
        /// to generate the <c>Proxy-Authorization</c> header for proxies requiring Basic authentication.
        /// </param>
        /// <param name="targetHost">
        /// The destination host name to connect to through the HTTP CONNECT tunnel. This value
        /// is sent to the proxy as part of the CONNECT request.
        /// </param>
        /// <param name="targetPort">
        /// The destination TCP port to connect to through the tunnel. Must be between 1 and 65535.
        /// </param>
        /// <param name="sslToProxy">
        /// If true, the connection to the proxy will be wrapped in TLS using <see cref="SslStream"/>
        /// before sending the CONNECT request. If false, the CONNECT request is sent over a plaintext
        /// TCP connection.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used when <paramref name="sslToProxy"/> is true.
        /// If null, reasonable defaults will be applied. The <see cref="SslClientAuthenticationOptions"/>
        /// must specify a valid <see cref="SslClientAuthenticationOptions.TargetHost"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous CONNECT operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="proxyHost"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when DNS resolution returns no usable addresses for <paramref name="proxyHost"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="proxyPort"/> or <paramref name="targetPort"/> is outside
        /// the valid range of 1–65535.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is already connected.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the proxy rejects the CONNECT request, returns a non‑200 status code,
        /// or closes the connection unexpectedly during the handshake.
        /// </exception>
        /// <remarks>
        /// <para>
        /// DNS resolution is performed on <paramref name="proxyHost"/>. If <paramref name="localEP"/>
        /// is provided, only addresses matching its address family are used. Otherwise, the
        /// <paramref name="addressFamily"/> parameter determines whether IPv4 or IPv6 addresses
        /// are selected.
        /// </para>
        /// <para>
        /// When <paramref name="sslToProxy"/> is true, the proxy connection is protected with TLS.
        /// The CONNECT request is sent inside the encrypted TLS session. After the proxy returns
        /// <c>200 Connection Established</c>, the proxy stops interpreting data and a raw TCP tunnel
        /// is created inside the TLS session.
        /// </para>
        /// <para>
        /// When <paramref name="sslToProxy"/> is false, the CONNECT request is sent over plaintext
        /// TCP. After the proxy returns <c>200 Connection Established</c>, the proxy stops interpreting
        /// data and a raw TCP tunnel is created over the plaintext connection.
        /// </para>
        /// <para>
        /// If <paramref name="userName"/> and <paramref name="password"/> are provided, the method
        /// includes a <c>Proxy-Authorization: Basic</c> header in the CONNECT request. This is the
        /// standard authentication mechanism for HTTP proxies requiring credentials.
        /// </para>
        /// <para>
        /// In all cases, once the tunnel is established, the underlying stream behaves exactly as if
        /// it were directly connected to <paramref name="targetHost"/>:<paramref name="targetPort"/>.
        /// Protocols such as SMTP, IMAP, POP3, and TLS/STARTTLS can be used normally through the tunnel.
        /// </para>
        /// </remarks>
        public ValueTask ConnectHttpConnectAsync(
            IPEndPoint? localEP,
            string proxyHost,
            int proxyPort,
            AddressFamily addressFamily,
            string userName,
            string password,
            string targetHost,
            int targetPort,
            bool sslToProxy,
            SslClientAuthenticationOptions? sslOptions,
            CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(this.IsConnected){
                throw new InvalidOperationException("The client is already connected.");
            }
            ArgumentNullException.ThrowIfNull(proxyHost);

            IPAddress[] addresses = System.Net.Dns.GetHostAddresses(proxyHost);
            // If a local endpoint is provided, its address family overrides everything.
            if(localEP != null){
                addresses = addresses.Where(ip => ip.AddressFamily == localEP.AddressFamily).ToArray();
            }
            // Otherwise apply addressFamily only when explicitly IPv4 or IPv6.
            else if(addressFamily == AddressFamily.InterNetwork){
                addresses = addresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToArray();
            }
            else if(addressFamily == AddressFamily.InterNetworkV6){
                addresses = addresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
            }

            LogAddText($"DNS lookup for host '{proxyHost}' returned: " + string.Join(", ", addresses));

            // Fail fast if nothing remains.
            if(addresses.Length == 0){
                throw new ArgumentException($"No {addressFamily} addresses found for host '{proxyHost}'.",nameof(proxyHost));
            }

            return ConnectHttpConnectAsync(localEP,addresses,proxyPort,userName,password,targetHost,targetPort,sslToProxy,sslOptions,cancellationToken);
        }

        /// <summary>
        /// Establishes a TCP tunnel through an HTTP or HTTPS proxy using the HTTP CONNECT method.
        /// This allows creating end‑to‑end TCP connections (e.g., SMTP, IMAP, POP3, TLS) through
        /// a proxy server.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying socket to. If null, the system will
        /// select an appropriate local address automatically.
        /// </param>
        /// <param name="proxyAddresses">
        /// One or more IP addresses of the proxy server. At least one address must be provided.
        /// </param>
        /// <param name="proxyPort">
        /// The TCP port of the proxy server. Must be between 1 and 65535.
        /// </param>
        /// <param name="userName">
        /// Optional proxy authentication username. If both <paramref name="userName"/> and
        /// <paramref name="password"/> are provided, a <c>Proxy-Authorization: Basic</c> header
        /// will be included in the CONNECT request.
        /// </param>
        /// <param name="password">
        /// Optional proxy authentication password. Used together with <paramref name="userName"/>
        /// to generate the <c>Proxy-Authorization</c> header for proxies requiring Basic authentication.
        /// </param>
        /// <param name="targetHost">
        /// The destination host name to connect to through the HTTP CONNECT tunnel. This value
        /// is sent to the proxy as part of the CONNECT request.
        /// </param>
        /// <param name="targetPort">
        /// The destination TCP port to connect to through the tunnel. Must be between 1 and 65535.
        /// </param>
        /// <param name="sslToProxy">
        /// If true, the connection to the proxy will be wrapped in TLS using <see cref="SslStream"/>
        /// before sending the CONNECT request. If false, the CONNECT request is sent over a plaintext
        /// TCP connection.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used when <paramref name="sslToProxy"/> is true.
        /// If null, reasonable defaults will be applied. The <see cref="SslClientAuthenticationOptions"/>
        /// must specify a valid <see cref="SslClientAuthenticationOptions.TargetHost"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="proxyAddresses"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="proxyAddresses"/> is empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="proxyPort"/> or <paramref name="targetPort"/> is outside
        /// the valid range of 1–65535.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is already connected.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the proxy rejects the CONNECT request, returns a non‑200 status code,
        /// or closes the connection unexpectedly during the handshake.
        /// </exception>
        /// <remarks>
        /// <para>
        /// When <paramref name="sslToProxy"/> is true, the proxy connection is protected with TLS.
        /// The CONNECT request is sent inside the encrypted TLS session. After the proxy returns
        /// <c>200 Connection Established</c>, the proxy stops interpreting data and a raw TCP tunnel
        /// is created inside the TLS session.
        /// </para>
        /// <para>
        /// When <paramref name="sslToProxy"/> is false, the CONNECT request is sent over plaintext
        /// TCP. After the proxy returns <c>200 Connection Established</c>, the proxy stops interpreting
        /// data and a raw TCP tunnel is created over the plaintext connection.
        /// </para>
        /// <para>
        /// If <paramref name="userName"/> and <paramref name="password"/> are provided, the method
        /// includes a <c>Proxy-Authorization: Basic</c> header in the CONNECT request. This is the
        /// standard authentication mechanism for HTTP proxies requiring credentials.
        /// </para>
        /// <para>
        /// In all cases, once the tunnel is established, the underlying stream behaves exactly as if
        /// it were directly connected to <paramref name="targetHost"/>:<paramref name="targetPort"/>.
        /// Protocols such as SMTP, IMAP, POP3, and TLS/STARTTLS can be used normally through the tunnel.
        /// </para>
        /// </remarks>
        public async ValueTask ConnectHttpConnectAsync(
            IPEndPoint? localEP,
            IPAddress[] proxyAddresses,
            int proxyPort,
            string userName,
            string password,
            string targetHost,
            int targetPort,
            bool sslToProxy,
            SslClientAuthenticationOptions? sslOptions,
            CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(GetType().Name);
            }
            if(IsConnected){
                throw new InvalidOperationException("The client is already connected.");
            }
            ArgumentNullException.ThrowIfNull(proxyAddresses);
            if(proxyAddresses.Length == 0){
                throw new ArgumentException("Proxy address list must contain at least one IP address.", nameof(proxyAddresses));
            }
            if(proxyPort <= 0 || proxyPort > 65535){
                throw new ArgumentOutOfRangeException(nameof(proxyPort), "Port must be between 1 and 65535.");
            }
            if(targetPort <= 0 || targetPort > 65535){
                throw new ArgumentOutOfRangeException(nameof(targetPort), "Port must be between 1 and 65535.");
            }

            Socket? socket = null;

            try{
                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp){
                    DualMode = true,
                    ReceiveTimeout = m_Timeout,
                    SendTimeout = m_Timeout
                };

                if(localEP != null){
                    socket.Bind(localEP);
                }

                await socket.ConnectAsync(proxyAddresses, proxyPort,cancellationToken);

                // Raw stream to proxy
                var rawStream = new NetworkStream(socket, ownsSocket: true);
                Stream proxyStream = rawStream;

                // Optional TLS to proxy
                if(sslToProxy){
                    var ssl = new SslStream(rawStream, leaveInnerStreamOpen: false);

                    // If user did not supply options, create safe defaults
                    sslOptions ??= new SslClientAuthenticationOptions{
                        TargetHost = proxyAddresses[0].ToString(),
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                                              System.Security.Authentication.SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                    };

                    // Ensure TargetHost is set
                    if(string.IsNullOrEmpty(sslOptions.TargetHost)){
                        sslOptions.TargetHost = proxyAddresses[0].ToString();
                    }

                    await ssl.AuthenticateAsClientAsync(sslOptions,cancellationToken);
                    proxyStream = ssl;
                }

                string connectRequest =
                $"CONNECT {targetHost}:{targetPort} HTTP/1.1\r\n" +
                $"Host: {targetHost}:{targetPort}\r\n" +
                (userName != null && password != null
                    ? $"Proxy-Authorization: Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{password}"))}\r\n"
                    : "") +
                "Proxy-Connection: Keep-Alive\r\n" +
                "\r\n";

                byte[] reqBytes = Encoding.ASCII.GetBytes(connectRequest);
                await proxyStream.WriteAsync(reqBytes, 0, reqBytes.Length,cancellationToken);
                await proxyStream.FlushAsync(cancellationToken);

                // Read HTTP response header
                using var reader = new StreamReader(proxyStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

                string? statusLine = await reader.ReadLineAsync(cancellationToken);
                if(statusLine == null || !statusLine.Contains("200")){
                    throw new IOException($"HTTP CONNECT failed: {statusLine}");
                }

                // Read remaining header lines until empty line
                string? line;
                while(!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken))){
                    // ignore header lines
                }

                // Tunnel established — wrap SmartStream for SMTP/etc
                m_pTcpStream = new SmartStream(proxyStream,true);

                m_IsConnected = true;
                m_pLocalEP = (IPEndPoint)socket.LocalEndPoint!;
                m_pRemoteEP = new IPEndPoint(IPAddress.None, targetPort);

                await OnConnectedAsync(cancellationToken);
            }
            catch{
                socket?.Dispose();
                throw;
            }
        }

        #endregion

        #region method Disconnect

        /// <summary>
        /// Disconnects connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is not connected.</exception>
        public override void Disconnect()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("TCP_Client");
            }
            if(!m_IsConnected){
                throw new InvalidOperationException("TCP client is not connected.");
            }
            m_IsConnected = false;

            m_pLocalEP = null;
            m_pRemoteEP = null;
            m_pTcpStream?.Dispose();
            m_IsSecure = false;
            m_pTcpStream = null;

            LogAddText("Disconnected.");
        }

        #endregion


        #region method SwitchToSecureAsync

        /// <summary>
        /// Upgrades the current TCP connection to a secure TLS connection using
        /// <see cref="SslStream"/> and the provided <see cref="SslClientAuthenticationOptions"/>.
        /// </summary>
        /// <param name="sslOptions">
        /// Optional TLS configuration.  
        /// If <c>null</c>, the method applies a permissive legacy‑compatible configuration:
        /// <list type="bullet">
        /// <item><description>TLS 1.2 only</description></item>
        /// <item><description>Certificate revocation checking disabled</description></item>
        /// <item><description>Renegotiation allowed</description></item>
        /// <item><description>ALPN disabled</description></item>
        /// <item><description>No client certificates</description></item>
        /// <item><description>Certificate validation callback that accepts all certificates</description></item>
        /// </list>
        /// These defaults are intended for maximum compatibility with older servers.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the underlying TCP client has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or if the connection is already secure.
        /// </exception>
        /// <exception cref="AuthenticationException">
        /// Thrown when the TLS handshake fails.
        /// </exception>
        /// <returns>
        /// A task representing the asynchronous TLS upgrade operation.
        /// </returns>
        protected async ValueTask SwitchToSecureAsync(SslClientAuthenticationOptions? sslOptions,CancellationToken cancellationToken = default)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("TCP_Client");
            }
            if(!m_IsConnected){
                throw new InvalidOperationException("TCP client is not connected.");
            }
            if(m_IsSecure){
                throw new InvalidOperationException("TCP client is already secure.");
            }
            ArgumentNullException.ThrowIfNull(m_pTcpStream);

            if(sslOptions == null){
                sslOptions = new SslClientAuthenticationOptions{
                    // MUST be set for SNI and certificate validation.
                    // If your client already knows the remote host name, use that.
                    TargetHost = "localhost",

                    // .NET 4.8 servers only reliably support TLS 1.2.
                    EnabledSslProtocols = SslProtocols.Tls12,

                    // Avoid OCSP stapling negotiation issues with older servers.
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,

                    // .NET 4.8 requires renegotiation to be allowed.
                    AllowRenegotiation = true,

                    // Empty list = ALPN extension disabled.
                    // .NET 4.8 rejects ALPN.
                    ApplicationProtocols = new List<SslApplicationProtocol>(),

                    // No client certificates by default.
                    ClientCertificates = null,

                    // No custom certificate validation unless caller provides one.                    
                    RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
                };
            }

            SslStream sslStream = new SslStream(m_pTcpStream.SourceStream,false,sslOptions.RemoteCertificateValidationCallback);
            await sslStream.AuthenticateAsClientAsync(sslOptions,cancellationToken).ConfigureAwait(false);

            // Close old stream, but leave source stream open.
            m_pTcpStream.IsOwner = false;
            m_pTcpStream.Dispose();

            m_IsSecure = true;
            m_pTcpStream = new SmartStream(sslStream,true);
        }

        #endregion

        
        #region virtual method OnConnectedAsync

        /// <summary>
        /// Called after the client has successfully established a TCP connection.
        /// Derived classes can override this method to perform protocol‑specific
        /// post‑connection actions, such as reading an initial server greeting,
        /// sending an initial command, or performing capability discovery.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <remarks>
        /// This method is invoked automatically by the base class once the underlying
        /// socket connection has been created and any optional SSL/TLS negotiation has
        /// completed. The default implementation performs no actions.
        /// </remarks>
        /// <returns>
        /// A task representing the asynchronous post‑connection operation.
        /// </returns>
        protected virtual async ValueTask OnConnectedAsync(CancellationToken cancellationToken = default)
        {
        }

        #endregion


        #region method ReadLine

        /// <summary>
        /// Reads and logs specified line from connected host.
        /// </summary>
        /// <returns>Returns readed line.</returns>
        protected string? ReadLine()
        {
            ArgumentNullException.ThrowIfNull(this.TcpStream);

            SmartStream.ReadLineAsyncOP args = new SmartStream.ReadLineAsyncOP(new byte[32000],SizeExceededAction.JunkAndThrowException);
            this.TcpStream.ReadLine(args,false);
            if(args.Error != null){
                throw args.Error;
            }
            string? line = args.LineUtf8;
            if(args.BytesInBuffer > 0){
                LogAddRead(args.BytesInBuffer,line!);
            }
            else{
                LogAddText("Remote host closed connection.");
            }

            return line;
        }

        #endregion

        #region method WriteLine

        /// <summary>
        /// Sends and logs specified line to connected host.
        /// </summary>
        /// <param name="line">Line to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>line</b> is null reference.</exception>
        protected void WriteLine(string line)
        {
            if(line == null){
                throw new ArgumentNullException("line");
            }
            ArgumentNullException.ThrowIfNull(this.TcpStream);

            int countWritten = this.TcpStream.WriteLine(line);
            LogAddWrite(countWritten,line);
        }

        #endregion


        #region mehtod LogAddRead

        /// <summary>
        /// Logs read operation.
        /// </summary>
        /// <param name="size">Number of bytes readed.</param>
        /// <param name="text">Log text.</param>
        internal protected void LogAddRead(long size,string text)
        {
            try{
                if(m_pLogger != null){
                    m_pLogger.AddRead(
                        this.ID,
                        this.AuthenticatedUserIdentity,
                        size,
                        text,                        
                        this.LocalEndPoint,
                        this.RemoteEndPoint
                    );
                }
            }
            catch{
                // We skip all logging errors, normally there shouldn't be any.
            }
        }

        #endregion

        #region method LogAddWrite

        /// <summary>
        /// Logs write operation.
        /// </summary>
        /// <param name="size">Number of bytes written.</param>
        /// <param name="text">Log text.</param>
        internal protected void LogAddWrite(long size,string text)
        {
            try{
                if(m_pLogger != null){
                    m_pLogger.AddWrite(
                        this.ID,
                        this.AuthenticatedUserIdentity,
                        size,
                        text,                        
                        this.LocalEndPoint,
                        this.RemoteEndPoint
                    );
                }
            }
            catch{
                // We skip all logging errors, normally there shouldn't be any.
            }
        }

        #endregion

        #region method LogAddText

        /// <summary>
        /// Logs free text entry.
        /// </summary>
        /// <param name="text">Log text.</param>
        internal protected void LogAddText(string text)
        {
            try{
                if(m_pLogger != null){
                    m_pLogger.AddText(
                        this.IsConnected ? this.ID : "",
                        this.IsConnected ? this.AuthenticatedUserIdentity : null,
                        text,                        
                        this.IsConnected ? this.LocalEndPoint : null,
                        this.IsConnected ? this.RemoteEndPoint : null
                    );
                }
            }
            catch{
                // We skip all logging errors, normally there shouldn't be any.
            }
        }

        #endregion

        #region method LogAddException

        /// <summary>
        /// Logs exception.
        /// </summary>
        /// <param name="text">Log text.</param>
        /// <param name="x">Exception happened.</param>
        internal protected void LogAddException(string text,Exception x)
        {
            try{
                if(m_pLogger != null){
                    m_pLogger.AddException(
                        this.IsConnected ? this.ID : "",
                        this.IsConnected ? this.AuthenticatedUserIdentity : null,
                        text,                        
                        this.IsConnected ? this.LocalEndPoint : null,
                        this.IsConnected ? this.RemoteEndPoint : null,
                        x
                    );
                }
            }
            catch{
                // We skip all logging errors, normally there shouldn't be any.
            }
        }

        #endregion
                

        #region Properties Implementation

        /// <summary>
        /// Gets a value indicating whether this object has been disposed.
        /// </summary>
        public bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }

        /// <summary>
        /// Gets or sets TCP client logger. Value null means no logging.
        /// </summary>
        public Logger? Logger
        {
            get{ return m_pLogger; }

            set{ m_pLogger = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the TCP client is connected.
        /// </summary>
        public override bool IsConnected
        {
            get{ return m_IsConnected; }
        }

        /// <summary>
        /// Gets the unique session identifier for this TCP client.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the object has been disposed and the property is accessed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the TCP client is not connected.
        /// </exception>
        public override string ID
        {
            get{                
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Client");
                }
                if(!m_IsConnected){
                    throw new InvalidOperationException("TCP client is not connected.");
                }

                return m_ID; 
            }
        }

        /// <summary>
        /// Gets the date and time at which the TCP client established the connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the object has been disposed and the property is accessed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the TCP client is not connected.
        /// </exception>
        public override DateTime ConnectTime
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Client");
                }
                if(!m_IsConnected){
                    throw new InvalidOperationException("TCP client is not connected.");
                }

                return m_ConnectTime; 
            }
        }

        /// <summary>
        /// Gets the date and time of the most recent send or receive activity on the TCP client.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the object has been disposed and the property is accessed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the TCP client is not connected.
        /// </exception>
        public override DateTime LastActivity
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Client");
                }
                if(!m_IsConnected){
                    throw new InvalidOperationException("TCP client is not connected.");
                }

                return m_pTcpStream?.LastActivity ?? DateTime.MinValue; 
            }
        }

        /// <summary>
        /// Gets the local IP endpoint associated with the current TCP client session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the object has been disposed and the property is accessed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the TCP client is not connected.
        /// </exception>
        public override IPEndPoint LocalEndPoint
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Client");
                }
                if(!m_IsConnected){
                    throw new InvalidOperationException("TCP client is not connected.");
                }

                return m_pLocalEP!; 
            }
        }

        /// <summary>
        /// Gets the remote IP endpoint associated with the current TCP client session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the object has been disposed and the property is accessed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the TCP client is not connected.
        /// </exception>
        public override IPEndPoint RemoteEndPoint
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Client");
                }
                if(!m_IsConnected){
                    throw new InvalidOperationException("TCP client is not connected.");
                }

                return m_pRemoteEP!; 
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether the current TCP client session is using a secure connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the object has been disposed and the property is accessed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the TCP client is not connected.
        /// </exception>
        public override bool IsSecureConnection
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Client");
                }
                if(!m_IsConnected){
                    throw new InvalidOperationException("TCP client is not connected.");
                }

                return m_IsSecure; 
            }
        }

        /// <summary>
        /// Gets the underlying TCP stream used to send and receive data for the current session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the object has been disposed and the property is accessed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the TCP client is not connected.
        /// </exception>
        public override SmartStream TcpStream
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Client");
                }
                if(!m_IsConnected){
                    throw new InvalidOperationException("TCP client is not connected.");
                }

                return m_pTcpStream!; 
            }
        }

        /// <summary>
        /// Gets or sets default TCP read/write timeout.
        /// </summary>
        /// <remarks>This timeout applies only synchronous TCP read/write operations.</remarks>
        public int Timeout
        {
            get{ return m_Timeout; }

            set{ m_Timeout = value; }
        }

        #endregion



        // ----- Obsolete -----------------------------------------------------

        #region method OnError

        /// <summary>
        /// This must be called when unexpected error happens. When inheriting <b>TCP_Client</b> class, be sure that you call <b>OnError</b>
        /// method for each unexpected error.
        /// </summary>
        /// <param name="x">Exception happened.</param>
        [Obsolete("Don't use this method.")]
        protected void OnError(Exception x)
        {
            try{
                if(m_pLogger != null){
                    //m_pLogger.AddException(x);
                }
            }
            catch{
            }
        }

        #endregion

        #region method SwitchToSecureAsync

        #region class SwitchToSecureAsyncOP

        /// <summary>
        /// This class represents  asynchronous operation.
        /// </summary>
        protected class SwitchToSecureAsyncOP : IDisposable,IAsyncOP
        {
            private object                               m_pLock         = new object();
            private bool                                 m_RiseCompleted = false;
            private AsyncOP_State                        m_State         = AsyncOP_State.WaitingForStart;
            private Exception?                           m_pException    = null;
            private RemoteCertificateValidationCallback? m_pCertCallback = null;
            private TCP_Client?                          m_pTcpClient    = null;
            private SslStream?                           m_pSslStream    = null;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="certCallback">SSL server certificate validation callback. Value null means any certificate is accepted.</param>
            public SwitchToSecureAsyncOP(RemoteCertificateValidationCallback? certCallback)
            {
                m_pCertCallback = certCallback;
            }

            #region method Dispose

            /// <summary>
            /// Cleans up any resource being used.
            /// </summary>
            public void Dispose()
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }
                SetState(AsyncOP_State.Disposed);
                
                m_pException    = null;
                m_pCertCallback = null;
                m_pSslStream    = null;

                this.CompletedAsync = null;
            }

            #endregion


            #region method Start

            /// <summary>
            /// Starts operation processing.
            /// </summary>
            /// <param name="owner">Owner TCP client.</param>
            /// <returns>Returns true if asynchronous operation in progress or false if operation completed synchronously.</returns>
            /// <exception cref="ArgumentNullException">Is raised when <b>owner</b> is null reference.</exception>
            internal bool Start(TCP_Client owner)
            {
                if(owner == null){
                    throw new ArgumentNullException("owner");
                }
                ArgumentNullException.ThrowIfNull(owner.m_pTcpStream);

                m_pTcpClient = owner;

                SetState(AsyncOP_State.Active);

                try{
                    m_pSslStream = new SslStream(m_pTcpClient.m_pTcpStream.SourceStream,false,this.RemoteCertificateValidationCallback);
                    m_pSslStream.BeginAuthenticateAsClient("dummy",this.BeginAuthenticateAsClientCompleted,null);                  
                }
                catch(Exception x){
                    m_pException = x;
                    SetState(AsyncOP_State.Completed);
                }

                // Set flag rise CompletedAsync event flag. The event is raised when async op completes.
                // If already completed sync, that flag has no effect.
                lock(m_pLock){
                    m_RiseCompleted = true;

                    return m_State == AsyncOP_State.Active;
                }
            }

            #endregion


            #region method SetState

            /// <summary>
            /// Sets operation state.
            /// </summary>
            /// <param name="state">New state.</param>
            private void SetState(AsyncOP_State state)
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }

                lock(m_pLock){
                    m_State = state;

                    if(m_State == AsyncOP_State.Completed && m_RiseCompleted){
                        OnCompletedAsync();
                    }
                }
            }

            #endregion

            #region method RemoteCertificateValidationCallback

            /// <summary>
            /// This method is called when we need to validate remote server certificate.
            /// </summary>
            /// <param name="sender">Sender.</param>
            /// <param name="certificate">Certificate.</param>
            /// <param name="chain">Certificate chain.</param>
            /// <param name="sslPolicyErrors">SSL policy errors.</param>
            /// <returns>Returns true if certificate validated, otherwise false.</returns>
            private bool RemoteCertificateValidationCallback(object sender,X509Certificate? certificate,X509Chain? chain,SslPolicyErrors sslPolicyErrors)
            {
                // User will handle it.
                if(m_pCertCallback != null){
                    return m_pCertCallback(sender,certificate,chain,sslPolicyErrors);
                }
                else{
                    if(sslPolicyErrors == SslPolicyErrors.None || ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) > 0)){
                        return true;
                    }

                    // Do not allow this client to communicate with unauthenticated servers.
                    return false;
                }
            }

            #endregion

            #region method BeginAuthenticateAsClientCompleted

            /// <summary>
            /// This method is called when "BeginAuthenticateAsClient" has completed.
            /// </summary>
            /// <param name="ar">Asynchronous result.</param>
            private void BeginAuthenticateAsClientCompleted(IAsyncResult ar)
            {
                ArgumentNullException.ThrowIfNull(m_pSslStream);
                ArgumentNullException.ThrowIfNull(m_pTcpClient);
                ArgumentNullException.ThrowIfNull(m_pTcpClient.m_pTcpStream);

                try {
                    m_pSslStream.EndAuthenticateAsClient(ar);

                    // Close old stream, but leave source stream open.
                    m_pTcpClient.m_pTcpStream.IsOwner = false;
                    m_pTcpClient.m_pTcpStream.Dispose();

                    m_pTcpClient.m_IsSecure = true;
                    m_pTcpClient.m_pTcpStream = new SmartStream(m_pSslStream,true);
                }
                catch(Exception x){
                    m_pException = x;                    
                }

                SetState(AsyncOP_State.Completed);
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets asynchronous operation state.
            /// </summary>
            public AsyncOP_State State
            {
                get{ return m_State; }
            }

            /// <summary>
            /// Gets error happened during operation. Returns null if no error.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public Exception? Error
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Error' is accessible only in 'AsyncOP_State.Completed' state.");
                    }

                    return m_pException; 
                }
            }

            #endregion

            #region Events implementation

            /// <summary>
            /// Is called when asynchronous operation has completed.
            /// </summary>
            public event EventHandler<EventArgs<SwitchToSecureAsyncOP>>? CompletedAsync = null;

            #region method OnCompletedAsync

            /// <summary>
            /// Raises <b>CompletedAsync</b> event.
            /// </summary>
            private void OnCompletedAsync()
            {
                if(this.CompletedAsync != null){
                    this.CompletedAsync(this,new EventArgs<SwitchToSecureAsyncOP>(this));
                }
            }

            #endregion

            #endregion
        }

        #endregion

        /// <summary>
        /// Starts switching connection to secure.
        /// </summary>
        /// <param name="op">Asynchronous operation.</param>
        /// <returns>Returns true if aynchronous operation is pending (The <see cref="SwitchToSecureAsyncOP.CompletedAsync"/> event is raised upon completion of the operation).
        /// Returns false if operation completed synchronously.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is not connected or connection is already secure.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>op</b> is null reference.</exception>
        protected bool SwitchToSecureAsync(SwitchToSecureAsyncOP op)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(this.IsSecureConnection){
                throw new InvalidOperationException("Connection is already secure.");
            }
            if(op == null){
                throw new ArgumentNullException("op");
            }
            if(op.State != AsyncOP_State.WaitingForStart){
                throw new ArgumentException("Invalid argument 'op' state, 'op' must be in 'AsyncOP_State.WaitingForStart' state.","op");
            }

            return op.Start(this);
        }

        #endregion

        #region method SwitchToSecure

        /// <summary>
        /// Switches session to secure connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is not connected or is already secure.</exception>
        protected void SwitchToSecure()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("TCP_Client");
            }
            if(!m_IsConnected){
                throw new InvalidOperationException("TCP client is not connected.");
            }
            if(m_IsSecure){
                throw new InvalidOperationException("TCP client is already secure.");
            }
            ArgumentNullException.ThrowIfNull(m_pTcpStream);

            LogAddText("Switching to SSL.");

            // FIX ME: if ssl switching fails, it closes source stream or otherwise if ssl successful, source stream leaks.

            SslStream sslStream = new SslStream(m_pTcpStream.SourceStream,true,this.RemoteCertificateValidationCallback);
            sslStream.AuthenticateAsClient("dummy");

            // Close old stream, but leave source stream open.
            m_pTcpStream.IsOwner = false;
            m_pTcpStream.Dispose();

            m_IsSecure = true;
            m_pTcpStream = new SmartStream(sslStream,true);
        }

        #region method RemoteCertificateValidationCallback

        private bool RemoteCertificateValidationCallback(object sender,X509Certificate? certificate,X509Chain? chain,SslPolicyErrors sslPolicyErrors)
        {
            // User will handle it.
            if(m_pCertificateCallback != null){
                return m_pCertificateCallback(sender,certificate,chain,sslPolicyErrors);
            }
            else{
                if(sslPolicyErrors == SslPolicyErrors.None || ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) > 0)){
                    return true;
                }

                // Do not allow this client to communicate with unauthenticated servers.
                return false;
            }
        }

        #endregion

        #endregion

        #region method ConnectAsync

        #region class ConnectAsyncOP

        /// <summary>
        /// This class represents asynchronous operation.
        /// </summary>
        public class ConnectAsyncOP : IDisposable,IAsyncOP
        {
            private object                               m_pLock         = new object();
            private AsyncOP_State                        m_State         = AsyncOP_State.WaitingForStart;
            private Exception?                           m_pException    = null;
            private IPEndPoint?                          m_pLocalEP      = null;
            private IPEndPoint                           m_pRemoteEP;
            private bool                                 m_SSL           = false;
            private RemoteCertificateValidationCallback? m_pCertCallback = null;
            private TCP_Client?                          m_pTcpClient    = null;
            private Socket?                              m_pSocket       = null;
            private Stream?                              m_pStream       = null;
            private bool                                 m_RiseCompleted = false;

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="localEP">Local IP end point to use. Value null means that system will allocate it.</param>
            /// <param name="remoteEP">Remote IP end point to connect.</param>
            /// <param name="ssl">Specifies if connection switches to SSL affter connect.</param>
            /// <param name="certCallback">SSL server certificate validation callback. Value null means any certificate is accepted.</param>
            /// <exception cref="ArgumentNullException">Is raised when <b>remoteEP</b> is null reference.</exception>
            public ConnectAsyncOP(IPEndPoint? localEP,IPEndPoint remoteEP,bool ssl,RemoteCertificateValidationCallback? certCallback)
            {
                if(remoteEP == null){
                    throw new ArgumentNullException("localEP");
                }

                m_pLocalEP      = localEP;
                m_pRemoteEP     = remoteEP;
                m_SSL           = ssl;
                m_pCertCallback = certCallback;
            }

            #region method Dispose

            /// <summary>
            /// Cleans up any resource being used.
            /// </summary>
            public void Dispose()
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }
                SetState(AsyncOP_State.Disposed);

                m_pException    = null;
                m_pLocalEP      = null;
                m_SSL           = false;
                m_pCertCallback = null;
                m_pTcpClient    = null;
                m_pSocket       = null;
                m_pStream       = null;

                this.CompletedAsync = null;
            }

            #endregion


            #region method Start

            /// <summary>
            /// Starts operation processing.
            /// </summary>
            /// <param name="owner">Owner TCP client.</param>
            /// <returns>Returns true if asynchronous operation in progress or false if operation completed synchronously.</returns>
            /// <exception cref="ArgumentNullException">Is raised when <b>owner</b> is null reference.</exception>
            internal bool Start(TCP_Client owner)
            {
                if(owner == null){
                    throw new ArgumentNullException("owner");
                }

                m_pTcpClient = owner;

                SetState(AsyncOP_State.Active);

                try{
                    // Create socket.
                    if(m_pRemoteEP.AddressFamily == AddressFamily.InterNetwork){
                        m_pSocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
                        m_pSocket.ReceiveTimeout = m_pTcpClient.m_Timeout;
                        m_pSocket.SendTimeout = m_pTcpClient.m_Timeout;
                    }
                    else if(m_pRemoteEP.AddressFamily == AddressFamily.InterNetworkV6){
                        m_pSocket = new Socket(AddressFamily.InterNetworkV6,SocketType.Stream,ProtocolType.Tcp);
                        m_pSocket.ReceiveTimeout = m_pTcpClient.m_Timeout;
                        m_pSocket.SendTimeout = m_pTcpClient.m_Timeout;
                    }
                    else{
                        throw new NotSupportedException("Address family '" + m_pRemoteEP.AddressFamily.ToString() + "' is not supported.");
                    }
                    // Bind socket to the specified end point.
                    if(m_pLocalEP != null){
                        m_pSocket.Bind(m_pLocalEP);
                    }

                    m_pTcpClient.LogAddText("Connecting to " + m_pRemoteEP.ToString() + ".");

                    // Start connecting.
                    m_pSocket.BeginConnect(m_pRemoteEP,this.BeginConnectCompleted,null);
                }
                catch(Exception x){
                    m_pException = x;
                    CleanupSocketRelated();
                    if(m_pTcpClient != null){
                        m_pTcpClient.LogAddException("Exception: " + x.Message,x);
                    }
                    SetState(AsyncOP_State.Completed);

                    return false;
                }

                // Set flag rise CompletedAsync event flag. The event is raised when async op completes.
                // If already completed sync, that flag has no effect.
                lock(m_pLock){
                    m_RiseCompleted = true;

                    return m_State == AsyncOP_State.Active;
                }
            }

            #endregion


            #region method SetState

            /// <summary>
            /// Sets operation state.
            /// </summary>
            /// <param name="state">New state.</param>
            private void SetState(AsyncOP_State state)
            {
                if(m_State == AsyncOP_State.Disposed){
                    return;
                }

                lock(m_pLock){
                    m_State = state;

                    if(m_State == AsyncOP_State.Completed && m_RiseCompleted){
                        OnCompletedAsync();
                    }
                }
            }

            #endregion

            #region method BeginConnectCompleted

            /// <summary>
            /// This method is called when "BeginConnect" has completed.
            /// </summary>
            /// <param name="ar">Asynchronous result.</param>
            private void BeginConnectCompleted(IAsyncResult ar)
            {
                ArgumentNullException.ThrowIfNull(m_pSocket);
                ArgumentNullException.ThrowIfNull(m_pTcpClient);

                try {
                    m_pSocket.EndConnect(ar);

                    m_pTcpClient.LogAddText("Connected, localEP='" + m_pSocket.LocalEndPoint?.ToString() + "'; remoteEP='" + m_pSocket.RemoteEndPoint?.ToString() + "'.");

                    // Start SSL handshake.
                    if(m_SSL){
                        m_pTcpClient.LogAddText("Starting SSL handshake.");

                        m_pStream = new SslStream(new NetworkStream(m_pSocket,true),false,this.RemoteCertificateValidationCallback);
                        ((SslStream)m_pStream).BeginAuthenticateAsClient("dummy",this.BeginAuthenticateAsClientCompleted,null);
                    }
                    // We are done.
                    else{
                        m_pStream = new NetworkStream(m_pSocket,true);

                        InternalConnectCompleted();
                    }
                }
                catch(Exception x){
                    m_pException = x;
                    CleanupSocketRelated();
                    if(m_pTcpClient != null){
                        m_pTcpClient.LogAddException("Exception: " + x.Message,x);
                    }
                    SetState(AsyncOP_State.Completed);
                }
            }

            #endregion

            #region method BeginAuthenticateAsClientCompleted

            /// <summary>
            /// This method is called when "BeginAuthenticateAsClient" has completed.
            /// </summary>
            /// <param name="ar">Asynchronous result.</param>
            private void BeginAuthenticateAsClientCompleted(IAsyncResult ar)
            {
                ArgumentNullException.ThrowIfNull(m_pStream);
                ArgumentNullException.ThrowIfNull(m_pTcpClient);

                try {
                    ((SslStream)m_pStream).EndAuthenticateAsClient(ar);

                    m_pTcpClient.LogAddText("SSL handshake completed sucessfully.");

                    InternalConnectCompleted();
                }
                catch(Exception x){
                    m_pException = x;
                    CleanupSocketRelated();
                    if(m_pTcpClient != null){
                        m_pTcpClient.LogAddException("Exception: " + x.Message,x);
                    }
                    SetState(AsyncOP_State.Completed);
                }
            }

            #endregion

            #region method RemoteCertificateValidationCallback

            /// <summary>
            /// This method is called when we need to validate remote server certificate.
            /// </summary>
            /// <param name="sender">Sender.</param>
            /// <param name="certificate">Certificate.</param>
            /// <param name="chain">Certificate chain.</param>
            /// <param name="sslPolicyErrors">SSL policy errors.</param>
            /// <returns>Returns true if certificate validated, otherwise false.</returns>
            private bool RemoteCertificateValidationCallback(object sender,X509Certificate? certificate,X509Chain? chain,SslPolicyErrors sslPolicyErrors)
            {
                // User will handle it.
                if(m_pCertCallback != null){
                    return m_pCertCallback(sender,certificate,chain,sslPolicyErrors);
                }
                else{
                    if(sslPolicyErrors == SslPolicyErrors.None || ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) > 0)){
                        return true;
                    }

                    // Do not allow this client to communicate with unauthenticated servers.
                    return false;
                }
            }

            #endregion

            #region method CleanupSocketRelated

            /// <summary>
            /// Cleans up any socket related resources.
            /// </summary>
            private void CleanupSocketRelated()
            {
                try{                    
                    if(m_pStream != null){
                        m_pStream.Dispose();
                    }
                    if(m_pSocket != null){
                        m_pSocket.Close();
                    }
                }
                catch{
                }
            }

            #endregion

            #region method InternalConnectCompleted

            /// <summary>
            /// Is called when when connecting has finished.
            /// </summary>
            private void InternalConnectCompleted()
            {
                ArgumentNullException.ThrowIfNull(m_pTcpClient);
                ArgumentNullException.ThrowIfNull(m_pSocket);
                ArgumentNullException.ThrowIfNull(m_pStream);

                m_pTcpClient.m_IsConnected = true;
                m_pTcpClient.m_ID          = Guid.NewGuid().ToString();
                m_pTcpClient.m_ConnectTime = DateTime.Now;
                m_pTcpClient.m_pLocalEP    = m_pSocket.LocalEndPoint as IPEndPoint;
                m_pTcpClient.m_pRemoteEP   = m_pSocket.RemoteEndPoint as IPEndPoint;
                m_pTcpClient.m_pTcpStream  = new SmartStream(m_pStream,true);
                m_pTcpClient.m_pTcpStream.Encoding = Encoding.UTF8;

                m_pTcpClient.OnConnected(this.CompleteConnectCallback);
            }

            #endregion

            #region method CompleteConnectCallback

            /// <summary>
            /// This method is called when this derrived class OnConnected processing has completed.
            /// </summary>
            /// <param name="error">Exception happened or null if no errors.</param>
            private void CompleteConnectCallback(Exception? error)
            {
                m_pException = error;
                                
                SetState(AsyncOP_State.Completed);
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets asynchronous operation state.
            /// </summary>
            public AsyncOP_State State
            {
                get{ return m_State; }
            }

            /// <summary>
            /// Gets error happened during operation. Returns null if no error.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public Exception? Error
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Error' is accessible only in 'AsyncOP_State.Completed' state.");
                    }

                    return m_pException; 
                }
            }

            /// <summary>
            /// Gets connected socket.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public Socket? Socket
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Socket' is accessible only in 'AsyncOP_State.Completed' state.");
                    }
                    if(m_pException != null){
                        throw m_pException;
                    }

                    return m_pSocket; 
                }
            }

            /// <summary>
            /// Gets connected TCP stream.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this property is accessed.</exception>
            /// <exception cref="InvalidOperationException">Is raised when this property is accessed other than <b>AsyncOP_State.Completed</b> state.</exception>
            public Stream? Stream
            {
                get{ 
                    if(m_State == AsyncOP_State.Disposed){
                        throw new ObjectDisposedException(this.GetType().Name);
                    }
                    if(m_State != AsyncOP_State.Completed){
                        throw new InvalidOperationException("Property 'Stream' is accessible only in 'AsyncOP_State.Completed' state.");
                    }
                    if(m_pException != null){
                        throw m_pException;
                    }

                    return m_pStream; 
                }
            }

            #endregion

            #region Events implementation

            /// <summary>
            /// Is called when asynchronous operation has completed.
            /// </summary>
            public event EventHandler<EventArgs<ConnectAsyncOP>>? CompletedAsync = null;

            #region method OnCompletedAsync

            /// <summary>
            /// Raises <b>CompletedAsync</b> event.
            /// </summary>
            private void OnCompletedAsync()
            {
                if(this.CompletedAsync != null){
                    this.CompletedAsync(this,new EventArgs<ConnectAsyncOP>(this));
                }
            }

            #endregion

            #endregion
        }

        #endregion

        /// <summary>
        /// Starts connecting to remote end point.
        /// </summary>
        /// <param name="op">Asynchronous operation.</param>
        /// <returns>Returns true if aynchronous operation is pending (The <see cref="ConnectAsyncOP.CompletedAsync"/> event is raised upon completion of the operation).
        /// Returns false if operation completed synchronously.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>op</b> is null reference.</exception>
        public bool ConnectAsync(ConnectAsyncOP op)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(op == null){
                throw new ArgumentNullException("op");
            }
            if(op.State != AsyncOP_State.WaitingForStart){
                throw new ArgumentException("Invalid argument 'op' state, 'op' must be in 'AsyncOP_State.WaitingForStart' state.","op");
            }

            return op.Start(this);
        }

        #endregion

        #region method BeginDisconnect

        /// <summary>
        /// Internal helper method for asynchronous Disconnect method.
        /// </summary>
        private delegate void DisconnectDelegate();

        /// <summary>
        /// Starts disconnecting connection.
        /// </summary>
        /// <param name="callback">Callback to call when the asynchronous operation is complete.</param>
        /// <param name="state">User data.</param>
        /// <returns>An IAsyncResult that references the asynchronous disconnect.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is not connected.</exception>
        public IAsyncResult BeginDisconnect(AsyncCallback callback,object state)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!m_IsConnected){
                throw new InvalidOperationException("TCP client is not connected.");
            }

            DisconnectDelegate asyncMethod = new DisconnectDelegate(this.Disconnect);
            AsyncResultState asyncState = new AsyncResultState(this,asyncMethod,callback,state);
            asyncState.SetAsyncResult(asyncMethod.BeginInvoke(new AsyncCallback(asyncState.CompletedCallback),null));

            return asyncState;
        }

        #endregion

        #region method EndDisconnect

        /// <summary>
        /// Ends a pending asynchronous disconnect request.
        /// </summary>
        /// <param name="asyncResult">An IAsyncResult that stores state information and any user defined data for this asynchronous operation.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>asyncResult</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when argument <b>asyncResult</b> was not returned by a call to the <b>BeginDisconnect</b> method.</exception>
        /// <exception cref="InvalidOperationException">Is raised when <b>EndDisconnect</b> was previously called for the asynchronous connection.</exception>
        public void EndDisconnect(IAsyncResult asyncResult)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(asyncResult == null){
                throw new ArgumentNullException("asyncResult");
            }
            
            AsyncResultState? castedAsyncResult = asyncResult as AsyncResultState;
            if(castedAsyncResult == null || castedAsyncResult.AsyncObject != this){
                throw new ArgumentException("Argument asyncResult was not returned by a call to the BeginDisconnect method.");
            }
            if(castedAsyncResult.IsEndCalled){
                throw new InvalidOperationException("EndDisconnect was previously called for the asynchronous connection.");
            }
             
            castedAsyncResult.IsEndCalled = true;
            if(castedAsyncResult.AsyncDelegate is DisconnectDelegate){
                ((DisconnectDelegate)castedAsyncResult.AsyncDelegate).EndInvoke(castedAsyncResult.AsyncResult);
            }
            else{
                throw new ArgumentException("Argument asyncResult was not returned by a call to the BeginDisconnect method.");
            }
        }

        #endregion

        #region virtual method OnConnected

        /// <summary>
        /// This method is called after TCP client has sucessfully connected.
        /// </summary>
        protected virtual void OnConnected()
        {
        }

        /// <summary>
        /// Represents callback to be called when to complete connect operation.
        /// </summary>
        /// <param name="error">Exception happened or null if no errors.</param>
        protected delegate void CompleteConnectCallback(Exception? error);

        /// <summary>
        /// This method is called when TCP client has sucessfully connected.
        /// </summary>
        /// <param name="callback">Callback to be called to complete connect operation.</param>
        protected virtual void OnConnected(CompleteConnectCallback callback)
        {
            try{
                OnConnected();

                callback(null);
            }
            catch(Exception x){
                callback(x);
            }
        }

        #endregion

        #region method Connect

        // [Obsolete("Use the new ConnectAsync overload with explicit AddressFamily and local endpoint.")]

        /// <summary>
        /// Connects to the specified host. If the hostname resolves to more than one IP address, 
        /// all IP addresses will be tried for connection, until one of them connects.
        /// </summary>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Port to connect.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is already connected.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public void Connect(string host,int port)
        {
            Connect(host,port,false);
        }

        /// <summary>
        /// Connects to the specified host. If the hostname resolves to more than one IP address, 
        /// all IP addresses will be tried for connection, until one of them connects.
        /// </summary>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Port to connect.</param>
        /// <param name="ssl">Specifies if connects to SSL end point.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is already connected.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public void Connect(string host,int port,bool ssl)
        {            
            if(m_IsDisposed){
                throw new ObjectDisposedException("TCP_Client");
            }
            if(m_IsConnected){
                throw new InvalidOperationException("TCP client is already connected.");
            }
            if(string.IsNullOrEmpty(host)){
                throw new ArgumentException("Argument 'host' value may not be null or empty.");
            }
            if(port < 1){
                throw new ArgumentException("Argument 'port' value must be >= 1.");
            }

            Connect(null,host,port,AddressFamily.Unspecified,ssl,null);
        }

        /// <summary>
        /// Connects to the specified remote end point.
        /// </summary>
        /// <param name="remoteEP">Remote IP end point where to connect.</param>
        /// <param name="ssl">Specifies if connects to SSL end point.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is already connected.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>remoteEP</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        public void Connect(IPEndPoint remoteEP,bool ssl)
        {
            Connect(null,remoteEP,ssl);
        }

        /// <summary>
        /// Connects to the specified remote end point.
        /// </summary>
        /// <param name="localEP">Local IP end point to use. Value null means that system will allocate it.</param>
        /// <param name="remoteEP">Remote IP end point to connect.</param>
        /// <param name="ssl">Specifies if connection switches to SSL affter connect.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is already connected.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>remoteEP</b> is null reference.</exception>
        public void Connect(IPEndPoint? localEP,IPEndPoint remoteEP,bool ssl)
        {
            Connect(localEP,remoteEP,ssl,null);
        }

        /// <summary>
        /// Connects to the specified remote end point.
        /// </summary>
        /// <param name="localEP">Local IP end point to use. Value null means that system will allocate it.</param>
        /// <param name="remoteEP">Remote IP end point to connect.</param>
        /// <param name="ssl">Specifies if connection switches to SSL affter connect.</param>
        /// <param name="certCallback">SSL server certificate validation callback. Value null means any certificate is accepted.</param>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when TCP client is already connected.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>remoteEP</b> is null reference.</exception>
        public void Connect(IPEndPoint? localEP,IPEndPoint remoteEP,bool ssl,RemoteCertificateValidationCallback? certCallback)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(m_IsConnected){
                throw new InvalidOperationException("TCP client is already connected.");
            }
            if(remoteEP == null){
                throw new ArgumentNullException("remoteEP");
            }

            var sslOptions = new SslClientAuthenticationOptions{
                TargetHost = "localhost",
                EnabledSslProtocols = SslProtocols.Tls12,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                AllowRenegotiation = true,
                ApplicationProtocols = new List<SslApplicationProtocol>(),
                ClientCertificates = null,                   
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
            };
            if(certCallback != null){
                sslOptions.RemoteCertificateValidationCallback = certCallback;
            }

            Connect(localEP,new []{remoteEP.Address},remoteEP.Port,ssl,sslOptions);
        }

        #endregion

        #region method SwitchToSecure

        /// <summary>
        /// Synchronously upgrades the current TCP connection to a secure TLS connection
        /// using <see cref="SslStream"/> and the provided <see cref="SslClientAuthenticationOptions"/>.
        /// </summary>
        /// <param name="sslOptions">
        /// Optional TLS configuration.  
        /// If <c>null</c>, the method applies a permissive legacy‑compatible configuration:
        /// <list type="bullet">
        /// <item><description>TLS 1.2 only</description></item>
        /// <item><description>Certificate revocation checking disabled</description></item>
        /// <item><description>Renegotiation allowed</description></item>
        /// <item><description>ALPN disabled</description></item>
        /// <item><description>No client certificates</description></item>
        /// <item><description>Certificate validation callback that accepts all certificates</description></item>
        /// </list>
        /// These defaults are intended for maximum compatibility with older servers.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the underlying TCP client has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or if the connection is already secure.
        /// </exception>
        /// <exception cref="AuthenticationException">
        /// Thrown when the TLS handshake fails.
        /// </exception>
        protected void SwitchToSecure(SslClientAuthenticationOptions? sslOptions)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("TCP_Client");
            }
            if(!m_IsConnected){
                throw new InvalidOperationException("TCP client is not connected.");
            }
            if(m_IsSecure){
                throw new InvalidOperationException("TCP client is already secure.");
            }
            ArgumentNullException.ThrowIfNull(m_pTcpStream);

            using var cts = new CancellationTokenSource(m_Timeout);

            SwitchToSecureAsync(sslOptions,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region property ValidateCertificateCallback

        /// <summary>
        /// Gets or stes remote callback which is called when remote server certificate needs to be validated.
        /// Value null means not sepcified.
        /// </summary>
        public RemoteCertificateValidationCallback? ValidateCertificateCallback
        {
            get{ return m_pCertificateCallback; }

            set{ m_pCertificateCallback = value; }
        }

        #endregion
    }
}
