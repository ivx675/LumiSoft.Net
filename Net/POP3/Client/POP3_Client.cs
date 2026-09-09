using LumiSoft.Net.AUTH;
using LumiSoft.Net.IO;
using LumiSoft.Net.SMTP;
using LumiSoft.Net.TCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace LumiSoft.Net.POP3.Client
{
	/// <summary>
    /// Implements a POP3 client as defined in RFC 1939, RFC 2449 (CAPA),
    /// and RFC 2595 (STLS).  
    /// The class provides high‑level methods for connecting to a POP3
    /// server, negotiating optional TLS, authenticating, retrieving
    /// server capabilities, and performing POP3 operations such as
    /// listing, retrieving, and deleting messages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A POP3 session consists of three protocol states:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>AUTHORIZATION</b> – The client connects to the server and
    /// receives the greeting. Authentication is performed using
    /// <c>USER/PASS</c>, APOP, or SASL (when supported).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>TRANSACTION</b> – After successful authentication, the client
    /// may issue commands such as <c>LIST</c>, <c>UIDL</c>, <c>RETR</c>,
    /// <c>TOP</c>, <c>DELE</c>, and <c>NOOP</c>.  
    /// The message list may be loaded using <see cref="LoadMessages"/> or
    /// <see cref="LoadMessagesAsync(System.Threading.CancellationToken)"/>,
    /// but this is optional.  
    /// The client can be used without ever loading the message list.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>UPDATE</b> – Entered when the client sends <c>QUIT</c>.  
    /// The server applies any pending deletions and closes the
    /// connection.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// TLS negotiation is supported via the POP3 <c>STLS</c> command
    /// (RFC 2595). When TLS is enabled, all subsequent commands are
    /// transmitted over the secure channel.
    /// </para>
    /// <para>
    /// Server capabilities are retrieved using the <c>CAPA</c> command
    /// (RFC 2449). The client automatically detects support for
    /// extensions such as <c>UIDL</c>, <c>UTF8</c>, and <c>UTF8=USER</c>.
    /// </para>
    /// <para>
    /// The <see cref="Messages"/> property exposes cached message metadata
    /// (size and optional unique‑id) after the message list has been
    /// loaded.  
    /// Accessing this property is optional; callers may instead use
    /// <c>LIST</c>, <c>UIDL</c>, <c>RETR</c>, and <c>TOP</c> directly.
    /// </para>
    /// <para>
    /// All network operations are available in both synchronous and
    /// asynchronous forms. The synchronous wrappers apply the configured
    /// <see cref="Timeout"/> value to limit operation duration.
    /// </para>
    /// </remarks>
	public class POP3_Client : TCP_Client
	{
        private string                        m_GreetingText       = "";
		private string                        m_ApopUniqueId       = "";
        private List<string>                  m_pCapabilities;
        private bool                          m_SupportsUidl       = false;
        private bool                          m_SupportsUtf8       = false;
        private bool                          m_SupportsUtf8User   = false;
        private GenericIdentity?              m_pAuthdUserIdentity = null;
        private POP3_ClientMessageCollection? m_pMessages          = null;
        private bool                          m_Utf8Enabled        = false;

		/// <summary>
		/// Default constructor.
		/// </summary>
		public POP3_Client()
		{
	        m_pCapabilities = new List<string>();
		}

		#region override method Dispose

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		public override void Dispose()
		{
			base.Dispose();
		}

		#endregion

        
        #region override method OnConnectedAsync

        /// <summary>
        /// Handles the POP3 server greeting received immediately after the TCP
        /// connection is established.  
        /// According to RFC 1939 section 6, the server sends a single greeting line
        /// beginning with <c>+OK</c>, optionally followed by a unique APOP challenge
        /// and human‑readable text.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the POP3 server returns a negative greeting (<c>-ERR</c>).
        /// </exception>
        /// <remarks>
        /// <para>
        /// POP3 greeting syntax (RFC 1939 §6):
        /// </para>
        /// <code>
        /// Greeting      = "+OK" [ SP greeting-text ] CRLF
        /// greeting-text = [ unique-id ] [ SP human-text ]
        /// unique-id     = "&lt;" *CHAR "&gt;"   ; Required for APOP authentication
        /// </code>
        /// <para>
        /// If a unique-id is present, it MUST be the first token in <c>greeting-text</c>.
        /// The unique-id is extracted and stored for optional APOP authentication.
        /// Any remaining text is stored as the server's human-readable greeting.
        /// </para>
        /// </remarks>
        protected override async ValueTask OnConnectedAsync(CancellationToken cancellationToken = default)
        {
            /* RFC 1939 section 6 — POP3 Greeting (with APOP support)

               The POP3 server sends a single greeting line immediately after the TCP
               connection is established.

               Greeting = "+OK" [ SP greeting-text ] CRLF

               greeting-text = [ unique-id ] [ SP human-text ]

               unique-id     = "<" *CHAR ">"        ; Required for APOP authentication
                                                    ; Must be a globally unique string
                                                    ; Typically includes timestamp, host, and random data

               Examples:
                 +OK POP3 server ready
                 +OK <1896.697170952@dbc.mtview.ca.us>
                 +OK <20260908.080000.1234@server.example.com> Dovecot ready.
            */

            var serverResponse = await ReadResponseAsync(cancellationToken);

            if(serverResponse.IsSuccess){
                m_GreetingText = serverResponse.Text;

                string[] parts = serverResponse.Text.Split(' ',2);
                if(parts[0].StartsWith('<')){
                    m_ApopUniqueId = parts[0].Substring(1,parts[0].Length - 2);
                    m_GreetingText = parts.Length > 1 ? parts[1] : "";
                }
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }
        }

        #endregion

		#region override method Disconnect

		/// <summary>
        /// Disconnects from the POP3 server by invoking the asynchronous
        /// <see cref="DisconnectAsync(bool, CancellationToken)"/> method with
        /// <c>sendQuit</c> set to <c>true</c> and waiting for its completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper uses the configured <see cref="Timeout"/> value
        /// to create a <see cref="CancellationTokenSource"/> that limits the duration
        /// of the disconnect sequence.  
        /// The underlying asynchronous method sends the POP3 <c>QUIT</c> command,
        /// reads the server's final response, clears all session state, and closes
        /// the connection.
        /// </para>
        /// <para>
        /// Any exceptions raised during the asynchronous disconnect sequence are
        /// propagated to the caller.
        /// </para>
        /// </remarks>
		public override void Disconnect()
		{
            using var cts = new CancellationTokenSource(this.Timeout);

            DisconnectAsync(true,cts.Token).GetAwaiter().GetResult();
		}

		#endregion

        #region method DisconnectAsync

        /// <summary>
        /// Disconnects from the POP3 server and optionally sends the <c>QUIT</c>
        /// command before closing the connection.
        /// </summary>
        /// <param name="sendQuit">
        /// If <c>true</c>, the method sends the POP3 <c>QUIT</c> command and reads
        /// the server's final response before terminating the connection.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations performed during the disconnect sequence.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous disconnect
        /// operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not currently connected.
        /// </exception>
        /// <remarks>
        /// <para>
        /// When <paramref name="sendQuit"/> is <c>true</c>, the method attempts to
        /// send the POP3 <c>QUIT</c> command and read the server's final reply.
        /// Any exceptions raised during this exchange are suppressed, and the
        /// connection is closed regardless.
        /// </para>
        /// <para>
        /// All POP3 session state (greeting text, APOP unique-id, capability list,
        /// message cache, and authenticated identity) is cleared before the
        /// underlying connection is closed.
        /// </para>
        /// </remarks>
        public async ValueTask DisconnectAsync(bool sendQuit,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("POP3 client is not connected.");
            }

            try{            
                if(sendQuit){
                    await SendCommandLineAsync("QUIT\r\n",true,cancellationToken);

                    _= await ReadResponseAsync(cancellationToken);
                }
            }
            catch{
            }

            m_GreetingText     = "";
            m_ApopUniqueId     = "";
            m_pCapabilities    = new List<string>();
            m_SupportsUidl     = false;
            m_SupportsUtf8     = false;
            m_SupportsUtf8User = false;
            if(m_pMessages != null){
                m_pMessages.Dispose();
                m_pMessages = null;
            } 
            m_pAuthdUserIdentity = null;
            m_Utf8Enabled        = false;

            base.Disconnect(); 
        }

        #endregion


        #region method Capa

        /// <summary>
        /// Retrieves the POP3 capability list by invoking the asynchronous
        /// <see cref="CapaAsync(CancellationToken)"/> method and waiting for its
        /// completion.  
        /// This synchronous wrapper uses the configured <see cref="Timeout"/> value
        /// to limit the duration of the operation.
        /// </summary>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// initial <c>+OK</c> or <c>-ERR</c> response to the <c>CAPA</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not currently connected.
        /// </exception>
        /// <exception cref="POP3_ProtocolException">
        /// Thrown when the server returns malformed data or violates POP3
        /// protocol rules.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The underlying asynchronous method sends the POP3 <c>CAPA</c> command,
        /// reads the server's initial status response, and if successful, reads the
        /// multiline capability list terminated by a single dot (<c>.</c>).  
        /// All capability lines are stored in the client's internal capability
        /// collection.
        /// </para>
        /// <para>
        /// Any exceptions raised during the asynchronous capability retrieval are
        /// propagated to the caller of this method.
        /// </para>
        /// </remarks>
        public POP3_ServerResponse Capa()
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
				throw new InvalidOperationException("You must connect first.");
			}

            using var cts = new CancellationTokenSource(this.Timeout);

            return CapaAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method CapaAsync

        /// <summary>
        /// Sends the POP3 <c>CAPA</c> command to the server and retrieves the
        /// capability list defined in RFC 2449.  
        /// The method clears any previously cached capabilities, issues the
        /// command, reads the server's initial status response, and if successful,
        /// reads the multiline capability list terminated by a single dot (<c>.</c>).
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations performed while retrieving the capability list.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// initial <c>+OK</c> or <c>-ERR</c> response to the <c>CAPA</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not currently connected.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>CAPA</c> command.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the POP3 server closes the connection before the capability
        /// list is fully read.
        /// </exception>
        /// <exception cref="POP3_ProtocolException">
        /// Thrown when the server returns malformed data or violates POP3
        /// protocol rules.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>CAPA</c> command (RFC 2449) returns a list of server
        /// capabilities, one per line, optionally followed by parameters.  
        /// The list is terminated by a line containing only a dot (<c>.</c>).
        /// </para>
        /// <para>
        /// A negative (<c>-ERR</c>) response indicates that the server does not
        /// implement the <c>CAPA</c> command, and the client must fall back to
        /// probing for individual capabilities.
        /// </para>
        /// <para>
        /// The method stores each capability line verbatim in the internal
        /// capability collection.
        /// </para>
        /// </remarks>
        public async ValueTask<POP3_ServerResponse> CapaAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            /* RFC 2449 CAPA
                Arguments:
                    none

                Restrictions:
                    none

                Discussion:
                    An -ERR response indicates the capability command is not
                    implemented and the client will have to probe for
                    capabilities as before.

                    An +OK response is followed by a list of capabilities, one
                    per line.  Each capability name MAY be followed by a single
                    space and a space-separated list of parameters.  Each
                    capability line is limited to 512 octets (including the
                    CRLF).  The capability list is terminated by a line
                    containing a termination octet (".") and a CRLF pair.

                Possible Responses:
                    +OK -ERR

                Examples:
                    C: CAPA
                    S: +OK Capability list follows
                    S: TOP
                    S: USER
                    S: SASL CRAM-MD5 KERBEROS_V4
                    S: RESP-CODES
                    S: LOGIN-DELAY 900
                    S: PIPELINING
                    S: EXPIRE 60
                    S: UIDL
                    S: IMPLEMENTATION Shlemazle-Plotz-v302
                    S: .
            */

            m_pCapabilities.Clear();

            await SendCommandLineAsync("CAPA\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);            
            if(serverResponse.IsSuccess){
                Memory<byte> lineBuffer = new byte[8000];
                int          totalItems = 0;
                while(true){
                    ReadLineResult responseline = await this.TcpStream.ReadLineAsync(lineBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
                    // Server closed connection.
                    if(responseline.BytesInBuffer == 0){
                        throw new IOException("POP3 server closed connection.");
                    }
                    else{
                        string line = responseline.LineUtf8 ?? "";

                        LogAddRead(responseline.BytesInBuffer,line);

                        if(line == "."){
                            break;
                        }
                        else{
                            if(totalItems > 100){
                                throw new POP3_ProtocolException($"CAPA response exceeded maximum allowed entries ({totalItems}).");
                            }
                            m_pCapabilities.Add(line);
                            totalItems++;
                            
                            if(string.Equals(line,"UIDL",StringComparison.OrdinalIgnoreCase)){
                                m_SupportsUidl = true;
                            }
                            else if(string.Equals(line,"UTF8",StringComparison.OrdinalIgnoreCase)){
                                m_SupportsUtf8 = true;
                            }
                            else if(string.Equals(line,"UTF8=USER",StringComparison.OrdinalIgnoreCase)){
                                m_SupportsUtf8User = true;
                            }
                        }
                    }
                }
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion

        #region method Stls

        /// <summary>
        /// Sends the POP3 <c>STLS</c> command and, if accepted by the server,
        /// upgrades the existing plaintext connection to a secure TLS connection
        /// by invoking the asynchronous <see cref="StlsAsync"/> method and waiting
        /// for its completion.
        /// </summary>
        /// <param name="sslOptions">
        /// Optional TLS configuration.  
        /// If <c>null</c>, a permissive legacy‑compatible configuration is applied inside:
        /// <list type="bullet">
        /// <item><description>TLS 1.2 only</description></item>
        /// <item><description>Certificate revocation checking disabled</description></item>
        /// <item><description>Renegotiation allowed</description></item>
        /// <item><description>ALPN disabled</description></item>
        /// <item><description>No client certificates</description></item>
        /// <item><description>Certificate validation callback that accepts all certificates</description></item>
        /// </list>
        /// These defaults maximize compatibility with older servers.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// initial <c>+OK</c> or <c>-ERR</c> response to the <c>STLS</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the connection is already
        /// secure, or when the <c>STLS</c> command is issued after authentication.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>STLS</c> command.
        /// </exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown when the TLS negotiation fails after the server accepts <c>STLS</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper uses the configured <see cref="Timeout"/> value
        /// to create a <see cref="CancellationTokenSource"/> that limits the duration
        /// of the STLS negotiation.  
        /// The underlying asynchronous method sends the <c>STLS</c> command, reads the
        /// server's response, and if successful, performs the TLS negotiation.
        /// </para>
        /// <para>
        /// Any exceptions raised during the asynchronous STLS negotiation are
        /// propagated to the caller of this method.
        /// </para>
        /// </remarks>
        public POP3_ServerResponse Stls(SslClientAuthenticationOptions? sslOptions)
        {    
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
				throw new InvalidOperationException("You must connect first.");
			}
			if(this.IsAuthenticated){
				throw new InvalidOperationException("The STLS command is only valid in non-authenticated state.");
			}
            if(this.IsSecureConnection){
                throw new InvalidOperationException("Connection is already secure.");
            }
                        
            using var cts = new CancellationTokenSource(this.Timeout);

            return StlsAsync(sslOptions,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method StlsAsync

        /// <summary>
        /// Sends the POP3 <c>STLS</c> command and, if accepted by the server,
        /// upgrades the existing plaintext connection to a secure TLS connection
        /// as defined in RFC 2595 (POP3 STARTTLS extension).
        /// </summary>
        /// <param name="sslOptions">
        /// Optional TLS configuration.  
        /// If <c>null</c>, a permissive legacy‑compatible configuration is applied inside:
        /// <list type="bullet">
        /// <item><description>TLS 1.2 only</description></item>
        /// <item><description>Certificate revocation checking disabled</description></item>
        /// <item><description>Renegotiation allowed</description></item>
        /// <item><description>ALPN disabled</description></item>
        /// <item><description>No client certificates</description></item>
        /// <item><description>Certificate validation callback that accepts all certificates</description></item>
        /// </list>
        /// These defaults maximize compatibility with older servers.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations performed during the STLS negotiation.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// initial <c>+OK</c> or <c>-ERR</c> response to the <c>STLS</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the connection is already
        /// secure, or when the <c>STLS</c> command is issued after authentication.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>STLS</c> command.
        /// </exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown when the TLS negotiation fails after the server accepts <c>STLS</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>STLS</c> command is permitted only in the POP3 AUTHORIZATION state.
        /// A successful <c>+OK</c> response indicates that the client must immediately
        /// begin TLS negotiation. After the negotiation completes, all subsequent POP3
        /// commands are sent over the secure TLS layer.
        /// </para>
        /// <para>
        /// A negative (<c>-ERR</c>) response indicates that the server does not permit
        /// TLS negotiation in the current state or does not support the <c>STLS</c>
        /// extension.
        /// </para>
        /// </remarks>
        public async ValueTask<POP3_ServerResponse> StlsAsync(SslClientAuthenticationOptions? sslOptions,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
				throw new InvalidOperationException("You must connect first.");
			}
			if(this.IsAuthenticated){
				throw new InvalidOperationException("The STLS command is only valid in non-authenticated state.");
			}
            if(this.IsSecureConnection){
                throw new InvalidOperationException("Connection is already secure.");
            }

            /* RFC 2595 4. POP3 STARTTLS extension.
                Arguments: none

                Restrictions:
                    Only permitted in AUTHORIZATION state.
             
                Possible Responses:
                     +OK -ERR

                 Examples:
                     C: STLS
                     S: +OK Begin TLS negotiation
                     <TLS negotiation, further commands are under TLS layer>
                       ...
                     C: STLS
                     S: -ERR Command not permitted when TLS active
            */

            await SendCommandLineAsync("STLS\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);
            if(serverResponse.IsSuccess){
                LogAddText("Starting TLS handshake.");

                await SwitchToSecureAsync(sslOptions,cancellationToken);

                LogAddText("TLS handshake completed successfully.");
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion
        
        #region method Login

        /// <summary>
        /// Authenticates the POP3 session using the USER/PASS login mechanism
        /// by invoking the asynchronous <see cref="LoginAsync"/> method and
        /// waiting for its completion.  
        /// This synchronous wrapper applies the configured <see cref="Timeout"/>
        /// value to limit the duration of the authentication sequence.
        /// </summary>
        /// <param name="user">
        /// The POP3 username supplied to the <c>USER</c> command.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <param name="password">
        /// The POP3 password supplied to the <c>PASS</c> command.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// final response to the <c>PASS</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the session is already
        /// authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="user"/> or <paramref name="password"/>
        /// is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// either the <c>USER</c> or <c>PASS</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around <see cref="LoginAsync"/>.
        /// It sends the POP3 <c>USER</c> and <c>PASS</c> commands and transitions
        /// the session into the TRANSACTION state upon successful authentication.
        /// </para>
        /// <para>
        /// Any exceptions raised during the asynchronous authentication sequence
        /// are propagated to the caller of this method.
        /// </para>
        /// </remarks>
        public POP3_ServerResponse Login(string user,string password)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return LoginAsync(user,password,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method LoginAsync

        /// <summary>
        /// Authenticates the POP3 session using the USER/PASS login mechanism
        /// defined in RFC 1939 section 7.  
        /// The method sends the <c>USER</c> command followed by the <c>PASS</c>
        /// command and transitions the session into the TRANSACTION state upon
        /// successful authentication.
        /// </summary>
        /// <param name="user">
        /// The POP3 username supplied to the <c>USER</c> command.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <param name="password">
        /// The POP3 password supplied to the <c>PASS</c> command.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations performed during the authentication sequence.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// final response to the <c>PASS</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the session is already
        /// authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="user"/> or <paramref name="password"/>
        /// is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// either the <c>USER</c> or <c>PASS</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 LOGIN mechanism consists of two commands:
        /// </para>
        /// <code>
        /// USER &lt;username&gt; CRLF
        /// PASS &lt;password&gt; CRLF
        /// </code>
        /// <para>
        /// A successful <c>PASS</c> command transitions the session from the
        /// AUTHORIZATION state into the TRANSACTION state.  
        /// A failed <c>PASS</c> command leaves the session in the AUTHORIZATION
        /// state and the method throws a <see cref="POP3_ClientException"/>.
        /// </para>
        /// <para>
        /// For security reasons, the method masks both the username and password
        /// in release-mode logging.  
        /// The authenticated identity is stored using
        /// <see cref="System.Security.Principal.GenericIdentity"/> with the
        /// authentication type <c>"USER/PASS"</c>.
        /// </para>
        /// </remarks>
        public async ValueTask<POP3_ServerResponse> LoginAsync(string user,string password,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
				throw new InvalidOperationException("You must connect first.");
			}
			if(this.IsAuthenticated){
				throw new InvalidOperationException("Session is already authenticated.");
			}
            if(string.IsNullOrWhiteSpace(user)){
                throw new ArgumentException("Argument 'user' may not be null or empty.",nameof(user));
            }
            if(string.IsNullOrWhiteSpace(password)){
                throw new ArgumentException("Argument 'password' may not be null or empty.",nameof(password));
            }

            /* RFC 1939 section 7 — POP3 USER/PASS Authentication (LOGIN)

               The POP3 LOGIN mechanism consists of two commands: USER and PASS.
               These commands are only valid in the AUTHORIZATION state.

               Syntax:
                   USER <username> CRLF
                   PASS <password> CRLF

               Possible Responses:
                   +OK  -ERR

               Discussion:
                   - A successful PASS command transitions the session into the
                     TRANSACTION state.
                   - A failed PASS command leaves the session in AUTHORIZATION state.

               Examples:
                   C: USER bob
                   S: +OK
                   C: PASS secret
                   S: +OK Mailbox locked and ready

                   C: USER alice
                   S: +OK
                   C: PASS wrongpass
                   S: -ERR Invalid password
            */

            string cmdLine = "USER " + user + "\r\n";

            await SendCommandLineAsync("USER " + user + "\r\n",false,cancellationToken);

            #if DEBUG
                LogAddWrite(cmdLine.Length,cmdLine.Trim());
            #else
                LogAddWrite(cmdLine.Length,"USER <***REMOVED***>");
            #endif

            var serverResponse = await ReadResponseAsync(cancellationToken);            
            if(serverResponse.IsSuccess){
                cmdLine = "PASS " + password + "\r\n";

                await SendCommandLineAsync(cmdLine,false,cancellationToken);

                #if DEBUG
                    LogAddWrite(cmdLine.Length,cmdLine.Trim());
                #else
                    LogAddWrite(cmdLine.Length,"PASS <***REMOVED***>");
                #endif

                serverResponse = await ReadResponseAsync(cancellationToken);
                if(serverResponse.IsSuccess){
                    m_pAuthdUserIdentity = new GenericIdentity(user,"USER/PASS");

                    return serverResponse;
                }
                else{
                    throw new POP3_ClientException(serverResponse);
                }
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }
        }

        #endregion

        #region method AuthApop

        /// <summary>
        /// Performs POP3 APOP authentication by invoking the asynchronous
        /// <see cref="AuthApopAsync"/> method and waiting for its completion.  
        /// This synchronous wrapper applies the configured <see cref="Timeout"/>
        /// value to limit the duration of the authentication sequence.
        /// </summary>
        /// <param name="user">
        /// The POP3 username supplied to the <c>APOP</c> command.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <param name="password">
        /// The POP3 password used to compute the APOP digest.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// response to the <c>APOP</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the session is already
        /// authenticated, or when the server greeting does not include an APOP
        /// unique-id.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="user"/> or <paramref name="password"/>
        /// is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>APOP</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around <see cref="AuthApopAsync"/>.
        /// It computes the APOP digest using the unique-id extracted from the
        /// server greeting and transitions the session into the TRANSACTION state
        /// upon successful authentication.
        /// </para>
        /// <para>
        /// Any exceptions raised during the asynchronous authentication sequence
        /// are propagated to the caller of this method.
        /// </para>
        /// </remarks>
        public POP3_ServerResponse AuthApop(string user,string password)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return AuthApopAsync(user,password,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method AuthApopAsync

        /// <summary>
        /// Authenticates the POP3 session using the APOP mechanism defined in
        /// RFC 1939 section 9.  
        /// The method computes the MD5 digest of the server's greeting
        /// timestamp concatenated with the user's password and sends the
        /// resulting value in the <c>APOP</c> command.
        /// </summary>
        /// <param name="user">
        /// The POP3 username supplied to the <c>APOP</c> command.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <param name="password">
        /// The POP3 password used to compute the APOP digest.  
        /// This value must not be null, empty, or whitespace.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations performed during the authentication sequence.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// response to the <c>APOP</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the session is already
        /// authenticated, or when the server greeting does not include an APOP
        /// timestamp.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="user"/> or <paramref name="password"/>
        /// is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>APOP</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The APOP mechanism avoids sending the password in plaintext by using
        /// a challenge-response digest:
        /// </para>
        /// <code>
        /// digest = MD5( &lt;timestamp&gt; + password )
        /// </code>
        /// <para>
        /// The timestamp is extracted from the server's greeting banner and is
        /// typically enclosed in angle brackets.  
        /// A successful <c>APOP</c> command transitions the session from the
        /// AUTHORIZATION state into the TRANSACTION state.
        /// </para>
        /// <para>
        /// For security reasons, the method masks the APOP command line in
        /// release-mode logging.
        /// </para>
        /// </remarks>
        public async ValueTask<POP3_ServerResponse> AuthApopAsync(string user,string password,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
				throw new InvalidOperationException("You must connect first.");
			}
			if(this.IsAuthenticated){
				throw new InvalidOperationException("Session is already authenticated.");
			}
            if(string.IsNullOrEmpty(this.ApopUniqueId)){
                throw new InvalidOperationException("Server greeting does not include an APOP timestamp; APOP authentication is not supported.");
            }
            if(string.IsNullOrWhiteSpace(user)){
                throw new ArgumentException("Argument 'user' may not be null or empty.",nameof(user));
            }
            if(string.IsNullOrWhiteSpace(password)){
                throw new ArgumentException("Argument 'password' may not be null or empty.",nameof(password));
            }

            /* RFC 1939 section 9 — POP3 APOP Authentication

               The APOP command provides an alternative authentication mechanism
               that avoids sending the password in plaintext.  
               APOP is only valid in the AUTHORIZATION state.

               Syntax:
                   APOP <username> <digest> CRLF

               Where:
                   <digest> = MD5( <timestamp> + <password> )
                   <timestamp> is extracted from the server greeting banner,
                   typically enclosed in angle brackets, e.g.:
                       +OK POP3 server ready <1896.697170952@dbc.mtview.ca.us>

               Possible Responses:
                   +OK  -ERR

               Discussion:
                   - The client must parse the server greeting and extract the
                     timestamp token.
                   - The client computes the MD5 digest of the concatenation of
                     the timestamp and the user's password.
                   - The server verifies the digest; if correct, the session
                     transitions to the TRANSACTION state.
                   - A failed APOP command leaves the session in the AUTHORIZATION
                     state.

               Examples:
                   S: +OK POP3 server ready <1896.697170952@dbc.mtview.ca.us>
                   C: APOP mrose c4c9334bac560ecc979e5801b3e22fb5
                   S: +OK Mailbox locked and ready

                   S: +OK POP3 server ready <1234.5678@host>
                   C: APOP bob 0123456789ABCDEF0123456789ABCDEF
                   S: -ERR Invalid digest
            */

            string cmdLine = "APOP " + user + " " + Net_Utils.ComputeMd5(m_ApopUniqueId + password,true) + "\r\n";

            await SendCommandLineAsync(cmdLine,false,cancellationToken);

            #if DEBUG
                LogAddWrite(cmdLine.Length,cmdLine);
            #else
                LogAddWrite(cmdLine.Length,"APOP <***REMOVED***>");
            #endif

            var serverResponse = await ReadResponseAsync(cancellationToken);            
            if(serverResponse.IsSuccess){
                m_pAuthdUserIdentity = new GenericIdentity(user,"APOP");

                return serverResponse;
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }
        }

        #endregion

        #region method AuthGetStrongestMethod

        /// <summary>
        /// Selects the strongest SASL authentication mechanism supported by both the
        /// POP3 server and this client. Mechanisms are evaluated in the following
        /// preference order: NTLM (when applicable), DIGEST-MD5, CRAM-MD5, LOGIN,
        /// and PLAIN.
        /// </summary>
        /// <param name="userName">
        /// The user name for authentication. For NTLM, this may include a domain
        /// prefix in the form <c>DOMAIN\username</c>.
        /// </param>
        /// <param name="password">
        /// The user password.
        /// </param>
        /// <returns>
        /// An <see cref="AUTH_SASL_Client"/> instance representing the strongest
        /// supported authentication mechanism.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="userName"/> or <paramref name="password"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if any argument contains an invalid value.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown if the POP3 server does not advertise any SASL mechanisms, or if
        /// none of the advertised mechanisms are supported by this client.
        /// </exception>
        public AUTH_SASL_Client AuthGetStrongestMethod(string userName,string password)
        {
            return AuthGetStrongestMethod(null,userName,password);
        }

        /// <summary>
        /// Selects the strongest SASL authentication mechanism supported by both the
        /// POP3 server and this client. Mechanisms are evaluated in the following
        /// preference order: NTLM (when applicable), DIGEST-MD5, CRAM-MD5, LOGIN,
        /// and PLAIN.
        /// </summary>
        /// <param name="domain">
        /// Optional domain name used by mechanisms that require it, such as NTLM.
        /// </param>
        /// <param name="userName">
        /// The user name for authentication. For NTLM, this may include a domain
        /// prefix in the form <c>DOMAIN\username</c>.
        /// </param>
        /// <param name="password">
        /// The user password.
        /// </param>
        /// <returns>
        /// An <see cref="AUTH_SASL_Client"/> instance representing the strongest
        /// supported authentication mechanism.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="userName"/> or <paramref name="password"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if any argument contains an invalid value.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown if the POP3 server does not advertise any SASL mechanisms, or if
        /// none of the advertised mechanisms are supported by this client.
        /// </exception>
        public AUTH_SASL_Client AuthGetStrongestMethod(string? domain,string userName,string password)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
				throw new InvalidOperationException("You must connect first.");
			}
            if(userName == null){
                throw new ArgumentNullException(nameof(userName));
            }
            if(password == null){
                throw new ArgumentNullException(nameof(password));
            }
            ArgumentNullException.ThrowIfNull(this.RemoteEndPoint);

            List<string> authMethods = new List<string>(this.SaslAuthMethods);
            if(authMethods.Count == 0){
                throw new NotSupportedException("POP3 server does not support authentication.");
            }
            else if(authMethods.Contains("NTLM") && (!string.IsNullOrEmpty(domain) || userName.IndexOf('\\') > -1)){
                if(!string.IsNullOrEmpty(domain)){
                    return new AUTH_SASL_Client_Ntlm(domain,userName, password);
                }
                else{
                    string[] domainUsername = userName.Split('\\');

                    return new AUTH_SASL_Client_Ntlm(domainUsername[0],domainUsername[1],password);
                }
            }
            else if(authMethods.Contains("DIGEST-MD5")){
                return new AUTH_SASL_Client_DigestMd5("POP3",this.RemoteEndPoint.Address.ToString(),userName,password);
            }
            else if(authMethods.Contains("CRAM-MD5")){
                return new AUTH_SASL_Client_CramMd5(userName,password);
            }
            else if(authMethods.Contains("LOGIN")){
                return new AUTH_SASL_Client_Login(userName,password);
            }
            else if(authMethods.Contains("PLAIN")){
                return new AUTH_SASL_Client_Plain(userName,password);
            }
            else{
                throw new NotSupportedException("We don't support any of the POP3 server authentication methods.");
            }
        }

        #endregion

        #region method Auth

        /// <summary>
        /// Performs POP3 SASL authentication using the <c>AUTH</c> command as
        /// defined in RFC 5034.  
        /// This synchronous wrapper invokes <see cref="AuthAsync"/> and blocks
        /// until the SASL authentication sequence completes.
        /// </summary>
        /// <param name="sasl">
        /// The SASL authentication mechanism to use. Any SASL client may be supplied.
        /// <see cref="AuthGetStrongestMethod(string?, string, string)"/> is the
        /// recommended helper, as it selects a strongest mechanism supported by the server.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// final <c>+OK</c> or <c>-ERR</c> response to the <c>AUTH</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the session is already
        /// authenticated.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="sasl"/> is null.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) final response
        /// to the <c>AUTH</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around <see cref="AuthAsync"/>.
        /// It performs SASL authentication using the POP3 <c>AUTH</c> command as
        /// specified in RFC 5034.  The client may send an initial response if the
        /// SASL mechanism supports it; otherwise the server issues continuation
        /// challenges beginning with the <c>'+'</c> character.
        /// </para>
        /// <para>
        /// The wrapper applies the configured <see cref="Timeout"/> value and
        /// waits for the asynchronous SASL exchange to complete.  Any exceptions
        /// raised during the asynchronous authentication sequence are propagated
        /// to the caller.
        /// </para>
        /// </remarks>
        public POP3_ServerResponse Auth(AUTH_SASL_Client sasl)
        {            
            using var cts = new CancellationTokenSource(this.Timeout);

            return AuthAsync(sasl,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method AuthAsync

        /// <summary>
        /// Performs POP3 SASL authentication using the <c>AUTH</c> command as
        /// defined in RFC 5034.  
        /// This method sends the selected SASL mechanism name, optionally
        /// includes an initial client response, and then processes any server
        /// continuation challenges until authentication succeeds or fails.
        /// </summary>
        /// <param name="sasl">
        /// The SASL authentication mechanism to use. Any SASL client may be supplied.
        /// <see cref="AuthGetStrongestMethod(string?, string, string)"/> is the
        /// recommended helper, as it selects a strongest mechanism supported by the server.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous POP3 I/O
        /// operations performed during the authentication sequence.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// final <c>+OK</c> or <c>-ERR</c> response to the <c>AUTH</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the session is already
        /// authenticated, or when the SASL client instance is invalid.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="sasl"/> is null.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the POP3 server unexpectedly closes the connection during
        /// the SASL exchange.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) final response
        /// to the <c>AUTH</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>AUTH</c> command enables SASL-based authentication as
        /// described in RFC 5034.  The client may send an initial response if
        /// the SASL mechanism supports it; otherwise the server issues a
        /// continuation challenge beginning with the <c>'+'</c> character.
        /// </para>
        /// <para>
        /// During authentication, the server may send one or more continuation
        /// responses of the form:
        /// </para>
        /// <code>
        /// + &lt;base64-challenge&gt;
        /// </code>
        /// <para>
        /// Each challenge is decoded and passed to the SASL mechanism via
        /// <see cref="AUTH_SASL_Client.Continue"/>.  The resulting client
        /// response is base64-encoded and sent back to the server.  The loop
        /// continues until the server returns either:
        /// </para>
        /// <list type="bullet">
        ///   <item><description><c>+OK</c> — authentication succeeded</description></item>
        ///   <item><description><c>-ERR</c> — authentication failed</description></item>
        /// </list>
        /// <para>
        /// Upon successful authentication, the session transitions from the
        /// AUTHORIZATION state into the TRANSACTION state, and the authenticated
        /// identity is stored in <c>m_pAuthdUserIdentity</c>.
        /// </para>
        /// <para>
        /// A SASL cancellation request is represented by a single byte containing
        /// <c>'='</c>, which is sent unencoded as the literal <c>"="</c>.
        /// </para>
        /// </remarks>
        public async ValueTask<POP3_ServerResponse> AuthAsync(AUTH_SASL_Client sasl,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
				throw new InvalidOperationException("You must connect first.");
			}
            if(this.IsAuthenticated){
                throw new InvalidOperationException("Connection is already authenticated.");
            }
            if(sasl == null){
                throw new ArgumentNullException(nameof(sasl));
            }

            /* RFC 5034 — POP3 SASL Authentication (AUTH Command)

               The AUTH command provides a mechanism for POP3 clients to perform
               SASL-based authentication instead of the traditional USER/PASS or
               APOP methods.  AUTH is only valid in the AUTHORIZATION state.

               Syntax:
                   AUTH <mechanism> [initial-response] CRLF

               Where:
                   <mechanism> is a SASL mechanism name registered with IANA,
                   such as PLAIN, LOGIN, CRAM-MD5, SCRAM-SHA-1, etc.

                   [initial-response] is optional and may contain a base64-encoded
                   initial client response if the mechanism supports it.

               Possible Server Responses:
                   +OK
                   -ERR
                   + <base64-challenge>

               Discussion:
                   - The client issues AUTH with the desired SASL mechanism.
                   - If the mechanism supports an initial response, it may be sent
                     on the same line; otherwise the server will issue a challenge.
                   - The client and server exchange base64-encoded challenge/response
                     data until the mechanism completes.
                   - Upon successful authentication, the session transitions to the
                     TRANSACTION state.
                   - A failed AUTH command leaves the session in the AUTHORIZATION
                     state.

               Capability Advertisement:
                   Servers indicate supported SASL mechanisms via the CAPA command:
                       SASL <mechanism1> <mechanism2> ...

               Examples:
                   S: +OK POP3 server ready
                   C: CAPA
                   S: SASL PLAIN LOGIN
                   C: AUTH PLAIN AGFkbWluAGFkbWlu
                   S: +OK Authentication successful

                   S: +OK POP3 server ready
                   C: AUTH LOGIN
                   S: + VXNlcm5hbWU6
                   C: YWxpY2U=
                   S: + UGFzc3dvcmQ6
                   C: c2VjcmV0
                   S: +OK Logged in
            */

            string authCommand;
            if(sasl.SupportsInitialResponse){
                authCommand = "AUTH " + sasl.Name + " " + Convert.ToBase64String(sasl.Continue(null)) + "\r\n";
            }
            else{
                authCommand = "AUTH " + sasl.Name + "\r\n";
            }

            await SendCommandLineAsync(authCommand,false,cancellationToken);
            #if DEBUG
                LogAddWrite(authCommand.Length,authCommand.Trim());
            #else
                LogAddWrite(authCommand.Length,"Client response sent.");
            #endif

            while(true){
                ReadLineResult serverResponse = await this.TcpStream.ReadLineAsync(new byte[8000],SizeExceededAction.JunkAndThrowException,cancellationToken);
                if(serverResponse.LineBytesInBuffer == 0){
                    throw new IOException("POP3 server closed connection.");
                }
                string serverResponseText = serverResponse.LineUtf8!;             
                LogAddRead(serverResponse.BytesInBuffer,serverResponseText);
                
                // Authentication suceeded.
                if(serverResponseText.StartsWith("+OK",StringComparison.OrdinalIgnoreCase)){
                    m_pAuthdUserIdentity = new GenericIdentity(sasl.UserName,sasl.Name);

                    return POP3_ServerResponse.Parse(serverResponseText);
                }
                // Continue authenticating.
                else if(serverResponseText.StartsWith("+ ")){
                    // + base64Data, we need to decode it and pass to SASL auth mechanism.

                    // Pass server response to SASL authentication.
                    byte[] saslResponse = sasl.Continue(Convert.FromBase64String(serverResponseText.Substring(1).Trim()));
                    
                    string clientResponse;
                    // SASL auth requested canel. Cancel reply is not encoded single token '='.
                    if(saslResponse.Length == 1 && saslResponse[0] == '='){
                        clientResponse = "=";
                    }
                    else{
                        clientResponse = Convert.ToBase64String(saslResponse);
                    }

                    // We need just send SASL authentication returned auth-response as base64.
                    await SendCommandLineAsync(clientResponse + "\r\n",false,cancellationToken);
                    #if DEBUG
                        LogAddWrite(clientResponse.Length,clientResponse);
                    #else
                        LogAddWrite(clientResponse.Length,"Client response sent.");
                    #endif
                }
                else{
                    throw new POP3_ClientException(POP3_ServerResponse.Parse(serverResponseText));
                }
            }
        }

        #endregion
                
        #region method Noop

        /// <summary>
        /// Sends the POP3 <c>NOOP</c> command to verify that the server is
        /// responsive without performing any mailbox operations.  
        /// This synchronous wrapper invokes <see cref="NoopAsync"/> and blocks
        /// until the server returns its response.
        /// </summary>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// final <c>+OK</c> or <c>-ERR</c> response to the <c>NOOP</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>NOOP</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>NOOP</c> command is defined in RFC 1939.  
        /// It performs no action on the server and does not alter session state,
        /// message flags, or mailbox contents.  Its primary purpose is to act as
        /// a lightweight keep‑alive operation or to confirm that the server is
        /// still responsive during idle periods.
        /// </para>
        /// <para>
        /// This method applies the configured <see cref="Timeout"/> and waits
        /// for the asynchronous NOOP operation to complete.  Any exceptions
        /// raised during the asynchronous sequence are propagated to the caller.
        /// </para>
        /// </remarks>
        public POP3_ServerResponse Noop()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return NoopAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method NoopAsync

        /// <summary>
        /// Sends the POP3 <c>NOOP</c> command to verify that the server is
        /// responsive without performing any mailbox operations.  
        /// This command is valid in both the AUTHORIZATION and TRANSACTION
        /// states as defined in RFC 1939.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations performed during the NOOP command.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// response to the <c>NOOP</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>NOOP</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>NOOP</c> command is defined in RFC 1939.  
        /// It performs no action on the server and does not alter session state,
        /// message flags, or mailbox contents.  Its primary purpose is to act as
        /// a lightweight keep‑alive operation or to confirm that the server is
        /// still responsive during idle periods.
        /// </para>
        /// <para>
        /// A successful NOOP command returns <c>+OK</c>.  
        /// A negative response (<c>-ERR</c>) indicates that the server cannot
        /// continue the session and the client should close the connection.
        /// </para>
        /// </remarks>
        public async ValueTask<POP3_ServerResponse> NoopAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            /* RFC 1939 — POP3 NOOP Command

               The NOOP command does not perform any action on the POP3 server.
               Its only purpose is to keep the connection alive or verify that the
               server is still responsive.  NOOP is valid in both the AUTHORIZATION
               and TRANSACTION states.

               Syntax:
                   NOOP CRLF

               Possible Responses:
                   +OK
                   -ERR

               Discussion:
                   - The server must always return a response to NOOP.
                   - A successful NOOP command does not alter the session state,
                     mailbox contents, or any message flags.
                   - Clients typically use NOOP as a keep-alive mechanism during
                     long-running operations or idle periods.
                   - A negative response (-ERR) indicates that the server is unable
                     to continue the session, and the client should close the
                     connection.

               Example:
                   C: NOOP
                   S: +OK
            */


            await SendCommandLineAsync("NOOP\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);
            if(!serverResponse.IsSuccess){
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion


        #region method Utf8

        /// <summary>
        /// Executes the POP3 <c>UTF8</c> command synchronously and enables UTF-8
        /// mode for the current session. This method blocks until the operation
        /// completes or the configured timeout expires.
        /// </summary>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> containing the server's response
        /// to the <c>UTF8</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around <see cref="Utf8Async(CancellationToken)"/>.
        /// It uses the client's <see cref="Timeout"/> value to limit how long the
        /// operation may block.
        /// </para>
        /// <para>
        /// UTF-8 mode is defined in RFC 6856. When enabled, the server may return
        /// UTF-8 encoded response text and UTF-8 message headers. If the server
        /// advertises the <c>UTF8=RESP-CODES</c> capability, response text following
        /// <c>+OK</c> and <c>-ERR</c> may contain UTF-8 characters. If the server
        /// advertises <c>UTF8=USER</c>, UTF-8 usernames and passwords may be used
        /// with the <c>USER</c> and <c>PASS</c> commands.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown if the operation does not complete within the configured timeout.
        /// </exception>
        public POP3_ServerResponse Utf8()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return Utf8Async(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method Utf8Async

        /// <summary>
        /// Executes the POP3 <c>UTF8</c> command and enables UTF-8 mode for the
        /// current session. When enabled, the server may return UTF-8 encoded
        /// response text and UTF-8 message headers as defined in RFC 6856.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> containing the server's response
        /// to the <c>UTF8</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The server must advertise the <c>UTF8</c> capability via the CAPA
        /// command before UTF-8 mode can be enabled. If the server also advertises
        /// <c>UTF8=RESP-CODES</c>, response text following <c>+OK</c> and
        /// <c>-ERR</c> may contain UTF-8 characters. If the server advertises
        /// <c>UTF8=USER</c>, UTF-8 usernames and passwords may be used with the
        /// <c>USER</c> and <c>PASS</c> commands.
        /// </para>
        /// <para>
        /// UTF-8 mode affects only response text and message headers. Message
        /// bodies are transmitted as raw octets and are not modified by UTF-8
        /// mode. Dot-stuffing rules for multi-line responses remain unchanged.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        public async ValueTask<POP3_ServerResponse> Utf8Async(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("UTF8 is valid only in the TRANSACTION state.");
            }

            /* RFC 6856 — POP3 Support for UTF-8

               The POP3 UTF8 command enables UTF-8 mode for the current session.
               When enabled, the server may return UTF-8 encoded response text,
               UTF-8 message headers, and UTF-8 capability listings.

               UTF8 is valid only in the TRANSACTION state.

               Capability Keywords (advertised via CAPA):
                   UTF8
                       The server supports UTF-8 mode. The client may issue the UTF8
                       command to enable UTF-8 responses and UTF-8 message headers.

                   UTF8=RESP-CODES
                       The server may include UTF-8 text in response lines following
                       +OK and -ERR. Without this capability, response text must be
                       strictly ASCII.

                   UTF8=USER
                       The server accepts UTF-8 encoded arguments to the USER and PASS
                       commands. Without this capability, USER/PASS arguments must be
                       ASCII only.

               Command:
                   UTF8 CRLF

               Possible Responses:
                   +OK
                   -ERR

               Discussion:
                   - The UTF8 command enables UTF-8 mode for the session.
                   - When UTF-8 mode is active, the server may return UTF-8 encoded
                     message headers in RETR and TOP responses.
                   - If UTF8=RESP-CODES is advertised, response text following +OK or
                     -ERR may contain UTF-8 characters.
                   - If UTF8=USER is advertised, the client may send UTF-8 usernames
                     and passwords.
                   - The server must continue to use the POP3 multi-line format,
                     including dot-stuffing rules, regardless of encoding.
                   - Message bodies are transmitted as raw octets and are not modified
                     by UTF-8 mode.

               Examples:
                   C: CAPA
                   S: +OK Capability list follows
                   S: TOP
                   S: UIDL
                   S: UTF8
                   S: UTF8=RESP-CODES
                   S: UTF8=USER
                   S: .
                   C: UTF8
                   S: +OK UTF-8 mode enabled

                   C: USER üsername
                   S: +OK
                   C: PASS pąsswörd
                   S: +OK Logged in

                   C: RETR 1
                   S: +OK message follows
                   S: From: Jürgen <jurgen@example.com>
                   S: Subject: UTF-8 test ✓
                   S: <message body>
                   S: .
            */


            await SendCommandLineAsync("UTF8\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);
            if(serverResponse.IsSuccess){
                m_Utf8Enabled = true;
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion   

        #region method Rset

        /// <summary>
        /// Performs a POP3 session reset by issuing the <c>RSET</c> command as
        /// defined in RFC 1939.  
        /// This synchronous wrapper invokes <see cref="RsetAsync"/> and blocks
        /// until the server responds, clearing all message deletion marks set
        /// during the current TRANSACTION state.
        /// </summary>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// final <c>+OK</c> or <c>-ERR</c> response to the <c>RSET</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the session is not
        /// yet authenticated.  The POP3 RSET command is valid only in the
        /// TRANSACTION state as defined in RFC 1939.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>RSET</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>RSET</c> command resets the session's state by clearing
        /// all in‑session deletion flags.  It does not modify the mailbox on the
        /// server; it only restores all messages to their original (undeleted)
        /// state within the client.
        /// </para>
        /// <para>
        /// This method applies the configured <see cref="Timeout"/> and waits
        /// for the asynchronous reset operation to complete.  Any exceptions
        /// raised during the asynchronous sequence are propagated to the caller.
        /// </para>
        /// </remarks>
		public POP3_ServerResponse Rset()
		{
			using var cts = new CancellationTokenSource(this.Timeout);

            return RsetAsync(cts.Token).GetAwaiter().GetResult();
		}

		#endregion

        #region method RsetAsync

        /// <summary>
        /// Sends the POP3 <c>RSET</c> command to reset the session's state by
        /// clearing all message deletion marks set during the current
        /// TRANSACTION state.  
        /// This synchronous wrapper invokes <see cref="RsetAsync"/> and blocks
        /// until the server responds.
        /// </summary>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the server's
        /// final <c>+OK</c> or <c>-ERR</c> response to the <c>RSET</c> command.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the session is not
        /// yet authenticated.  The POP3 RSET command is valid only in the
        /// TRANSACTION state as defined in RFC 1939.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative (<c>-ERR</c>) response to
        /// the <c>RSET</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The POP3 <c>RSET</c> command is defined in RFC 1939.  
        /// It clears all in‑session deletion marks and restores all messages to
        /// their original (undeleted) state.  The command does not modify the
        /// mailbox on the server; it only resets the client's in‑memory state.
        /// </para>
        /// <para>
        /// This method applies the configured <see cref="Timeout"/> and waits
        /// for the asynchronous operation to complete.  Any exceptions raised
        /// during the asynchronous reset sequence are propagated to the caller.
        /// </para>
        /// </remarks>
        public async ValueTask<POP3_ServerResponse> RsetAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("RSET is valid only in the TRANSACTION state.");
            }

            /* RFC 1939 — POP3 RSET Command

               The RSET command unconditionally resets the POP3 session's state.
               Any message deletion marks set during the current TRANSACTION state
               are cleared, restoring all messages to their original (undeleted)
               state.  RSET does not affect the contents of the mailbox on the
               server; it only resets the session's in‑memory deletion flags.

               RSET is valid only in the TRANSACTION state.

               Syntax:
                   RSET CRLF

               Possible Responses:
                   +OK
                   -ERR

               Discussion:
                   - The client uses RSET to abandon any pending deletions before
                     issuing QUIT or continuing with other operations.
                   - After RSET, all messages previously marked for deletion are
                     unmarked.
                   - RSET does not re-read the mailbox or alter message numbers.
                   - A negative response (-ERR) indicates that the server cannot
                     reset the session state.

               Example:
                   C: RSET
                   S: +OK Reset state
            */

            await SendCommandLineAsync("RSET\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);
            if(serverResponse.IsSuccess){
                if(m_pMessages != null){
                    foreach(POP3_ClientMessage message in m_pMessages){
                        message.SetMarkedForDeletion(false);
                    }
                }                
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion

        #region method List

        /// <summary>
        /// Executes the POP3 <c>LIST</c> command synchronously and returns a scan
        /// listing of all messages in the mailbox. This method is a blocking wrapper
        /// around <see cref="ListAsync"/>.
        /// </summary>
        /// <returns>
        /// An array of <see cref="POP3_t_List_Item"/> objects representing the
        /// message-number and size pairs returned by the POP3 server.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method invokes <see cref="ListAsync"/> and blocks the calling thread
        /// until the operation completes. It is intended for environments where
        /// asynchronous execution is not desirable or not supported.
        /// </para>
        /// <para>
        /// The underlying POP3 <c>LIST</c> command is defined in RFC 1939 and returns
        /// a multi-line listing of message numbers and their sizes in octets.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the POP3 server closes the connection unexpectedly.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        /// <exception cref="POP3_ProtocolException">
        /// Thrown when the server returns malformed LIST data or violates POP3
        /// protocol rules.
        /// </exception>
        public POP3_t_List_Item[] List()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return ListAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method ListAsync

        /// <summary>
        /// Executes the POP3 <c>LIST</c> command and retrieves a scan listing of all
        /// messages in the mailbox. The listing contains the POP3 message number and
        /// the octet size of each message as reported by the server.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// An array of <see cref="POP3_t_List_Item"/> objects representing the
        /// message-number and size pairs returned by the POP3 server.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>LIST</c> command is defined in RFC 1939. When issued without
        /// arguments, the server responds with a multi-line listing where each line
        /// contains a message number and its size in octets. The final line of the
        /// response is a single period character.
        /// </para>
        /// <para>
        /// This method may only be called while the client is in the POP3
        /// TRANSACTION state. The connection must be established and the user must
        /// be authenticated.
        /// </para>
        /// <para>
        /// Malformed server responses, invalid numeric values, or protocol violations
        /// result in a <see cref="POP3_ProtocolException"/>.
        /// Server-side negative responses (<c>-ERR</c>) result in a
        /// <see cref="POP3_ClientException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the POP3 server closes the connection unexpectedly.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        /// <exception cref="POP3_ProtocolException">
        /// Thrown when the server returns malformed LIST data or violates POP3
        /// protocol rules.
        /// </exception>
        public async ValueTask<POP3_t_List_Item[]> ListAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("LIST is valid only in the TRANSACTION state.");
            }

            /* RFC 1939 — POP3 LIST Command

               The LIST command returns information about messages in the mailbox.
               When issued without arguments, the server responds with a multi-line
               listing containing the message number and octet size for each message
               in the mailbox. When issued with a message number argument, the server
               returns a single-line response containing the size of that specific
               message.

               LIST is valid only in the TRANSACTION state.

               Syntax:
                   LIST CRLF
                   LIST <msg-number> CRLF

               Possible Responses:
                   +OK
                   -ERR

               Multi-Line Response Format (no argument):
                   +OK scan listing follows
                   <msg-number> <size>
                   <msg-number> <size>
                   ...
                   .

               Single-Line Response Format (with argument):
                   +OK <msg-number> <size>
                   -ERR no such message

               Discussion:
                   - LIST without arguments returns a scan listing of all messages.
                   - LIST with a message number returns only the size of that message.
                   - The scan listing does not include message headers or content.
                   - The final line of a multi-line response is a single period (".").
                   - A negative response (-ERR) indicates an invalid message number
                     or that the server cannot provide the listing.

               Examples:
                   C: LIST
                   S: +OK scan listing follows
                   S: 1 1204
                   S: 2 850
                   S: 3 498
                   S: .

                   C: LIST 2
                   S: +OK 2 850
            */
            List<POP3_t_List_Item> retVal = new List<POP3_t_List_Item>();

            await SendCommandLineAsync("LIST\r\n",true,cancellationToken);
            var serverResponse = await ReadResponseAsync(cancellationToken);            
            if(serverResponse.IsSuccess){
                Memory<byte> lineBuffer = new byte[8000];
                long         totalBytes = 0;
                long         totalItems = 0;
                while(true){
                    ReadLineResult responseline = await this.TcpStream.ReadLineAsync(lineBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
                    // Server closed connection.
                    if(responseline.BytesInBuffer == 0){
                        throw new IOException("POP3 server closed connection.");
                    }
                    else{
                        totalBytes += responseline.BytesInBuffer;

                        string line = responseline.LineUtf8 ?? ""; 
                        if(line == "."){
                            LogAddRead(totalBytes,$"Mailbox scan listing received. Total messages: {totalItems}.");

                            return retVal.ToArray();
                        }
                        else{
                            totalItems++;
                            if(totalItems > 100000){
                                throw new POP3_ProtocolException($"LIST response exceeded maximum allowed entries ({totalItems}).");
                            }

                            string[] parts = line.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
                            if(parts.Length != 2){
                                throw new POP3_ProtocolException($"Invalid LIST entry: \"{line}\"");
                            }
                            if(!int.TryParse(parts[0], out int msgNumber) || msgNumber <= 0){
                                throw new POP3_ProtocolException($"Invalid LIST message number: \"{parts[0]}\"");
                            }
                            if(!int.TryParse(parts[1], out int sizeInBytes) || sizeInBytes < 0){
                                throw new POP3_ProtocolException($"Invalid LIST size value: \"{parts[1]}\"");
                            }
                            
                            retVal.Add(new POP3_t_List_Item(msgNumber,sizeInBytes));
                        }
                    }
                }                
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }            
        }

        #endregion

        #region method Uidl

        /// <summary>
        /// Executes the POP3 <c>UIDL</c> command synchronously and returns a listing
        /// of unique message identifiers for all messages in the mailbox. This method
        /// is a blocking wrapper around <see cref="UidlAsync"/>.
        /// </summary>
        /// <returns>
        /// An array of <see cref="POP3_t_Uidl_Item"/> objects representing the
        /// message-number and unique-identifier pairs returned by the POP3 server.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method invokes <see cref="UidlAsync"/> and blocks the calling thread
        /// until the operation completes. It is intended for environments where
        /// asynchronous execution is not desirable or not supported.
        /// </para>
        /// <para>
        /// The underlying POP3 <c>UIDL</c> command is defined in RFC 1939 and returns
        /// a multi-line listing of message numbers and their unique identifiers. The
        /// final line of the response is a single period character.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the POP3 server closes the connection unexpectedly.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        /// <exception cref="POP3_ProtocolException">
        /// Thrown when the server returns malformed UIDL data or violates POP3
        /// protocol rules.
        /// </exception>
        public POP3_t_Uidl_Item[] Uidl()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return UidlAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method UidlAsync

        /// <summary>
        /// Executes the POP3 <c>UIDL</c> command and retrieves a listing of unique
        /// message identifiers for all messages in the mailbox. The listing contains
        /// the POP3 message number and the server-defined unique identifier for each
        /// message.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// An array of <see cref="POP3_t_Uidl_Item"/> objects representing the
        /// message-number and unique-identifier pairs returned by the POP3 server.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>UIDL</c> command is defined in RFC 1939. When issued without
        /// arguments, the server responds with a multi-line listing where each line
        /// contains a message number and its unique identifier. The final line of the
        /// response is a single period character.
        /// </para>
        /// <para>
        /// Unique identifiers are opaque server-defined strings that remain stable
        /// across sessions and allow clients to track which messages have already
        /// been downloaded. The format of the unique identifier is not specified by
        /// the POP3 protocol and may contain any printable characters except
        /// whitespace.
        /// </para>
        /// <para>
        /// This method may only be called while the client is in the POP3
        /// TRANSACTION state. The connection must be established and the user must
        /// be authenticated.
        /// </para>
        /// <para>
        /// Malformed server responses, invalid numeric values, or protocol violations
        /// result in a <see cref="POP3_ProtocolException"/>. Server-side negative
        /// responses (<c>-ERR</c>) result in a <see cref="POP3_ClientException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the POP3 server closes the connection unexpectedly.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        /// <exception cref="POP3_ProtocolException">
        /// Thrown when the server returns malformed UIDL data or violates POP3
        /// protocol rules.
        /// </exception>
        public async ValueTask<POP3_t_Uidl_Item[]> UidlAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("UIDL is valid only in the TRANSACTION state.");
            }

            /* RFC 1939 — POP3 UIDL Command

               The UIDL command returns a listing of unique message identifiers
               assigned by the POP3 server. These identifiers are stable across
               sessions and allow clients to track which messages have already
               been downloaded.

               UIDL is valid only in the TRANSACTION state.

               Syntax:
                   UIDL CRLF
                   UIDL <msg-number> CRLF

               Possible Responses:
                   +OK
                   -ERR

               Multi-Line Response Format (no argument):
                   +OK unique-id listing follows
                   <msg-number> <unique-id>
                   <msg-number> <unique-id>
                   ...
                   .

               Single-Line Response Format (with argument):
                   +OK <msg-number> <unique-id>
                   -ERR no such message

               Discussion:
                   - UIDL without arguments returns a list of all messages and
                     their unique identifiers.
                   - UIDL with a message number returns only the unique identifier
                     for that specific message.
                   - Unique identifiers are server-defined opaque strings and may
                     contain any printable characters except whitespace.
                   - The final line of a multi-line response is a single period (.).
                   - A negative response (-ERR) indicates an invalid message number
                     or that the server cannot provide the UIDL listing.

               Examples:
                   C: UIDL
                   S: +OK unique-id listing follows
                   S: 1 whqtswO00WBw418f9t5JxYwZ
                   S: 2 QhdPYR:00WBw1Ph7x7
                   S: 3 8uZp9Pz00WBw1Ph7x7
                   S: .

                   C: UIDL 2
                   S: +OK 2 QhdPYR:00WBw1Ph7x7
            */

            List<POP3_t_Uidl_Item> retVal = new List<POP3_t_Uidl_Item>();

            await SendCommandLineAsync("UIDL\r\n",true,cancellationToken);
            var serverResponse = await ReadResponseAsync(cancellationToken);            
            if(serverResponse.IsSuccess){
                Memory<byte> lineBuffer = new byte[8000];
                long         totalBytes = 0;
                long         totalItems = 0;
                while(true){
                    ReadLineResult responseline = await this.TcpStream.ReadLineAsync(lineBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
                    // Server closed connection.
                    if(responseline.BytesInBuffer == 0){
                        throw new IOException("POP3 server closed connection.");
                    }
                    else{
                        totalBytes += responseline.BytesInBuffer;

                        string line = responseline.LineUtf8 ?? ""; 
                        if(line == "."){
                            LogAddRead(totalBytes,$"Mailbox scan listing received. Total messages: {totalItems}.");

                            return retVal.ToArray();
                        }
                        else{
                            totalItems++;
                            if(totalItems > 100000){
                                throw new POP3_ProtocolException($"UIDL response exceeded maximum allowed entries ({totalItems}).");
                            }

                            string[] parts = line.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
                            if(parts.Length != 2){
                                throw new POP3_ProtocolException($"Invalid UIDL entry: \"{line}\"");
                            }
                            if(!int.TryParse(parts[0], out int msgNumber) || msgNumber <= 0){
                                throw new POP3_ProtocolException($"Invalid UIDL message number: \"{parts[0]}\"");
                            }
                            string uniqueId = parts[1];
                            
                            retVal.Add(new POP3_t_Uidl_Item(msgNumber,uniqueId));
                        }
                    }
                }                
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }            
        }

        #endregion

        #region method Top

        /// <summary>
        /// Executes the POP3 <c>TOP</c> command synchronously and retrieves the
        /// message headers and the specified number of body lines. The data is
        /// written to the provided <see cref="Stream"/>.
        /// </summary>
        /// <param name="messageNumber">
        /// The 1-based POP3 message number of the message to retrieve.
        /// </param>
        /// <param name="lineCount">
        /// The number of body lines to return after the message headers. A value
        /// of zero returns only the headers.
        /// </param>
        /// <param name="stream">
        /// The destination stream to which the retrieved message data will be
        /// written. The stream must be writable.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of bytes allowed to be written to the destination
        /// stream. If the retrieved data exceeds this limit, a size-related
        /// exception may be thrown depending on the configured overflow action.
        /// The value must be at least 64 000 bytes.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> representing the server's initial
        /// response to the <c>TOP</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is a blocking wrapper around <see cref="TopAsync"/> and is
        /// intended for environments where asynchronous execution is not
        /// desirable.
        /// </para>
        /// <para>
        /// The <c>TOP</c> command retrieves the message headers and the first N
        /// lines of the message body, where N is the line-count argument. The
        /// server returns the data using POP3 multi-line format, including
        /// dot-stuffing rules.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="messageNumber"/> is less than 1.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="lineCount"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than 64000.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the retrieved data exceeds <paramref name="maxCount"/> and
        /// the overflow action is configured to throw.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        public POP3_ServerResponse Top(int messageNumber,int lineCount,Stream stream,long maxCount)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return TopAsync(messageNumber,lineCount,stream,maxCount,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method TopAsync

        /// <summary>
        /// Executes the POP3 <c>TOP</c> command and retrieves the message headers
        /// and the specified number of body lines. The data is written to the
        /// provided <see cref="Stream"/>.
        /// </summary>
        /// <param name="messageNumber">
        /// The 1-based POP3 message number of the message to retrieve.
        /// </param>
        /// <param name="lineCount">
        /// The number of body lines to return after the message headers. A value
        /// of zero returns only the headers.
        /// </param>
        /// <param name="stream">
        /// The destination stream to which the retrieved message data will be
        /// written. The stream must be writable.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of bytes allowed to be written to the destination
        /// stream. If the retrieved data exceeds this limit, a size-related
        /// exception may be thrown depending on the configured overflow action.
        /// The value must be at least 64 000 bytes.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> representing the server's initial
        /// response to the <c>TOP</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>TOP</c> command is defined in RFC 1939. It retrieves the message
        /// headers and the first N lines of the message body, where N is the
        /// line-count argument. The server returns the data using POP3 multi-line
        /// format, including dot-stuffing rules.
        /// </para>
        /// <para>
        /// The terminating line of the response is a single period character.
        /// This method automatically handles dot-stuffing and writes the decoded
        /// data to the provided stream.
        /// </para>
        /// <para>
        /// This method may only be called while the client is in the POP3
        /// TRANSACTION state. The connection must be established and the user
        /// must be authenticated.
        /// </para>
        /// <para>
        /// A negative server response (<c>-ERR</c>) results in a
        /// <see cref="POP3_ClientException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="messageNumber"/> is less than 1.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="lineCount"/> is less than 0.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than 64000.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the retrieved data exceeds <paramref name="maxCount"/> and
        /// the overflow action is configured to throw.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        public async ValueTask<POP3_ServerResponse> TopAsync(int messageNumber,int lineCount,Stream stream,long maxCount,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("TOP is valid only in the TRANSACTION state.");
            }
            if(messageNumber < 1){
                throw new ArgumentException("Message number must be >= 1.",nameof(messageNumber));
            }
            if(lineCount < 0){
                throw new ArgumentException("Line count must be >= 0.",nameof(lineCount));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(maxCount < 64000){
                throw new ArgumentException("Argument 'maxCount' must be >= 64000.");
            }

            /* RFC 1939 — POP3 TOP Command

               The TOP command retrieves the message headers and a specified number
               of lines from the message body. The server responds with a multi-line
               reply containing the headers, followed by the requested number of body
               lines. The final line of the response is a single period character.

               TOP is valid only in the TRANSACTION state.

               Syntax:
                   TOP msg-number line-count CRLF

               Possible Responses:
                   +OK
                   -ERR

               Multi-Line Response Format:
                   +OK top of message follows
                   <message headers>
                   <requested number of body lines>
                   .

               Discussion:
                   - TOP returns the message headers and the first N lines of the
                     message body, where N is the line-count argument.
                   - The message is transmitted using the POP3 multi-line format,
                     including dot-stuffing rules.
                   - The terminating line of the response is a single period (.).
                   - A negative response (-ERR) indicates an invalid message number
                     or that the server cannot return the requested portion.

               Examples:
                   C: TOP 1 10
                   S: +OK top of message follows
                   S: From: someone@example.com
                   S: Subject: Hello
                   S: <first 10 lines of body>
                   S: .
            */

            await SendCommandLineAsync($"TOP {messageNumber} {lineCount}\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);
            if(serverResponse.IsSuccess){
                int countStored = await this.TcpStream.ReadPeriodTerminatedAsync(stream,maxCount,64000,SizeExceededAction.JunkAndThrowException,cancellationToken);
                LogAddWrite(countStored,$"Top of message {messageNumber} retrieved ({countStored} bytes).");
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion

        #region method Retr

        /// <summary>
        /// Executes the POP3 <c>RETR</c> command synchronously and retrieves the
        /// full contents of the specified message. The message headers and body
        /// are written to the provided <see cref="Stream"/>.
        /// </summary>
        /// <param name="messageNumber">
        /// The 1-based POP3 message number of the message to retrieve.
        /// </param>
        /// <param name="stream">
        /// The destination stream to which the retrieved message data will be
        /// written. The stream must be writable.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of bytes allowed to be written to the destination
        /// stream. If the retrieved data exceeds this limit, a size-related
        /// exception may be thrown depending on the configured overflow action.
        /// The value must be at least 64 000 bytes.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> representing the server's initial
        /// response to the <c>RETR</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is a blocking wrapper around <see cref="RetrAsync"/> and
        /// is intended for environments where asynchronous execution is not
        /// desirable.
        /// </para>
        /// <para>
        /// The <c>RETR</c> command retrieves the entire message, including all
        /// headers and the full body, using POP3 multi-line format. Dot-stuffing
        /// is automatically handled by the underlying stream reader.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="messageNumber"/> is less than 1.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than 64 000.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the retrieved data exceeds <paramref name="maxCount"/> and
        /// the overflow action is configured to throw.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        public POP3_ServerResponse Retr(int messageNumber,Stream stream,long maxCount)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return RetrAsync(messageNumber,stream,maxCount,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method RetrAsync

        /// <summary>
        /// Executes the POP3 <c>RETR</c> command and retrieves the full contents
        /// of the specified message. The message headers and body are written to
        /// the provided <see cref="Stream"/>.
        /// </summary>
        /// <param name="messageNumber">
        /// The 1-based POP3 message number of the message to retrieve.
        /// </param>
        /// <param name="stream">
        /// The destination stream to which the retrieved message data will be
        /// written. The stream must be writable.
        /// </param>
        /// <param name="maxCount">
        /// The maximum number of bytes allowed to be written to the destination
        /// stream. If the retrieved data exceeds this limit, a size-related
        /// exception may be thrown depending on the configured overflow action.
        /// The value must be at least 64 000 bytes.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> representing the server's initial
        /// response to the <c>RETR</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>RETR</c> command is defined in RFC 1939. It retrieves the full
        /// message content, including all headers and the complete body. The
        /// server returns the message using POP3 multi-line format, where lines
        /// beginning with a period are dot-stuffed.
        /// </para>
        /// <para>
        /// The terminating line of the message data is a single period character.
        /// This method automatically handles dot-stuffing and writes the decoded
        /// message data to the provided stream.
        /// </para>
        /// <para>
        /// This method may only be called while the client is in the POP3
        /// TRANSACTION state. The connection must be established and the user
        /// must be authenticated.
        /// </para>
        /// <para>
        /// A negative server response (<c>-ERR</c>) results in a
        /// <see cref="POP3_ClientException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="messageNumber"/> is less than 1.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="maxCount"/> is less than 64 000.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the retrieved data exceeds <paramref name="maxCount"/> and
        /// the overflow action is configured to throw.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        public async ValueTask<POP3_ServerResponse> RetrAsync(int messageNumber,Stream stream,long maxCount,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("RETR is valid only in the TRANSACTION state.");
            }
            if(messageNumber < 1){
                throw new ArgumentException("Message number must be >= 1.",nameof(messageNumber));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            if(maxCount < 64000){
                throw new ArgumentException("Argument 'maxCount' must be >= 64000.");
            }

            /* RFC 1939 — POP3 RETR Command

               The RETR command retrieves the full contents of a message. The server
               responds with a multi-line reply containing the message headers and
               body. The final line of the response is a single period character.

               RETR is valid only in the TRANSACTION state.

               Syntax:
                   RETR msg-number CRLF

               Possible Responses:
                   +OK
                   -ERR

               Multi-Line Response Format:
                   +OK message follows
                   <message data>
                   .
   
               Discussion:
                   - RETR returns the entire message, including all headers and the
                     full body.
                   - The message is transmitted using the POP3 multi-line format,
                     where lines beginning with a period are dot-stuffed by the
                     server.
                   - The terminating line of the response is a single period (.).
                   - A negative response (-ERR) indicates an invalid message number
                     or that the server cannot retrieve the message.

               Examples:
                   C: RETR 1
                   S: +OK 120 octets
                   S: From: someone@example.com
                   S: Subject: Hello
                   S: <message body>
                   S: .
            */

            await SendCommandLineAsync($"RETR {messageNumber}\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);
            if(serverResponse.IsSuccess){
                int countStored = await this.TcpStream.ReadPeriodTerminatedAsync(stream,maxCount,64000,SizeExceededAction.JunkAndThrowException,cancellationToken);
                LogAddWrite(countStored,$"Message {messageNumber} retrieved ({countStored} bytes).");
            }
            else{
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion

        #region method Dele

        /// <summary>
        /// Executes the POP3 <c>DELE</c> command synchronously and marks the
        /// specified message for deletion. This method is a blocking wrapper
        /// around <see cref="DeleAsync"/>.
        /// </summary>
        /// <param name="messageNumber">
        /// The 1-based POP3 message number of the message to mark for deletion.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> representing the server's response
        /// to the <c>DELE</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>DELE</c> command marks a message so that it will be removed
        /// when the POP3 session ends with the <c>QUIT</c> command. Until then,
        /// the message may remain accessible during the same session.
        /// </para>
        /// <para>
        /// This synchronous wrapper blocks the calling thread until the
        /// underlying asynchronous operation completes. It is intended for
        /// environments where asynchronous execution is not desirable.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="messageNumber"/> is less than 1.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        public POP3_ServerResponse Dele(int messageNumber)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return DeleAsync(messageNumber,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method DeleAsync

        /// <summary>
        /// Executes the POP3 <c>DELE</c> command and marks the specified message
        /// for deletion. The actual deletion occurs when the POP3 session is
        /// terminated with the <c>QUIT</c> command.
        /// </summary>
        /// <param name="messageNumber">
        /// The 1-based POP3 message number of the message to mark for deletion.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> representing the server's response
        /// to the <c>DELE</c> command.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>DELE</c> command is defined in RFC 1939. It marks a message so
        /// that it will be removed when the session ends with <c>QUIT</c>. Until
        /// then, the message may still be accessible during the same session,
        /// depending on server implementation.
        /// </para>
        /// <para>
        /// This method may only be called while the client is in the POP3
        /// TRANSACTION state. The connection must be established and the user
        /// must be authenticated.
        /// </para>
        /// <para>
        /// A negative server response (<c>-ERR</c>) results in a
        /// <see cref="POP3_ClientException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not authenticated.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="messageNumber"/> is less than 1.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the server returns a negative POP3 response (<c>-ERR</c>).
        /// </exception>
        public async ValueTask<POP3_ServerResponse> DeleAsync(int messageNumber,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("DELE is valid only in the TRANSACTION state.");
            }
            if(messageNumber < 1){
                throw new ArgumentException("Message number must be >= 1.",nameof(messageNumber));
            }

            /* RFC 1939 — POP3 DELE Command

               The DELE command marks a message for deletion. The actual deletion
               occurs only when the POP3 session ends with the QUIT command. Until
               then, the message remains available and may still be retrieved.

               DELE is valid only in the TRANSACTION state.

               Syntax:
                   DELE msg-number CRLF

               Possible Responses:
                   +OK
                   -ERR

               Response Format:
                   +OK message deleted
                   -ERR no such message

               Discussion:
                   - DELE marks the specified message so that it will be removed
                     when the session terminates with QUIT.
                   - A message marked for deletion may still be accessed during
                     the same session unless the server chooses to hide it.
                   - If the client issues RSET, all deletion marks are cleared.
                   - A negative response (-ERR) indicates an invalid message number
                     or that the server cannot mark the message for deletion.

               Examples:
                   C: DELE 1
                   S: +OK message 1 deleted

                   C: DELE 99
                   S: -ERR no such message
            */

            await SendCommandLineAsync($"DELE {messageNumber}\r\n",true,cancellationToken);

            var serverResponse = await ReadResponseAsync(cancellationToken);
            if(!serverResponse.IsSuccess){
                throw new POP3_ClientException(serverResponse);
            }

            return serverResponse;
        }

        #endregion

        #region method LoadMessages
                
        /// <summary>
        /// Loads the POP3 message list for the current session by issuing the
        /// <c>LIST</c> command and, when supported, the <c>UIDL</c> command.
        /// The method populates the <see cref="Messages"/> collection with
        /// message metadata (size and optional unique-id) and transitions the
        /// client into a state where message retrieval operations may be
        /// performed.
        /// This synchronous wrapper applies the configured <see cref="Timeout"/>
        /// value to limit the duration of the operation.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the session is not
        /// authenticated (not in the TRANSACTION state), or when the message
        /// list has already been loaded.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the POP3 server returns a negative (<c>-ERR</c>) response
        /// to either the <c>LIST</c> or <c>UIDL</c> command.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the POP3 server closes the connection unexpectedly while
        /// the message list is being read.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous convenience wrapper around
        /// <see cref="LoadMessagesAsync(CancellationToken)"/>.  
        /// It should be used only when asynchronous execution is not required.
        /// </para>
        /// <para>
        /// The message list may be loaded only once per POP3 session.  
        /// To reload the list, the client must disconnect and reconnect.
        /// </para>
        /// </remarks>
        public void LoadMessages()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            LoadMessagesAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method LoadMessagesAsync

        /// <summary>
        /// Loads the POP3 message list for the current session by issuing the
        /// <c>LIST</c> command and, when supported, the <c>UIDL</c> command.
        /// The method populates the <see cref="Messages"/> collection with
        /// message metadata (size and optional unique-id) and transitions the
        /// client into a state where message retrieval operations may be
        /// performed.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous network
        /// operations performed while loading the message list.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the session is not
        /// authenticated (not in the TRANSACTION state), or when the message
        /// list has already been loaded.
        /// </exception>
        /// <exception cref="POP3_ClientException">
        /// Thrown when the POP3 server returns a negative (<c>-ERR</c>) response
        /// to either the <c>LIST</c> or <c>UIDL</c> command.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the POP3 server closes the connection unexpectedly while
        /// the message list is being read.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method retrieves message metadata only. Message bodies are not
        /// downloaded; they must be retrieved separately using <c>RETR</c> or
        /// <c>TOP</c>.
        /// </para>
        /// <para>
        /// When the server supports <c>UIDL</c>, the method attempts to match
        /// unique-ids to the corresponding <c>LIST</c> entries. If the number of
        /// <c>UIDL</c> entries does not match the number of <c>LIST</c> entries,
        /// the method logs the mismatch and falls back to using <c>LIST</c>
        /// data only.
        /// </para>
        /// <para>
        /// The message list may be loaded only once per POP3 session. To reload
        /// the list, the client must disconnect and reconnect.
        /// </para>
        /// </remarks>
        public async ValueTask LoadMessagesAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("This method is valid only in the TRANSACTION state.");
            }
            if(m_pMessages != null){
                throw new InvalidOperationException("The message list has already been loaded.");
            }

            m_pMessages = new POP3_ClientMessageCollection(this);

            POP3_t_List_Item[]  listItems = await ListAsync(cancellationToken);
            POP3_t_Uidl_Item[]? uidlItems = null;
            if(this.SupportsUidl){
                uidlItems = await UidlAsync(cancellationToken);
            }

            bool uidlMatches = uidlItems != null && listItems.Length == uidlItems.Length;
            if(!uidlMatches){
                LogAddText($"UIDL count mismatch: LIST={listItems.Length}, UIDL={uidlItems?.Length}. Falling back to LIST-only.");
            }

            for(int i = 0; i < listItems.Length; i++){
                m_pMessages.Add(listItems[i].SizeInBytes, uidlMatches ? uidlItems![i].UniqueId : null);
            }
        }

        #endregion


        #region method SendCommandLineAsync
                
        /// <summary>
        /// Sends a single POP3 command line to the server.
        /// </summary>
        /// <param name="cmdLine">
        /// The POP3 command line to send.  
        /// The value must already be terminated with a CRLF sequence (<c>\r\n</c>).
        /// </param>
        /// <param name="log">
        /// Specifies whether the command line is written to the protocol log.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="cmdLine"/> or the underlying TCP stream is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="cmdLine"/> does not end with the required CRLF terminator.
        /// </exception>
        /// <remarks>
        /// POP3 commands are always single‑line and must be terminated with CRLF.  
        /// This method writes the command line to the server without reading any response.
        /// The caller is responsible for invoking <c>ReadResponseAsync</c> to obtain the
        /// corresponding POP3 server reply.
        /// </remarks>

        private async ValueTask SendCommandLineAsync(string cmdLine,bool log,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(cmdLine);
            ArgumentNullException.ThrowIfNull(this.TcpStream);
            if(!cmdLine.EndsWith("\r\n")){
                throw new ArgumentException("Argument cmdLine does not end with CRLF","cmdLine");
            }   

            if(log){
                LogAddWrite(Encoding.UTF8.GetByteCount(cmdLine),cmdLine.TrimEnd());
            }
            
            await this.TcpStream.WriteLineAsync(cmdLine,cancellationToken);
        }

        #endregion

        #region method ReadResponseAsync

        /// <summary>
        /// Reads a single POP3 server response line as defined in RFC 1939.
        /// A POP3 response always begins with a status indicator (<c>+OK</c> or <c>-ERR</c>)
        /// and may optionally include a POP3 response code (RFC 2449 RESP-CODES)
        /// followed by human‑readable text.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A <see cref="POP3_ServerResponse"/> instance containing the parsed status,
        /// optional response code, and text returned by the POP3 server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the underlying TCP stream is <c>null</c>.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the POP3 server closes the connection before a complete
        /// response line is received.
        /// </exception>
        /// <remarks>
        /// POP3 server replies consist of a single status line. Multi‑line data responses
        /// (used by commands such as LIST, UIDL, RETR, TOP, and CAPA) must be read
        /// separately after the initial status line is parsed.
        /// </remarks>
        private async ValueTask<POP3_ServerResponse> ReadResponseAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(this.TcpStream);

            Memory<byte> lineBuffer = new byte[8000];
            
            ReadLineResult responseline = await this.TcpStream.ReadLineAsync(lineBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
            // Server closed connection.
            if(responseline.BytesInBuffer == 0){
                throw new IOException("POP3 server closed connection.");
            }
            else{
                string line = responseline.LineUtf8 ?? "";

                LogAddRead(responseline.BytesInBuffer,line);

                return POP3_ServerResponse.Parse(line);
            }            
        }

        #endregion


        #region Properties Implementation
        
        /// <summary>
        /// Gets the POP3 server greeting text received immediately after connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        public string GreetingText
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }

                return m_GreetingText; 
            }
        }

        /// <summary>
        /// Gets the APOP unique-id (challenge) extracted from the POP3 server's
        /// greeting line.  
        /// This value is present only when the server includes a timestamp token
        /// enclosed in angle brackets (e.g. &lt;1234.5678@host&gt;) and is required
        /// for APOP authentication.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        public string ApopUniqueId
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }

                return m_ApopUniqueId; 
            }
        }

        /// <summary>
        /// Gets the list of POP3 server capabilities retrieved via the
        /// <c>CAPA</c> command as defined in RFC 2449.  
        /// Each entry corresponds to a single capability line returned by
        /// the server, including any optional parameters.
        /// </summary>
        /// <returns>
        /// An array containing all capability lines exactly as reported
        /// by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        public string[] Capabilities
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }

                return m_pCapabilities.ToArray(); 
            }
        }

        /// <summary>
        /// Gets the list of SASL authentication mechanisms advertised by the
        /// POP3 server via the <c>AUTH</c> capability line.  
        /// The returned array contains all mechanism names exactly as reported
        /// by the server (for example: <c>CRAM-MD5</c>, <c>DIGEST-MD5</c>,
        /// <c>PLAIN</c>, <c>LOGIN</c>).
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        public string[] SaslAuthMethods
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }

                // Search AUTH entry.
                foreach(string feature in this.Capabilities){
                    string featureName = feature.Split(' ')[0];
                    if(string.Equals(featureName,"SASL",StringComparison.InvariantCultureIgnoreCase)){
                        // Remove AUTH<SP> and split authentication methods.
                        return feature.Substring(4).Trim().Split(' ');
                    }
                }

                return new string[0];
            }
        }

        /// <summary>
        /// Gets a value indicating whether the POP3 server supports the
        /// <c>UIDL</c> command.  
        /// The result is based on the presence of the <c>UIDL</c> capability
        /// in the server's <c>CAPA</c> response.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not yet authenticated.
        /// </exception>
        public bool SupportsUidl
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }
                if(!this.IsAuthenticated){
				    throw new InvalidOperationException("You must authenticate first.");
			    }

                return m_SupportsUidl; 
            }
        }

        /// <summary>
        /// Gets a value indicating whether the POP3 server advertises the
        /// <c>UTF8</c> capability as defined in RFC 6856.  
        /// When present, the server supports the POP3 <c>UTF8</c> command,
        /// allowing UTF‑8 response text and UTF‑8 message headers once
        /// UTF‑8 mode has been enabled.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not yet authenticated.
        /// </exception>
        public bool SupportsUtf8
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }
                if(!this.IsAuthenticated){
				    throw new InvalidOperationException("You must authenticate first.");
			    }

                return m_SupportsUtf8; 
            }
        }

        /// <summary>
        /// Gets a value indicating whether the POP3 server advertises the
        /// <c>UTF8=USER</c> capability as defined in RFC 6856.  
        /// When present, the server accepts UTF‑8 encoded arguments to the
        /// <c>USER</c> and <c>PASS</c> commands.  
        /// Without this capability, usernames and passwords must be ASCII
        /// even if UTF‑8 mode is enabled.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or not yet authenticated.
        /// </exception>
        public bool SupportsUtf8User
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }
                if(!this.IsAuthenticated){
				    throw new InvalidOperationException("You must authenticate first.");
			    }

                return m_SupportsUtf8User; 
            }
        }
        
        /// <summary>
        /// Gets the authenticated user identity for the current POP3 session,
        /// or <c>null</c> if no authentication has been performed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        public override GenericIdentity? AuthenticatedUserIdentity
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }

                return m_pAuthdUserIdentity; 
            }
        }

        /// <summary>
        /// Gets the POP3 message collection for the current session.  
        /// The collection becomes available only after the message list has been
        /// loaded using <see cref="LoadMessages"/> or
        /// <see cref="LoadMessagesAsync(CancellationToken)"/>.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the session is not
        /// authenticated (not in the TRANSACTION state), or when the message
        /// list has not yet been loaded.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This property provides access to the cached POP3 message metadata
        /// (size and optional unique-id) retrieved via the <c>LIST</c> and
        /// <c>UIDL</c> commands.  
        /// Message bodies are not downloaded automatically; they must be
        /// retrieved separately using <c>RETR</c> or <c>TOP</c>.
        /// </para>
        /// <para>
        /// Accessing this property before the message list has been loaded
        /// results in an <see cref="InvalidOperationException"/>.
        /// </para>
        /// </remarks>
        public POP3_ClientMessageCollection? Messages
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }
                if(!this.IsAuthenticated){
				    throw new InvalidOperationException("You must authenticate first.");
			    }
                if(m_pMessages == null){
                    throw new InvalidOperationException("The message list has not been loaded. Call LoadMessages() first.");
                }

                return m_pMessages; 
            }
        }

        /// <summary>
        /// Gets a value indicating whether UTF-8 mode is enabled for the current
        /// POP3 session. UTF-8 mode is activated when the client issues the
        /// <c>UTF8</c> command and the server responds with a positive (<c>+OK</c>)
        /// response, as defined in RFC 6856.
        /// </summary>
        public bool Utf8Enabled
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }

                return m_Utf8Enabled; 
            }
        }

		#endregion


        //--- Obsolete -------------------------------------------------------------------
          
	}
}
