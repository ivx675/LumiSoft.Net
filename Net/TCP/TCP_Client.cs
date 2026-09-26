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
        private bool         m_IsDisposed   = false;
        private bool         m_IsConnected  = false;
        private string       m_ID           = "";
        private DateTime     m_ConnectTime;
        private IPEndPoint?  m_pLocalEP     = null;
        private IPEndPoint?  m_pRemoteEP    = null;
        private bool         m_IsSecure     = false;
        private SmartStream? m_pTcpStream   = null;
        private Logger?      m_pLogger      = null;
        private int          m_Timeout      = 61000;

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
                m_ID          = Guid.NewGuid().ToString();
                m_ConnectTime = DateTime.Now;
                m_pLocalEP    = (IPEndPoint)socket.LocalEndPoint!;
                m_pRemoteEP   = (IPEndPoint)socket.RemoteEndPoint!;

                m_pTcpStream = new SmartStream(new NetworkStream(socket,true),true);
                m_pTcpStream.Encoding = Encoding.UTF8;

                if(ssl){
                    LogAddText("Switching to secure connection (SSL).");
                    await SwitchToSecureAsync(sslOptions,cancellationToken);
                    LogAddText("Secure connection established (SSL).");
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
                m_ID          = Guid.NewGuid().ToString();
                m_ConnectTime = DateTime.Now;
                m_pLocalEP    = (IPEndPoint)socket.LocalEndPoint!;
                m_pRemoteEP   = (IPEndPoint)socket.RemoteEndPoint!;

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
                m_ID          = Guid.NewGuid().ToString();
                m_ConnectTime = DateTime.Now;
                m_pLocalEP    = (IPEndPoint)socket.LocalEndPoint!;
                m_pRemoteEP   = (IPEndPoint)socket.RemoteEndPoint!;

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

        #region virtual method OnConnected

        /// <summary>
        /// This method is called after TCP client has sucessfully connected.
        /// </summary>
        protected virtual void OnConnected()
        {
        }

        #endregion
    }
}
