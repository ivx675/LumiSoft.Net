using LumiSoft.Net.AUTH;
using LumiSoft.Net.IMAP;
using LumiSoft.Net.IO;
using LumiSoft.Net.MIME;
using LumiSoft.Net.POP3;
using LumiSoft.Net.POP3.Client;
using LumiSoft.Net.TCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;

namespace LumiSoft.Net.IMAP.Client
{
    /// <summary>
    /// Represents an IMAP4rev1 client capable of connecting to an IMAP server,
    /// authenticating a user, selecting mailboxes, retrieving messages, issuing
    /// IMAP commands, and managing server-side message state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This client implements the core IMAP4rev1 protocol as defined in RFC 3501,
    /// including support for authentication, mailbox selection, message fetching,
    /// message copying, message flag manipulation, and folder listing. The client
    /// maintains a persistent TCP connection to the server and processes all IMAP
    /// responses according to the protocol's tagged and untagged response model.
    /// </para>
    /// </remarks>
    public class IMAP_Client : TCP_Client
    {   
        private Memory<byte>                m_LineReadBuffer     = new Memory<byte>(new byte[128000]);
        private GenericIdentity?            m_pAuthdUserIdentity = null;
        private string                      m_GreetingText       = "";
        private int                         m_CommandIndex       = 1;
        private List<string>?               m_pCapabilities      = null;
        private bool                        m_LiteralPluss       = false;
        private bool                        m_Utf8User           = false;
        private bool                        m_Utf8Search         = false;
        private IMAP_Client_SelectedFolder? m_pSelectedFolder    = null;
        private IMAP_Mailbox_Encoding       m_MailboxEncoding    = IMAP_Mailbox_Encoding.ImapUtf7;
        private Task?                       m_pIdle              = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_Client()
        {
        }

        
        #region override method OnConnectedAsync

        /// <summary>
        /// Handles the initial IMAP server greeting received immediately after the TCP
        /// connection is established. According to RFC 3501 section 7.1.1, the server
        /// must send a single untagged status response beginning with the asterisk
        /// (<c>*</c>) tag. The greeting indicates whether the server is ready for
        /// authentication (<c>OK</c>), has pre-authenticated the session (<c>PREAUTH</c>),
        /// or is closing the connection (<c>BYE</c>).
        /// 
        /// This method reads the greeting line, verifies that it is an untagged status
        /// response (<see cref="IMAP_r_u_ServerStatus"/>), and checks whether the status
        /// code represents an error condition. A greeting with a non-OK status code
        /// (<c>NO</c>, <c>BAD</c>, or <c>BYE</c>) results in an <see cref="IMAP_ClientException"/>
        /// being thrown. For valid greetings, the human-readable greeting text is stored
        /// for later use.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server greeting is not an untagged status response or when
        /// the greeting contains an error status code.
        /// </exception>
        protected override async ValueTask OnConnectedAsync(CancellationToken cancellationToken = default)
        {
            /* RFC 3501 section 7.1.1 — IMAP Server Greeting

               The IMAP server sends a single greeting line immediately after the TCP
               connection is established. The greeting is always untagged and begins
               with the asterisk ("*") response tag.

               Greeting = "* " (resp-cond-auth / resp-cond-bye) CRLF

               resp-cond-auth = ("OK" SP resp-text) /
                                ("PREAUTH" SP resp-text)

               resp-cond-bye  = "BYE" SP resp-text

               resp-text      = [ resp-code ] SP text
                                ; resp-text never contains IMAP literals
                                ; resp-code is optional and enclosed in "[" ... "]"

               resp-code      = "[" atom *(SP atom) "]"
                                ; e.g. [CAPABILITY IMAP4rev1 STARTTLS LOGINDISABLED]

               Examples:
                 * OK IMAP4rev1 Service Ready
                 * OK [CAPABILITY IMAP4rev1 LITERAL+ SASL-IR] Dovecot ready.
                 * PREAUTH IMAP server logged in as admin
                 * BYE Server shutting down
            */

            var response = await ReadResponseAsync(true,cancellationToken);
            if(response is not IMAP_r_u_ServerStatus){
                throw new IMAP_ClientException(new IMAP_r_ServerStatus("*","BAD",response?.ToString() ?? ""));
            }
            var responseSatus = (IMAP_r_u_ServerStatus)response;

            if(responseSatus.IsError){                
                throw new IMAP_ClientException(new IMAP_r_ServerStatus("*",responseSatus.ResponseCode,responseSatus.ResponseText));
            }
            else{
                m_GreetingText = responseSatus.ResponseText;
            }
        }

        #endregion

        #region override method Disconnect

		/// <summary>
        /// Disconnects from the IMAP server by invoking the asynchronous
        /// <see cref="DisconnectAsync(bool, CancellationToken)"/> method with
        /// <c>sendQuit</c> set to <c>true</c> and waiting for its completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper creates a <see cref="CancellationTokenSource"/>
        /// using the client's configured <see cref="Timeout"/> value and blocks until
        /// the asynchronous disconnect operation completes. The underlying
        /// <see cref="DisconnectAsync(bool, CancellationToken)"/> method optionally
        /// sends the IMAP <c>LOGOUT</c> command, reads the server's final response,
        /// resets all session state, and closes the connection.
        /// </para>
        /// <para>
        /// Any exceptions thrown by the asynchronous disconnect sequence are
        /// propagated to the caller of this method.
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
        /// Disconnects from the IMAP server and optionally sends the <c>LOGOUT</c>
        /// command before closing the underlying connection. According to RFC 3501
        /// section 6.1.3, the <c>LOGOUT</c> command requests that the server terminate
        /// the session and return a final tagged status response, typically followed
        /// by an untagged <c>* BYE</c> notification.
        /// 
        /// When <paramref name="sendQuit"/> is <c>true</c>, this method sends a
        /// <c>LOGOUT</c> command using a new command tag and reads the server's final
        /// response. Any exceptions raised during transmission or reception of the
        /// logout sequence are suppressed, and the connection is closed regardless.
        /// 
        /// After the logout sequence (or immediately if <paramref name="sendQuit"/>
        /// is <c>false</c>), all client session state is reset, including the
        /// authenticated user, greeting text, command index, cached capabilities,
        /// selected folder, and mailbox encoding.
        /// </summary>
        /// <param name="sendQuit">
        /// If <c>true</c>, the IMAP <c>LOGOUT</c> command is sent and the server's
        /// final response is read before disconnecting. If <c>false</c>, the client
        /// disconnects without issuing <c>LOGOUT</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations performed during the logout sequence.
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
        public async ValueTask DisconnectAsync(bool sendQuit,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("IMAP client is not connected.");
            }

            try{            
                if(sendQuit){
                    await SendCommandLineAsync((m_CommandIndex++).ToString("d5") + " LOGOUT\r\n",true,cancellationToken);

                    _= await ReadFinalResponseAsync(true,null,cancellationToken);
                }
            }
            catch{
            }

            // Reset state varibles.
            m_pAuthdUserIdentity = null;
            m_GreetingText       = "";
            m_CommandIndex       = 1;
            m_pCapabilities      = null;
            m_pSelectedFolder    = null;
            m_MailboxEncoding    = IMAP_Mailbox_Encoding.ImapUtf7;

            base.Disconnect(); 
        }

        #endregion


        #region method StartTls

        /// <summary>
        /// Executes the IMAP <c>STARTTLS</c> command synchronously by invoking
        /// <see cref="StartTlsAsync(SslClientAuthenticationOptions, CancellationToken)"/>
        /// and blocking until the operation completes. The STARTTLS command,
        /// defined in RFC 3501 section 6.2.1, requests that the server begin TLS
        /// negotiation on the existing IMAP connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper creates a <see cref="CancellationTokenSource"/>
        /// using the client's configured <see cref="Timeout"/> value and waits for
        /// the asynchronous STARTTLS operation to finish. If the server accepts the
        /// command with an <c>OK</c> completion response, the method performs the TLS
        /// handshake and upgrades the underlying connection to a secure
        /// <see cref="SslStream"/>.
        /// </para>
        /// <para>
        /// After a successful TLS negotiation, the client MUST discard all previous
        /// IMAP protocol state and MUST issue a new <c>CAPABILITY</c> command, as
        /// required by RFC 3501. Any capabilities advertised before STARTTLS MUST NOT
        /// be used after the secure channel is established.
        /// </para>
        /// <para>
        /// Any exceptions raised by the underlying asynchronous operation—including
        /// server rejection (<c>NO</c>/<c>BAD</c>) or TLS handshake failures—are
        /// propagated to the caller.
        /// </para>
        /// </remarks>
        /// <param name="sslOptions">
        /// Optional TLS configuration used when creating the secure stream. If
        /// <c>null</c>, default TLS settings are applied.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the client is already
        /// authenticated, or when the connection is already secure.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server rejects the <c>STARTTLS</c> command.
        /// </exception>
        /// <exception cref="AuthenticationException">
        /// Thrown when the TLS handshake fails after the server accepts STARTTLS.
        /// </exception>
        public void StartTls(SslClientAuthenticationOptions? sslOptions)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            StartTlsAsync(sslOptions,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method StartTlsAsync

        /// <summary>
        /// Sends the IMAP <c>STARTTLS</c> command to the server and, if accepted,
        /// upgrades the existing plaintext connection to a secure TLS connection
        /// using the provided <see cref="SslClientAuthenticationOptions"/>.
        /// STARTTLS is defined in RFC 3501 section 6.2.1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the client issues <c>STARTTLS</c>, the server replies with a
        /// tagged completion response. If the server returns <c>OK</c>, the client
        /// MUST immediately begin TLS negotiation on the current TCP connection.
        /// If the server returns <c>NO</c> or <c>BAD</c>, TLS is not available and
        /// the command fails.
        /// </para>
        /// <para>
        /// After a successful TLS handshake, the client MUST discard all previous
        /// IMAP protocol state and MUST issue a new <c>CAPABILITY</c> command.
        /// Any capabilities advertised before STARTTLS MUST NOT be used after the
        /// secure channel is established. This requirement ensures that capability
        /// changes related to authentication mechanisms or security extensions are
        /// correctly reflected.
        /// </para>
        /// <para>
        /// STARTTLS is only valid in the non‑authenticated state. Once the client
        /// has authenticated, the command MUST NOT be used. The command also cannot
        /// be issued if the connection is already secure.
        /// </para>
        /// </remarks>
        /// <param name="sslOptions">
        /// Optional TLS configuration used when creating the <see cref="SslStream"/>
        /// after the server accepts STARTTLS. If <c>null</c>, default TLS settings
        /// are applied.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations associated with sending the command and performing the TLS
        /// handshake.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous STARTTLS
        /// negotiation and TLS upgrade process.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, when the client is already
        /// authenticated, or when the connection is already secure.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server rejects the <c>STARTTLS</c> command with a
        /// <c>NO</c> or <c>BAD</c> completion response.
        /// </exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown when the TLS handshake fails after the server accepts STARTTLS.
        /// </exception>
        public async ValueTask StartTlsAsync(SslClientAuthenticationOptions? sslOptions,CancellationToken cancellationToken = default)
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

            /* RFC 3501 section 6.2.1 — STARTTLS Command

               The STARTTLS command requests that the server begin TLS negotiation
               over the existing IMAP connection. If the server supports TLS, it
               replies with a  OK completion response and the client MUST immediately
               begin the TLS handshake. If the server does not support TLS, it replies
               with a NO or BAD completion response.

               STARTTLS-Command = tag SP "STARTTLS" CRLF

               Server Responses:
                 - If TLS is available:
                       tag SP "OK" SP resp-text CRLF
                       (Client MUST begin TLS handshake immediately)
                 - If TLS is not available:
                       tag SP "NO" SP resp-text CRLF
                       or
                       tag SP "BAD" SP resp-text CRLF

               Notes:
                 - After a successful TLS negotiation, the client MUST discard all
                   previous IMAP protocol state and MUST issue a new CAPABILITY
                   command. Any capabilities advertised before STARTTLS MUST NOT be
                   used after TLS is established.
                 - STARTTLS is only valid in the non-authenticated state. Once the
                   client has authenticated, STARTTLS MUST NOT be used.
                 - The server MUST NOT advertise LOGINDISABLED after TLS is active.
                 - STARTTLS does not change the selected mailbox; however, because
                   the client MUST treat the session as new, it MUST reissue any
                   required commands (e.g., SELECT).

               Example:
                 C: A001 STARTTLS
                 S: A001 OK Begin TLS negotiation
                 (TLS handshake begins)
                 C: A002 CAPABILITY
                 S: * CAPABILITY IMAP4rev1 AUTH=PLAIN IDLE UIDPLUS
                 S: A002 OK CAPABILITY completed
            */


            await SendCommandLineAsync((m_CommandIndex++).ToString("d5") + " STARTTLS\r\n",true,cancellationToken);

            var serverResponse = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(serverResponse.IsSuccess){
                LogAddText("Starting TLS handshake.");

                await SwitchToSecureAsync(sslOptions,cancellationToken);

                LogAddText("TLS handshake completed successfully.");
            }
            else{
                throw new IMAP_ClientException(serverResponse);
            }
        }

        #endregion
        

        #region method Login

        /// <summary>
        /// Performs a synchronous IMAP <c>LOGIN</c> operation using the specified
        /// username and password. This method is a blocking wrapper around
        /// <see cref="LoginAsync(string,string,CancellationToken)"/> and will not
        /// return until authentication has completed or the operation times out.
        /// </summary>
        /// <param name="user">
        /// The IMAP username. The value must not be <c>null</c> or empty. The username
        /// is encoded as either a quoted string or a literal depending on its content.
        /// </param>
        /// <param name="password">
        /// The IMAP password. As with the username, the password is encoded either as
        /// a quoted string or a literal segment based on its content.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, is already authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="user"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="user"/> is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns a <c>NO</c> or <c>BAD</c> response to the
        /// <c>LOGIN</c> command.
        /// </exception>
        /// <remarks>
        /// This method creates a <see cref="CancellationTokenSource"/> using the
        /// client's configured timeout and then invokes <c>LoginAsync</c> in a
        /// synchronous manner via <c>GetAwaiter().GetResult()</c>. Any exceptions
        /// thrown by the asynchronous operation are propagated directly to the caller.
        /// 
        /// Because this method blocks the calling thread, it should be used only in
        /// environments where synchronous IMAP operations are appropriate. For
        /// asynchronous workflows, <see cref="LoginAsync(string,string,CancellationToken)"/>
        /// should be used instead.
        /// </remarks>
        public void Login(string user,string password)
        {            
            using var cts = new CancellationTokenSource(this.Timeout);

            LoginAsync(user,password,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method LoginAsync

        /// <summary>
        /// Sends an IMAP <c>LOGIN</c> command using the supplied username and password,
        /// performing authentication according to RFC 3501. The method constructs the
        /// command using quoted strings or literal segments as required and transitions
        /// the client into the authenticated state upon success.
        /// </summary>
        /// <param name="user">
        /// The IMAP username. The value may be sent either as a quoted string or as a
        /// literal depending on whether it contains characters that require literal
        /// encoding.
        /// </param>
        /// <param name="password">
        /// The IMAP password. As with the username, the password is encoded either as a
        /// quoted string or a literal segment based on its content. Literal encoding is
        /// used when the password contains characters that cannot appear safely inside
        /// a quoted string.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous authentication
        /// operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, is already authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the <paramref name="user"/> parameter is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the <paramref name="user"/> parameter is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns a <c>NO</c> or <c>BAD</c> response to the
        /// <c>LOGIN</c> command.
        /// </exception>
        public async ValueTask LoginAsync(string user,string password,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(this.IsAuthenticated){
                throw new InvalidOperationException("Re-authentication error, you are already authenticated.");
            }            
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(user == null){
                throw new ArgumentNullException("user");
            }
            if(user == string.Empty){
                throw new ArgumentException("Argument 'user' value must be specified.");
            }

            /* RFC 3501 section 6.2.3 — LOGIN Command

               The LOGIN command requests that the server authenticate the client
               using a plaintext username and password. LOGIN is only permitted
               in the non-authenticated state. If authentication succeeds, the
               server transitions the session to the authenticated state.

               LOGIN-Command = tag SP "LOGIN" SP userid SP password CRLF

               Server Responses:
                 - On success:
                       tag SP "OK" SP resp-text CRLF
                 - On failure:
                       tag SP "NO" SP resp-text CRLF
                 - On protocol error:
                       tag SP "BAD" SP resp-text CRLF

               Notes:
                 - The LOGIN command transmits credentials in plaintext and SHOULD NOT
                   be used without a secure channel (e.g., after STARTTLS).
                 - Servers MAY disable LOGIN until TLS is active by advertising the
                   LOGINDISABLED capability.
                 - If authentication succeeds, the server MUST NOT send a PREAUTH
                   response; PREAUTH is only permitted in the initial greeting.
                 - After successful LOGIN, the client is in the authenticated state
                   and may issue mailbox-selection commands such as SELECT or EXAMINE.
                 - LOGIN is mutually exclusive with AUTHENTICATE; clients SHOULD prefer
                   SASL mechanisms when available.

               Example:
                 C: A001 LOGIN "fred" "secret"
                 S: A001 OK LOGIN completed

                 C: A002 LOGIN "fred" "wrongpass"
                 S: A002 NO LOGIN failed

                 C: A003 LOGIN
                 S: A003 BAD Missing arguments
            */

            List<CommandPart> cmdItems = new List<CommandPart> ();
            StringBuilder     cmdLine  = new StringBuilder();
            string            cmdTag   = (m_CommandIndex++).ToString("d5");

            cmdLine.Append($"{cmdTag} LOGIN");

            // User must be literal string.
            if(IMAP_Utils.MustUseLiteralString(user,m_Utf8User)){
                byte[] literal = Encoding.UTF8.GetBytes(user);

                cmdLine.Append(" {" + literal.Length + "}\r\n");
                cmdItems.Add(new CommandPart(false,cmdLine.ToString(),null));
                cmdItems.Add(new CommandPart(true,null,literal));
                cmdLine.Clear();
            }
            // User normal quoted string.
            else{
                cmdLine.Append($" \"{user}\"");
            }

            // Password must be literal string.
            if(IMAP_Utils.MustUseLiteralString(password,false)){
                byte[] literal = Encoding.UTF8.GetBytes(password);

                cmdLine.Append(" {" + literal.Length + "}\r\n");
                cmdItems.Add(new CommandPart(false,cmdLine.ToString(),null));
                cmdItems.Add(new CommandPart(true,null,literal));
                cmdLine.Clear();
            }
            // Password normal quoted string.
            else{
                cmdLine.Append($" \"{password}\"");
            }

            // Command line terminator.
            cmdLine.Append("\r\n");
            cmdItems.Add(new CommandPart(false,cmdLine.ToString(),null));

            bool isDebug = false;
            #if DEBUG
                isDebug = true;
            #else
                LogAddWrite(0,$"{cmdTag} LOGIN <username omitted> <password omitted>");
            #endif

            await SendCommandAsync(cmdItems.ToArray(),isDebug,cancellationToken);
            
            var response = await ReadFinalResponseAsync(isDebug,null,cancellationToken);
            if(response.IsSuccess){
                m_pAuthdUserIdentity = new GenericIdentity(user,"IMAP-LOGIN");
            }
            else{
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method AuthGetStrongestMethod

        /// <summary>
        /// Selects the strongest SASL authentication mechanism supported by both the
        /// IMAP server and this client. Mechanisms are evaluated in the following
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
        /// Thrown if the IMAP server does not advertise any SASL mechanisms, or if
        /// none of the advertised mechanisms are supported by this client.
        /// </exception>
        public AUTH_SASL_Client AuthGetStrongestMethod(string userName,string password)
        {
            return AuthGetStrongestMethod(null,userName,password);
        }

        /// <summary>
        /// Selects the strongest SASL authentication mechanism supported by both the
        /// IMAP server and this client. Mechanisms are evaluated in the following
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
        /// Thrown if the IMAP server does not advertise any SASL mechanisms, or if
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
                throw new NotSupportedException("IMAP server does not support authentication.");
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
                throw new NotSupportedException("We don't support any of the IMAP server authentication methods.");
            }
        }

        #endregion

        #region method Authenticate

        /// <summary>
        /// Performs a synchronous IMAP <c>AUTHENTICATE</c> operation using the specified
        /// SASL mechanism. This method is a blocking wrapper around
        /// <see cref="AuthenticateAsync(AUTH_SASL_Client, CancellationToken)"/> and will
        /// not return until the SASL authentication exchange has completed.
        /// </summary>
        /// <param name="sasl">
        /// The SASL client mechanism instance responsible for generating and processing
        /// base64-encoded challenge/response data. The mechanism name must match one of
        /// the SASL mechanisms advertised by the server in the CAPABILITY response.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, is already authenticated, or if the
        /// <paramref name="sasl"/> parameter is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns a <c>NO</c> or <c>BAD</c> status during the
        /// authentication exchange.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method creates a <see cref="CancellationTokenSource"/> using the
        /// client's configured timeout and invokes <c>AuthenticateAsync</c> in a
        /// synchronous manner via <c>GetAwaiter().GetResult()</c>. Any exceptions
        /// thrown by the asynchronous operation are propagated directly to the caller.
        /// </para>
        ///
        /// <para>
        /// Because this method blocks the calling thread, it should be used only in
        /// environments where synchronous IMAP operations are appropriate. For
        /// asynchronous workflows, <see cref="AuthenticateAsync(AUTH_SASL_Client, CancellationToken)"/>
        /// should be used instead.
        /// </para>
        /// </remarks>
        public void Authenticate(AUTH_SASL_Client sasl)
        {            
            using var cts = new CancellationTokenSource(this.Timeout);

            AuthenticateAsync(sasl,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method AuthenticateAsync

        /// <summary>
        /// Performs IMAP authentication using the <c>AUTHENTICATE</c> command and the
        /// specified SASL mechanism. This method implements the SASL challenge/response
        /// exchange defined in RFC 3501, RFC 4422, and RFC 4959 (SASL-IR).
        /// </summary>
        /// <param name="sasl">
        /// The SASL client mechanism instance responsible for generating and processing
        /// base64-encoded challenge/response data. The mechanism name must match one of
        /// the SASL mechanisms advertised by the server in the CAPABILITY response.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the authentication operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous authentication
        /// exchange. The method completes when the server returns a final tagged
        /// <c>OK</c>, <c>NO</c>, or <c>BAD</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, is already authenticated, or if the
        /// SASL mechanism is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns a <c>NO</c> or <c>BAD</c> status during the
        /// authentication exchange.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>AUTHENTICATE</c> command initiates a SASL authentication sequence.
        /// After sending the mechanism name (and optional initial response when the
        /// server advertises the <c>SASL-IR</c> capability), the client and server
        /// exchange base64-encoded challenge/response data until the server returns a
        /// final tagged status response.
        /// </para>
        ///
        /// <para>
        /// Protocol rules:
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// The server sends continuation prompts (<c>+</c>) containing base64-encoded
        /// challenge data.
        /// </description></item>
        /// <item><description>
        /// The client responds with base64-encoded data generated by the SASL
        /// mechanism.
        /// </description></item>
        /// <item><description>
        /// The exchange continues until the server sends a tagged <c>OK</c>,
        /// <c>NO</c>, or <c>BAD</c> response.
        /// </description></item>
        /// <item><description>
        /// If the server advertises <c>SASL-IR</c>, the client MAY send an initial
        /// response on the same line as the <c>AUTHENTICATE</c> command.
        /// </description></item>
        /// </list>
        ///
        /// <para>
        /// Upon receiving a tagged <c>OK</c> response, the client transitions into the
        /// authenticated state and records the authenticated identity.
        /// </para>
        /// </remarks>
        public async ValueTask AuthenticateAsync(AUTH_SASL_Client sasl,CancellationToken cancellationToken = default)
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

            /* RFC 3501 section 6.2.2 — AUTHENTICATE Command
               RFC 4959 — SASL Initial Client Response Capability (SASL-IR)
               RFC 4422 — Simple Authentication and Security Layer (SASL) Framework

               The AUTHENTICATE command initiates a SASL authentication exchange between
               the client and server. Unlike LOGIN, which transmits credentials in
               plaintext, AUTHENTICATE uses a challenge/response mechanism defined by the
               selected SASL mechanism.

               AUTHENTICATE-Command = tag SP "AUTHENTICATE" SP auth-type [SP initial-response] CRLF

               SASL Exchange:
                 - After receiving the AUTHENTICATE command, the server sends a
                   continuation response ("+") containing a base64-encoded challenge.
                 - The client responds with base64-encoded data appropriate for the SASL
                   mechanism.
                 - This challenge/response cycle continues until the server returns a
                   tagged OK, NO, or BAD response.

               Initial Client Response (RFC 4959 — SASL-IR):
                 - Servers advertise support for initial client responses using the
                   "SASL-IR" capability.
                 - If SASL-IR is present, the client MAY send an initial response on the
                   same line as the AUTHENTICATE command.
                 - If the initial response is empty, the client sends "=" (base64 of empty
                   string).
                 - If SASL-IR is NOT advertised, the client MUST NOT send an initial
                   response and must wait for the server's first continuation prompt.

               Server Responses:
                 - "+" SP base64-challenge CRLF
                 - tag SP "OK" SP resp-text CRLF      — authentication succeeded
                 - tag SP "NO" SP resp-text CRLF      — authentication failed
                 - tag SP "BAD" SP resp-text CRLF     — command rejected or malformed

               Notes:
                 - AUTHENTICATE does not use IMAP literal syntax ({N}). All SASL data is
                   transmitted as base64-encoded lines terminated by CRLF.
                 - The server MUST advertise supported SASL mechanisms via CAPABILITY.
                 - The client SHOULD prefer AUTHENTICATE over LOGIN when secure mechanisms
                   are available.
                 - Servers MAY disable plaintext mechanisms (e.g., PLAIN) until TLS is
                   active by advertising LOGINDISABLED.
                 - The client MUST NOT send additional IMAP commands until the SASL
                   exchange completes.

               Example (PLAIN with SASL-IR):
                 C: A001 AUTHENTICATE PLAIN AHJlZEBleGFtcGxlLmNvbQBwYXNzd29yZA==
                 S: A001 OK Authentication successful

               Example (PLAIN without SASL-IR):
                 C: A002 AUTHENTICATE PLAIN
                 S: +
                 C: AHJlZEBleGFtcGxlLmNvbQBwYXNzd29yZA==
                 S: A002 OK Authentication successful

               Example (CRAM-MD5):
                 C: A003 AUTHENTICATE CRAM-MD5
                 S: + PDEyMzQuNTY3ODkwMTIzNEBleGFtcGxlLmNvbT4=
                 C: cmVkIDBmYzE5Y2QzYjYzYzY5YzQzYzQzYzQzYzQz
                 S: A003 OK CRAM-MD5 authentication successful
            */

            string authCommand;
            if(sasl.SupportsInitialResponse && SupportsCapability("SASL-IR")){
                authCommand = (m_CommandIndex++).ToString("d5") + " AUTHENTICATE " + sasl.Name + " " + Convert.ToBase64String(sasl.Continue(null)) + "\r\n";
            }
            else{
                authCommand = (m_CommandIndex++).ToString("d5") + " AUTHENTICATE " + sasl.Name + "\r\n";
            }

            await SendCommandLineAsync(authCommand,false,cancellationToken);
            #if DEBUG
                LogAddWrite(authCommand.Length,authCommand.Trim());
            #else
                LogAddWrite(authCommand.Length,"Client response sent.");
            #endif

            while(true){
                var serverResponse = await ReadFinalResponseAsync(true,null,cancellationToken);
                
                // Authentication suceeded.
                if(serverResponse.IsSuccess){
                    m_pAuthdUserIdentity = new GenericIdentity(sasl.UserName,sasl.Name);

                    return;
                }
                // Continue authenticating.
                else if(serverResponse.IsContinue){
                    // + base64Data, we need to decode it and pass to SASL auth mechanism.

                    // Pass server response to SASL authentication.
                    byte[] saslResponse = sasl.Continue(Convert.FromBase64String(serverResponse.ResponseText.Trim()));
                    
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
                    throw new IMAP_ClientException(serverResponse);
                }
            }
        }

        #endregion


        #region method Namespace

        /// <summary>
        /// Executes the IMAP <c>NAMESPACE</c> command synchronously and returns the
        /// server‑provided namespace information, if available. This method blocks
        /// until the command completes or the operation times out.
        /// </summary>
        /// <returns>
        /// An <see cref="IMAP_r_u_Namespace"/> object containing the personal,
        /// other‑users, and shared namespace lists returned by the server, or
        /// <c>null</c> if the server does not send an untagged <c>NAMESPACE</c>
        /// response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="NamespaceAsync(CancellationToken)"/> and is provided for
        /// convenience in environments where asynchronous execution is not
        /// desirable. It uses the client's configured <see cref="Timeout"/> value
        /// to cancel the underlying asynchronous operation.
        /// </para>
        /// <para>
        /// The <c>NAMESPACE</c> command is defined in RFC 2342. Servers are not
        /// required to support this command. If the server responds with a tagged
        /// <c>OK</c> but does not send an untagged <c>NAMESPACE</c> response, this
        /// method returns <c>null</c> to indicate that namespace information is
        /// unavailable.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Namespace? Namespace()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return NamespaceAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method NamespaceAsync

        /// <summary>
        /// Executes the IMAP <c>NAMESPACE</c> command and returns the server‑provided
        /// namespace information, if available. The method returns an
        /// <see cref="IMAP_r_u_Namespace"/> instance when the server sends an
        /// untagged <c>NAMESPACE</c> response, or <c>null</c> if the server replies
        /// with a tagged <c>OK</c> without providing namespace data.
        /// </summary>
        /// <returns>
        /// An <see cref="IMAP_r_u_Namespace"/> object containing the personal,
        /// other‑users, and shared namespace lists returned by the server, or
        /// <c>null</c> if the server does not supply a <c>NAMESPACE</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>NAMESPACE</c> command is defined in RFC 2342. It allows a
        /// client to discover the prefix and hierarchy delimiter used for mailbox
        /// names in different namespace categories. A server may return up to
        /// three namespace lists:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Personal namespaces</description></item>
        /// <item><description>Other‑users namespaces</description></item>
        /// <item><description>Shared namespaces</description></item>
        /// </list>
        /// <para>
        /// Each list may contain zero or more namespace descriptors, or may be
        /// <c>NIL</c> if the server provides no entries for that category.
        /// </para>
        /// <para>
        /// Servers are not required to support the <c>NAMESPACE</c> command.
        /// If the server responds with a tagged <c>OK</c> but does not send an
        /// untagged <c>NAMESPACE</c> response, this method returns <c>null</c>
        /// to indicate that namespace information is unavailable.
        /// </para>
        /// <para>
        /// The namespace information is informational only and does not affect
        /// IMAP session state. It is typically used to construct correct mailbox
        /// paths for <c>LIST</c>, <c>SELECT</c>, <c>CREATE</c>, <c>RENAME</c>,
        /// and other folder‑related operations.
        /// </para>
        /// </remarks>
        public async ValueTask<IMAP_r_u_Namespace?> NamespaceAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 2342 – IMAP4 Namespace Extension
               Command:     NAMESPACE
               Responses:   untagged NAMESPACE response
                            tagged OK / NO / BAD completion

               The NAMESPACE command allows a client to discover the prefix and
               hierarchy delimiter used for mailboxes in different namespace
               categories. A server returns a single untagged NAMESPACE response
               containing up to three namespace lists:

                   1) Personal namespaces
                   2) Other-users namespaces
                   3) Shared namespaces

               Each namespace list contains zero or more namespace descriptors.
               A namespace descriptor has the form:

                   ("prefix" "delimiter")

               The prefix is a string prepended to mailbox names in that namespace.
               The delimiter is the hierarchy separator used within that namespace.
               If a namespace list is empty, the server returns NIL for that list.

               Example:
                   C: NAMESPACE
                   S: * NAMESPACE (("" "/")) NIL (("Public Folders/" "/"))
                   S: OK Completed

               A server MAY return NIL for any or all namespace categories.
               A server MAY also return no untagged NAMESPACE response at all,
               replying only with a tagged OK. This indicates that the server does
               not provide namespace information.

               Clients MUST NOT assume that the NAMESPACE command is supported.
               If the server returns OK without an untagged NAMESPACE response,
               the client should treat the namespace information as unavailable.

               The NAMESPACE response is informational and does not affect IMAP
               session state. It is typically used to construct correct mailbox
               paths for LIST, SELECT, CREATE, RENAME, and other folder operations.
            */

            IMAP_r_u_Namespace? retVal = null;
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(retVal == null && e is IMAP_r_u_Namespace ns){
                    retVal = ns;
                }
            };

            await SendCommandLineAsync((m_CommandIndex++).ToString("d5") + " NAMESPACE\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            return retVal;
        }

        #endregion

        #region method Folders

        /// <summary>
        /// Executes the IMAP <c>LIST</c> command synchronously and returns all
        /// mailbox entries that match the specified reference name and pattern.
        /// This method blocks until the command completes or the operation times out.
        /// </summary>
        /// <param name="referenceName">
        /// The reference name used as the base for mailbox lookup. Typically an
        /// empty string (<c>""</c>) to indicate the root of the hierarchy.
        /// </param>
        /// <param name="pattern">
        /// The mailbox pattern to match. Common values include <c>"*"</c> for all
        /// mailboxes or <c>"%"</c> for the current hierarchy level.
        /// </param>
        /// <returns>
        /// An array of <see cref="IMAP_r_u_List"/> objects representing the
        /// mailboxes returned by the server. If the server returns no untagged
        /// <c>LIST</c> responses, an empty array is returned. This method never
        /// returns <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="FoldersAsync(string,string,System.Threading.CancellationToken)"/>
        /// and is provided for convenience in environments where asynchronous
        /// execution is not desirable. It uses the client's configured
        /// <see cref="Timeout"/> value to cancel the underlying asynchronous
        /// operation.
        /// </para>
        /// <para>
        /// The <c>LIST</c> command is defined in RFC 3501. It returns zero or
        /// more untagged <c>LIST</c> responses, each describing a mailbox. The
        /// mailbox name is always a string and MUST NOT be <c>NIL</c>. The hierarchy
        /// delimiter MAY be <c>NIL</c>, indicating that the mailbox has no hierarchy.
        /// </para>
        /// </remarks>
        public IMAP_r_u_List[] Folders(string referenceName = "",string pattern = "*")
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return FoldersAsync(referenceName,pattern,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FoldersAsync

        /// <summary>
        /// Executes the IMAP <c>LIST</c> command and returns all mailbox entries
        /// that match the specified reference name and pattern. The method returns
        /// zero or more <see cref="IMAP_r_u_List"/> objects corresponding to the
        /// untagged <c>LIST</c> responses sent by the server.
        /// </summary>
        /// <param name="referenceName">
        /// The reference name used as the base for mailbox lookup. Typically an
        /// empty string (<c>""</c>) to indicate the root of the hierarchy.
        /// </param>
        /// <param name="pattern">
        /// The mailbox pattern to match. Common values include <c>"*"</c> for all
        /// mailboxes or <c>"%"</c> for the current hierarchy level.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// An array of <see cref="IMAP_r_u_List"/> objects representing the
        /// mailboxes returned by the server. If the server returns no untagged
        /// <c>LIST</c> responses, an empty array is returned. This method never
        /// returns <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>LIST</c> command is defined in RFC 3501. It returns zero or
        /// more untagged <c>LIST</c> responses, each describing a mailbox. A
        /// mailbox entry includes attribute flags, an optional hierarchy delimiter,
        /// and a mailbox name. The mailbox name is always a string and MUST NOT be
        /// <c>NIL</c>. The hierarchy delimiter MAY be <c>NIL</c>, indicating that
        /// the mailbox has no hierarchy.
        /// </para>
        /// </remarks>
        public async ValueTask<IMAP_r_u_List[]> FoldersAsync(string referenceName = "",string pattern = "*",CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     LIST
               Responses:   zero or more untagged LIST responses
                            tagged OK / NO / BAD completion

               The LIST command returns mailbox names that match the specified
               reference name and mailbox pattern. A server may return zero or more
               untagged LIST responses. Each LIST response describes a single mailbox
               and has the form:

                   * LIST (<attributes>) "<delimiter>" "<name>"

               The <attributes> list may include flags such as \Noselect, \Noinferiors,
               \Marked, or \Unmarked. The <delimiter> is the hierarchy separator used
               within that mailbox namespace. The <name> is the mailbox name.

               IMPORTANT (RFC‑required):
                   - The mailbox name MUST NOT be NIL.
                     It is always a string, possibly the empty string ("").
                   - The hierarchy delimiter MAY be NIL.
                     NIL delimiter indicates that the mailbox has no hierarchy.

               Examples:
                   C: A0001 LIST "" "*"
                   S: * LIST (\HasChildren) "/" "INBOX"
                   S: * LIST (\Noselect) "/" "Archive"
                   S: A0001 OK Completed

                   C: A0002 LIST "" "INBOX"
                   S: * LIST () NIL "INBOX"        ; NIL delimiter is allowed
                   S: A0002 OK Completed

               Empty result cases:
                   1) The server may return no untagged LIST responses at all:
                          C: A0003 LIST "" "NonExisting/*"
                          S: A0003 OK Completed
                      This indicates that the pattern matched no mailboxes.
                      The client must treat this as an empty result set.

                   2) The server may return a LIST response with an empty mailbox name:
                          * LIST () "/" ""
                      This is a valid mailbox name (empty string), not NIL.
                      The client must treat this as a valid mailbox entry.

               In both empty-result cases above, the LIST command succeeded and the
               correct client behavior is to return an empty collection (zero mailboxes),
               not null.

               Error cases:
                   If the server returns a tagged NO or BAD response, the LIST command
                   has failed. A BAD response may indicate that the server does not
                   support the LIST command or that the arguments were invalid. The
                   client should throw an exception in these cases.

               The LIST command is mandatory in IMAP4rev1. A successful LIST command
               always ends with a tagged OK, regardless of whether any mailboxes were
               returned. The absence of LIST responses does not indicate lack of
               support; it simply means that the pattern matched zero mailboxes.
            */
            referenceName = IMAP_Utils.EncodeMailbox(referenceName,m_MailboxEncoding);
            pattern       = IMAP_Utils.EncodeMailbox(pattern,m_MailboxEncoding);

            List<IMAP_r_u_List> retVal = new List<IMAP_r_u_List>();
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(e is IMAP_r_u_List item){
                    retVal.Add(item);
                }
            };

            await SendCommandLineAsync($"{(m_CommandIndex++).ToString("d5")} LIST {referenceName} {pattern}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            return retVal.ToArray();
        }

        #endregion

        #region method FolderCreate

        /// <summary>
        /// Executes the IMAP <c>CREATE</c> command synchronously and creates a new
        /// mailbox with the specified name. This method blocks until the command
        /// completes or the operation times out.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to create. The value must be a valid IMAP
        /// mailbox name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response, indicating that the mailbox could not be created.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="FolderCreateAsync(string,System.Threading.CancellationToken)"/>.
        /// It uses the client's configured <see cref="Timeout"/> value to cancel the
        /// underlying asynchronous operation if it does not complete in time.
        /// </para>
        /// <para>
        /// The IMAP <c>CREATE</c> command is defined in RFC 3501. It requests
        /// the server to create a new mailbox. The mailbox name MUST be a valid
        /// IMAP mailbox name and MUST NOT be <c>NIL</c>. The server returns a
        /// tagged <c>OK</c> response on success, or a tagged <c>NO</c> or <c>BAD</c>
        /// response if the mailbox cannot be created.
        /// </para>
        /// <para>
        /// Common reasons for failure include: the mailbox already exists, the
        /// mailbox name is invalid, creation under the specified parent is not
        /// permitted, insufficient permissions, or server‑specific namespace
        /// restrictions (such as prohibiting creation of subfolders under
        /// <c>INBOX</c>).
        /// </para>
        /// </remarks>
        public void FolderCreate(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderCreateAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderCreateAsync

        /// <summary>
        /// Executes the IMAP <c>CREATE</c> command asynchronously and requests the
        /// server to create a new mailbox with the specified name.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to create. The value must be a valid IMAP
        /// mailbox name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous completion of the mailbox creation
        /// request. The method does not return a value; it completes normally if
        /// the server returns a tagged <c>OK</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response, indicating that the mailbox could not be created.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The IMAP <c>CREATE</c> command is defined in RFC 3501. It requests
        /// the server to create a new mailbox. The mailbox name MUST be a valid
        /// IMAP mailbox name and MUST NOT be <c>NIL</c>. The server returns a
        /// tagged <c>OK</c> response on success, or a tagged <c>NO</c> or <c>BAD</c>
        /// response if the mailbox cannot be created.
        /// </para>
        /// <para>
        /// Common reasons for failure include: the mailbox already exists, the
        /// mailbox name is invalid, creation under the specified parent is not
        /// permitted, insufficient permissions, or server‑specific namespace
        /// restrictions (such as prohibiting creation of subfolders under
        /// <c>INBOX</c>).
        /// </para>
        /// <para>
        /// The <c>CREATE</c> command does not automatically select the new mailbox
        /// and does not return any untagged responses. It only reports success or
        /// failure through the final tagged response.
        /// </para>
        /// </remarks>
        public async ValueTask FolderCreateAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(folder == string.Empty){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     CREATE
               Responses:   tagged OK / NO / BAD completion
                            (no untagged responses are required)

               The CREATE command requests the server to create a new mailbox with the
               specified name. The mailbox name MUST be a valid IMAP mailbox name and
               MUST NOT be NIL. The server creates the mailbox if possible and returns
               a tagged OK response. If the mailbox cannot be created, the server
               returns a tagged NO or BAD response.

               Syntax:
                   CREATE "<mailbox>"

               The mailbox name is interpreted using the server's hierarchy delimiter.
               If the name contains the hierarchy separator, intermediate levels may
               or may not be created automatically depending on server implementation.
               IMAP does not require servers to create intermediate directories.

               Examples:
                   C: A0001 CREATE "Archive/2024"
                   S: A0001 OK Create completed

                   C: A0002 CREATE "INBOX/Reports"
                   S: A0002 NO Cannot create inside INBOX

                   C: A0003 CREATE "Archive/2024"
                   S: A0003 NO Mailbox already exists

               Error cases:
                   - NO: The server refuses to create the mailbox. Common reasons:
                         * Mailbox already exists
                         * Invalid mailbox name
                         * Server does not allow creation under the specified parent
                         * Permission denied
                         * Intermediate hierarchy not allowed
                   - BAD: The command is invalid or not supported.

               Special notes:
                   - CREATE does not automatically select the new mailbox.
                   - CREATE does not return any untagged responses.
                   - CREATE does not create messages; it only creates the mailbox.
                   - Servers MAY restrict creation under certain namespaces (e.g., INBOX).
                   - Servers MAY require the client to use LIST to discover valid
                     hierarchy delimiters before issuing CREATE.

               Client behavior:
                   - On tagged OK: mailbox was successfully created.
                   - On tagged NO/BAD: the client must throw an exception.
                   - CREATE never returns NIL and never returns an empty result set.
                     It always ends with a tagged OK/NO/BAD response.
            */
            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} CREATE {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderDelete

        /// <summary>
        /// Executes the IMAP <c>DELETE</c> command synchronously and requests the
        /// server to permanently remove the specified mailbox. This method blocks
        /// until the command completes or the operation times out.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to delete. The value must be a valid IMAP
        /// mailbox name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response, indicating that the mailbox could not be deleted.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="FolderDeleteAsync(string,System.Threading.CancellationToken)"/>.
        /// It uses the client's configured <see cref="Timeout"/> value to cancel the
        /// underlying asynchronous operation if it does not complete in time.
        /// </para>
        /// <para>
        /// The IMAP <c>DELETE</c> command is defined in RFC 3501. It requests
        /// the server to permanently remove the specified mailbox. The mailbox name
        /// MUST be a valid IMAP mailbox name and MUST NOT be <c>NIL</c>. The server
        /// returns a tagged <c>OK</c> response on success, or a tagged <c>NO</c> or
        /// <c>BAD</c> response if the mailbox cannot be deleted.
        /// </para>
        /// <para>
        /// Common reasons for failure include: the mailbox does not exist, the
        /// mailbox is protected (such as <c>INBOX</c>), the user lacks sufficient
        /// rights, or the server requires subordinate mailboxes to be removed
        /// first. Servers MAY also impose namespace‑specific restrictions.
        /// </para>
        /// <para>
        /// The <c>DELETE</c> command does not return untagged responses and does not
        /// remove messages individually; it removes the mailbox itself. A client
        /// MUST inspect the final tagged response to determine whether the mailbox
        /// was successfully deleted.
        /// </para>
        /// </remarks>
        public void FolderDelete(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderDeleteAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderDeleteAsync

        /// <summary>
        /// Executes the IMAP <c>DELETE</c> command asynchronously and requests the
        /// server to permanently remove the specified mailbox.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to delete. The value must be a valid IMAP
        /// mailbox name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous completion of the mailbox deletion
        /// request. The method does not return a value; it completes normally if
        /// the server returns a tagged <c>OK</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response, indicating that the mailbox could not be deleted.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The IMAP <c>DELETE</c> command is defined in RFC 3501. It requests
        /// the server to permanently remove the specified mailbox. The mailbox name
        /// MUST be a valid IMAP mailbox name and MUST NOT be <c>NIL</c>. The server
        /// returns a tagged <c>OK</c> response on success, or a tagged <c>NO</c> or
        /// <c>BAD</c> response if the mailbox cannot be deleted.
        /// </para>
        /// <para>
        /// Common reasons for failure include: the mailbox does not exist, the
        /// mailbox is protected (such as <c>INBOX</c>), the user lacks sufficient
        /// rights, or the server requires subordinate mailboxes to be removed
        /// first. Servers MAY also impose namespace‑specific restrictions.
        /// </para>
        /// <para>
        /// This method automatically encodes the mailbox name using the client's
        /// configured mailbox encoding (IMAP modified UTF‑7 or UTF‑8), escapes any
        /// required characters, and formats the mailbox name according to IMAP
        /// quoted‑string rules before issuing the <c>DELETE</c> command.
        /// </para>
        /// <para>
        /// The <c>DELETE</c> command does not return untagged responses and does not
        /// remove messages individually; it removes the mailbox itself. A client
        /// MUST inspect the final tagged response to determine whether the mailbox
        /// was successfully deleted.
        /// </para>
        /// </remarks>
        public async ValueTask FolderDeleteAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(folder == string.Empty){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     DELETE
               Responses:   tagged OK / NO / BAD completion
                            (no untagged responses are required)

               The DELETE command permanently removes the specified mailbox. The mailbox
               name MUST be a valid IMAP mailbox name and MUST NOT be NIL. If the mailbox
               exists and the server permits deletion, a tagged OK response is returned.
               If the mailbox cannot be deleted, the server returns a tagged NO or BAD
               response.

               Syntax:
                   DELETE "<mailbox>"

               A server MAY refuse to delete a mailbox for several reasons, including:
                   - The mailbox does not exist.
                   - The mailbox has the \Noselect attribute.
                   - The mailbox is a special or protected mailbox (e.g., INBOX).
                   - The user does not have sufficient rights to delete the mailbox.
                   - The mailbox contains inferior hierarchical mailboxes that must be
                     removed first (server-dependent behavior).

               Examples:
                   C: A0001 DELETE "Archive/2024"
                   S: A0001 OK Delete completed

                   C: A0002 DELETE "INBOX"
                   S: A0002 NO Cannot delete INBOX

                   C: A0003 DELETE "Archive/2024"
                   S: A0003 NO Mailbox does not exist

               Error cases:
                   - NO: The server refuses to delete the mailbox. Common reasons:
                         * Mailbox does not exist
                         * Mailbox is protected (e.g., INBOX)
                         * Insufficient permissions
                         * Inferior mailboxes prevent deletion
                   - BAD: The command is invalid or not supported.

               Special notes:
                   - DELETE does not remove messages individually; it removes the mailbox
                     itself.
                   - DELETE does not automatically remove subordinate mailboxes unless
                     the server explicitly supports such behavior.
                   - DELETE does not return any untagged responses.
                   - Servers MAY restrict deletion of certain namespaces or special-use
                     mailboxes.

               Client behavior:
                   - On tagged OK: mailbox was successfully deleted.
                   - On tagged NO/BAD: the client must throw an exception.
                   - DELETE never returns NIL and never returns an empty result set.
                     It always ends with a tagged OK/NO/BAD response.
            */
            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} DELETE {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderRename

        /// <summary>
        /// Executes the IMAP <c>RENAME</c> command synchronously and requests the
        /// server to change the name of an existing mailbox. This method blocks
        /// until the command completes or the operation times out.
        /// </summary>
        /// <param name="folder">
        /// The current name of the mailbox to rename. The value must be a valid
        /// IMAP mailbox name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <param name="newFolder">
        /// The new name for the mailbox. The value must be a valid IMAP mailbox
        /// name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> or <paramref name="newFolder"/>
        /// is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="newFolder"/>
        /// is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response, indicating that the mailbox could not be renamed.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="FolderRenameAsync(string,string,System.Threading.CancellationToken)"/>.
        /// It uses the client's configured <see cref="Timeout"/> value to cancel the
        /// underlying asynchronous operation if it does not complete in time.
        /// </para>
        /// <para>
        /// The IMAP <c>RENAME</c> command is defined in RFC 3501. It requests
        /// the server to change the name of an existing mailbox. The source mailbox
        /// MUST exist, and the destination mailbox name MUST be a valid IMAP
        /// mailbox name. The server returns a tagged <c>OK</c> response on success,
        /// or a tagged <c>NO</c> or <c>BAD</c> response if the rename cannot be
        /// performed.
        /// </para>
        /// <para>
        /// Common reasons for failure include: the source mailbox does not exist,
        /// the destination mailbox already exists, insufficient permissions,
        /// renaming <c>INBOX</c>, or namespace restrictions. Servers MAY also apply
        /// implementation‑specific rules to subordinate mailboxes.
        /// </para>
        /// <para>
        /// The <c>RENAME</c> command does not return untagged responses and does not
        /// automatically select the new mailbox. A client MUST inspect the final
        /// tagged response to determine whether the rename was successful.
        /// </para>
        /// </remarks>
        public void FolderRename(string folder,string newFolder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderRenameAsync(folder,newFolder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderRenameAsync

        /// <summary>
        /// Executes the IMAP <c>RENAME</c> command asynchronously and requests the
        /// server to change the name of an existing mailbox.
        /// </summary>
        /// <param name="folder">
        /// The current name of the mailbox to rename. The value must be a valid
        /// IMAP mailbox name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <param name="newFolder">
        /// The new name for the mailbox. The value must be a valid IMAP mailbox
        /// name and must not be <c>null</c> or an empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous completion of the mailbox rename
        /// request. The method completes normally if the server returns a tagged
        /// <c>OK</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> or <paramref name="newFolder"/>
        /// is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="newFolder"/>
        /// is an empty string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response, indicating that the mailbox could not be renamed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The IMAP <c>RENAME</c> command is defined in RFC 3501. It requests
        /// the server to change the name of an existing mailbox. The source mailbox
        /// MUST exist, and the destination mailbox name MUST be a valid IMAP
        /// mailbox name. The server returns a tagged <c>OK</c> response on success,
        /// or a tagged <c>NO</c> or <c>BAD</c> response if the rename cannot be
        /// performed.
        /// </para>
        /// <para>
        /// Common reasons for failure include: the source mailbox does not exist,
        /// the destination mailbox already exists, insufficient permissions,
        /// renaming <c>INBOX</c>, or namespace restrictions. Servers MAY also apply
        /// implementation‑specific rules to subordinate mailboxes.
        /// </para>
        /// <para>
        /// This method automatically encodes both mailbox names using the client's
        /// configured mailbox encoding (IMAP modified UTF‑7 or UTF‑8), escapes any
        /// required characters, and formats the names according to IMAP
        /// quoted‑string rules before issuing the <c>RENAME</c> command.
        /// </para>
        /// <para>
        /// The <c>RENAME</c> command does not return untagged responses and does not
        /// automatically select the new mailbox. A client MUST inspect the final
        /// tagged response to determine whether the rename was successful.
        /// </para>
        /// </remarks>
        public async ValueTask FolderRenameAsync(string folder,string newFolder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(folder == string.Empty){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }            
            if(newFolder == null){
                throw new ArgumentNullException(nameof(newFolder));
            }
            if(newFolder == string.Empty){
                throw new ArgumentException("New folder name must be specified.", nameof(newFolder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     RENAME
               Responses:   tagged OK / NO / BAD completion
                            (no untagged responses are required)

               The RENAME command changes the name of an existing mailbox. The source
               mailbox name MUST refer to an existing mailbox, and the destination
               mailbox name MUST be a valid IMAP mailbox name and MUST NOT be NIL.
               If the server is able to perform the rename, it returns a tagged OK
               response. If the rename cannot be performed, the server returns a
               tagged NO or BAD response.

               Syntax:
                   RENAME "<old-mailbox>" "<new-mailbox>"

               A server MAY refuse to rename a mailbox for several reasons, including:
                   - The source mailbox does not exist.
                   - The source mailbox has the \Noselect attribute.
                   - The source mailbox is a special or protected mailbox (e.g., INBOX).
                   - The user does not have sufficient rights to rename the mailbox.
                   - The destination mailbox name is invalid or already exists.
                   - The server does not permit renaming across certain namespaces.
                   - The server requires subordinate mailboxes to be renamed or moved
                     according to implementation-specific rules.

               Examples:
                   C: A0001 RENAME "Archive/2024" "Archive/2025"
                   S: A0001 OK Rename completed

                   C: A0002 RENAME "INBOX" "OldInbox"
                   S: A0002 NO Cannot rename INBOX

                   C: A0003 RENAME "Archive/2024" "Archive/2024"
                   S: A0003 NO Destination mailbox already exists

               Error cases:
                   - NO: The server refuses to rename the mailbox. Common reasons:
                         * Source mailbox does not exist
                         * Destination mailbox already exists
                         * Insufficient permissions
                         * Renaming INBOX or other protected mailboxes
                         * Namespace restrictions
                   - BAD: The command is invalid or not supported.

               Special notes:
                   - RENAME does not automatically select the new mailbox.
                   - RENAME does not return any untagged responses.
                   - Servers MAY automatically rename subordinate mailboxes, but this
                     behavior is implementation-specific and not required by the RFC.
                   - RENAME never returns NIL and never returns an empty result set.
                     It always ends with a tagged OK/NO/BAD response.

               Client behavior:
                   - On tagged OK: the mailbox was successfully renamed.
                   - On tagged NO/BAD: the client must throw an exception.
            */

            folder    = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);
            newFolder = IMAP_Utils.EncodeMailbox(newFolder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} RENAME {folder} {newFolder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FoldersSubscribed

        /// <summary>
        /// Executes the IMAP <c>LSUB</c> command and returns all subscribed mailbox
        /// entries that match the specified reference name and pattern. This is the
        /// synchronous wrapper for <see cref="FoldersSubscribedAsync"/>.
        /// </summary>
        /// <param name="referenceName">
        /// The reference name used as the base for subscribed mailbox lookup.
        /// Typically an empty string (<c>""</c>) to indicate the root of the
        /// hierarchy.
        /// </param>
        /// <param name="pattern">
        /// The mailbox pattern to match. Common values include <c>"*"</c> for all
        /// subscribed mailboxes or <c>"%"</c> for the current hierarchy level.
        /// </param>
        /// <returns>
        /// An array of <see cref="IMAP_r_u_LSub"/> objects representing the
        /// subscribed mailboxes returned by the server. If the server returns no
        /// untagged <c>LSUB</c> responses, an empty array is returned. This method
        /// never returns <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks until the asynchronous <c>LSUB</c> operation completes.
        /// It uses the client's configured timeout to create a cancellation token
        /// for the underlying asynchronous call.
        /// </para>
        /// </remarks>
        public IMAP_r_u_LSub[] FoldersSubscribed(string referenceName = "",string pattern = "*")
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return FoldersSubscribedAsync(referenceName,pattern,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FoldersSubscribedAsync

        /// <summary>
        /// Executes the IMAP <c>LSUB</c> command and returns all subscribed mailbox
        /// entries that match the specified reference name and pattern. The method
        /// returns zero or more <see cref="IMAP_r_u_LSub"/> objects corresponding to
        /// the untagged <c>LSUB</c> responses sent by the server.
        /// </summary>
        /// <param name="referenceName">
        /// The reference name used as the base for mailbox lookup. Typically an
        /// empty string (<c>""</c>) to indicate the root of the hierarchy.
        /// </param>
        /// <param name="pattern">
        /// The mailbox pattern to match. Common values include <c>"*"</c> for all
        /// subscribed mailboxes or <c>"%"</c> for the current hierarchy level.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// An array of <see cref="IMAP_r_u_LSub"/> objects representing the
        /// subscribed mailboxes returned by the server. If the server returns no
        /// untagged <c>LSUB</c> responses, an empty array is returned. This method
        /// never returns <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>LSUB</c> command is defined in RFC 3501. It returns zero or
        /// more untagged <c>LSUB</c> responses, each describing a subscribed mailbox.
        /// The response format is identical to <c>LIST</c>, consisting of attribute
        /// flags, an optional hierarchy delimiter, and a mailbox name.
        /// </para>
        /// <para>
        /// The mailbox name is always a string and MUST NOT be <c>NIL</c>. The
        /// hierarchy delimiter MAY be <c>NIL</c>, indicating that the mailbox has
        /// no hierarchy. Unlike <c>LIST</c>, the <c>LSUB</c> command reports only
        /// mailboxes that the user is subscribed to, and does not guarantee that
        /// the mailbox currently exists.
        /// </para>
        /// <para>
        /// If the pattern matches no subscribed mailboxes, the server may return
        /// no untagged <c>LSUB</c> responses. This is treated as a successful
        /// command with an empty result set.
        /// </para>
        /// </remarks>
        public async ValueTask<IMAP_r_u_LSub[]> FoldersSubscribedAsync(string referenceName = "",string pattern = "*",CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     LSUB
               Responses:   zero or more untagged LSUB responses
                            tagged OK / NO / BAD completion

               The LSUB command returns the set of mailboxes that the user is
               subscribed to. It does NOT list all mailboxes — only those that
               have been previously subscribed via the SUBSCRIBE command.

               The server may return zero or more untagged LSUB responses. Each
               LSUB response describes a single subscribed mailbox and has the form:

                   * LSUB (<attributes>) "<delimiter>" "<name>"

               The <attributes> list may include flags such as:
                   \Noselect     mailbox cannot be selected
                   \Marked       mailbox has new messages
                   \Unmarked     mailbox does not have new messages

               The <delimiter> is the hierarchy separator used within that mailbox
               namespace. The delimiter MAY be NIL, indicating that the mailbox has
               no hierarchy.

               IMPORTANT:
                   - The mailbox name MUST NOT be NIL.
                     It is always a string, possibly the empty string ("").
                   - LSUB returns only subscribed mailboxes.
                     It does not indicate whether the mailbox actually exists.
                   - Servers MAY return \Noselect for mailboxes that no longer exist
                     but remain in the subscription list.

               Examples:
                   C: A0001 LSUB "" "*"
                   S: * LSUB (\HasChildren) "/" "INBOX"
                   S: * LSUB (\Noselect) "/" "OldArchive"
                   S: A0001 OK Completed

                   C: A0002 LSUB "" "INBOX"
                   S: * LSUB () NIL "INBOX"
                   S: A0002 OK Completed

               Empty result cases:
                   1) The server may return no untagged LSUB responses at all:
                          C: A0003 LSUB "" "NonExisting/*"
                          S: A0003 OK Completed
                      This indicates that the pattern matched no subscribed mailboxes.
                      The client must treat this as an empty result set.

                   2) The server may return an LSUB response with an empty mailbox name:
                          * LSUB () "/" ""
                      This is a valid mailbox name (empty string), not NIL.
                      The client must treat this as a valid mailbox entry.

               In both empty-result cases above, the LSUB command succeeded and the
               correct client behavior is to return an empty collection (zero mailboxes),
               not null.

               Error cases:
                   If the server returns a tagged NO or BAD response, the LSUB command
                   has failed. A BAD response may indicate that the server does not
                   support the LSUB command or that the arguments were invalid. The
                   client should throw an exception in these cases.

               Notes:
                   - LSUB does not indicate subscription state changes; it only returns
                     the current subscription list.
                   - LSUB does not return message counts or mailbox status.
                   - LSUB is optional in IMAP4rev1, but widely implemented.
            */
            
            referenceName = IMAP_Utils.EncodeMailbox(referenceName,m_MailboxEncoding);
            pattern       = IMAP_Utils.EncodeMailbox(pattern,m_MailboxEncoding);

            List<IMAP_r_u_LSub> retVal = new List<IMAP_r_u_LSub>();
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(e is IMAP_r_u_LSub item){
                    retVal.Add(item);
                }
            };

            await SendCommandLineAsync($"{(m_CommandIndex++).ToString("d5")} LSUB {referenceName} {pattern}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            return retVal.ToArray();
        }

        #endregion

        #region method FolderSubscribe

        /// <summary>
        /// Executes the IMAP <c>SUBSCRIBE</c> command for the specified mailbox.
        /// This is the synchronous wrapper for <see cref="FolderSubscribeAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name to subscribe. The value must be a non‑empty string.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks until the asynchronous <c>SUBSCRIBE</c> operation
        /// completes. It uses the client's configured timeout to create a
        /// cancellation token for the underlying asynchronous call.
        /// </para>
        /// <para>
        /// The <c>SUBSCRIBE</c> command adds the mailbox name to the user's
        /// subscription list but does not create the mailbox. Servers may accept
        /// subscriptions for nonexistent mailboxes or may reject them with a
        /// <c>NO</c> response.
        /// </para>
        /// </remarks>
        public void FolderSubscribe(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderSubscribeAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderSubscribeAsync

        /// <summary>
        /// Executes the IMAP <c>SUBSCRIBE</c> command for the specified mailbox.
        /// This adds the mailbox name to the user's subscription list so that it
        /// appears in <c>LSUB</c> results.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name to subscribe. The value must be a non‑empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. If the server returns
        /// a tagged <c>OK</c> response, the subscription succeeds. If the server
        /// returns <c>NO</c> or <c>BAD</c>, an <see cref="IMAP_ClientException"/>
        /// is thrown.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SUBSCRIBE</c> command is defined in RFC 3501. It records
        /// the subscription state for a mailbox but does not create the mailbox.
        /// Servers may accept subscriptions for nonexistent mailboxes or may
        /// reject them with a <c>NO</c> response.
        /// </para>
        /// <para>
        /// The mailbox name is encoded using the client's current mailbox encoding
        /// before being sent to the server. The command does not produce untagged
        /// mailbox responses; only the final tagged completion response is returned.
        /// </para>
        /// </remarks>
        public async ValueTask FolderSubscribeAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     SUBSCRIBE
               Responses:   tagged OK / NO / BAD completion
                            (no untagged mailbox responses)

               The SUBSCRIBE command adds the specified mailbox name to the user's
               subscription list. Subscribed mailboxes are those returned by the
               <LSUB> command. SUBSCRIBE does not create the mailbox; it only records
               the subscription state.

               Command syntax:
                   tag SUBSCRIBE "<mailbox>"

               The server does not return untagged mailbox data. A successful
               SUBSCRIBE command ends with a tagged OK response. A tagged NO or BAD
               indicates failure.

               IMPORTANT:
                   - The mailbox name MUST NOT be NIL.
                     It is always a string, possibly the empty string ("").
                   - SUBSCRIBE does NOT create the mailbox.
                     If the mailbox does not exist, servers may still accept the
                     subscription or may reject it with NO.
                   - SUBSCRIBE only modifies the subscription list.
                     It does not affect mailbox existence, flags, or message state.
                   - Servers MAY allow subscriptions to nonexistent mailboxes.
                     This is server‑dependent behavior.

               Examples:
                   C: A0001 SUBSCRIBE "INBOX"
                   S: A0001 OK Subscribed

                   C: A0002 SUBSCRIBE "Archive/2020"
                   S: A0002 OK Completed

                   C: A0003 SUBSCRIBE "NonExisting"
                   S: A0003 NO Mailbox does not exist

               Error cases:
                   - A tagged NO response indicates that the server refused the
                     subscription (e.g., nonexistent mailbox, permission denied).
                   - A tagged BAD response indicates invalid arguments or that the
                     server does not support SUBSCRIBE.
                   - The client should throw an exception for NO or BAD responses.

               Notes:
                   - SUBSCRIBE is optional in IMAP4rev1 but widely implemented.
                   - SUBSCRIBE does not return mailbox attributes or hierarchy
                     information; use LIST or LSUB for that.
                   - SUBSCRIBE does not affect the selected mailbox state.
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} SUBSCRIBE {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderUnsubscribe

        /// <summary>
        /// Executes the IMAP <c>UNSUBSCRIBE</c> command for the specified mailbox.
        /// This is the synchronous wrapper for <see cref="FolderUnsubscribeAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name to unsubscribe. The value must be a non‑empty string.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks until the asynchronous <c>UNSUBSCRIBE</c> operation
        /// completes. It uses the client's configured timeout to create a
        /// cancellation token for the underlying asynchronous call.
        /// </para>
        /// <para>
        /// The <c>UNSUBSCRIBE</c> command removes the mailbox name from the user's
        /// subscription list but does not delete the mailbox. Servers may accept
        /// unsubscribe requests for nonexistent mailboxes or may reject them with
        /// a <c>NO</c> response.
        /// </para>
        /// </remarks>
        public void FolderUnsubscribe(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderUnsubscribeAsync(folder, cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderUnsubscribeAsync

        /// <summary>
        /// Executes the IMAP <c>UNSUBSCRIBE</c> command for the specified mailbox.
        /// This removes the mailbox name from the user's subscription list so that
        /// it no longer appears in <c>LSUB</c> results.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name to unsubscribe. The value must be a non‑empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. If the server returns
        /// a tagged <c>OK</c> response, the unsubscribe succeeds. If the server
        /// returns <c>NO</c> or <c>BAD</c>, an <see cref="IMAP_ClientException"/>
        /// is thrown.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>UNSUBSCRIBE</c> command is defined in RFC 3501. It removes
        /// the mailbox name from the user's subscription list but does not delete
        /// the mailbox. Servers may accept unsubscribe requests for nonexistent
        /// mailboxes or may reject them with a <c>NO</c> response.
        /// </para>
        /// <para>
        /// The mailbox name is encoded using the client's current mailbox encoding
        /// before being sent to the server. The command does not produce untagged
        /// mailbox responses; only the final tagged completion response is returned.
        /// </para>
        /// </remarks>
        public async ValueTask FolderUnsubscribeAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     UNSUBSCRIBE
               Responses:   tagged OK / NO / BAD completion
                            (no untagged mailbox responses)

               The UNSUBSCRIBE command removes the specified mailbox name from the
               user's subscription list. After successful completion, the mailbox
               will no longer appear in <LSUB> results. UNSUBSCRIBE does not delete
               the mailbox; it only modifies the subscription state.

               Command syntax:
                   tag UNSUBSCRIBE "<mailbox>"

               The server does not return untagged mailbox data. A successful
               UNSUBSCRIBE command ends with a tagged OK response. A tagged NO or BAD
               indicates failure.

               IMPORTANT:
                   - The mailbox name MUST NOT be NIL.
                     It is always a string, possibly the empty string ("").
                   - UNSUBSCRIBE does NOT delete the mailbox.
                     It only removes the subscription entry.
                   - Servers MAY allow unsubscribing from nonexistent mailboxes.
                     This is server‑dependent behavior.
                   - If the mailbox is not currently subscribed, servers may still
                     return OK or may return NO depending on implementation.

               Examples:
                   C: A0001 UNSUBSCRIBE "INBOX"
                   S: A0001 OK Unsubscribed

                   C: A0002 UNSUBSCRIBE "Archive/2020"
                   S: A0002 OK Completed

                   C: A0003 UNSUBSCRIBE "NonExisting"
                   S: A0003 NO Mailbox not subscribed

               Error cases:
                   - A tagged NO response indicates that the server refused the
                     unsubscribe request (e.g., mailbox not subscribed, permission denied).
                   - A tagged BAD response indicates invalid arguments or that the
                     server does not support UNSUBSCRIBE.
                   - The client should throw an exception for NO or BAD responses.

               Notes:
                   - UNSUBSCRIBE is optional in IMAP4rev1 but widely implemented.
                   - UNSUBSCRIBE does not return mailbox attributes or hierarchy
                     information; use LIST or LSUB for that.
                   - UNSUBSCRIBE does not affect the selected mailbox state.
            */


            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} UNSUBSCRIBE {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderStatus

        /// <summary>
        /// Executes the IMAP <c>STATUS</c> command for the specified mailbox.
        /// This is the synchronous wrapper for <see cref="FolderStatusAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose status information is requested. The value must
        /// be a non‑empty string.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_Status"/> object containing the mailbox status
        /// values returned by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server violates the IMAP protocol by failing to return
        /// the required untagged <c>STATUS</c> response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks until the asynchronous <c>STATUS</c> operation
        /// completes. It uses the client's configured timeout to create a
        /// cancellation token for the underlying asynchronous call.
        /// </para>
        /// <para>
        /// The <c>STATUS</c> command retrieves mailbox metadata such as message
        /// counts and UID values without selecting the mailbox. The server MUST
        /// return exactly one untagged <c>STATUS</c> response.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Status FolderStatus(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return FolderStatusAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderStatusAsync

        /// <summary>
        /// Executes the IMAP <c>STATUS</c> command for the specified mailbox and
        /// returns the server‑reported status information.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose status information is requested. The value must
        /// be a non‑empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_Status"/> object containing the mailbox status
        /// values returned by the server. The server MUST return exactly one
        /// untagged <c>STATUS</c> response. If the server returns <c>NO</c> or
        /// <c>BAD</c>, an <see cref="IMAP_ClientException"/> is thrown.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server violates the IMAP protocol by failing to return
        /// the required untagged <c>STATUS</c> response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>STATUS</c> command is defined in RFC 3501. It retrieves
        /// mailbox metadata such as message counts and UID values without
        /// selecting the mailbox. The mailbox name is encoded using the client's
        /// configured mailbox encoding before being sent to the server.
        /// </para>
        /// <para>
        /// The server MUST return exactly one untagged <c>STATUS</c> response.
        /// Multiple responses or the absence of a <c>STATUS</c> response indicate
        /// a protocol violation and result in an <see cref="IMAP_ProtocolException"/>.
        /// </para>
        /// </remarks>
        public async ValueTask<IMAP_r_u_Status> FolderStatusAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     STATUS
               Responses:   one untagged STATUS response
                            tagged OK / NO / BAD completion

               The STATUS command requests the status of a single mailbox. The client
               specifies a mailbox name and a list of status data items. The server
               returns exactly one untagged STATUS response containing the requested
               data items.

               Command syntax:
                   tag STATUS "<mailbox>" (status-att-list)

               The server MUST return exactly one untagged STATUS response. Multiple
               STATUS responses are not permitted. The STATUS response contains the
               mailbox name and a parenthesized list of attribute/value pairs.

               Common status data items:
                   MESSAGES   – number of messages in the mailbox
                   RECENT     – number of messages with the \Recent flag set
                   UIDNEXT    – next unique identifier value
                   UIDVALIDITY– unique identifier validity value
                   UNSEEN     – number of messages without the \Seen flag set

               Examples:
                   C: A0001 STATUS "INBOX" (MESSAGES RECENT UIDNEXT)
                   S: * STATUS "INBOX" (MESSAGES 17 RECENT 2 UIDNEXT 123)
                   S: A0001 OK STATUS completed

                   C: A0002 STATUS "Archive/2020" (UNSEEN)
                   S: * STATUS "Archive/2020" (UNSEEN 5)
                   S: A0002 OK Completed

               IMPORTANT:
                   - STATUS queries a mailbox without selecting it.
                   - STATUS does not change the selected mailbox state.
                   - STATUS does not return message data; only mailbox metadata.
                   - The mailbox name MUST NOT be NIL.
                   - Servers MUST NOT return more than one STATUS response.

               Error cases:
                   - A tagged NO response indicates that the server refused the
                     request (e.g., nonexistent mailbox, permission denied).
                   - A tagged BAD response indicates invalid arguments or unsupported
                     status data items.
                   - The client should throw an exception for NO or BAD responses.

               Notes:
                   - STATUS is often used by clients to update mailbox counts without
                     selecting the mailbox.
                   - STATUS does not return hierarchy information; use LIST for that.
                   - STATUS does not return subscription state; use LSUB for that.
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            IMAP_r_u_Status? retVal = null;
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(retVal == null && e is IMAP_r_u_Status v){
                    retVal = v;
                }
            };

            await SendCommandLineAsync($"{m_CommandIndex++:d5} STATUS {folder} (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN)\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            if(retVal == null){
                throw new IMAP_ProtocolException("Server did not return required untagged STATUS response.");
            }

            return retVal;
        }

        #endregion

        #region method FolderSelect

        /// <summary>
        /// Synchronously executes the IMAP <c>SELECT</c> command and sets the specified
        /// mailbox as the currently selected folder.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to select. Must not be <c>null</c> or empty.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, or is currently
        /// in the IDLE state. The <c>SELECT</c> command cannot be issued while IDLE
        /// is active.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is empty or contains only whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response,
        /// indicating that the mailbox could not be selected.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This is the synchronous wrapper for <see cref="FolderSelectAsync"/>.  
        /// A cancellation token with the client's configured timeout is created
        /// internally, and the asynchronous operation is executed synchronously
        /// using <c>GetAwaiter().GetResult()</c>.
        /// </para>
        /// <para>
        /// For detailed behavior, mailbox state handling, and RFC 3501 semantics,
        /// see <see cref="FolderSelectAsync"/>.
        /// </para>
        /// </remarks>
        public void FolderSelect(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderSelectAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderSelectAsync

        /// <summary>
        /// Executes the IMAP <c>SELECT</c> command and sets the specified mailbox
        /// as the currently selected folder.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to select. Must not be <c>null</c> or empty.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, or is currently
        /// in the IDLE state. The <c>SELECT</c> command cannot be issued while IDLE
        /// is active.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is empty or contains only whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response,
        /// indicating that the mailbox could not be selected.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SELECT</c> command enters the IMAP "selected" state and makes the
        /// specified mailbox the active folder for subsequent message‑level commands
        /// such as <c>FETCH</c>, <c>STORE</c>, <c>SEARCH</c>, <c>COPY</c>, and others.
        /// Only one mailbox may be selected at a time.
        /// </para>
        /// <para>
        /// The server returns multiple untagged responses describing the mailbox
        /// state (e.g. <c>FLAGS</c>, <c>EXISTS</c>, <c>RECENT</c>,
        /// <c>UIDVALIDITY</c>, <c>UIDNEXT</c>, <c>PERMANENTFLAGS</c>,
        /// <c>UNSEEN</c>, <c>HIGHESTMODSEQ</c>). These responses are processed by the
        /// client's global untagged response handler, which updates the
        /// <see cref="IMAP_Client_SelectedFolder"/> instance accordingly.
        /// </para>
        /// <para>
        /// If the server indicates <c>[READ-ONLY]</c> in the tagged completion
        /// response, the selected folder is marked as read‑only. Otherwise, the folder
        /// is considered read‑write.
        /// </para>
        /// <para>
        /// If the command fails, the selected folder is cleared and an
        /// <see cref="IMAP_ClientException"/> is thrown.
        /// </para>
        /// <para>
        /// The mailbox name is encoded using the client's configured mailbox
        /// encoding before being sent to the server.
        /// </para>
        /// </remarks>
        public async ValueTask FolderSelectAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 – IMAP4rev1
               Command:     SELECT
               Responses:   multiple untagged mailbox state responses
                            tagged OK / NO / BAD completion

               The SELECT command selects a mailbox and enters the selected state.
               Once a mailbox is selected, the client may issue commands that operate
               on messages (FETCH, STORE, SEARCH, COPY, etc.). Only one mailbox may
               be selected at a time.

               Command syntax:
                   tag SELECT "<mailbox>"

               The server MUST return a series of untagged responses describing the
               mailbox state. These typically include:
                   FLAGS         – list of defined flags
                   OK [PERMANENTFLAGS] – flags the client may change permanently
                   OK [UIDVALIDITY]    – unique identifier validity value
                   OK [UIDNEXT]        – next unique identifier value
                   EXISTS        – number of messages in the mailbox
                   RECENT        – number of messages with the \Recent flag set
                   OK [UNSEEN]   – first unseen message (optional)
                   OK [HIGHESTMODSEQ] – highest modification sequence (if CONDSTORE)

               After all untagged responses, the server sends a tagged OK, NO, or BAD
               completion response.

               Examples:
                   C: A0001 SELECT "INBOX"
                   S: * 172 EXISTS
                   S: * 1 RECENT
                   S: * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)
                   S: * OK [PERMANENTFLAGS (\Deleted \Seen)] Limited
                   S: * OK [UIDVALIDITY 3857529045] UIDs valid
                   S: * OK [UIDNEXT 4392] Predicted next UID
                   S: A0001 OK [READ-WRITE] SELECT completed

                   C: A0002 SELECT "Archive/2020"
                   S: * 42 EXISTS
                   S: * 0 RECENT
                   S: * FLAGS (\Seen \Deleted)
                   S: * OK [UIDVALIDITY 123456] UIDs valid
                   S: * OK [UIDNEXT 987] Predicted next UID
                   S: A0002 OK [READ-ONLY] Completed

               IMPORTANT:
                   - SELECT changes the connection state to "selected".
                   - Only one mailbox may be selected at a time.
                   - SELECT may return READ-WRITE or READ-ONLY depending on server
                     permissions.
                   - The mailbox name MUST NOT be NIL.
                   - SELECT does not return subscription state; use LSUB for that.
                   - SELECT does not return hierarchy information; use LIST for that.

               Error cases:
                   - A tagged NO response indicates that the mailbox cannot be selected
                     (e.g., nonexistent mailbox, permission denied).
                   - A tagged BAD response indicates invalid arguments or unsupported
                     options.
                   - The client should throw an exception for NO or BAD responses.

               Notes:
                   - SELECT is one of the most complex IMAP commands due to the number
                     of required untagged responses.
                   - The client MUST process all untagged responses before the final
                     tagged completion.
                   - EXAMINE behaves like SELECT but always returns READ-ONLY.
            */

            // Release old selected folder.
            if(m_pSelectedFolder != null){
                m_pSelectedFolder.Dispose();
            }

            // Set new folder as selected folder, untagged responses fill all server resturend info.
            m_pSelectedFolder = new IMAP_Client_SelectedFolder(this,folder);

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} SELECT {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsSuccess){
                if(response.OptionalResponse is IMAP_t_orc_ReadOnly){
                    m_pSelectedFolder.SetReadOnly(true);
                }
            }
            else{
                m_pSelectedFolder = null;
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderExamine

        /// <summary>
        /// Executes the IMAP <c>EXAMINE</c> command and opens the specified mailbox
        /// in read-only mode.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to examine. Must not be <c>null</c> or empty.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, or is currently
        /// in the IDLE state. The <c>EXAMINE</c> command cannot be issued while IDLE
        /// is active.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is empty or contains only whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response,
        /// indicating that the mailbox could not be examined.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is the synchronous wrapper for
        /// <see cref="FolderExamineAsync(string, CancellationToken)"/>. A cancellation
        /// token with the client’s configured timeout is created internally, and the
        /// asynchronous operation is executed synchronously using
        /// <c>GetAwaiter().GetResult()</c>.
        /// </para>
        /// <para>
        /// The <c>EXAMINE</c> command selects a mailbox in read-only mode. The server
        /// MUST NOT permit any permanent state changes while the mailbox is examined.
        /// This includes flag updates, message deletions, keyword changes, and any
        /// operation that would alter the mailbox’s persistent state.
        /// </para>
        /// </remarks>
        public void FolderExamine(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderExamineAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderExamineAsync

        /// <summary>
        /// Executes the IMAP <c>EXAMINE</c> command and opens the specified mailbox
        /// in read-only mode.
        /// </summary>
        /// <param name="folder">
        /// The name of the mailbox to examine. Must not be <c>null</c> or empty.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token used to cancel the operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, or is currently
        /// in the IDLE state. The <c>EXAMINE</c> command cannot be issued while IDLE
        /// is active.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is empty or contains only whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response,
        /// indicating that the mailbox could not be examined.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>EXAMINE</c> command selects a mailbox in read-only mode. The server
        /// MUST NOT permit any permanent state changes while the mailbox is examined.
        /// This includes flag updates, message deletions, keyword changes, and any
        /// operation that would alter the mailbox’s persistent state.
        /// </para>
        /// <para>
        /// The server returns the same untagged responses as <c>SELECT</c>, including:
        /// <c>FLAGS</c>, <c>PERMANENTFLAGS</c>, <c>EXISTS</c>, <c>RECENT</c>,
        /// <c>UIDVALIDITY</c>, <c>UIDNEXT</c>, and optionally <c>UNSEEN</c> and
        /// <c>HIGHESTMODSEQ</c>. The tagged completion response MUST include
        /// <c>[READ-ONLY]</c> to indicate that the mailbox cannot be modified.
        /// </para>
        /// <para>
        /// Because the mailbox is opened read-only, the server may suppress certain
        /// <c>\Recent</c> adjustments that would normally occur when a mailbox is
        /// opened read-write. Clients should not rely on <c>\Recent</c> semantics for
        /// synchronization when using <c>EXAMINE</c>.
        /// </para>
        /// <para>
        /// This method is the asynchronous implementation of the EXAMINE command.
        /// Untagged responses received during execution populate the
        /// <see cref="IMAP_Client_SelectedFolder"/> instance associated with the
        /// selected mailbox.
        /// </para>
        /// </remarks>
        public async ValueTask FolderExamineAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
                EXAMINE Command (RFC 3501 §6.3.2)

                Arguments:
                    mailbox name

                Purpose:
                    Selects a mailbox in read-only mode. The server MUST NOT permit
                    any permanent state changes while the mailbox is examined.

                Server Requirements:
                    - MUST return the same untagged responses as SELECT:
                        * FLAGS
                        * PERMANENTFLAGS
                        * EXISTS
                        * RECENT
                        * UIDVALIDITY
                        * UIDNEXT
                        * (optional) UNSEEN
                        * (optional) HIGHESTMODSEQ (CONDSTORE/QRESYNC)
                    - Tagged completion response MUST include [READ-ONLY].

                Effects:
                    - Mailbox is selected but cannot be modified.
                    - STORE operations that would change flags MUST fail.
                    - EXPUNGE MUST NOT be permitted.
                    - Server MAY suppress \Recent adjustments normally done on SELECT.

                Failure Conditions:
                    - NO: mailbox cannot be examined (e.g., permission denied).
                    - BAD: command syntax error or invalid arguments.

                Notes:
                    - EXAMINE is identical to SELECT except for access mode.
                    - Clients MUST NOT assume \Recent semantics are reliable in EXAMINE.
                    - No permanent state changes are allowed in this mode.
            */

            // Release old selected folder.
            if(m_pSelectedFolder != null){
                m_pSelectedFolder.Dispose();
            }

            // Set new folder as selected folder, untagged responses fill all server resturend info.
            m_pSelectedFolder = new IMAP_Client_SelectedFolder(this,folder);

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} EXAMINE {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsSuccess){
                if(response.OptionalResponse is IMAP_t_orc_ReadOnly){
                    m_pSelectedFolder.SetReadOnly(true);
                }
            }
            else{
                m_pSelectedFolder = null;
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderQuotaRoots

        /// <summary>
        /// Executes the IMAP <c>GETQUOTAROOT</c> command for the specified mailbox.
        /// This is the synchronous wrapper for <see cref="FolderQuotaRootsAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose quota‑root information is requested. The value
        /// must be a non‑empty string.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_QuotaRoot"/> object containing the quota roots
        /// associated with the mailbox. The server MUST return exactly one
        /// untagged <c>QUOTAROOT</c> response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server violates the IMAP protocol by failing to return
        /// the required untagged <c>QUOTAROOT</c> response, or by returning more
        /// than one such response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method blocks until the asynchronous <c>GETQUOTAROOT</c> operation
        /// completes. It uses the client's configured timeout to create a
        /// cancellation token for the underlying asynchronous call.
        /// </para>
        /// <para>
        /// The <c>GETQUOTAROOT</c> command is defined in RFC 2087. It retrieves
        /// the quota roots associated with a mailbox. A mailbox may have zero, one,
        /// or multiple quota roots. The server MUST return exactly one
        /// <c>QUOTAROOT</c> response, and that response may list zero or more quota
        /// root identifiers.
        /// </para>
        /// </remarks>
        public IMAP_r_u_QuotaRoot FolderQuotaRoots(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return FolderQuotaRootsAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderQuotaRootsAsync

        /// <summary>
        /// Executes the IMAP <c>GETQUOTAROOT</c> command for the specified mailbox
        /// and returns the quota‑root information reported by the server.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose quota‑root information is requested. The value
        /// must be a non‑empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_QuotaRoot"/> object containing the quota roots
        /// associated with the mailbox. The server MUST return exactly one
        /// untagged <c>QUOTAROOT</c> response. If the server returns <c>NO</c> or
        /// <c>BAD</c>, an <see cref="IMAP_ClientException"/> is thrown.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the <c>IDLE</c> state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is an empty or whitespace‑only
        /// string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// completion response.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server violates the IMAP protocol by failing to return
        /// the required untagged <c>QUOTAROOT</c> response, or by returning more
        /// than one such response.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>GETQUOTAROOT</c> command is defined in RFC 2087. It retrieves
        /// the quota roots associated with a mailbox. A mailbox may have zero, one,
        /// or multiple quota roots. The server MUST return exactly one
        /// <c>QUOTAROOT</c> response, and that response may list zero or more quota
        /// root identifiers.
        /// </para>
        /// <para>
        /// For each quota root listed, the server MAY return one or more untagged
        /// <c>QUOTA</c> responses describing usage and limits for that root. These
        /// responses are processed separately by the client.
        /// </para>
        /// <para>
        /// The mailbox name is encoded using the client's configured mailbox
        /// encoding before being sent to the server.
        /// </para>
        /// </remarks>
        public async ValueTask<IMAP_r_u_QuotaRoot> FolderQuotaRootsAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 2087 – IMAP QUOTA Extension
               Command:     GETQUOTAROOT
               Responses:   one untagged QUOTAROOT response
                            zero or more untagged QUOTA responses
                            tagged OK / NO / BAD completion

               The GETQUOTAROOT command retrieves the quota roots associated with a
               mailbox. A mailbox may have zero or more quota roots. The server always
               returns exactly one QUOTAROOT response for the command; that response
               contains the mailbox name followed by zero or more quota root
               identifiers.

               Command syntax:
                   tag GETQUOTAROOT "<mailbox>"

               QUOTAROOT response:
                   * QUOTAROOT "<mailbox>" [<root1> <root2> ...]

               If the mailbox has no quota roots, the QUOTAROOT response contains only
               the mailbox name and no root identifiers.

               For each quota root listed, the server MAY return a corresponding
               QUOTA response:
                   * QUOTA "<root>" (resource usage limit)

               Examples:
                   C: A0001 GETQUOTAROOT INBOX
                   S: * QUOTAROOT INBOX user
                   S: * QUOTA user (STORAGE 512 1024)
                   S: A0001 OK Completed

                   C: A0002 GETQUOTAROOT "Archive/2020"
                   S: * QUOTAROOT "Archive/2020" user root2
                   S: * QUOTA user (STORAGE 900 2000)
                   S: * QUOTA root2 (MESSAGE 120 500)
                   S: A0002 OK Completed

                   C: A0003 GETQUOTAROOT "NoQuotaMailbox"
                   S: * QUOTAROOT "NoQuotaMailbox"
                   S: A0003 OK Completed

               IMPORTANT:
                   - A mailbox may have zero, one, or multiple quota roots.
                   - The server MUST return exactly one QUOTAROOT response per
                     GETQUOTAROOT command.
                   - The QUOTAROOT response may list zero or more quota roots.
                   - The server MAY return multiple QUOTA responses (one per root).
            */


            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            IMAP_r_u_QuotaRoot? retVal = null;
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(retVal == null && e is IMAP_r_u_QuotaRoot item){
                    retVal = item;
                }
            };

            await SendCommandLineAsync($"{(m_CommandIndex++).ToString("d5")} GETQUOTAROOT {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            if(retVal == null){
                throw new IMAP_ProtocolException("Server did not return required untagged QUOTAROOT response.");
            }

            return retVal;
        }

        #endregion

        #region method Quota

        /// <summary>
        /// Executes the IMAP GETQUOTA command for the specified quota root.
        /// This is the synchronous wrapper for <see cref="QuotaAsync"/>.
        /// </summary>
        /// <param name="quotaRoot">
        /// The quota root whose resource usage and limits are requested. The value
        /// must be a non‑empty string.
        /// </param>
        /// <returns>
        /// An IMAP_r_u_Quota object containing the resource usage and limits
        /// associated with the quota root. The server MUST return exactly one
        /// untagged QUOTA response.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when quotaRoot is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when quotaRoot is an empty or whitespace‑only string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD completion response.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged QUOTA
        /// response.
        /// </exception>
        /// <remarks>
        /// This method blocks until the asynchronous GETQUOTA operation completes
        /// and uses the client's configured timeout to create a cancellation token.
        ///
        /// GETQUOTA is defined in RFC 2087. It retrieves the resource usage and
        /// limits associated with a quota root. A quota root defines a set of
        /// mailboxes whose combined resource usage is tracked together.
        ///
        /// Only the first QUOTA response is used. Additional untagged responses
        /// sent by some servers are ignored.
        /// </remarks>
        public IMAP_r_u_Quota Quota(string quotaRoot)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return QuotaAsync(quotaRoot,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method QuotaAsync

        /// <summary>
        /// Executes the IMAP GETQUOTA command for the specified quota root and
        /// returns the resource‑usage information reported by the server.
        /// </summary>
        /// <param name="quotaRoot">
        /// The quota root whose resource usage and limits are requested. The value
        /// must be a non‑empty string.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// An IMAP_r_u_Quota object containing the resource usage and limits
        /// associated with the quota root. The server MUST return exactly one
        /// untagged QUOTA response. If the server returns NO or BAD, an
        /// IMAP_ClientException is thrown.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when quotaRoot is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when quotaRoot is an empty or whitespace‑only string.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD completion response.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged QUOTA
        /// response.
        /// </exception>
        /// <remarks>
        /// GETQUOTA is defined in RFC 2087. It retrieves the resource usage and
        /// limits associated with a quota root. A quota root defines a set of
        /// mailboxes whose combined resource usage is tracked together.
        ///
        /// The server MUST return exactly one QUOTA response. Some servers may send
        /// additional untagged responses; these are ignored. Only the first QUOTA
        /// response is used.
        ///
        /// Resource names are case‑insensitive atoms. Common resource types include
        /// STORAGE (kilobytes used and allowed) and MESSAGE (messages used and
        /// allowed).
        ///
        /// The quota root name is encoded using the client's configured mailbox
        /// encoding before being sent to the server.
        /// </remarks>
        public async ValueTask<IMAP_r_u_Quota> QuotaAsync(string quotaRoot,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(quotaRoot == null){
                throw new ArgumentNullException(nameof(quotaRoot));
            }
            if(string.IsNullOrWhiteSpace(quotaRoot)){
                throw new ArgumentException("Quota root must be specified.", nameof(quotaRoot));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 2087 – IMAP QUOTA Extension
               Command:     GETQUOTA
               Responses:   one untagged QUOTA response
                            tagged OK / NO / BAD completion

               The GETQUOTA command retrieves the resource usage and limits associated
               with a specific quota root. A quota root defines a set of mailboxes
               whose combined resource usage is tracked together. The server MUST
               return exactly one QUOTA response for the command.

               Command syntax:
                   tag GETQUOTA "<root>"

               QUOTA response:
                   * QUOTA "<root>" (resource usage limit)

               Each resource is represented as a name/value pair:
                   <resource> <usage> <limit>

               Common resource types:
                   STORAGE   – number of kilobytes used and allowed
                   MESSAGE   – number of messages used and allowed

               Examples:
                   C: A0001 GETQUOTA user
                   S: * QUOTA user (STORAGE 512 1024)
                   S: A0001 OK Completed

                   C: A0002 GETQUOTA root2
                   S: * QUOTA root2 (MESSAGE 120 500 STORAGE 2048 4096)
                   S: A0002 OK Completed

               IMPORTANT:
                   - The server MUST return exactly one QUOTA response per GETQUOTA
                     command.
                   - A quota root may contain multiple resource types.
                   - GETQUOTA does not modify quota usage or limits.
                   - GETQUOTA does not return quota roots for a mailbox; use
                     GETQUOTAROOT for that.

               Error cases:
                   - A tagged NO response indicates that the quota root does not exist
                     or the client lacks permission to query it.
                   - A tagged BAD response indicates invalid arguments or unsupported
                     quota extensions.
                   - The client should throw an exception for NO or BAD responses.

               Notes:
                   - QUOTA is part of the IMAP QUOTA extension and requires the server
                     to advertise "QUOTA" in CAPABILITY.
                   - Resource names are case-insensitive atoms.
                   - The client MUST process the untagged QUOTA response before the
                     final tagged completion.
            */

            quotaRoot = IMAP_Utils.EncodeMailbox(quotaRoot,m_MailboxEncoding);

            IMAP_r_u_Quota? retVal = null;
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(retVal == null && e is IMAP_r_u_Quota v){
                    retVal = v;
                }
            };

            await SendCommandLineAsync($"{m_CommandIndex++:d5} GETQUOTA {quotaRoot}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            if(retVal == null){
                throw new IMAP_ProtocolException("Server did not return required untagged QUOTA response.");
            }

            return retVal;
        }

        #endregion

        #region method QuotaSet

        /// <summary>
        /// Executes the IMAP SETQUOTA command for the specified quota root.
        /// This is the synchronous wrapper for <see cref="QuotaSetAsync"/>.
        /// </summary>
        /// <param name="quotaRoot">
        /// The quota root whose resource limits are to be modified. The value must
        /// be a non‑empty string.
        /// </param>
        /// <param name="limits">
        /// The quota‑limit definitions to apply to the quota root. Each entry
        /// specifies a resource name and its new maximum allowed value.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when quotaRoot is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when quotaRoot is an empty or whitespace‑only string, or when
        /// limits is null or contains no entries.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD completion response.
        /// </exception>
        /// <remarks>
        /// This method blocks until the asynchronous SETQUOTA operation completes
        /// and uses the client's configured timeout to create a cancellation token.
        ///
        /// SETQUOTA is defined in RFC 2087. It modifies the resource limits
        /// associated with a quota root. Only a tagged completion response (OK,
        /// NO, or BAD) is returned. Limits for unspecified resources remain
        /// unchanged.
        /// </remarks>
        public void QuotaSet(string quotaRoot,Dictionary<string,long> limits)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            QuotaSetAsync(quotaRoot,limits,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method QuotaSetAsync

        /// <summary>
        /// Executes the IMAP SETQUOTA command for the specified quota root and
        /// updates the resource‑limit values maintained by the server.
        /// </summary>
        /// <param name="quotaRoot">
        /// The quota root whose resource limits are to be modified. The value must
        /// be a non‑empty string.
        /// </param>
        /// <param name="limits">
        /// The quota‑limit definitions to apply to the quota root. Each entry
        /// specifies a resource name and its new maximum allowed value. Existing
        /// limits for the specified resources are replaced.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation. If the server returns
        /// NO or BAD, an IMAP_ClientException is thrown.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when quotaRoot is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when quotaRoot is an empty or whitespace‑only string, or when
        /// limits is null or contains no entries.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD completion response.
        /// </exception>
        /// <remarks>
        /// SETQUOTA is defined in RFC 2087. It modifies the resource limits
        /// associated with a quota root. A quota root defines a set of mailboxes
        /// whose combined resource usage is tracked together.
        ///
        /// The command syntax is:
        ///     SETQUOTA "root" (resource limit ...)
        ///
        /// Each resource/limit pair consists of a case‑insensitive resource name
        /// such as STORAGE or MESSAGE, followed by a numeric limit value. Servers
        /// may define additional resource types, including ANNOTATION-STORAGE,
        /// MAILBOXES, or vendor‑specific X‑CUSTOM‑name entries.
        ///
        /// SETQUOTA does not return a QUOTA response. Only a tagged completion
        /// response (OK, NO, or BAD) is returned. Limits for unspecified resources
        /// remain unchanged.
        ///
        /// The quota root name is encoded using the client's configured mailbox
        /// encoding before being sent to the server.
        /// </remarks>
        public async ValueTask QuotaSetAsync(string quotaRoot,Dictionary<string,long> limits,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(quotaRoot == null){
                throw new ArgumentNullException(nameof(quotaRoot));
            }
            if(string.IsNullOrWhiteSpace(quotaRoot)){
                throw new ArgumentException("Quota root must be specified.", nameof(quotaRoot));
            }
            if(limits == null || limits.Count == 0){
                throw new ArgumentException("Quota limits can't be null or empty.", nameof(limits));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 2087 – IMAP QUOTA Extension
               Command:     SETQUOTA
               Responses:   tagged OK / NO / BAD completion
                            (no untagged QUOTA response is returned)

               The SETQUOTA command modifies the resource limits associated with a
               specific quota root. A quota root defines a set of mailboxes whose
               combined resource usage is tracked together. The server replaces the
               limits for the specified resources with the values provided.

               Command syntax:
                   tag SETQUOTA "<root>" (resource limit ...)

               Each resource/limit pair consists of:
                   <resource> <limit>

               Resource names are case‑insensitive atoms. Common resource types:
                   STORAGE   – maximum kilobytes allowed
                   MESSAGE   – maximum number of messages allowed

               Servers may define additional resource types. Examples include:
                   ANNOTATION-STORAGE
                   MAILBOXES
                   X-CUSTOM-<name>

               Examples:
                   C: A0003 SETQUOTA user (STORAGE 2048)
                   S: A0003 OK Completed

                   C: A0004 SETQUOTA root2 (MESSAGE 50000 STORAGE 102400)
                   S: A0004 OK Completed

               IMPORTANT:
                   - SETQUOTA does not return a QUOTA response.
                   - Only a tagged OK / NO / BAD completion response is returned.
                   - Limits for unspecified resources remain unchanged.
                   - Servers may reject unsupported resource names with a NO response.

               Error cases:
                   - A tagged NO response indicates that the quota root does not exist,
                     the client lacks permission, or the resource type is unsupported.
                   - A tagged BAD response indicates invalid arguments or unsupported
                     quota extensions.

               Notes:
                   - SETQUOTA is part of the IMAP QUOTA extension and requires the
                     server to advertise "QUOTA" in CAPABILITY.
                   - Resource names must be valid IMAP atoms.
                   - The client MUST send the quota root using the correct mailbox
                     encoding.
            */

            quotaRoot = IMAP_Utils.EncodeMailbox(quotaRoot,m_MailboxEncoding);

            List<string> limitList = new List<string>();
            foreach(var item in limits){
                limitList.Add($"{item.Key} {item.Value}");
            }

            await SendCommandLineAsync($"{m_CommandIndex++:d5} SETQUOTA {quotaRoot} ({string.Join(' ',limitList)})\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderAcl

        /// <summary>
        /// Retrieves the Access Control List (ACL) for the specified IMAP mailbox.
        /// This is the synchronous wrapper for <see cref="FolderAclAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose ACL entries will be requested.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_Acl"/> instance containing the ACL entries returned by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the GETACL command.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged ACL response.
        /// </exception>
        /// <remarks>
        /// This method executes GETACL synchronously by invoking <see cref="FolderAclAsync"/>
        /// and blocking until the operation completes or the timeout expires.
        /// </remarks>
        public IMAP_r_u_Acl FolderAcl(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return FolderAclAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region methood FolderAclAsync

        /// <summary>
        /// Retrieves the Access Control List (ACL) for the specified IMAP mailbox.
        /// Implements the IMAP GETACL command as defined in RFC 4314.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose ACL entries will be requested. The value must be a valid
        /// IMAP mailbox identifier and will be encoded using the active mailbox encoding.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_Acl"/> instance containing the ACL entries returned by the server.
        /// Each ACL entry maps a user identifier to a rights string.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="folder"/> parameter is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the <paramref name="folder"/> parameter is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the GETACL command.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged ACL response.
        /// </exception>
        /// <remarks>
        /// The GETACL command returns exactly one untagged ACL response containing zero or more
        /// identifier-rights pairs. The method registers a callback to capture this untagged response
        /// and validates that it is received before returning.
        /// </remarks>
        public async ValueTask<IMAP_r_u_Acl> FolderAclAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder must be specified.",nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

                        /*
             IMAP GETACL Command
             RFC 4314 – Access Control List (ACL) Extension

             Syntax:
                 GETACL <mailbox>

             Purpose:
                 Returns the Access Control List for the specified mailbox.
                 Each ACL entry maps a user identifier to a rights string.

             Semantics:
                 The server responds with one ACL data item containing:
                     ACL <mailbox> <identifier> <rights> [<identifier> <rights> ...]
                 Rights strings consist of permission letters. Rights are cumulative.

             Typical Response:
                 C: A003 GETACL INBOX
                 S: * ACL INBOX user1 lrswipkxte otheruser lr
                 S: A003 OK GETACL completed

             Rights Letters (RFC 4314):
                 l  lookup (mailbox visible)
                 r  read messages
                 s  keep seen/unseen state
                 w  write flags
                 i  insert messages
                 p  post (send mail to mailbox)
                 k  create sub-mailboxes
                 x  delete mailbox
                 t  delete messages
                 e  perform administrative actions (server-defined)

             Error Conditions:
                 NO   mailbox exists but user lacks permission to view ACL
                 BAD  invalid syntax or unsupported command

             Client Usage:
                 Used by administrative tools to inspect mailbox permissions.
                 Rarely used by normal mail clients.
                 Often paired with SETACL and DELETEACL.

             Implementation Notes:
                 Server must verify that the authenticated user has rights to read ACL.
                 Returned ACL entries must reflect current state atomically.
                 Rights strings should be returned in canonical order (server-defined).
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            IMAP_r_u_Acl? retVal = null;
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(retVal == null && e is IMAP_r_u_Acl v){
                    retVal = v;
                }
            };

            await SendCommandLineAsync($"{m_CommandIndex++:d5} GETACL {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            if(retVal == null){
                throw new IMAP_ProtocolException("Server did not return required untagged ACL response.");
            }

            return retVal;
        }

        #endregion

        #region method FolderAclSet

        /// <summary>
        /// Synchronously sends an IMAP <c>SETACL</c> command to modify the access
        /// control rights for the specified mailbox and authentication identifier,
        /// as defined in RFC 4314 (IMAP Access Control List Extension).
        /// </summary>
        /// <param name="folder">
        /// The mailbox whose ACL entry should be modified. The mailbox name is
        /// encoded according to the client's current mailbox encoding
        /// (Modified UTF-7 or UTF-8 when <c>UTF8=ACCEPT</c> is enabled).
        /// </param>
        /// <param name="identifier">
        /// The authentication identifier (user or group) whose rights are being
        /// modified. Identifiers are case-sensitive IMAP atoms or quoted strings.
        /// </param>
        /// <param name="rights">
        /// The rights string to apply. This argument controls how the ACL entry is
        /// modified:
        /// <list type="bullet">
        /// <item><description>
        /// <c>"lrwst"</c> — replaces the entire rights set.
        /// </description></item>
        /// <item><description>
        /// <c>"+d"</c> — adds the listed rights to the identifier.
        /// </description></item>
        /// <item><description>
        /// <c>"-w"</c> — removes the listed rights from the identifier.
        /// </description></item>
        /// <item><description>
        /// <c>""</c> (empty string) — removes all rights; this is transmitted as
        /// <c>""</c> in the IMAP command.
        /// </description></item>
        /// </list>
        /// Rights are case-sensitive and must be sent exactly as intended.
        /// </param>
        /// <returns>
        /// A task representing the synchronous operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/>, <paramref name="identifier"/>,
        /// or <paramref name="rights"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="identifier"/>
        /// is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response
        /// to the <c>SETACL</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="FolderAclSetAsync(string,string,string,CancellationToken)"/>.
        /// It uses the client's configured timeout and blocks until the ACL update
        /// completes.
        /// </para>
        /// <para>
        /// The rights string is passed directly to the asynchronous implementation,
        /// which handles quoting of empty rights (<c>""</c>) and mailbox encoding.
        /// </para>
        /// </remarks>
        public void FolderAclSet(string folder,string identifier,string rights)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderAclSetAsync(folder,identifier,rights,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderAclSetAsync

        /// <summary>
        /// Sends an IMAP <c>SETACL</c> command to modify the access control rights
        /// for the specified mailbox and authentication identifier, as defined in
        /// RFC 4314 (IMAP Access Control List Extension).
        /// </summary>
        /// <param name="folder">
        /// The mailbox whose ACL entry should be modified. The mailbox name is
        /// encoded according to the client's current mailbox encoding
        /// (Modified UTF-7 or UTF-8 when <c>UTF8=ACCEPT</c> is enabled).
        /// </param>
        /// <param name="identifier">
        /// The authentication identifier (user or group) whose rights are being
        /// modified. Identifiers are case-sensitive IMAP atoms or quoted strings.
        /// </param>
        /// <param name="rights">
        /// The rights string to apply. This argument controls how the ACL entry is
        /// modified:
        /// <list type="bullet">
        /// <item><description>
        /// <c>"lrwst"</c> — replaces the entire rights set.
        /// </description></item>
        /// <item><description>
        /// <c>"+d"</c> — adds the listed rights to the identifier.
        /// </description></item>
        /// <item><description>
        /// <c>"-w"</c> — removes the listed rights from the identifier.
        /// </description></item>
        /// <item><description>
        /// <c>""</c> (empty string) — removes all rights; this is transmitted as
        /// <c>""</c> in the IMAP command.
        /// </description></item>
        /// </list>
        /// Rights are case-sensitive and must be sent exactly as intended.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous
        /// operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/>, <paramref name="identifier"/>,
        /// or <paramref name="rights"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="identifier"/>
        /// is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response
        /// to the <c>SETACL</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SETACL</c> command updates the access rights for a mailbox. When
        /// the rights string begins with <c>'+'</c> or <c>'-'</c>, the server
        /// modifies the existing rights incrementally. When the rights string does
        /// not begin with a prefix, the server replaces the entire rights set.
        /// </para>
        /// <para>
        /// An empty rights string (<c>""</c>) removes all rights for the identifier.
        /// This method automatically quotes the empty rights string when sending
        /// the IMAP command.
        /// </para>
        /// </remarks>
        public async ValueTask FolderAclSetAsync(string folder,string identifier,string rights,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder must be specified.",nameof(folder));
            }            
            if(string.IsNullOrWhiteSpace(identifier)){
                throw new ArgumentException("Identifier must be specified.",nameof(identifier));
            } 
            if(rights == null){
                throw new ArgumentNullException(nameof(rights));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
                IMAP SETACL Command
                RFC 4314 – IMAP Access Control List (ACL) Extension

                Syntax:
                    SETACL <mailbox> <identifier> <rights>
                    SETACL <mailbox> <identifier> +<rights>   ; add rights
                    SETACL <mailbox> <identifier> -<rights>   ; remove rights

                Purpose:
                    Modifies the access control rights for a specific authentication
                    identifier (user or group) on the given mailbox. Rights determine
                    what operations the identifier may perform.

                Rights:
                    Rights are case-sensitive IMAP ACL tokens. Common rights include:
                        l   – lookup (mailbox is visible to LIST/LSUB)
                        r   – read messages
                        s   – keep seen/unseen state
                        w   – write flags other than \Seen and \Deleted
                        i   – insert (APPEND) messages
                        p   – post (send mail to mailbox's submission address)
                        c   – create sub-mailboxes
                        d   – delete messages
                        t   – delete mailbox
                    Servers may define additional rights.

                Rights Modification:
                    The <rights> argument may be:
                        "<rights>"     – replace rights entirely
                        "+<rights>"    – add the listed rights to the identifier
                        "-<rights>"    – remove the listed rights from the identifier

                    Examples:
                        SETACL INBOX ivar lrwst        ; replace rights with lrwst
                        SETACL INBOX ivar +d           ; add delete-right
                        SETACL INBOX ivar -w           ; remove write-right

                Typical Response:
                    C: A003 SETACL INBOX ivar lrwst
                    S: A003 OK SETACL completed

                Semantics:
                    - Rights are case-sensitive.
                    - When rights are given without "+" or "-", the server replaces the
                      entire rights set for the identifier.
                    - When "+" or "-" is used, the server modifies the existing rights
                      incrementally.
                    - An empty rights string ("") removes all rights for the identifier.

                Error Conditions:
                    NO   – server refuses the ACL change (e.g., insufficient rights)
                    BAD  – invalid syntax or unsupported command

                Client Usage:
                    Used to grant or revoke permissions for users on shared mailboxes.
                    Clients typically follow SETACL with GETACL or MYRIGHTS to verify
                    the resulting ACL state.

                Implementation Notes:
                    - Mailbox names must be encoded according to the current mailbox
                      encoding (Modified UTF-7 or UTF-8 if UTF8=ACCEPT is enabled).
                    - Identifiers are case-sensitive and must be transmitted as IMAP
                      atoms or quoted strings depending on server requirements.
                    - Rights modifiers ("+<rights>" / "-<rights>") must be sent exactly
                      as intended; the server does not interpret symbolic expressions.
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);
            if(rights == string.Empty){
                rights = "\"\"";
            }

            await SendCommandLineAsync($"{m_CommandIndex++:d5} SETACL {folder} {identifier} {rights}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderAclDelete

        /// <summary>
        /// Removes the Access Control List (ACL) entry for the specified user identifier
        /// from the given IMAP mailbox. This is the synchronous wrapper for
        /// <see cref="FolderAclDeleteAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose ACL entry will be removed.
        /// </param>
        /// <param name="identifier">
        /// The user identifier whose rights should be deleted from the mailbox.
        /// </param>
        /// <returns>
        /// Nothing. The method completes when the DELETEACL command finishes.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="identifier"/> is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the DELETEACL command.
        /// </exception>
        /// <remarks>
        /// This method executes DELETEACL synchronously by invoking
        /// <see cref="FolderAclDeleteAsync"/> and blocking until the operation completes
        /// or the timeout expires.
        /// </remarks>
        public void FolderAclDelete(string folder,string identifier)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderAclDeleteAsync(folder,identifier,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderAclDeleteAsync

        /// <summary>
        /// Removes the Access Control List (ACL) entry for the specified user identifier
        /// from the given IMAP mailbox. Implements the IMAP DELETEACL command as defined
        /// in RFC 4314.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose ACL entry will be removed. The value must be a valid
        /// IMAP mailbox identifier and will be encoded using the active mailbox encoding.
        /// </param>
        /// <param name="identifier">
        /// The user identifier whose rights should be deleted from the mailbox.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous DELETEACL operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="identifier"/> is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the DELETEACL command.
        /// </exception>
        /// <remarks>
        /// The DELETEACL command removes all rights associated with the specified identifier.
        /// Servers typically return a single tagged OK response and no untagged ACL data.
        /// </remarks>
        public async ValueTask FolderAclDeleteAsync(string folder,string identifier,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder must be specified.",nameof(folder));
            }            
            if(string.IsNullOrWhiteSpace(identifier)){
                throw new ArgumentException("Identifier must be specified.",nameof(identifier));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
             IMAP DELETEACL Command
             RFC 4314 – Access Control List (ACL) Extension

             Syntax:
                 DELETEACL <mailbox> <identifier>

             Purpose:
                 Removes the ACL entry for the specified user identifier from the mailbox.
                 After deletion, the user loses all rights previously granted on that mailbox.

             Semantics:
                 The server deletes the rights associated with the given identifier.
                 If the identifier does not exist, the server may return OK or NO depending
                 on implementation, but no rights remain for that identifier after completion.

             Typical Response:
                 C: A004 DELETEACL INBOX otheruser
                 S: A004 OK DELETEACL completed

             Error Conditions:
                 NO   mailbox exists but the authenticated user lacks permission to modify ACLs
                 BAD  invalid syntax or unsupported command

             Client Usage:
                 Used by administrative tools to remove user-specific rights from a mailbox.
                 Often paired with GETACL and SETACL for full ACL management.

             Implementation Notes:
                 Server must verify that the authenticated user has rights to modify ACLs.
                 Deletion must be atomic and reflect immediately in subsequent GETACL results.
                 If the identifier is not present, server behavior may vary but must remain
                 consistent with its ACL model.
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} DELETEACL {folder} {identifier}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method FolderAclRights

        /// <summary>
        /// Retrieves the rights that may be granted to the specified user identifier
        /// for the given IMAP mailbox. This is the synchronous wrapper for
        /// <see cref="FolderAclRightsAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose assignable rights will be queried.
        /// </param>
        /// <param name="identifier">
        /// The user identifier for whom the assignable rights are requested.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_ListRights"/> instance containing the required and
        /// optional rights returned by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="identifier"/> is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the LISTRIGHTS command.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged LISTRIGHTS response.
        /// </exception>
        /// <remarks>
        /// This method executes LISTRIGHTS synchronously by invoking
        /// <see cref="FolderAclRightsAsync"/> and blocking until the operation completes
        /// or the timeout expires.
        /// </remarks>
        public IMAP_r_u_ListRights FolderAclRights(string folder,string identifier)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return FolderAclRightsAsync(folder,identifier,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderAclRightsAsync

        /// <summary>
        /// Retrieves the rights that may be granted to the specified user identifier
        /// for the given IMAP mailbox. Implements the IMAP LISTRIGHTS command as
        /// defined in RFC 4314.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose assignable rights will be queried. The value will be
        /// encoded using the active mailbox encoding.
        /// </param>
        /// <param name="identifier">
        /// The user identifier for whom the assignable rights are requested.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_ListRights"/> instance containing the required and
        /// optional rights returned by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> or <paramref name="identifier"/> is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the LISTRIGHTS command.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged LISTRIGHTS response.
        /// </exception>
        /// <remarks>
        /// LISTRIGHTS returns exactly one untagged LISTRIGHTS response containing the
        /// rights that must always be granted and the rights that may optionally be
        /// granted to the specified identifier.
        /// </remarks>
        public async ValueTask<IMAP_r_u_ListRights> FolderAclRightsAsync(string folder,string identifier,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder must be specified.",nameof(folder));
            }           
            if(string.IsNullOrWhiteSpace(identifier)){
                throw new ArgumentException("Identifier must be specified.",nameof(identifier));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
             IMAP LISTRIGHTS Command
             RFC 4314 – Access Control List (ACL) Extension

             Syntax:
                 LISTRIGHTS <mailbox> <identifier>

             Purpose:
                 Returns the set of rights that may be granted to the specified user
                 identifier for the given mailbox, along with the rights that are
                 always granted and those that are optional.

             Semantics:
                 The server responds with one LISTRIGHTS data item containing:
                     LISTRIGHTS <mailbox> <identifier> <required-rights> <optional-rights...>
                 Required rights are those that must always be granted if any rights
                 are granted at all. Optional rights are those that may be granted
                 or withheld.

             Typical Response:
                 C: A005 LISTRIGHTS INBOX otheruser
                 S: * LISTRIGHTS INBOX otheruser lr swipkxte
                 S: A005 OK LISTRIGHTS completed

             Interpretation:
                 In the example above:
                     lr          = rights that must always be granted
                     swipkxte    = rights that may optionally be granted

             Error Conditions:
                 NO   mailbox exists but the authenticated user lacks permission to view rights
                 BAD  invalid syntax or unsupported command

             Client Usage:
                 Used by administrative tools to determine which rights can be assigned
                 to a user before issuing SETACL. Helps prevent invalid or unsupported
                 rights assignments.

             Implementation Notes:
                 Server must verify that the authenticated user has rights to inspect
                 ACL capabilities for the mailbox. Returned rights must reflect the
                 server’s ACL model and be consistent with subsequent SETACL operations.
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            IMAP_r_u_ListRights? retVal = null;
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(retVal == null && e is IMAP_r_u_ListRights v){
                    retVal = v;
                }
            };

            await SendCommandLineAsync($"{m_CommandIndex++:d5} LISTRIGHTS {folder} {identifier}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            if(retVal == null){
                throw new IMAP_ProtocolException("Server did not return required untagged LISTRIGHTS response.");
            }

            return retVal;
        }

        #endregion

        #region method FolderAclMyRights

        /// <summary>
        /// Retrieves the effective rights that the authenticated user currently has
        /// on the specified IMAP mailbox. This is the synchronous wrapper for
        /// <see cref="FolderAclMyRightsAsync"/>.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose effective rights will be queried.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_MyRights"/> instance containing the rights returned
        /// by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the MYRIGHTS command.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged MYRIGHTS response.
        /// </exception>
        /// <remarks>
        /// This method executes MYRIGHTS synchronously by invoking
        /// <see cref="FolderAclMyRightsAsync"/> and blocking until the operation
        /// completes or the timeout expires.
        /// </remarks>
        public IMAP_r_u_MyRights FolderAclMyRights(string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return FolderAclMyRightsAsync(folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderAclMyRightsAsync

        /// <summary>
        /// Retrieves the effective rights that the authenticated user currently has
        /// on the specified IMAP mailbox. Implements the IMAP MYRIGHTS command as
        /// defined in RFC 4314.
        /// </summary>
        /// <param name="folder">
        /// The mailbox name whose effective rights will be queried. The value will be
        /// encoded using the active mailbox encoding.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_MyRights"/> instance containing the rights returned
        /// by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="folder"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="folder"/> is empty or whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged NO or BAD response to the MYRIGHTS command.
        /// </exception>
        /// <exception cref="IMAP_ProtocolException">
        /// Thrown when the server does not return the required untagged MYRIGHTS response.
        /// </exception>
        /// <remarks>
        /// MYRIGHTS returns exactly one untagged MYRIGHTS response containing the
        /// effective rights available to the authenticated user.
        /// </remarks>
        public async ValueTask<IMAP_r_u_MyRights> FolderAclMyRightsAsync(string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder must be specified.",nameof(folder));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
             IMAP MYRIGHTS Command
             RFC 4314 – Access Control List (ACL) Extension

             Syntax:
                 MYRIGHTS <mailbox>

             Purpose:
                 Returns the set of rights that the authenticated user currently has
                 on the specified mailbox. This reflects the effective rights after
                 all ACL entries, inherited rights, and server-defined rules have
                 been applied.

             Semantics:
                 The server responds with one MYRIGHTS data item containing:
                     MYRIGHTS <mailbox> <rights>
                 The rights string represents the permissions available to the
                 authenticated user for the mailbox.

             Typical Response:
                 C: A006 MYRIGHTS INBOX
                 S: * MYRIGHTS INBOX lrswipkxte
                 S: A006 OK MYRIGHTS completed

             Interpretation:
                 The returned rights indicate what the authenticated user is allowed
                 to do on the mailbox, including reading, writing, inserting, deleting,
                 creating sub-mailboxes, and administrative actions depending on the
                 server’s ACL model.

             Error Conditions:
                 NO   mailbox exists but the authenticated user lacks permission to view rights
                 BAD  invalid syntax or unsupported command

             Client Usage:
                 Used by clients to determine what operations the authenticated user
                 may perform on a mailbox. Helps avoid issuing commands that would
                 fail due to insufficient rights.

             Implementation Notes:
                 Server must compute effective rights based on ACL entries, inherited
                 rights, and any server-specific rules. Returned rights must match
                 the server’s enforcement of permissions for subsequent commands.
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            IMAP_r_u_MyRights? retVal = null;
            
            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(retVal == null && e is IMAP_r_u_MyRights v){
                    retVal = v;
                }
            };

            await SendCommandLineAsync($"{m_CommandIndex++:d5} MYRIGHTS {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            if(retVal == null){
                throw new IMAP_ProtocolException("Server did not return required untagged MYRIGHTS response.");
            }

            return retVal;
        }

        #endregion

        #region method MessageAppend

        /// <summary>
        /// Synchronously executes the IMAP <c>APPEND</c> command to add a new message
        /// to the specified mailbox. This is a blocking wrapper around
        /// <see cref="MessageAppendAsync(string,string[],DateTime,Stream,long,CancellationToken)"/>.
        /// </summary>
        /// <param name="folder">
        /// The target mailbox to which the message will be appended.
        /// </param>
        /// <param name="flags">
        /// Optional message flags to associate with the newly appended message.
        /// </param>
        /// <param name="date">
        /// Optional internal date for the message. If not <see cref="DateTime.MinValue"/>,
        /// the value is sent as a quoted IMAP date-time string.
        /// </param>
        /// <param name="message">
        /// The message content stream. The method sends exactly <paramref name="count"/>
        /// bytes from this stream as the literal body of the APPEND command.
        /// </param>
        /// <param name="count">
        /// The number of bytes to read from <paramref name="message"/> and send as the
        /// literal. Must be greater than zero.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method creates a cancellation token using the client’s configured
        /// timeout and then invokes the asynchronous <c>APPEND</c> operation
        /// synchronously using <c>GetAwaiter().GetResult()</c>.
        /// </para>
        /// <para>
        /// Any errors returned by the server during the APPEND operation are surfaced
        /// through <see cref="IMAP_ClientException"/>.
        /// </para>
        /// </remarks>
        public void MessageAppend(string folder,string[] flags,DateTime date,Stream message,long count,CancellationToken cancellationToken = default)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessageAppendAsync(folder,flags,date,message,count,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessageAppendAsync

        /// <summary>
        /// Executes the IMAP <c>APPEND</c> command to add a new message to the
        /// specified mailbox.
        /// </summary>
        /// <param name="folder">
        /// The target mailbox to which the message will be appended. The mailbox
        /// name is encoded according to the client’s configured mailbox encoding.
        /// </param>
        /// <param name="flags">
        /// Optional message flags to associate with the newly appended message.
        /// If provided, they are sent as a parenthesized list (e.g., <c>(\Seen \Flagged)</c>).
        /// </param>
        /// <param name="date">
        /// Optional internal date for the message. If not <see cref="DateTime.MinValue"/>,
        /// the value is sent as a quoted IMAP date-time string. If omitted, the server
        /// assigns the current time.
        /// </param>
        /// <param name="message">
        /// The message content stream. The method sends exactly <paramref name="count"/>
        /// bytes from this stream as the literal body of the APPEND command.
        /// </param>
        /// <param name="count">
        /// The number of bytes to read from <paramref name="message"/> and send as the
        /// literal. Must be greater than zero.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, the folder name
        /// is invalid, or the client is currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="folder"/> or <paramref name="message"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is empty or whitespace, or if
        /// <paramref name="count"/> is less than 1.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response,
        /// or fails to send the required continuation request for the literal.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method implements the IMAP <c>APPEND</c> command as defined in
        /// RFC 3501 §6.3.11. The command creates a new message in the target
        /// mailbox. If flags or an internal date are supplied, they are stored with
        /// the message; otherwise, the server assigns defaults.
        /// </para>
        /// <para>
        /// The <c>APPEND</c> command does not modify any existing messages and does
        /// not generate untagged <c>EXPUNGE</c> or <c>FETCH</c> responses. If the
        /// server supports UIDPLUS, the final tagged <c>OK</c> response may include
        /// an <c>APPENDUID</c> response code containing the UID assigned to the new
        /// message.
        /// </para>
        /// </remarks>
        public async ValueTask MessageAppendAsync(string folder,string[] flags,DateTime date,Stream message,long count,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(folder == null){
                throw new ArgumentNullException(nameof(folder));
            }
            if(string.IsNullOrWhiteSpace(folder)){
                throw new ArgumentException("Folder must be specified.",nameof(folder));
            }            
            if(message == null){
                throw new ArgumentNullException(nameof(message));
            }
            if(count < 1){
                throw new ArgumentException("Count must be > 0.",nameof(count));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
                APPEND Command (RFC 3501 §6.3.11)

                Arguments:
                    mailbox name
                    optional flag parenthesized list
                    optional date-time string (MUST be a quoted string)
                    message literal

                Purpose:
                    Adds a new message to the specified mailbox. The server stores the
                    message with the provided flags and internal date if supplied; otherwise
                    it assigns default flags and the current time.

                Server Requirements:
                    - For a synchronizing literal (“{size}”), the server MUST send a
                      continuation request (“+”) before the client sends the literal data.
                    - For a non-synchronizing literal (“{size+}”), the client sends the
                      literal immediately and the server MUST NOT send a continuation.
                    - MUST append the message to the mailbox as a new message.
                    - MUST assign a unique UID to the new message.
                      (If the server supports UIDPLUS, the tagged OK response may include
                       an APPENDUID response code containing the assigned UID.)
                    - MUST store the message with the supplied flags and internal date, if
                      provided.
                    - MUST return a tagged OK response on success.
                    - MUST NOT modify any existing messages in the mailbox.

                Effects:
                    - A new message is created in the target mailbox.
                    - The message’s flags and internal date are set according to the
                      arguments or server defaults.
                    - No untagged EXPUNGE or FETCH responses are generated by APPEND.

                Failure Conditions:
                    - NO: mailbox cannot accept the message (e.g., quota exceeded,
                      permission denied, invalid flags).
                    - BAD: command syntax error or invalid arguments.
                    - BYTES: server rejects the literal size (e.g., too large).

                Notes:
                    - APPEND does not operate on existing messages; it only creates a new
                      message.
                    - The date-time argument MUST be enclosed in double quotes, e.g.:
                          "20-Feb-2020 10:30:00 +0200"
                    - APPEND is atomic: either the entire message is stored or none of it.

                Example (synchronizing literal):
                    A003 APPEND INBOX (\Seen) "20-Feb-2020 10:30:00 +0200" {57}
                    + Ready for literal data
                    <57 bytes of message>
                    A003 OK APPEND completed
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            StringBuilder cmd = new StringBuilder();
            cmd.Append($"{m_CommandIndex++:d5} APPEND {folder}");
            if(flags != null && flags.Length > 0){
                cmd.Append(" (" + string.Join(' ',flags) + ")");
            }
            if(date != DateTime.MinValue){
                cmd.Append(" \"" + IMAP_Utils.DateTimeToString(date) + "\"");
            }
            cmd.Append(" {" + count + "}\r\n");

            await SendCommandLineAsync(cmd.ToString(),true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(!response.IsContinue){
                throw new IMAP_ClientException(response);
            }

            await this.TcpStream.WriteStreamAsync(message,count,int.MaxValue,cancellationToken);
            LogAddWrite(count,$"<literal of {count} bytes>");
            await SendCommandLineAsync("\r\n",true,cancellationToken);

            response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method Enable

        /// <summary>
        /// Synchronously sends the IMAP <c>ENABLE</c> command to request activation
        /// of a single optional server capability for the current connection.
        /// This method blocks until the operation completes and returns whether
        /// the server actually enabled the requested capability.
        /// </summary>
        /// <param name="capability">
        /// The capability name to enable. Only one capability is sent per command
        /// to ensure deterministic server behavior.
        /// </param>
        /// <returns>
        /// <c>true</c> if and only if the server returned an untagged
        /// <c>ENABLED</c> response containing the requested capability;
        /// otherwise <c>false</c>. A tagged <c>OK</c> response alone does not
        /// indicate that the capability was enabled.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="capability"/> is empty or consists only
        /// of whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// response to the <c>ENABLE</c> command.
        /// </exception>
        /// <remarks>
        /// This method is a synchronous wrapper around <see cref="EnableAsync"/>.
        /// It uses the client's configured timeout and blocks the calling thread
        /// until the ENABLE command completes.
        /// </remarks>
        public bool Enable(string capability)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return EnableAsync(capability,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method EnableAsync

        /// <summary>
        /// Sends the IMAP <c>ENABLE</c> command to request activation of a single
        /// optional server capability for the current connection, as defined in
        /// RFC 5161.
        /// </summary>
        /// <param name="capability">
        /// The capability name to enable. Only one capability is sent per command
        /// to ensure deterministic server behavior and accurate interpretation of
        /// server responses.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous
        /// operation.
        /// </param>
        /// <returns>
        /// <c>true</c> if and only if the server returned an untagged
        /// <c>ENABLED</c> response containing the requested capability;
        /// otherwise <c>false</c>. A tagged <c>OK</c> response alone does not
        /// indicate success.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="capability"/> is empty or consists only
        /// of whitespace.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// response to the <c>ENABLE</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The server may return one or more untagged <c>ENABLED</c> responses
        /// indicating which capabilities were successfully enabled. Only the
        /// capabilities listed in these responses should be considered active.
        /// </para>
        /// <para>
        /// If the server returns <c>OK</c> without any <c>ENABLED</c> response,
        /// the requested capability was not enabled, even though the command
        /// completed successfully.
        /// </para>
        /// <para>
        /// Clients should send only one capability per <c>ENABLE</c> command.
        /// Sending multiple capabilities at once makes it impossible to determine
        /// which individual capabilities were accepted.
        /// </para>
        /// </remarks>
        public async ValueTask<bool> EnableAsync(string capability,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(string.IsNullOrWhiteSpace(capability)){
                throw new ArgumentException("Capability must be specified.",nameof(capability));
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
             IMAP ENABLE Command
             RFC 5161 – IMAP ENABLE Extension

             Syntax:
                 ENABLE <capability> [<capability> ...]

             Purpose:
                 Requests the server to enable one or more optional IMAP capabilities
                 for the current connection. Capabilities enabled via this command
                 may alter server behavior, allow new commands, or activate enhanced
                 features.

             Semantics:
                 The server responds with one or more ENABLED data items indicating
                 which capabilities have been successfully enabled:
                     ENABLED <capability> [<capability> ...]
                 Capabilities not listed in the ENABLED response were not enabled,
                 either because the server does not support them or because they
                 cannot be enabled dynamically.

             Typical Response:
                 C: A102 ENABLE UTF8=ACCEPT CONDSTORE
                 S: * ENABLED UTF8=ACCEPT
                 S: A102 OK ENABLE completed

             Interpretation:
                 In the example above:
                     UTF8=ACCEPT   was successfully enabled
                     CONDSTORE     was not enabled (server did not include it)

             Error Conditions:
                 NO   server refuses to enable the requested capabilities
                 BAD  invalid syntax or unsupported command

             Client Usage:
                 Used by clients to activate optional server features such as UTF‑8
                 support, conditional store, or other extensions that improve
                 performance or functionality.

             Implementation Notes:
                 Servers must list only the capabilities that were successfully
                 enabled. Clients should not assume that all requested capabilities
                 were accepted. ENABLED responses may appear as untagged data.
            */

            bool retVal = false;

            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(e is IMAP_r_u_Enabled v){
                    if(v.Capabilities.Contains(capability,StringComparer.OrdinalIgnoreCase)){
                        retVal = true;
                    }
                }
            };

            await SendCommandLineAsync($"{m_CommandIndex++:d5} ENABLE {capability}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            return retVal;
        }

        #endregion

        #region method EnableUtf8IfSupported

        /// <summary>
        /// Synchronously attempts to enable UTF‑8 related IMAP capabilities on the
        /// current connection using the IMAP <c>ENABLE</c> command (RFC 5161).
        /// 
        /// This method blocks until the operation completes and applies the same
        /// UTF‑8 activation logic as <see cref="EnableUtf8IfSupportedAsync(CancellationToken)"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method uses the client's configured timeout and executes the
        /// asynchronous UTF‑8 enabling sequence in a blocking manner. It enables
        /// UTF‑8 features in the recommended RFC‑defined order and updates internal
        /// state based on which capabilities the server actually activates.
        /// </para>
        /// 
        /// <para>
        /// Only capabilities explicitly listed in the server's untagged
        /// <c>ENABLED</c> responses are considered active. A tagged <c>OK</c>
        /// response alone does not indicate that a capability was successfully
        /// enabled.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c>
        /// response to any <c>ENABLE</c> command.
        /// </exception>
        public async ValueTask EnableUtf8IfSupported()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            EnableUtf8IfSupportedAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method EnableUtf8IfSupportedAsync

        /// <summary>
        /// Attempts to enable UTF‑8 related IMAP capabilities on the current
        /// connection using the IMAP <c>ENABLE</c> command (RFC 5161).
        /// 
        /// The method enables UTF‑8 features in a safe, RFC‑defined order and
        /// updates internal client state based on which capabilities the server
        /// actually activates.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous
        /// operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, or is
        /// currently in the IDLE state.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The method checks the server's advertised capabilities and attempts to
        /// enable the following UTF‑8 related extensions in the recommended order:
        /// <c>UTF8=ACCEPT</c>, <c>UTF8=RESP-CODES</c>, <c>UTF8=SEARCH</c>,
        /// and <c>UTF8=USER</c>. Each capability is enabled using a separate
        /// <c>ENABLE</c> command to ensure deterministic behavior.
        /// </para>
        /// 
        /// <para>
        /// Only capabilities explicitly listed in the server's untagged
        /// <c>ENABLED</c> responses are considered active. A tagged <c>OK</c>
        /// response alone does not indicate that a capability was successfully
        /// enabled.
        /// </para>
        /// 
        /// <para>
        /// When <c>UTF8=ACCEPT</c> is enabled, the client switches mailbox name
        /// encoding to UTF‑8. When <c>UTF8=SEARCH</c> is enabled, the client
        /// allows UTF‑8 search keys. When <c>UTF8=USER</c> is enabled, the client
        /// permits UTF‑8 usernames and passwords for authentication.
        /// </para>
        /// 
        /// <para>
        /// The capabilities <c>UTF8=ONLY</c> and <c>UTF8=ALL</c> are intentionally
        /// not enabled automatically because they alter mailbox name semantics or
        /// are obsolete.
        /// </para>
        /// </remarks>
        public async ValueTask EnableUtf8IfSupportedAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            // Enable in RFC-defined safe order
            string[] order =
            {
                "UTF8=ACCEPT",
                "UTF8=RESP-CODES",
                "UTF8=SEARCH",
                "UTF8=USER",
                // UTF8=ONLY
                //"UTF8=ONLY",
                //"UTF8=ALL"
            };

            foreach(string cap in order){
                if(this.Capabilities.Contains(cap,StringComparer.OrdinalIgnoreCase)){
                    var enabled = await EnableAsync(cap,cancellationToken);

                    if(enabled){
                        if(cap.Equals("UTF8=ACCEPT", StringComparison.OrdinalIgnoreCase)){
                            m_MailboxEncoding = IMAP_Mailbox_Encoding.ImapUtf8;
                        }
                        else if (cap.Equals("UTF8=SEARCH", StringComparison.OrdinalIgnoreCase)){
                            m_Utf8Search = true;
                        }                        
                        else if (cap.Equals("UTF8=USER", StringComparison.OrdinalIgnoreCase)){
                            m_Utf8User = true;
                        }
                    }
                }
            }
        }

        #endregion


        #region method FolderClose

        /// <summary>
        /// Executes the IMAP <c>CLOSE</c> command on the currently selected mailbox.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no mailbox is
        /// currently selected, or the client is in the IDLE state. The <c>CLOSE</c>
        /// command cannot be issued while IDLE is active.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response,
        /// indicating that the mailbox could not be closed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is the synchronous wrapper for
        /// <see cref="FolderCloseAsync(CancellationToken)"/>. A cancellation token
        /// with the client’s configured timeout is created internally, and the
        /// asynchronous operation is executed synchronously using
        /// <c>GetAwaiter().GetResult()</c>.
        /// </para>
        /// <para>
        /// The <c>CLOSE</c> command permanently removes all messages in the selected
        /// mailbox that have the <c>\Deleted</c> flag set. The expunge is performed
        /// silently: the server MUST NOT send untagged <c>EXPUNGE</c> responses for
        /// the removed messages.
        /// </para>
        /// <para>
        /// After expunging deleted messages, the server deselects the mailbox and
        /// returns to the authenticated (non-selected) state. No further commands
        /// may operate on the mailbox until a new <c>SELECT</c> or <c>EXAMINE</c>
        /// command is issued.
        /// </para>
        /// </remarks>
        public void FolderClose()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            FolderCloseAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method FolderCloseAsync

        /// <summary>
        /// Executes the IMAP <c>CLOSE</c> command on the currently selected mailbox.
        /// </summary>
        /// <param name="cancellationToken">
        /// Optional cancellation token used to cancel the operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no mailbox is
        /// currently selected, or the client is in the IDLE state. The <c>CLOSE</c>
        /// command cannot be issued while IDLE is active.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a tagged <c>NO</c> or <c>BAD</c> response,
        /// indicating that the mailbox could not be closed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>CLOSE</c> command permanently removes all messages in the selected
        /// mailbox that have the <c>\Deleted</c> flag set. The expunge is performed
        /// silently: the server MUST NOT send untagged <c>EXPUNGE</c> responses for
        /// the removed messages.
        /// </para>
        /// <para>
        /// After expunging deleted messages, the server deselects the mailbox and
        /// returns to the authenticated (non-selected) state. No further commands
        /// may operate on the mailbox until a new <c>SELECT</c> or <c>EXAMINE</c>
        /// command is issued.
        /// </para>
        /// <para>
        /// This method sends the <c>CLOSE</c> command asynchronously and processes
        /// the server’s tagged response. On success, the client clears its selected
        /// mailbox state to reflect the transition back to authenticated mode.
        /// </para>
        /// </remarks>
        public async ValueTask FolderCloseAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /*
                CLOSE Command (RFC 3501 §6.4.3)

                Arguments:
                    none

                Purpose:
                    Permanently removes all messages in the selected mailbox that have the
                    \Deleted flag set, then deselects the mailbox.

                Server Requirements:
                    - MUST expunge all messages marked \Deleted.
                    - MUST return no untagged EXPUNGE responses for the expunged messages.
                      (The expunge is implicit and silent.)
                    - MUST return a tagged OK response when the mailbox is successfully closed.
                    - MUST deselect the mailbox; no further commands may operate on it
                      until a new SELECT or EXAMINE is issued.

                Effects:
                    - All messages flagged \Deleted are permanently removed.
                    - No EXPUNGE responses are sent; clients cannot track which messages
                      were removed.
                    - The mailbox becomes unselected; server state returns to authenticated
                      (non-selected) mode.

                Failure Conditions:
                    - NO: mailbox cannot be closed (e.g., server internal error).
                    - BAD: command syntax error or invalid state (e.g., no mailbox selected).

                Notes:
                    - CLOSE is equivalent to issuing EXPUNGE followed by UNSELECT,
                      except EXPUNGE is silent.
                    - Clients that need to know which messages were expunged must use
                      EXPUNGE instead of CLOSE.
                    - CLOSE does not affect messages without the \Deleted flag.
            */

            await SendCommandLineAsync((m_CommandIndex++).ToString("d5") + " CLOSE\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsSuccess){
                m_pSelectedFolder.Dispose();
                m_pSelectedFolder = null;
            }
            else{
                
                throw new IMAP_ClientException(response);
            }            
        }

        #endregion

        #region method MessagesFetch

        /// <summary>
        /// Sends a <c>FETCH</c> command for the specified message sequence set
        /// and data-items, delivering each returned <c>FETCH</c> result through
        /// the provided callback. This method executes synchronously.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method creates a cancellation token with the client's configured
        /// timeout and invokes <see cref="MessagesFetchAsync"/> to perform the
        /// actual command execution. Each untagged <c>FETCH</c> result received
        /// during the operation is delivered through the
        /// <paramref name="responseCallback"/> delegate.
        /// </para>
        /// <para>
        /// If any data-item includes literal content, a custom storage stream may
        /// be supplied through the optional
        /// <paramref name="getStoreStreamCallback"/> event. When provided, the
        /// callback is invoked before the literal content is stored.
        /// </para>
        /// <para>
        /// Any error reported by the final command completion response results in
        /// an <see cref="IMAP_ClientException"/> being thrown.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The message sequence set identifying which messages the <c>FETCH</c>
        /// command applies to.
        /// </param>
        /// <param name="items">
        /// The collection of data-item descriptors specifying which information
        /// is requested for each message.
        /// </param>
        /// <param name="responseCallback">
        /// Callback invoked for each <c>FETCH</c> result returned during command
        /// execution.
        /// </param>
        /// <param name="getStoreStreamCallback">
        /// Optional callback allowing assignment of a custom storage stream for
        /// data-items that contain literal content.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="items"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="items"/> contains no elements.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public void MessagesFetch(
            IMAP_t_SeqSet seqSet,
            IMAP_t_Fetch_i[] items,
            EventHandler<IMAP_r_u_Fetch> responseCallback,
            EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesFetchAsync(seqSet,items,responseCallback,getStoreStreamCallback,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesFetchAsync

        /// <summary>
        /// Sends a <c>FETCH</c> command for the specified message sequence set
        /// and data-items, delivering each returned <c>FETCH</c> result through
        /// the provided callback.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method constructs and transmits a <c>FETCH</c> command using the
        /// supplied sequence set and data-item descriptors. For each untagged
        /// <c>FETCH</c> response received during command execution, the
        /// <paramref name="responseCallback"/> is invoked with an
        /// <see cref="IMAP_r_u_Fetch"/> instance containing the message sequence
        /// number and associated data-items.
        /// </para>
        /// <para>
        /// When a data-item includes literal content, a custom storage stream may
        /// be supplied through the optional
        /// <paramref name="getStoreStreamCallback"/> event. If provided, the
        /// callback is invoked before the literal content is stored, allowing the
        /// caller to assign a preferred stream.
        /// </para>
        /// <para>
        /// After all untagged responses have been delivered, the method waits for
        /// the final command completion response. If the final response indicates
        /// an error, an <see cref="IMAP_ClientException"/> is thrown.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The message sequence set identifying which messages the <c>FETCH</c>
        /// command applies to.
        /// </param>
        /// <param name="items">
        /// The collection of data-item descriptors specifying which information
        /// is requested for each message.
        /// </param>
        /// <param name="responseCallback">
        /// Callback invoked for each <c>FETCH</c> result returned during command
        /// execution.
        /// </param>
        /// <param name="getStoreStreamCallback">
        /// Optional callback allowing assignment of a custom storage stream for
        /// data-items that contain literal content.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="items"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="items"/> contains no elements.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public async ValueTask MessagesFetchAsync(
            IMAP_t_SeqSet seqSet,
            IMAP_t_Fetch_i[] items,
            EventHandler<IMAP_r_u_Fetch> responseCallback,
            EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback,
            CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException("seqSet");
            }
            if(items == null){
                throw new ArgumentNullException("items");
            }
            if(items.Length < 1){
                throw new ArgumentException("Argument 'items' must conatain at least 1 value.","items");
            }

            
            StringBuilder command = new StringBuilder();
            command.Append((m_CommandIndex++).ToString("d5"));
            command.Append(" FETCH " + seqSet.ToString() + " (");
            for(int i=0;i<items.Length;i++){
                if(i > 0){
                    command.Append(" ");
                }
                command.Append(items[i].ToString());
            }
            command.Append(")\r\n");

            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(e is IMAP_r_u_Fetch v){
                    if(responseCallback != null){
                        responseCallback(this,v);
                    }
                }
            };

            await SendCommandLineAsync(command.ToString(),true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,getStoreStreamCallback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method MessagesFetchUid

        /// <summary>
        /// Sends a UID‑based <c>FETCH</c> command for the specified message
        /// sequence set and data-items, delivering each returned <c>FETCH</c>
        /// result through the provided callback. This method executes
        /// synchronously.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method creates a cancellation token using the client's configured
        /// timeout and invokes <see cref="MessagesFetchUidAsync"/> to perform the
        /// actual command execution. Each untagged <c>FETCH</c> result received
        /// during the operation is delivered through the
        /// <paramref name="responseCallback"/> delegate.
        /// </para>
        /// <para>
        /// If any data-item includes literal content, a custom storage stream may
        /// be supplied through the optional
        /// <paramref name="getStoreStreamCallback"/> event. When provided, the
        /// callback is invoked before the literal content is stored.
        /// </para>
        /// <para>
        /// Any error reported by the final command completion response results in
        /// an <see cref="IMAP_ClientException"/> being thrown.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The UID sequence set identifying which messages the <c>FETCH UID</c>
        /// command applies to.
        /// </param>
        /// <param name="items">
        /// The collection of data-item descriptors specifying which information
        /// is requested for each message.
        /// </param>
        /// <param name="responseCallback">
        /// Callback invoked for each <c>FETCH</c> result returned during command
        /// execution.
        /// </param>
        /// <param name="getStoreStreamCallback">
        /// Optional callback allowing assignment of a custom storage stream for
        /// data-items that contain literal content.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="items"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="items"/> contains no elements.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public void MessagesFetchUid(
            IMAP_t_SeqSet seqSet,
            IMAP_t_Fetch_i[] items,
            EventHandler<IMAP_r_u_Fetch> responseCallback,
            EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesFetchUidAsync(seqSet,items,responseCallback,getStoreStreamCallback,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesFetchUidAsync

        /// <summary>
        /// Sends a UID‑based <c>FETCH</c> command for the specified message
        /// sequence set and data-items, delivering each returned <c>FETCH</c>
        /// result through the provided callback.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method constructs and transmits a <c>FETCH UID</c> command using
        /// the supplied sequence set and data-item descriptors. For each untagged
        /// <c>FETCH</c> response received during command execution, the
        /// <paramref name="responseCallback"/> is invoked with an
        /// <see cref="IMAP_r_u_Fetch"/> instance containing the message UID and
        /// associated data-items.
        /// </para>
        /// <para>
        /// When a data-item includes literal content, a custom storage stream may
        /// be supplied through the optional
        /// <paramref name="getStoreStreamCallback"/> event. When provided, the
        /// callback is invoked before the literal content is stored, allowing the
        /// caller to assign a preferred stream.
        /// </para>
        /// <para>
        /// After all untagged responses have been delivered, the method waits for
        /// the final command completion response. If the final response indicates
        /// an error, an <see cref="IMAP_ClientException"/> is thrown.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The UID sequence set identifying which messages the <c>FETCH UID</c>
        /// command applies to.
        /// </param>
        /// <param name="items">
        /// The collection of data-item descriptors specifying which information
        /// is requested for each message.
        /// </param>
        /// <param name="responseCallback">
        /// Callback invoked for each <c>FETCH</c> result returned during command
        /// execution.
        /// </param>
        /// <param name="getStoreStreamCallback">
        /// Optional callback allowing assignment of a custom storage stream for
        /// data-items that contain literal content.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="items"/> is
        /// <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="items"/> contains no elements.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public async ValueTask MessagesFetchUidAsync(
            IMAP_t_SeqSet seqSet,
            IMAP_t_Fetch_i[] items,
            EventHandler<IMAP_r_u_Fetch> responseCallback,
            EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback,
            CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException("seqSet");
            }
            if(items == null){
                throw new ArgumentNullException("items");
            }
            if(items.Length < 1){
                throw new ArgumentException("Argument 'items' must conatain at least 1 value.","items");
            }

            
            StringBuilder command = new StringBuilder();
            command.Append((m_CommandIndex++).ToString("d5"));
            command.Append(" UID FETCH " + seqSet.ToString() + " (");
            for(int i=0;i<items.Length;i++){
                if(i > 0){
                    command.Append(" ");
                }
                command.Append(items[i].ToString());
            }
            command.Append(")\r\n");

            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(e is IMAP_r_u_Fetch v){
                    if(responseCallback != null){
                        responseCallback(this,v);
                    }
                }
            };

            await SendCommandLineAsync(command.ToString(),true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,getStoreStreamCallback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method MessagesSearch

        /// <summary>
        /// Executes an IMAP <c>SEARCH</c> command synchronously using the specified
        /// search criteria and returns the matching message numbers (or UIDs when
        /// performing a UID SEARCH).
        /// </summary>
        /// <param name="criteria">
        /// The IMAP search criteria describing which messages should be matched.
        /// Must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// An array of message identifiers returned by the IMAP server. For a normal
        /// <c>SEARCH</c> command these are message sequence numbers; for a
        /// <c>UID SEARCH</c> they are UIDs.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, no mailbox is
        /// selected, or the client is currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="criteria"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the IMAP server returns a <c>NO</c> or <c>BAD</c> response
        /// to the <c>SEARCH</c> command.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <remarks>
        /// This method is a synchronous wrapper around <see cref="MessagesSearchAsync"/>.
        /// It creates a cancellation token using the client's configured timeout and
        /// blocks the calling thread until the asynchronous search operation completes.
        /// </remarks>
        public long[] MessagesSearch(IMAP_t_Search_Key criteria)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return MessagesSearchAsync(criteria,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesSearchAsync

        /// <summary>
        /// Executes an IMAP <c>SEARCH</c> command against the currently selected mailbox
        /// using the specified search criteria and returns the matching message numbers
        /// (or UIDs when the criteria represent a UID SEARCH).
        /// </summary>
        /// <param name="criteria">
        /// The IMAP search criteria describing which messages should be matched.
        /// Must not be <c>null</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// An array of message identifiers returned by the IMAP server. For a normal
        /// <c>SEARCH</c> command these are message sequence numbers; for a
        /// <c>UID SEARCH</c> they are UIDs.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, no mailbox is
        /// selected, or the client is currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="criteria"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the IMAP server returns a <c>NO</c> or <c>BAD</c> response
        /// to the <c>SEARCH</c> command.
        /// </exception>
        /// <remarks>
        /// This method sends the IMAP <c>SEARCH</c> command and collects all untagged
        /// <c>SEARCH</c> responses emitted by the server. Multiple untagged responses
        /// are supported and their identifiers are accumulated. The final tagged
        /// response is validated to ensure successful completion.
        /// </remarks>
        public async ValueTask<long[]> MessagesSearchAsync(IMAP_t_Search_Key criteria,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(criteria == null){
                throw new ArgumentNullException(nameof(criteria));
            }

            /* RFC 3501 — SEARCH Command
               RFC 6855 — IMAP Support for UTF-8
               RFC 9051 — IMAP4rev2 (Literal8 and UTF8=SEARCH)

               The SEARCH command evaluates search criteria against messages in the
               currently selected mailbox. SEARCH may operate in legacy ASCII mode
               or in UTF‑8 mode depending on server capabilities and whether the
               client has enabled UTF8=SEARCH.

               SEARCH-Command = tag SP "SEARCH"
                                [SP "CHARSET" SP charset]
                                SP search-key *(SP search-key) CRLF

               Search Keys (partial list):
                 ALL, SEEN, UNSEEN, ANSWERED, UNANSWERED, FLAGGED, UNFLAGGED,
                 DELETED, UNDELETED, DRAFT, UNDRAFT, RECENT, OLD,
                 FROM <string>, TO <string>, CC <string>, BCC <string>,
                 SUBJECT <string>, BODY <string>, TEXT <string>,
                 BEFORE <date>, ON <date>, SINCE <date>,
                 LARGER <n>, SMALLER <n>,
                 NOT <key>, OR <key> <key>,
                 UID <set>

               Server Behavior:
                 - Valid only in the Selected state.
                 - The server MUST return an untagged SEARCH response containing
                   matching message numbers (or UIDs for UID SEARCH).
                 - The server MUST send a final tagged OK response on success.
                 - If the charset is unsupported, the server MUST return BAD.
                 - If the search criteria are invalid or unsupported, the server
                   MUST return BAD or NO.

               CHARSET Rules:
                 - In legacy mode (UTF8=SEARCH not enabled), servers MAY reject
                   non-ASCII search keys and MAY reject CHARSET UTF-8.
                 - CHARSET is OPTIONAL and often unsupported (e.g., Gmail, Exchange).
                 - CHARSET UTF-8 is only valid if the server supports UTF8=SEARCH.
                 - After enabling UTF8=SEARCH, CHARSET becomes redundant and SHOULD
                   NOT be sent; all search keys are interpreted as UTF‑8.

               UTF‑8 Mode (UTF8=SEARCH):
                 - The client MUST enable UTF8=SEARCH using the ENABLE command:
                     C: A01 ENABLE UTF8=SEARCH
                     S: * ENABLED UTF8=SEARCH
                     S: A01 OK ENABLE completed
                 - After enabling, all search keys MUST be UTF‑8.
                 - CHARSET MUST NOT specify any encoding other than UTF‑8.
                 - CHARSET is unnecessary and SHOULD be omitted entirely.
                 - Servers MUST accept UTF‑8 literals (literal8: {size+}).

               Literals:
                 - Legacy literal: {size} <bytes>
                   Used in ASCII mode; servers may reject non-ASCII bytes.
                 - Literal8: {size+} <UTF‑8 bytes>
                   Required for UTF‑8 search keys when UTF8=SEARCH is enabled.
                   Servers MUST accept literal8 in UTF‑8 mode.

               Examples (Legacy Mode):
                 C: A202 SEARCH SUBJECT "report"
                 S: * SEARCH 5 9 11
                 S: A202 OK SEARCH completed

                 C: A203 SEARCH CHARSET UTF-8 SUBJECT "välja"
                 S: BAD Unsupported charset

               Examples (UTF‑8 Mode Enabled):
                 C: A01 ENABLE UTF8=SEARCH
                 S: * ENABLED UTF8=SEARCH
                 S: A01 OK ENABLE completed

                 C: A02 SEARCH SUBJECT {5+}
                 C: välja
                 S: * SEARCH 100 104
                 S: A02 OK SEARCH completed

               UID SEARCH Example:
                 C: A204 UID SEARCH FROM "bob@example.com" UNSEEN
                 S: * SEARCH 100 104 108
                 S: A204 OK UID SEARCH completed
            */

            var cmdBuilder = new CommandBuilder(m_Utf8Search,m_LiteralPluss);
            cmdBuilder.AddString($"{m_CommandIndex++:d5} SEARCH ");
            criteria.ToCommandBuilder(cmdBuilder);
            cmdBuilder.AddString("\r\n");
            var cmdParts = cmdBuilder.Finish();

            List<long> retVal = [];

            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){
                if(e is IMAP_r_u_Search v){
                    retVal.AddRange(v.Ids);
                }
            };

            await SendCommandAsync(cmdParts,true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            return retVal.ToArray();
        }

        #endregion

        #region method MessagesSearchUid

        /// <summary>
        /// Executes an IMAP <c>UID SEARCH</c> command synchronously using the specified
        /// search criteria and returns the matching message UIDs.
        /// </summary>
        /// <param name="criteria">
        /// The IMAP search criteria describing which messages should be matched.
        /// Must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// An array of UIDs returned by the IMAP server. Although the untagged
        /// response is labeled <c>* SEARCH</c>, the numeric identifiers contained
        /// within it MUST be interpreted as UIDs when the <c>UID SEARCH</c>
        /// command is used.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, no mailbox is
        /// selected, or the client is currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="criteria"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the IMAP server returns a <c>NO</c> or <c>BAD</c> response
        /// to the <c>UID SEARCH</c> command.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation exceeds the configured timeout.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="MessagesSearchUidAsync(IMAP_t_Search_Key, CancellationToken)"/>.
        /// It creates a cancellation token using the client's configured timeout and
        /// blocks the calling thread until the asynchronous UID SEARCH operation
        /// completes.
        /// </para>
        /// <para>
        /// Multiple untagged <c>* SEARCH</c> responses may be emitted by some IMAP
        /// servers. All UIDs are accumulated and returned.
        /// </para>
        /// </remarks>
        public long[] MessagesSearchUid(IMAP_t_Search_Key criteria)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return MessagesSearchUidAsync(criteria,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesSearchUidAsync

        /// <summary>
        /// Executes an IMAP <c>UID SEARCH</c> command asynchronously using the specified
        /// search criteria and returns the matching message UIDs.
        /// </summary>
        /// <param name="criteria">
        /// The IMAP search criteria describing which messages should be matched.
        /// Must not be <c>null</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// An array of UIDs returned by the IMAP server. The identifiers in the
        /// untagged <c>* SEARCH</c> response are always UIDs when the <c>UID SEARCH</c>
        /// command is used.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected, not authenticated, no mailbox is
        /// selected, or the client is currently in the IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="criteria"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the IMAP server returns a <c>NO</c> or <c>BAD</c> response
        /// to the <c>UID SEARCH</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method sends the IMAP <c>UID SEARCH</c> command and collects all
        /// untagged <c>* SEARCH</c> responses emitted by the server. Although the
        /// untagged response is labeled <c>SEARCH</c>, the numeric identifiers
        /// contained within it MUST be interpreted as UIDs, as defined by RFC 3501
        /// and RFC 9051.
        /// </para>
        /// <para>
        /// Some IMAP servers may emit multiple untagged <c>SEARCH</c> responses.
        /// All UIDs are accumulated and returned to the caller.
        /// </para>
        /// <para>
        /// When UTF8=SEARCH is enabled, search keys MUST be UTF‑8 and literal8
        /// ({size+}) MUST be used for UTF‑8 search keys.
        /// </para>
        /// </remarks>
        public async ValueTask<long[]> MessagesSearchUidAsync(IMAP_t_Search_Key criteria,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(criteria == null){
                throw new ArgumentNullException(nameof(criteria));
            }

            /* RFC 3501 — UID SEARCH
               RFC 9051 — IMAP4rev2 (UID command semantics)

               UID SEARCH performs the same logical operation as SEARCH, but all
               identifiers returned by the server are UIDs rather than message
               sequence numbers.

               Command Syntax:
                 <tag> UID SEARCH <search-key> *(SP <search-key>) CRLF

               Example:
                 C: A204 UID SEARCH FROM "bob@example.com" UNSEEN
                 S: * SEARCH 100 104 108
                 S: A204 OK UID SEARCH completed

               Key Behavioral Differences vs. SEARCH:
                 - SEARCH returns message sequence numbers.
                 - UID SEARCH returns UIDs.
                 - The untagged response still uses "* SEARCH" (never "* UID SEARCH").
                   The numbers inside MUST be interpreted as UIDs.

               UID Properties:
                 - UIDs are strictly increasing within a mailbox.
                 - UIDs are never reused.
                 - UIDs persist across sessions.
                 - Sequence numbers are ephemeral and may change after EXPUNGE.

               Server Requirements:
                 - MUST return an untagged SEARCH response containing UIDs.
                 - MUST send a final tagged OK response on success.
                 - MUST NOT mix sequence numbers and UIDs in a UID SEARCH response.
                 - SHOULD return UIDs in ascending order, but ordering is not guaranteed.

               Client Requirements:
                 - MUST treat returned identifiers as UIDs.
                 - MUST NOT assume ordering; client SHOULD sort if ordering is required.
                 - SHOULD accumulate multiple untagged SEARCH responses (some servers
                   emit more than one).

               UTF‑8 Mode (UTF8=SEARCH):
                 - UID SEARCH follows the same UTF‑8 rules as SEARCH.
                 - When UTF8=SEARCH is enabled, search keys MUST be UTF‑8.
                 - Literal8 ({size+}) MUST be used for UTF‑8 search keys.
                 - CHARSET parameter MUST NOT be used once UTF8=SEARCH is active.

               Error Handling:
                 - BAD → malformed syntax or unsupported search keys.
                 - NO  → valid syntax but server refuses the operation.
                 - Both MUST be treated as command failure.
            */

            var cmdBuilder = new CommandBuilder(m_Utf8Search,m_LiteralPluss);
            cmdBuilder.AddString($"{m_CommandIndex++:d5} UID SEARCH ");
            criteria.ToCommandBuilder(cmdBuilder);
            cmdBuilder.AddString("\r\n");
            var cmdParts = cmdBuilder.Finish();

            List<long> retVal = [];

            // Create callback. It is called for each untagged IMAP server response.
            EventHandler<IMAP_r_u> callback = delegate(object? sender,IMAP_r_u e){                
                if(e is IMAP_r_u_Search v){
                    retVal.AddRange(v.Ids);
                }
            };

            await SendCommandAsync(cmdParts,true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,callback,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            return retVal.ToArray();
        }

        #endregion

        #region method MessagesStoreFlags

        /// <summary>
        /// Executes the IMAP <c>STORE</c> command synchronously, updating message
        /// flags for the messages identified by the specified sequence set.
        /// </summary>
        /// <param name="seqSet">The sequence set identifying the messages whose flags will be updated.</param>
        /// <param name="flagsMode">The flag update mode used for the STORE operation.</param>
        /// <param name="flags">The list of flags to apply. May be empty.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>STORE</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="MessagesStoreFlagsAsync(IMAP_t_SeqSet, IMAP_t_Store_FlagsMode, string[], CancellationToken)"/>.
        /// It blocks until the <c>STORE</c> operation completes.
        /// </para>
        ///
        /// <para>
        /// <b>RFC 3501 — STORE Command</b><br/>
        /// The <c>STORE</c> command modifies message flags for the messages in the
        /// provided sequence set. Depending on the selected mode, the operation
        /// may replace the entire flag set, add new flags, or remove existing
        /// flags. Silent variants suppress untagged <c>FETCH</c> responses.
        /// </para>
        ///
        /// <para>
        /// On success, the server returns a final tagged <c>OK</c> response.
        /// Non‑silent operations cause the server to send untagged <c>FETCH</c>
        /// responses containing updated flags. Silent operations suppress these
        /// responses.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// STORE operates on message sequence numbers.
        /// </item>
        /// <item>
        /// <see cref="IMAP_t_Store_FlagsMode"/> determines whether flags are
        /// replaced, added, or removed, and whether the operation is silent.
        /// </item>
        /// <item>
        /// The flag list may be empty. For <c>FLAGS</c>, this clears all flags.
        /// For <c>+FLAGS</c> and <c>-FLAGS</c>, an empty list performs a no‑op.
        /// </item>
        /// <item>
        /// The <c>\Deleted</c> flag marks a message as deleted but does not
        /// physically remove it.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public void MessagesStoreFlags(IMAP_t_SeqSet seqSet,IMAP_t_Store_FlagsMode flagsMode, string[] flags)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesStoreFlagsAsync(seqSet,flagsMode,flags,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesStoreFlagsAsync

        /// <summary>
        /// Executes the IMAP <c>STORE</c> command, updating message flags for the
        /// messages identified by the specified sequence set.
        /// </summary>
        /// <param name="seqSet">The sequence set identifying the messages whose flags will be updated.</param>
        /// <param name="flagsMode">The flag update mode used for the STORE operation.</param>
        /// <param name="flags">The list of flags to apply. May be empty.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>STORE</c> command.
        /// </exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// <b>RFC 3501 — STORE Command</b><br/>
        /// The <c>STORE</c> command modifies message flags for the messages in the
        /// provided sequence set. Depending on the selected mode, the operation
        /// may replace the entire flag set, add new flags, or remove existing
        /// flags. Silent variants suppress untagged <c>FETCH</c> responses.
        /// </para>
        ///
        /// <para>
        /// On success, the server returns a final tagged <c>OK</c> response.
        /// Non‑silent operations cause the server to send untagged <c>FETCH</c>
        /// responses containing updated flags. Silent operations suppress these
        /// responses.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// STORE operates on message sequence numbers.
        /// </item>
        /// <item>
        /// <see cref="IMAP_t_Store_FlagsMode"/> determines whether flags are
        /// replaced, added, or removed, and whether the operation is silent.
        /// </item>
        /// <item>
        /// The flag list may be empty. For <c>FLAGS</c>, this clears all flags.
        /// For <c>+FLAGS</c> and <c>-FLAGS</c>, an empty list performs a no‑op.
        /// </item>
        /// <item>
        /// The <c>\Deleted</c> flag marks a message as deleted but does not
        /// physically remove it.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public async ValueTask MessagesStoreFlagsAsync(IMAP_t_SeqSet seqSet,IMAP_t_Store_FlagsMode flagsMode, string[] flags,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }            
            if(flags == null){
                throw new ArgumentNullException(nameof(flags));
            }

            /* RFC 3501 — STORE Command (Flags)

               The STORE command changes message flags for the messages specified
               by the given sequence set. STORE may add flags, remove flags, or
               replace the entire flag set depending on the operation used.

               STORE-Command = tag SP "STORE" SP sequence-set SP store-att-flags CRLF

               store-att-flags = ( "FLAGS" / "FLAGS.SILENT"
                                 / "+FLAGS" / "+FLAGS.SILENT"
                                 / "-FLAGS" / "-FLAGS.SILENT" )
                                 SP flag-list

               Server Behavior:
                 - Valid only in the Selected state.
                 - The server MUST apply the flag changes to each message in the
                   sequence set.
                 - For non-SILENT operations, the server MUST send untagged FETCH
                   responses showing the updated flags.
                 - For SILENT operations, the server MUST NOT send untagged FETCH
                   responses.
                 - The server MUST send a final tagged OK response on success.
                 - If any flag is invalid or cannot be set, the server MUST return
                   a tagged NO response.

               Notes:
                 - STORE operates on message sequence numbers.
                 - FLAGS replaces the entire flag set.
                 - +FLAGS adds the specified flags.
                 - -FLAGS removes the specified flags.
                 - The \Deleted flag marks a message as deleted but does not remove it.
                 - SILENT variants suppress untagged FETCH responses.

               Short Example:
                 C: A202 STORE 5 +FLAGS (\Seen)
                 S: * 5 FETCH (FLAGS (\Seen))
                 S: A202 OK STORE completed

                 C: A203 STORE 2:4 FLAGS.SILENT (\Deleted)
                 S: A203 OK STORE completed
            */

            StringBuilder cmd = new StringBuilder();
            cmd.Append($"{m_CommandIndex++:d5} STORE");
            cmd.Append(" " + seqSet.ToString());
            if(flagsMode == IMAP_t_Store_FlagsMode.Replace){
                cmd.Append(" FLAGS");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.ReplaceSilent){
                cmd.Append(" FLAGS.SILENT");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.Add){
                cmd.Append(" +FLAGS");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.AddSilent){
                cmd.Append(" +FLAGS.SILENT");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.Remove){
                cmd.Append(" -FLAGS");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.RemoveSilent){
                cmd.Append(" -FLAGS.SILENT");
            }
            else{
                throw new ArgumentException($"Invalid flagMode '{flagsMode}' value, this never should happen.",nameof(flags));
            }
            cmd.Append(" (" + string.Join(' ',flags) + ")");
            cmd.Append("\r\n");

            await SendCommandLineAsync(cmd.ToString(),true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method MessagesStoreFlagsUid

        /// <summary>
        /// Executes the IMAP <c>UID STORE</c> command synchronously, updating
        /// message flags for the messages identified by the specified UID set.
        /// </summary>
        /// <param name="seqSet">The UID set identifying the messages whose flags will be updated.</param>
        /// <param name="flagsMode">The flag update mode used for the UID STORE operation.</param>
        /// <param name="flags">The list of flags to apply. May be empty.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>UID STORE</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="MessagesStoreFlagsUidAsync(IMAP_t_SeqSet, IMAP_t_Store_FlagsMode, string[], CancellationToken)"/>.
        /// It blocks until the <c>UID STORE</c> operation completes.
        /// </para>
        ///
        /// <para>
        /// <b>RFC 4315 — UIDPLUS Extension</b><br/>
        /// The <c>UID STORE</c> command behaves like the standard <c>STORE</c>
        /// command but operates on stable message UIDs instead of volatile
        /// sequence numbers. Depending on the selected mode, the operation may
        /// replace the entire flag set, add new flags, or remove existing flags.
        /// Silent variants suppress untagged <c>FETCH</c> responses.
        /// </para>
        ///
        /// <para>
        /// On success, the server returns a final tagged <c>OK</c> response.
        /// Non‑silent operations cause the server to send untagged <c>FETCH</c>
        /// responses containing updated flags. Silent operations suppress these
        /// responses.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// UID STORE operates on message UIDs, which remain stable throughout
        /// the session.
        /// </item>
        /// <item>
        /// <see cref="IMAP_t_Store_FlagsMode"/> determines whether flags are
        /// replaced, added, or removed, and whether the operation is silent.
        /// </item>
        /// <item>
        /// The flag list may be empty. For <c>FLAGS</c>, this clears all flags.
        /// For <c>+FLAGS</c> and <c>-FLAGS</c>, an empty list performs a no‑op.
        /// </item>
        /// <item>
        /// The <c>\Deleted</c> flag marks a message as deleted but does not
        /// physically remove it.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public void MessagesStoreFlagsUid(IMAP_t_SeqSet seqSet,IMAP_t_Store_FlagsMode flagsMode,string[] flags)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesStoreFlagsUidAsync(seqSet,flagsMode,flags,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesStoreFlagsUidAsync

        /// <summary>
        /// Executes the IMAP <c>UID STORE</c> command, updating message flags for
        /// the messages identified by the specified UID set.
        /// </summary>
        /// <param name="seqSet">The UID set identifying the messages whose flags will be updated.</param>
        /// <param name="flagsMode">The flag update mode used for the UID STORE operation.</param>
        /// <param name="flags">The list of flags to apply. May be empty.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> or <paramref name="flags"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>UID STORE</c> command.
        /// </exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// <b>RFC 4315 — UIDPLUS Extension</b><br/>
        /// The <c>UID STORE</c> command behaves like the standard <c>STORE</c>
        /// command but operates on stable message UIDs instead of volatile
        /// sequence numbers. Depending on the selected mode, the operation may
        /// replace the entire flag set, add new flags, or remove existing flags.
        /// Silent variants suppress untagged <c>FETCH</c> responses.
        /// </para>
        ///
        /// <para>
        /// On success, the server returns a final tagged <c>OK</c> response.
        /// Non‑silent operations cause the server to send untagged <c>FETCH</c>
        /// responses containing updated flags. Silent operations suppress these
        /// responses.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// UID STORE operates on message UIDs, which remain stable throughout
        /// the session.
        /// </item>
        /// <item>
        /// <see cref="IMAP_t_Store_FlagsMode"/> determines whether flags are
        /// replaced, added, or removed, and whether the operation is silent.
        /// </item>
        /// <item>
        /// The flag list may be empty. For <c>FLAGS</c>, this clears all flags.
        /// For <c>+FLAGS</c> and <c>-FLAGS</c>, an empty list performs a no‑op.
        /// </item>
        /// <item>
        /// The <c>\Deleted</c> flag marks a message as deleted but does not
        /// physically remove it.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public async ValueTask MessagesStoreFlagsUidAsync(IMAP_t_SeqSet seqSet,IMAP_t_Store_FlagsMode flagsMode, string[] flags,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }            
            if(flags == null){
                throw new ArgumentNullException(nameof(flags));
            }

            /* RFC 4315 — UIDPLUS Extension: UID STORE Command (Flags)

               The UID STORE command modifies message flags for the messages whose
               UIDs appear in the specified UID set. UID STORE behaves identically
               to the standard STORE command except that it operates on stable
               message UIDs rather than volatile sequence numbers.

               UID-STORE-Command = tag SP "UID" SP "STORE"
                                   SP uid-set SP store-att-flags CRLF

               store-att-flags = ( "FLAGS" / "FLAGS.SILENT"
                                 / "+FLAGS" / "+FLAGS.SILENT"
                                 / "-FLAGS" / "-FLAGS.SILENT" )
                                 SP flag-list

               Server Behavior:
                 - Valid only in the Selected state.
                 - The server MUST apply the flag changes to each message whose UID
                   is included in the provided UID set.
                 - For non-SILENT operations, the server MUST send untagged FETCH
                   responses showing the updated flags.
                 - For SILENT operations, the server MUST NOT send untagged FETCH
                   responses.
                 - The server MUST send a final tagged OK response on success.
                 - If any flag is invalid or cannot be set, the server MUST return
                   a tagged NO response.
                 - UID STORE does not require UIDPLUS support; however, servers
                   implementing UIDPLUS MAY include additional response codes.

               Notes:
                 - UID STORE operates on message UIDs, which do not change during
                   the session, unlike message sequence numbers.
                 - FLAGS replaces the entire flag set.
                 - +FLAGS adds the specified flags.
                 - -FLAGS removes the specified flags.
                 - The \Deleted flag marks a message as deleted but does not remove it.
                 - SILENT variants suppress untagged FETCH responses.
                 - The flag list may be empty. For FLAGS, this clears all flags.
                   For +FLAGS and -FLAGS, an empty list performs a no-op.

               Short Example:
                 C: A202 UID STORE 100 +FLAGS (\Seen)
                 S: * 7 FETCH (FLAGS (\Seen))
                 S: A202 OK UID STORE completed

                 C: A203 UID STORE 200:205 FLAGS.SILENT (\Deleted)
                 S: A203 OK UID STORE completed
            */

            StringBuilder cmd = new StringBuilder();
            cmd.Append($"{m_CommandIndex++:d5} UID STORE");
            cmd.Append(" " + seqSet.ToString());
            if(flagsMode == IMAP_t_Store_FlagsMode.Replace){
                cmd.Append(" FLAGS");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.ReplaceSilent){
                cmd.Append(" FLAGS.SILENT");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.Add){
                cmd.Append(" +FLAGS");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.AddSilent){
                cmd.Append(" +FLAGS.SILENT");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.Remove){
                cmd.Append(" -FLAGS");
            }
            else if(flagsMode == IMAP_t_Store_FlagsMode.RemoveSilent){
                cmd.Append(" -FLAGS.SILENT");
            }
            else{
                throw new ArgumentException($"Invalid flagMode '{flagsMode}' value, this never should happen.",nameof(flags));
            }
            cmd.Append(" (" + string.Join(' ',flags) + ")");
            cmd.Append("\r\n");

            await SendCommandLineAsync(cmd.ToString(),true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method MessagesCopy

        /// <summary>
        /// Executes the IMAP <c>COPY</c> command synchronously, copying the
        /// specified messages to the target mailbox using message sequence
        /// numbers.
        /// </summary>
        /// <param name="seqSet">The sequence set identifying the messages to copy.</param>
        /// <param name="folder">The target mailbox to which the messages are copied.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>COPY</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="MessagesCopyAsync(IMAP_t_SeqSet, string, CancellationToken)"/>.
        /// It blocks until the <c>COPY</c> operation completes.
        /// </para>
        ///
        /// <para>
        /// <b>RFC 3501 — COPY Command</b><br/>
        /// The <c>COPY</c> command copies the messages identified by the provided
        /// sequence set to the end of the target mailbox. The source messages
        /// remain unchanged in the currently selected mailbox.
        /// </para>
        ///
        /// <para>
        /// The server returns a final tagged <c>OK</c> response on success.
        /// If the target mailbox does not exist or cannot be accessed, the
        /// server returns a tagged <c>NO</c> response.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// COPY operates on <b>message sequence numbers</b>, which refer to the
        /// current positions of messages in the selected mailbox.
        /// </item>
        /// <item>
        /// The target mailbox name must be valid and accessible to the
        /// authenticated user.
        /// </item>
        /// <item>
        /// COPY does not modify flags on the source messages.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public void MessagesCopy(IMAP_t_SeqSet seqSet,string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesCopyAsync(seqSet,folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesCopyAsync

        /// <summary>
        /// Executes the IMAP <c>COPY</c> command, copying the specified messages
        /// to the target mailbox using message sequence numbers.
        /// </summary>
        /// <param name="seqSet">The sequence set identifying the messages to copy.</param>
        /// <param name="folder">The target mailbox to which the messages are copied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>COPY</c> command.
        /// </exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// <b>RFC 3501 — COPY Command</b><br/>
        /// The <c>COPY</c> command copies the messages identified by the provided
        /// sequence set to the end of the target mailbox. The source messages
        /// remain unchanged in the currently selected mailbox.
        /// </para>
        ///
        /// <para>
        /// The server returns a final tagged <c>OK</c> response on success.
        /// If the target mailbox does not exist or cannot be accessed, the
        /// server returns a tagged <c>NO</c> response.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// COPY operates on <b>message sequence numbers</b>, which refer to the
        /// current positions of messages in the selected mailbox.
        /// </item>
        /// <item>
        /// The target mailbox name must be valid and accessible to the
        /// authenticated user.
        /// </item>
        /// <item>
        /// COPY does not modify flags on the source messages.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public async ValueTask MessagesCopyAsync(IMAP_t_SeqSet seqSet,string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }            
            if(string.IsNullOrEmpty(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }

            /* RFC 3501 — COPY Command

               The COPY command copies the specified messages to the end of the
               target mailbox. The source messages remain unchanged in the
               currently selected mailbox.

               COPY-Command = tag SP "COPY" SP sequence-set SP mailbox CRLF

               Server Behavior:
                 - Valid only in the Selected state.
                 - The server MUST copy each message in the sequence-set to the
                   target mailbox.
                 - The server MUST NOT alter the source messages.
                 - The server MUST send a final tagged OK response on success.
                 - If the target mailbox does not exist, the server MUST return
                   a tagged NO response.

               Notes:
                 - COPY uses message sequence numbers. These refer to the current
                   positions of messages in the selected mailbox.
                 - The target mailbox name MUST be valid and accessible to the
                   authenticated user.
                 - COPY does not set or clear any flags on the source messages.

               Short Example:
                 C: A142 COPY 1:3 "Archive"
                 S: A142 OK COPY completed
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} COPY {seqSet.ToString()} {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method MessagesCopyUid

        /// <summary>
        /// Executes the IMAP <c>UID COPY</c> command synchronously, copying the
        /// messages identified by the specified UID set to the target mailbox.
        /// </summary>
        /// <param name="seqSet">The UID set identifying the messages to copy.</param>
        /// <param name="folder">The target mailbox to which the messages are copied.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>UID COPY</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="MessagesCopyUidAsync(IMAP_t_SeqSet, string, CancellationToken)"/>.
        /// It blocks until the <c>UID COPY</c> operation completes.
        /// </para>
        ///
        /// <para>
        /// <b>RFC 4315 — UIDPLUS Extension</b><br/>
        /// The <c>UID COPY</c> command copies the messages whose UIDs appear in
        /// the provided UID set to the end of the target mailbox. The source
        /// messages remain unchanged in the currently selected mailbox.
        /// </para>
        ///
        /// <para>
        /// On success, the server returns a final tagged <c>OK</c> response.
        /// If supported, the server SHOULD include a <c>COPYUID</c> response code:
        /// <c>COPYUID &lt;uidvalidity&gt; &lt;source-uids&gt; &lt;dest-uids&gt;</c>,
        /// which maps the copied source UIDs to the newly assigned UIDs in the
        /// target mailbox.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// UID COPY operates on stable message UIDs rather than volatile
        /// sequence numbers.
        /// </item>
        /// <item>
        /// The target mailbox name must be valid and accessible to the
        /// authenticated user.
        /// </item>
        /// <item>
        /// COPY does not modify flags on the source messages.
        /// </item>
        /// <item>
        /// The <c>COPYUID</c> response code is optional but recommended for
        /// servers implementing UIDPLUS.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public void MessagesCopyUid(IMAP_t_SeqSet seqSet,string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesCopyUidAsync(seqSet,folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesCopyUidAsync

        /// <summary>
        /// Executes the IMAP <c>UID COPY</c> command, copying the messages
        /// identified by the specified UID set to the target mailbox.
        /// </summary>
        /// <param name="seqSet">The UID set identifying the messages to copy.</param>
        /// <param name="folder">The target mailbox to which the messages are copied.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>UID COPY</c> command.
        /// </exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// <b>RFC 4315 — UIDPLUS Extension</b><br/>
        /// The <c>UID COPY</c> command copies the messages whose UIDs appear in
        /// the provided UID set to the end of the target mailbox. The source
        /// messages remain unchanged in the currently selected mailbox.
        /// </para>
        ///
        /// <para>
        /// On success, the server returns a final tagged <c>OK</c> response.
        /// If supported, the server SHOULD include a <c>COPYUID</c> response code:
        /// <c>COPYUID &lt;uidvalidity&gt; &lt;source-uids&gt; &lt;dest-uids&gt;</c>,
        /// which maps the copied source UIDs to the newly assigned UIDs in the
        /// target mailbox.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// <list type="bullet">
        /// <item>
        /// UID COPY operates on stable message UIDs rather than volatile
        /// sequence numbers.
        /// </item>
        /// <item>
        /// The target mailbox name must be valid and accessible to the
        /// authenticated user.
        /// </item>
        /// <item>
        /// COPY does not modify flags on the source messages.
        /// </item>
        /// <item>
        /// The <c>COPYUID</c> response code is optional but recommended for
        /// servers implementing UIDPLUS.
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        public async ValueTask MessagesCopyUidAsync(IMAP_t_SeqSet seqSet,string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }            
            if(string.IsNullOrEmpty(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }

            /* RFC 4315 — UIDPLUS Extension: UID COPY Command

               The UID COPY command copies the messages whose UIDs appear in the
               specified UID set to the end of the target mailbox. The source
               messages remain unchanged in the currently selected mailbox.

               UID-COPY-Command = tag SP "UID" SP "COPY" SP uid-set SP mailbox CRLF

               Server Behavior:
                 - Valid only in the Selected state.
                 - Requires server support for the UIDPLUS extension to return
                   the COPYUID response code.
                 - The server MUST copy each message whose UID is included in
                   the provided UID set.
                 - The server MUST NOT alter the source messages.
                 - On success, the server MUST send a final tagged OK response.
                 - If supported, the server SHOULD include a COPYUID response code:
                       COPYUID <uidvalidity> <source-uids> <dest-uids>
                   which maps the copied source UIDs to the newly assigned UIDs
                   in the target mailbox.
                 - If the target mailbox does not exist, the server MUST return
                   a tagged NO response.

               Notes:
                 - UID COPY operates on stable message UIDs rather than volatile
                   sequence numbers.
                 - The target mailbox name MUST be valid and accessible to the
                   authenticated user.
                 - COPY does not modify flags on the source messages.
                 - The COPYUID response code is optional but recommended for
                   servers implementing UIDPLUS.

               Short Example:
                 C: A003 UID COPY 100:102 "Archive"
                 S: A003 OK [COPYUID 385752 100:102 200:202] COPY completed
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            await SendCommandLineAsync($"{m_CommandIndex++:d5} UID COPY {seqSet.ToString()} {folder}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method MessagesMove

        /// <summary>
        /// Sends a <c>MOVE</c> command for the specified message sequence set,
        /// relocating the messages to the target mailbox. This method executes
        /// the operation synchronously using the client's configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method creates a cancellation token based on the client's timeout
        /// value and invokes <see cref="MessagesMoveAsync"/> to perform the
        /// actual MOVE operation. If the server supports the IMAP <c>MOVE</c>
        /// extension (RFC 6851), the relocation is performed atomically. If not,
        /// a compatibility sequence consisting of <c>COPY</c>, <c>STORE</c>
        /// <c>+FLAGS.SILENT (\Deleted)</c>, and <c>EXPUNGE</c> is used.
        /// </para>
        /// <para>
        /// Any error reported by the final command completion response results in
        /// an <see cref="IMAP_ClientException"/> being thrown.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The message sequence set identifying which messages are to be moved.
        /// </param>
        /// <param name="folder">
        /// The name of the destination mailbox to which the messages will be
        /// relocated.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public void MessagesMove(IMAP_t_SeqSet seqSet,string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesMoveAsync(seqSet,folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesMoveAsync

        /// <summary>
        /// Sends a <c>MOVE</c> command for the specified message sequence set,
        /// relocating the messages to the target mailbox. When the server
        /// supports the IMAP <c>MOVE</c> extension (RFC 6851), the operation is
        /// performed atomically; otherwise, a compatibility sequence is used.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the server advertises the <c>MOVE</c> capability, the method issues
        /// a <c>MOVE</c> command that copies the messages to the destination
        /// mailbox and removes them from the currently selected mailbox as a
        /// single atomic operation. The method then waits for the final command
        /// completion response. If the final response indicates an error, an
        /// <see cref="IMAP_ClientException"/> is thrown.
        /// </para>
        /// <para>
        /// If the server does not support the <c>MOVE</c> extension, the method
        /// performs a fallback sequence equivalent to <c>MOVE</c>:
        /// <list type="number">
        /// <item><description><c>COPY</c> the messages to the destination mailbox</description></item>
        /// <item><description><c>STORE +FLAGS.SILENT (\Deleted)</c> on the source messages</description></item>
        /// <item><description><c>EXPUNGE</c> the selected mailbox</description></item>
        /// </list>
        /// This provides MOVE‑equivalent behavior for servers implementing only
        /// RFC 3501.
        /// </para>
        /// <para>
        /// The method operates on message sequence numbers. A UID‑based variant
        /// is available through <c>MessagesMoveUidAsync</c>.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The message sequence set identifying which messages are to be moved.
        /// </param>
        /// <param name="folder">
        /// The name of the destination mailbox to which the messages will be
        /// relocated.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public async ValueTask MessagesMoveAsync(IMAP_t_SeqSet seqSet,string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }            
            if(string.IsNullOrEmpty(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }

            /* RFC 6851 — MOVE Command

               The MOVE command relocates the specified messages to the end of the
               target mailbox. After the messages are successfully copied, they are
               removed from the source mailbox as part of the same atomic operation.

               MOVE-Command = tag SP "MOVE" SP sequence-set SP mailbox CRLF

               Server Behavior:
                 - Valid only in the Selected state.
                 - The server MUST copy each message in the sequence-set to the
                   target mailbox.
                 - After copying, the server MUST remove the source messages from
                   the currently selected mailbox.
                 - The server MUST send a final tagged OK response on success.
                 - If the target mailbox does not exist, the server MUST return
                   a tagged NO response.
                 - If the server supports UIDPLUS, it MAY include a MOVEUID response
                   code:
                       MOVEUID <uidvalidity> <source-uids> <dest-uids>
                   which maps the moved source UIDs to the newly assigned UIDs in
                   the target mailbox.

               Notes:
                 - MOVE is functionally equivalent to COPY followed by
                   STORE +FLAGS.SILENT (\Deleted) and then EXPUNGE, but performed
                   as a single atomic command.
                 - The target mailbox name MUST be valid and accessible to the
                   authenticated user.
                 - MOVE operates on message sequence numbers. The UID variant
                   (UID MOVE) operates on stable message UIDs.

               Short Example:
                 C: A143 MOVE 1:3 "Archive"
                 S: A143 OK MOVE completed

               UID Example:
                 C: A144 UID MOVE 100:102 "Archive"
                 S: A144 OK [MOVEUID 385752 100:102 200:202] MOVE completed
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            if(this.SupportsCapability("MOVE")){
                await SendCommandLineAsync($"{m_CommandIndex++:d5} MOVE {seqSet.ToString()} {folder}\r\n",true,cancellationToken);

                var response = await ReadFinalResponseAsync(true,null,cancellationToken);
                if(response.IsError){
                    throw new IMAP_ClientException(response);
                }
            }
            else{
                await MessagesCopyAsync(seqSet,folder,cancellationToken);
                await MessagesStoreFlagsAsync(seqSet,IMAP_t_Store_FlagsMode.AddSilent,new string[]{"\\Deleted"},cancellationToken);
                await MessagesExpungeAsync(cancellationToken);
            }
        }

        #endregion

        #region method MessagesMoveUid

        /// <summary>
        /// Sends a <c>UID MOVE</c> command for the specified UID set, relocating
        /// the messages to the target mailbox. This method executes the operation
        /// synchronously using the client's configured timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method creates a cancellation token based on the client's timeout
        /// value and invokes <see cref="MessagesMoveUidAsync"/> to perform the
        /// actual UID MOVE operation. If the server supports the IMAP
        /// <c>MOVE</c> extension (RFC 6851), the relocation is performed
        /// atomically. If not, a compatibility sequence consisting of
        /// <c>UID COPY</c>, <c>UID STORE +FLAGS.SILENT (\Deleted)</c>, and either
        /// <c>UID EXPUNGE</c> (when <c>UIDPLUS</c> is supported) or a normal
        /// <c>EXPUNGE</c> is used.
        /// </para>
        /// <para>
        /// Any error reported by the final command completion response results in
        /// an <see cref="IMAP_ClientException"/> being thrown.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The UID set identifying which messages are to be moved.
        /// </param>
        /// <param name="folder">
        /// The name of the destination mailbox to which the messages will be
        /// relocated.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public void MessagesMoveUid(IMAP_t_SeqSet seqSet,string folder)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesMoveUidAsync(seqSet,folder,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesMoveUidAsync

        /// <summary>
        /// Sends a <c>UID MOVE</c> command for the specified UID set, relocating
        /// the messages to the target mailbox. When the server supports the IMAP
        /// <c>MOVE</c> extension (RFC 6851), the operation is performed atomically.
        /// Otherwise, a compatibility sequence using UID‑based operations is used.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the server advertises the <c>MOVE</c> capability, the method issues
        /// a <c>UID MOVE</c> command that copies the messages to the destination
        /// mailbox and removes them from the currently selected mailbox as a
        /// single atomic operation. The method then waits for the final command
        /// completion response. If the final response indicates an error, an
        /// <see cref="IMAP_ClientException"/> is thrown.
        /// </para>
        /// <para>
        /// If the server does not support the <c>MOVE</c> extension, the method
        /// performs a fallback sequence equivalent to <c>UID MOVE</c>:
        /// <list type="number">
        /// <item><description><c>UID COPY</c> the messages to the destination mailbox</description></item>
        /// <item><description><c>UID STORE +FLAGS.SILENT (\Deleted)</c> on the source messages</description></item>
        /// <item><description>
        /// If the server supports <c>UIDPLUS</c>, issue <c>UID EXPUNGE</c> for the
        /// specified UID set; otherwise, issue a normal <c>EXPUNGE</c>.
        /// </description></item>
        /// </list>
        /// This provides MOVE‑equivalent behavior for servers implementing only
        /// RFC 3501 and RFC 4315.
        /// </para>
        /// <para>
        /// The method operates on stable message UIDs. A sequence‑number variant
        /// is available through <c>MessagesMoveAsync</c>.
        /// </para>
        /// </remarks>
        /// <param name="seqSet">
        /// The UID set identifying which messages are to be moved.
        /// </param>
        /// <param name="folder">
        /// The name of the destination mailbox to which the messages will be
        /// relocated.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder
        /// is selected, or the client is in an IDLE state.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="folder"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the final command response indicates an error.
        /// </exception>
        public async ValueTask MessagesMoveUidAsync(IMAP_t_SeqSet seqSet,string folder,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }            
            if(string.IsNullOrEmpty(folder)){
                throw new ArgumentException("Folder name must be specified.", nameof(folder));
            }

            /* RFC 6851 — MOVE Command

               The MOVE command relocates the specified messages to the end of the
               target mailbox. After the messages are successfully copied, they are
               removed from the source mailbox as part of the same atomic operation.

               MOVE-Command = tag SP "MOVE" SP sequence-set SP mailbox CRLF

               Server Behavior:
                 - Valid only in the Selected state.
                 - The server MUST copy each message in the sequence-set to the
                   target mailbox.
                 - After copying, the server MUST remove the source messages from
                   the currently selected mailbox.
                 - The server MUST send a final tagged OK response on success.
                 - If the target mailbox does not exist, the server MUST return
                   a tagged NO response.
                 - If the server supports UIDPLUS, it MAY include a MOVEUID response
                   code:
                       MOVEUID <uidvalidity> <source-uids> <dest-uids>
                   which maps the moved source UIDs to the newly assigned UIDs in
                   the target mailbox.

               Notes:
                 - MOVE is functionally equivalent to COPY followed by
                   STORE +FLAGS.SILENT (\Deleted) and then EXPUNGE, but performed
                   as a single atomic command.
                 - The target mailbox name MUST be valid and accessible to the
                   authenticated user.
                 - MOVE operates on message sequence numbers. The UID variant
                   (UID MOVE) operates on stable message UIDs.

               Short Example:
                 C: A143 MOVE 1:3 "Archive"
                 S: A143 OK MOVE completed

               UID Example:
                 C: A144 UID MOVE 100:102 "Archive"
                 S: A144 OK [MOVEUID 385752 100:102 200:202] MOVE completed
            */

            folder = IMAP_Utils.EncodeMailbox(folder,m_MailboxEncoding);

            if(this.SupportsCapability("MOVE")){
                await SendCommandLineAsync($"{m_CommandIndex++:d5} UID MOVE {seqSet.ToString()} {folder}\r\n",true,cancellationToken);

                var response = await ReadFinalResponseAsync(true,null,cancellationToken);
                if(response.IsError){
                    throw new IMAP_ClientException(response);
                }
            }
            else{
                await MessagesCopyUidAsync(seqSet,folder,cancellationToken);
                await MessagesStoreFlagsUidAsync(seqSet,IMAP_t_Store_FlagsMode.AddSilent,new string[]{"\\Deleted"},cancellationToken);
                if(this.SupportsCapability("UIDPLUS")){
                    await MessagesExpungeUidAsync(seqSet,cancellationToken);
                }
                else{
                    await MessagesExpungeAsync(cancellationToken);
                }
            }
        }

        #endregion

        #region method MessagesExpunge

        /// <summary>
        /// Executes the IMAP <c>EXPUNGE</c> command on the currently selected mailbox.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>EXPUNGE</c> command permanently removes all messages in the selected
        /// mailbox that have the <c>\Deleted</c> flag set. For each removed message,
        /// the server sends untagged <c>EXPUNGE</c> responses indicating that messages
        /// have been expunged.
        /// </para>
        ///
        /// <para>
        /// This synchronous wrapper invokes <see cref="MessagesExpungeAsync"/> and
        /// blocks until the operation completes.
        /// </para>
        ///
        /// <para>
        /// <b>Client Responsibilities</b><br/>
        /// The client must update its local message cache based on untagged
        /// <c>EXPUNGE</c> responses received during command execution.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// The <c>EXPUNGE</c> command has no arguments and always processes all
        /// messages flagged <c>\Deleted</c> in the selected mailbox.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>EXPUNGE</c> command.
        /// </exception>
        public void MessagesExpunge()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesExpungeAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesExpungeAsync

        /// <summary>
        /// Executes the IMAP <c>EXPUNGE</c> command on the currently selected mailbox.
        /// </summary>
        /// <remarks>
        /// <para>
        /// RFC 3501 section 6.4.3 — <b>EXPUNGE Command</b><br/>
        /// The <c>EXPUNGE</c> command permanently removes all messages in the
        /// selected mailbox that have the <c>\Deleted</c> flag set. For each
        /// message removed, the server sends an untagged <c>EXPUNGE</c> response
        /// indicating that a message has been expunged from the mailbox.
        /// </para>
        ///
        /// <para>
        /// <b>Server Behavior</b>
        /// <list type="bullet">
        ///   <item><description>Valid only in the Selected state.</description></item>
        ///   <item><description>All messages marked <c>\Deleted</c> are removed.</description></item>
        ///   <item><description>
        ///     The server sends one untagged <c>EXPUNGE</c> response per removed
        ///     message.
        ///   </description></item>
        ///   <item><description>
        ///     A final tagged <c>OK</c> response indicates completion of the command.
        ///   </description></item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// <b>Client Responsibilities</b><br/>
        /// The client must update its local message cache based on untagged
        /// <c>EXPUNGE</c> responses received during command execution.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// The <c>EXPUNGE</c> command has no arguments and always processes all
        /// messages flagged <c>\Deleted</c> in the selected mailbox.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or the client is currently in IDLE mode.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>EXPUNGE</c> command.
        /// </exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async ValueTask MessagesExpungeAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

           /* RFC 3501 section 6.4.3 — EXPUNGE Command

               The EXPUNGE command permanently removes all messages in the selected
               mailbox that have the \Deleted flag set. For each message removed,
               the server MUST send an untagged EXPUNGE response reporting the
               message’s *current* sequence number at the moment of removal.

               Important:
                 - EXPUNGE reports the sequence number of the message being removed.
                 - It does NOT report updated sequence numbers for the remaining
                   messages.
                 - Although remaining messages shift down internally, EXPUNGE
                   responses only describe the message that is actually expunged.

               EXPUNGE-Command = tag SP "EXPUNGE" CRLF

               Server Behavior:
                 - Valid only in the Selected state.
                 - The server MUST remove every message marked \Deleted.
                 - For each removed message, the server MUST send:
                       "<seqno> EXPUNGE"
                   where <seqno> is the message’s sequence number BEFORE removal.
                 - After each EXPUNGE response, all higher sequence numbers shift
                   down by one, but this shift is NOT reported directly.
                 - EXPUNGE responses MUST be sent in strictly ascending order of
                   the original sequence numbers.
                 - The server MUST send a final tagged OK response:
                       tag SP "OK" SP resp-text CRLF

               Notes:
                 - EXPUNGE has no arguments; it always processes all \Deleted messages.
                 - Clients MUST update their internal sequence maps after each
                   EXPUNGE response.
                 - EXPUNGE does not affect session state beyond mailbox contents.

               Example:
                 Initial mailbox sequence numbers:
                   1  2  3  4  5
                   (messages 2 and 4 are marked \Deleted)

                 C: A202 EXPUNGE
                 S: 2 EXPUNGE        ; message 2 removed
                                     ; remaining messages shift: 1,2,3,4
                 S: 3 EXPUNGE        ; original message 4 is now at seqno 3
                                     ; remaining messages shift: 1,2,3
                 S: A202 OK EXPUNGE completed
            */

            await SendCommandLineAsync((m_CommandIndex++).ToString("d5") + " EXPUNGE\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method MessagesExpungeUid

        /// <summary>
        /// Executes the IMAP <c>UID EXPUNGE</c> command synchronously, removing
        /// only the messages whose UIDs are included in the specified UID set.
        /// </summary>
        /// <param name="seqSet">The UID set specifying which messages to expunge.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, the client is currently in IDLE mode, or the server does not
        /// support UIDPLUS.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>UID EXPUNGE</c> command.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around
        /// <see cref="MessagesExpungeUidAsync(IMAP_t_SeqSet, CancellationToken)"/>.
        /// It blocks until the <c>UID EXPUNGE</c> operation completes.
        /// </para>
        ///
        /// <para>
        /// <b>RFC 4315 — UIDPLUS Extension</b><br/>
        /// The <c>UID EXPUNGE</c> command permanently removes only those messages
        /// in the selected mailbox that both appear in the provided UID set and
        /// have the <c>\Deleted</c> flag set. Messages marked <c>\Deleted</c> but
        /// not included in the UID set remain in the mailbox.
        /// </para>
        ///
        /// <para>
        /// This command requires server support for the UIDPLUS extension.
        /// If UIDPLUS is not available, clients must fall back to marking
        /// messages <c>\Deleted</c> via <c>UID STORE</c> and issuing a normal
        /// <c>EXPUNGE</c>.
        /// </para>
        /// </remarks>
        public void MessagesExpungeUid(IMAP_t_SeqSet seqSet)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            MessagesExpungeUidAsync(seqSet,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MessagesExpungeUidAsync

        /// <summary>
        /// Executes the IMAP <c>UID EXPUNGE</c> command, selectively removing
        /// messages whose UIDs are included in the specified UID set.
        /// </summary>
        /// <param name="seqSet">The UID set specifying which messages to expunge.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance is disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="seqSet"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, the client is currently in IDLE mode, or the server does not
        /// support UIDPLUS.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server returns an error response to the <c>UID EXPUNGE</c> command.
        /// </exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <para>
        /// <b>RFC 4315 — UIDPLUS Extension</b><br/>
        /// The <c>UID EXPUNGE</c> command permanently removes only those messages
        /// in the selected mailbox that both appear in the provided UID set and
        /// have the <c>\Deleted</c> flag set. Messages marked <c>\Deleted</c> but
        /// not included in the UID set remain in the mailbox.
        /// </para>
        ///
        /// <para>
        /// For each removed message, the server sends an untagged
        /// <c>EXPUNGE</c> response, followed by a final tagged <c>OK</c> response
        /// indicating completion of the command.
        /// </para>
        ///
        /// <para>
        /// <b>Notes</b><br/>
        /// This command requires server support for the UIDPLUS extension.
        /// If UIDPLUS is not available, clients must fall back to marking
        /// messages <c>\Deleted</c> via <c>UID STORE</c> and issuing a normal
        /// <c>EXPUNGE</c>, which removes all <c>\Deleted</c> messages.
        /// </para>
        /// </remarks>
        public async ValueTask MessagesExpungeUidAsync(IMAP_t_SeqSet seqSet,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }
            if(seqSet == null){
                throw new ArgumentNullException(nameof(seqSet));
            }

            /* RFC 4315 — UIDPLUS Extension: UID EXPUNGE Command

               The UID EXPUNGE command selectively removes messages from the
               selected mailbox. Only messages whose UIDs appear in the specified
               UID set and that also have the \Deleted flag set are expunged.
               Messages marked \Deleted but not included in the UID set remain
               in the mailbox.

               UID-EXPUNGE-Command = tag SP "UID" SP "EXPUNGE" SP uid-set CRLF

               Server Behavior:
                 - Valid only in the Selected state.
                 - Requires server support for the UIDPLUS extension.
                 - The server MUST expunge only those messages that:
                     (1) have the \Deleted flag set, and
                     (2) appear in the provided UID set.
                 - For each removed message, the server sends an untagged
                   EXPUNGE response of the form:
                       "* <seqno> EXPUNGE"
                   where <seqno> is the current sequence number of the message
                   being removed.
                 - The server MUST send a final tagged OK response indicating
                   completion of the command.

               Notes:
                 - UID EXPUNGE provides selective expunging based on stable UIDs.
                 - If UIDPLUS is not supported, clients must fall back to marking
                   messages \Deleted via UID STORE and issuing a normal EXPUNGE.
                 - UID EXPUNGE has no effect on messages not included in the UID set.

               Short Example:
                 C: A001 UID EXPUNGE 100:102
                 S: * 3 EXPUNGE
                 S: * 5 EXPUNGE
                 S: * 7 EXPUNGE
                 S: A001 OK UID EXPUNGE completed
            */

            await SendCommandLineAsync($"{m_CommandIndex++:d5} UID EXPUNGE {seqSet.ToString()}\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion

        #region method IdleStart

        /// <summary>
        /// Synchronously starts the IMAP <c>IDLE</c> command on the currently selected folder.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around <see cref="IdleStartAsync"/> and behaves
        /// identically, including all protocol requirements and error conditions. The client
        /// enters the IMAP <c>IDLE</c> state after the server sends the continuation response
        /// (<c>+</c>), and begins receiving untagged mailbox update responses until
        /// <see cref="IdleStop"/> is called.
        /// </para>
        /// <para>
        /// The operation is executed with the client's configured <see cref="Timeout"/>, and
        /// will throw if the server does not respond within that interval.
        /// </para>
        /// <para>
        /// Only one <c>IDLE</c> operation may be active at a time. Any other IMAP command is
        /// invalid while idling and must be preceded by a call to <see cref="IdleStop"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is selected,
        /// or an <c>IDLE</c> operation is already active.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server does not accept the <c>IDLE</c> command.
        /// </exception>
        public void IdleStart()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            IdleStartAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method IdleStartAsync

        /// <summary>
        /// Starts the IMAP <c>IDLE</c> command on the currently selected folder.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>IDLE</c> command places the connection into a server‑driven state where
        /// the server sends untagged mailbox update responses (such as <c>EXISTS</c>,
        /// <c>EXPUNGE</c>, and <c>FETCH</c> flag changes) as they occur.
        /// </para>
        /// <para>
        /// Only one <c>IDLE</c> operation may be active at a time. Any other IMAP command
        /// is invalid while the client is idling and must be preceded by a call to
        /// <see cref="IdleStopAsync"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or an <c>IDLE</c> operation is already active.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown if the server does not accept the <c>IDLE</c> command.
        /// </exception>
        /// <param name="cancellationToken">
        /// Optional cancellation token for aborting the initial <c>IDLE</c> command
        /// transmission or continuation response read.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        public async ValueTask IdleStartAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            await SendCommandLineAsync($"{m_CommandIndex++:d5} IDLE\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(!response.IsContinue){
                throw new IMAP_ClientException(response);
            }
              
            m_pIdle = Task.Run(async () => await ReadFinalResponseAsync(true,null,cancellationToken));
        }

        #endregion

        #region method IdleStop

        /// <summary>
        /// Synchronously stops the IMAP <c>IDLE</c> command and returns the client to
        /// normal command mode.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is a synchronous wrapper around <see cref="IdleStopAsync"/> and
        /// behaves identically. It sends the <c>DONE</c> command to terminate the active
        /// <c>IDLE</c> operation and waits for the server's final tagged completion
        /// response, which causes the idle loop to exit.
        /// </para>
        /// <para>
        /// Any exception raised by the idle loop—such as connection failures, protocol
        /// errors, or <c>NO</c>/<c>BAD</c> server responses—is allowed to propagate
        /// directly to the caller. This ensures that the caller receives the original
        /// error condition without suppression or wrapping.
        /// </para>
        /// <para>
        /// The operation is executed with the client's configured <see cref="Timeout"/>,
        /// and will throw if the server does not complete the <c>IDLE</c> termination
        /// within that interval.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or no <c>IDLE</c> operation is currently active.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the timeout elapses before the <c>IDLE</c> termination completes.
        /// </exception>
        /// <exception cref="Exception">
        /// Propagated from the idle loop if the server returns an error response or the
        /// connection fails while waiting for the final <c>IDLE</c> completion.
        /// </exception>
        public void IdleStop()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            IdleStopAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method IdleStopAsync

        /// <summary>
        /// Stops the IMAP <c>IDLE</c> command and returns the client to normal command mode.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any exception raised by the idle loop (for example, connection loss, protocol
        /// errors, or <c>NO</c>/<c>BAD</c> server responses) is allowed to propagate to the
        /// caller. This ensures that the caller receives the original error condition
        /// rather than a wrapped or suppressed exception.
        /// </para>
        /// <para>
        /// After the idle loop completes—whether normally or due to an error—the internal
        /// idle state is cleared, allowing new IMAP commands or a subsequent
        /// <see cref="IdleStartAsync"/> call.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected, not authenticated, no folder is
        /// selected, or no <c>IDLE</c> operation is currently active.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the provided <paramref name="cancellationToken"/> is canceled before
        /// the <c>DONE</c> command is sent.
        /// </exception>
        /// <exception cref="Exception">
        /// Propagated from the idle loop if the server returns an error response or the
        /// connection fails while waiting for the final <c>IDLE</c> completion.
        /// </exception>
        /// <param name="cancellationToken">
        /// Optional cancellation token for aborting the transmission of <c>DONE</c>.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// </returns>
        public async ValueTask IdleStopAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("Not connected, you need to connect first.");
            }
            if(!this.IsAuthenticated){
                throw new InvalidOperationException("Not authenticated, you need to authenticate first.");
            }
            if(m_pSelectedFolder == null){
                throw new InvalidOperationException("Not selected state, you need to select some folder first.");
            }
            if(m_pIdle == null){
                throw new InvalidOperationException("This command is only valid in IDLE state, you need start idling before calling this command.");
            }

            await SendCommandLineAsync("DONE\r\n",true,cancellationToken);
            
            try{            
                // Wait server to send final resonse to Idle loop, it will exit.
                await m_pIdle;
            }
            finally{
                m_pIdle = null;
            }            
        }

        #endregion


        #region method Capability

        /// <summary>
        /// Executes the IMAP <c>CAPABILITY</c> command synchronously by invoking
        /// <see cref="CapabilityAsync(CancellationToken)"/> and blocking until the
        /// operation completes. The CAPABILITY command, defined in RFC 3501
        /// section 6.1.1, requests that the server report the set of supported
        /// IMAP protocol features, extensions, authentication mechanisms, and
        /// server-specific tokens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper creates a <see cref="CancellationTokenSource"/>
        /// using the client's configured <see cref="Timeout"/> value and waits for
        /// the asynchronous CAPABILITY operation to finish. Server returned capabilities are stored in the client's
        /// <see cref="Capabilities"/> collection.
        /// </para>
        /// <para>
        /// After all untagged capability data has been delivered, the server MUST
        /// send a final tagged completion response. If the server returns <c>OK</c>,
        /// the command completes normally. If the server returns <c>NO</c> or
        /// <c>BAD</c>, the underlying asynchronous method throws an
        /// <see cref="IMAP_ClientException"/>, which is propagated to the caller
        /// of this synchronous wrapper.
        /// </para>
        /// <para>
        /// The command is not valid while the client is in the IDLE state; callers
        /// must stop IDLE mode before issuing <c>CAPABILITY</c>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the command is issued
        /// while the client is in the IDLE state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a <c>NO</c> or <c>BAD</c> completion
        /// response for the <c>CAPABILITY</c> command.
        /// </exception>
        public void Capability()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            CapabilityAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region mehtod CapabilityAsync

        /// <summary>
        /// Sends the IMAP <c>CAPABILITY</c> command to the server and waits for the
        /// tagged completion response. The CAPABILITY command, defined in RFC 3501
        /// section 6.1.1, requests that the server report the set of supported IMAP
        /// protocol features, extensions, authentication mechanisms, and
        /// server-specific tokens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the client issues <c>CAPABILITY</c>, the server responds with one or
        /// more untagged <c>* CAPABILITY</c> responses, each containing a space‑
        /// separated list of capability atoms. Server returned capabilities are stored in the client's
        /// <see cref="Capabilities"/> collection.
        /// </para>
        /// <para>
        /// After all untagged capability data has been delivered, the server MUST
        /// send a final tagged completion response. If the server returns <c>OK</c>,
        /// the command is considered successful. If the server returns <c>NO</c> or
        /// <c>BAD</c>, this method throws an <see cref="IMAP_ClientException"/>
        /// containing the server's completion status.
        /// </para>
        /// <para>
        /// The command is not valid while the client is in the IDLE state; callers
        /// must stop IDLE mode before issuing <c>CAPABILITY</c>.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations associated with sending the command and receiving the final
        /// response.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous CAPABILITY
        /// operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the command is issued
        /// while the client is in the IDLE state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a <c>NO</c> or <c>BAD</c> completion
        /// response for the <c>CAPABILITY</c> command.
        /// </exception>
        public async ValueTask CapabilityAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }          
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 section 6.1.1 — CAPABILITY Command

               The CAPABILITY command requests that the server return a list of
               supported IMAP protocol features, extensions, authentication
               mechanisms, and server-specific tokens. The server responds with
               one or more untagged CAPABILITY responses followed by a tagged
               status response indicating completion of the command.

               Capability-Command = tag SP "CAPABILITY" CRLF

               Server Responses:
                 Capability-Response = "* CAPABILITY" *(SP capability) CRLF
                 Capability-Complete = tag SP ("OK" / "BAD") SP resp-text CRLF

               capability = atom
                            ; Case-insensitive token identifying a protocol feature.
                            ; Examples include:
                            ;   IMAP4rev1     — Required base protocol
                            ;   STARTTLS      — TLS negotiation supported
                            ;   LOGINDISABLED — LOGIN disabled until TLS active
                            ;   AUTH=mechanism
                            ;   LITERAL+      — Non-synchronizing literals
                            ;   SASL-IR       — SASL initial client response
                            ;   IDLE, UIDPLUS, MOVE, ESEARCH, UNSELECT, etc.

               Notes:
                 - CAPABILITY responses never contain literals or quoted strings.
                 - The server may include CAPABILITY information in the initial
                   greeting; clients SHOULD still issue CAPABILITY after STARTTLS
                   or authentication to discover updated capabilities.
                 - Servers MAY send unsolicited CAPABILITY responses at any time.

               Examples:
                 C: A001 CAPABILITY
                 S: * CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN IDLE UIDPLUS
                 S: A001 OK CAPABILITY completed

                 C: A002 CAPABILITY
                 S: * CAPABILITY IMAP4rev1 LITERAL+ SASL-IR MOVE ESEARCH
                 S: A002 OK CAPABILITY completed
            */

            await SendCommandLineAsync((m_CommandIndex++).ToString("d5") + " CAPABILITY\r\n",true,cancellationToken);
            
            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }

            if(SupportsCapability("LITERAL+")){
                m_LiteralPluss = true;
            }
        }

        #endregion

        #region method Noop

        /// <summary>
        /// Executes the IMAP <c>NOOP</c> command synchronously by invoking
        /// <see cref="NoopAsync(CancellationToken)"/> and blocking until the
        /// operation completes. The NOOP command, defined in RFC 3501 section
        /// 6.1.2, performs no action but allows the server to deliver any pending
        /// untagged status or mailbox update responses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This synchronous wrapper creates a <see cref="CancellationTokenSource"/>
        /// using the client's configured <see cref="Timeout"/> value and waits for
        /// the asynchronous NOOP operation to finish. Any untagged responses
        /// generated by the server—such as <c>EXISTS</c>, <c>RECENT</c>,
        /// <c>EXPUNGE</c>, <c>FETCH</c>, <c>FLAGS</c>, or <c>OK [ALERT]</c>—are
        /// processed as part of normal IMAP
        /// event handling.
        /// </para>
        /// <para>
        /// After all untagged responses have been processed, the server MUST send
        /// a final tagged completion response. If the server returns <c>OK</c>,
        /// the command completes normally. If the server returns <c>NO</c> or
        /// <c>BAD</c>, the underlying asynchronous method throws an
        /// <see cref="IMAP_ClientException"/>, which is propagated to the caller
        /// of this synchronous wrapper.
        /// </para>
        /// <para>
        /// The command is not valid while the client is in the IDLE state; callers
        /// must stop IDLE mode before issuing <c>NOOP</c>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the command is issued
        /// while the client is in the IDLE state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a <c>NO</c> or <c>BAD</c> completion
        /// response for the <c>NOOP</c> command.
        /// </exception>
        public void Noop()
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            NoopAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region mehtod NoopAsync

        /// <summary>
        /// Sends the IMAP <c>NOOP</c> command to the server and waits for the
        /// tagged completion response. The NOOP command, defined in RFC 3501
        /// section 6.1.2, performs no action but allows the server to deliver
        /// any pending untagged status or mailbox update responses.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When a <c>NOOP</c> command is issued, the server MAY send any number
        /// of untagged responses defined in RFC 3501 sections 7.1–7.4, including
        /// mailbox size changes (<c>EXISTS</c>, <c>RECENT</c>), message status
        /// updates (<c>EXPUNGE</c>, <c>FETCH</c>, <c>FLAGS</c>), or server status
        /// notifications such as <c>OK [ALERT]</c>. These responses are processed
        /// as part of normal IMAP event flow.
        /// </para>
        /// <para>
        /// After all untagged responses have been processed, the server MUST send
        /// a final tagged completion response. If the server returns <c>OK</c>,
        /// the command is considered successful. If the server returns <c>NO</c>
        /// or <c>BAD</c>, this method throws an <see cref="IMAP_ClientException"/>
        /// containing the server's completion status.
        /// </para>
        /// <para>
        /// The command is not valid while the client is in the IDLE state; callers
        /// must stop IDLE mode before issuing <c>NOOP</c>.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write or read
        /// operations associated with sending the command and receiving the final
        /// response.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous NOOP operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the client is not connected or when the command is issued
        /// while the client is in the IDLE state.
        /// </exception>
        /// <exception cref="IMAP_ClientException">
        /// Thrown when the server returns a <c>NO</c> or <c>BAD</c> completion
        /// response for the <c>NOOP</c> command.
        /// </exception>
        public async ValueTask NoopAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }          
            if(m_pIdle != null){
                throw new InvalidOperationException("This command is not valid in IDLE state, you need stop idling before calling this command.");
            }

            /* RFC 3501 section 6.1.2 — NOOP Command

               The NOOP command does nothing. It is used by the client to poll the
               server for any pending status updates, mailbox changes, or other
               untagged responses. The server MUST return a tagged status response
               indicating completion of the command.

               NOOP-Command = tag SP "NOOP" CRLF

               Server Responses:
                 - The server MAY send any number of untagged status or mailbox
                   update responses (RFC 3501 sections 7.1–7.4), including:
                       * EXISTS
                       * RECENT
                       * EXPUNGE
                       * FETCH
                       * FLAGS
                       * OK [ALERT]
                       * Other unsolicited responses
                 - The server MUST send a final tagged OK response:
                       tag SP "OK" SP resp-text CRLF

               Notes:
                 - NOOP is commonly used to keep the connection alive and to allow
                   the server to deliver pending mailbox changes.
                 - NOOP does not affect session state and is valid in any state
                   except before authentication.
                 - Servers MAY send unsolicited responses at any time; NOOP simply
                   provides a convenient synchronization point for clients.

               Example:
                 C: A047 NOOP
                 S: * 22 EXISTS
                 S: * 1 RECENT
                 S: A047 OK NOOP completed
            */

            await SendCommandLineAsync((m_CommandIndex++).ToString("d5") + " NOOP\r\n",true,cancellationToken);

            var response = await ReadFinalResponseAsync(true,null,cancellationToken);
            if(response.IsError){
                throw new IMAP_ClientException(response);
            }
        }

        #endregion


        #region method SendCommandAsync

        /// <summary>
        /// Sends an IMAP command composed of multiple <see cref="CommandPart"/> segments,
        /// handling literal continuation responses as required by RFC 3501.
        /// </summary>
        /// <param name="cmdItems">
        /// The ordered sequence of command segments to send. Each segment is either a
        /// command-line fragment or a literal payload. Literal headers (<c>{N}\r\n</c>)
        /// must appear as non-literal items immediately followed by a literal item.
        /// </param>
        /// <param name="log">
        /// Indicates whether command-line fragments should be logged. Literal payloads
        /// are never logged for security reasons.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous send operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous send operation.
        /// </returns>
        /// <remarks>
        /// IMAP literal continuation requires the client to pause after sending a
        /// literal header (<c>{N}\r\n</c>) and wait for a server continuation response
        /// (<c>+</c>). Only after receiving this continuation may the client transmit
        /// the literal payload.
        ///
        /// This method enforces that rule by reading a continuation response only when
        /// the next <see cref="CommandPart"/> in the sequence is a literal payload.
        /// No continuation is read after sending literal bytes or after the final
        /// command terminator.
        /// 
        /// The sequence must follow this pattern:
        /// <list type="bullet">
        /// <item><description>
        /// Non-literal item containing a literal header (<c>{N}\r\n</c>)
        /// </description></item>
        /// <item><description>
        /// Literal item containing the literal payload
        /// </description></item>
        /// <item><description>
        /// Additional non-literal items completing the command
        /// </description></item>
        /// </list>
        /// </remarks>
        private async ValueTask SendCommandAsync(CommandPart[] cmdItems,bool log,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(cmdItems);
            ArgumentNullException.ThrowIfNull(this.TcpStream);

            for(int i=0; i<cmdItems.Length; i++){
                CommandPart cmdItem = cmdItems[i];

                if(cmdItem.IsLiteral){
                    await this.TcpStream.WriteAsync(cmdItem.Literal,cancellationToken);
                    if(log){
                        LogAddWrite(0,$"<literal {cmdItem.Literal!.Length} bytes>");
                    }
                }
                else{
                    await this.TcpStream.WriteLineAsync(cmdItem.CommandLine!,cancellationToken);
                    if(log){
                        LogAddWrite(cmdItem.CommandLine!.Length,cmdItem.CommandLine.Trim());
                    }
                } 
                
                // Next itmem is literal, need to read + Continue response, before continuing.
                if(i < cmdItems.Length - 1 && cmdItems[i + 1].IsLiteral){
                    IMAP_r response = await ReadResponseAsync(log,cancellationToken);
                    if(response is not IMAP_r_ServerStatus){
                            throw new Exception("Unexcpected server response.");
                    }

                    IMAP_r_ServerStatus statusReponse = ((IMAP_r_ServerStatus)response);
                    if(!statusReponse.IsContinue){
                        throw new IMAP_ClientException(statusReponse);
                    }
                }
            }
        }

        #endregion
             
        #region method SendCommandLineAsync
         
        /// <summary>
        /// Sends a single IMAP command-line fragment to the server. The fragment must
        /// already be terminated with a CRLF sequence (<c>\r\n</c>). This method is
        /// used for command segments that do not contain literal payloads.
        /// </summary>
        /// <param name="cmdLine">
        /// The IMAP command-line text to send. The value must end with a CRLF
        /// terminator and must not contain literal payload bytes.
        /// </param>
        /// <param name="log">
        /// When true, the command-line fragment is written to the protocol log. Any
        /// sensitive values (such as credentials in the LOGIN command) must already be
        /// masked by the caller before invoking this method.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous write operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous write operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="cmdLine"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="cmdLine"/> does not end with the required CRLF
        /// terminator.
        /// </exception>
        private async ValueTask SendCommandLineAsync(string cmdLine,bool log,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(cmdLine);
            if(!cmdLine.EndsWith("\r\n")){
                throw new ArgumentException("Argument cmdLine does not end with CRLF",nameof(cmdLine));
            }   

            if(log){
                LogAddWrite(Encoding.UTF8.GetByteCount(cmdLine),cmdLine.TrimEnd());
            }
            
            await this.TcpStream.WriteLineAsync(cmdLine,cancellationToken);
        }

        #endregion

        #region method ReadFinalResponseAsync

        /// <summary>
        /// Reads IMAP server responses until a tagged <see cref="IMAP_r_ServerStatus"/>
        /// response is encountered. This overload does not provide a FETCH store‑stream
        /// callback and therefore uses the client’s default storage mechanism for any
        /// literal‑based FETCH data items.
        /// </summary>
        /// <param name="log">
        /// When true, each response line read from the server is written to the
        /// protocol log.
        /// </param>
        /// <param name="callback">
        /// Optional callback invoked for each untagged non‑FETCH response received
        /// while waiting for the final server status.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// The final tagged <see cref="IMAP_r_ServerStatus"/> response returned by the
        /// server, indicating whether the command succeeded (<c>OK</c>), failed
        /// (<c>NO</c>), or was rejected (<c>BAD</c>).
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        private ValueTask<IMAP_r_ServerStatus> ReadFinalResponseAsync(bool log,EventHandler<IMAP_r_u>? callback,CancellationToken cancellationToken = default)
        {
            return ReadFinalResponseAsync(log,callback,null,cancellationToken);
        }

                /// <summary>
        /// Reads IMAP server responses until a tagged <see cref="IMAP_r_ServerStatus"/>
        /// response is encountered. This method is used to obtain the final completion
        /// status of a command after all intermediate untagged responses have been
        /// processed.
        /// </summary>
        /// <param name="log">
        /// When true, each response line read from the server is written to the
        /// protocol log. The logging behavior is identical to that of
        /// <see cref="ReadResponseAsync(bool, CancellationToken)"/>.
        /// </param>
        /// <param name="callback">
        /// Optional callback invoked for each untagged non‑FETCH response received
        /// while waiting for the final server status. This includes unsolicited
        /// responses such as <c>EXISTS</c>, <c>EXPUNGE</c>, and <c>FLAGS</c>.
        /// </param>
        /// <param name="fetchGetStoreStreamCallback">
        /// Optional callback invoked when a FETCH response requires a storage stream
        /// for a literal‑based data item (for example <c>RFC822</c>, <c>RFC822.HEADER</c>,
        /// <c>RFC822.TEXT</c>, <c>BODY[]</c>, or <c>BODY[section]</c>). The callback
        /// allows the caller to supply a stream into which the literal payload will be
        /// written. If not provided, the client uses its default storage mechanism.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// The final tagged <see cref="IMAP_r_ServerStatus"/> response returned by the
        /// server, indicating whether the command succeeded (<c>OK</c>), failed
        /// (<c>NO</c>), or was rejected (<c>BAD</c>).
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        private async ValueTask<IMAP_r_ServerStatus> ReadFinalResponseAsync(bool log,EventHandler<IMAP_r_u>? callback,EventHandler<IMAP_e_Fetch_GetStoreStream>? fetchGetStoreStreamCallback,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            while(true){
                IMAP_r r = await ReadResponseAsync(log,fetchGetStoreStreamCallback,cancellationToken);                
                if(r is IMAP_r_ServerStatus){
                    return (IMAP_r_ServerStatus)r;
                }
                if(r is IMAP_r_u u && callback != null){
                    callback(this,u);
                }
            }
        }

        #endregion

        #region method ReadResponseAsync

        /// <summary>
        /// Reads a single IMAP server response line and returns the corresponding
        /// <see cref="IMAP_r"/> object. This overload does not provide a FETCH
        /// store‑stream callback and therefore uses the client’s default storage
        /// mechanism for any literal‑based FETCH data items.
        /// </summary>
        /// <param name="log">
        /// When true, the received response line is written to the protocol log.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A <see cref="IMAP_r"/> instance representing the server response. This may
        /// be an untagged response (<see cref="IMAP_r_u"/>), a continuation response
        /// (<c>+</c>), or a tagged completion response
        /// (<see cref="IMAP_r_ServerStatus"/>).
        /// </returns>
        private ValueTask<IMAP_r> ReadResponseAsync(bool log,CancellationToken cancellationToken = default)
        {
            return ReadResponseAsync(log,null,cancellationToken);
        }

        /// <summary>
        /// Reads a single IMAP server response line and returns the corresponding
        /// <see cref="IMAP_r"/> object. This method handles all untagged, continuation,
        /// and completion responses defined in RFC 3501, including FETCH responses
        /// that may contain literal data.
        /// </summary>
        /// <param name="log">
        /// When true, the received response line is written to the protocol log.
        /// </param>
        /// <param name="fetchGetStoreStreamCallback">
        /// Optional callback invoked when a FETCH response requires a storage stream
        /// for a literal‑based data item (such as <c>RFC822</c>, <c>RFC822.HEADER</c>,
        /// <c>RFC822.TEXT</c>, <c>BODY[]</c>, or <c>BODY[section]</c>). The callback
        /// allows the caller to supply a destination stream into which the literal
        /// payload will be written. If not provided, the client uses its default
        /// storage mechanism.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A parsed <see cref="IMAP_r"/> instance representing the server response.
        /// This may be an untagged response (<see cref="IMAP_r_u"/>), a continuation
        /// response (<c>+</c>), or a tagged completion response
        /// (<see cref="IMAP_r_ServerStatus"/>).
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the server closes the connection unexpectedly.
        /// </exception>
        private async ValueTask<IMAP_r> ReadResponseAsync(bool log,EventHandler<IMAP_e_Fetch_GetStoreStream>? fetchGetStoreStreamCallback,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            ReadLineResult responseline = await this.TcpStream.ReadLineAsync(m_LineReadBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
            // Server closed connection.
            if(responseline.BytesInBuffer == 0){
                throw new IOException("IMAP server closed connection.");
            }
            else{
                string  responseLine = responseline.LineUtf8 ?? "";
                IMAP_r? response     = null;

                LogAddRead(responseline.BytesInBuffer,responseLine);

                var imapReader = new _IMAP_Reader(responseLine,m_LineReadBuffer,this);

                // Untagged response.
                if(responseLine.StartsWith("*")){
                    string[] parts = responseLine.Split(new char[]{' '},4);
                    string   word  = responseLine.Split(' ')[1];

                    #region Untagged status responses. RFC 3501 7.1.

                    // OK,NO,BAD,PREAUTH,BYE

                    if(word.Equals("OK",StringComparison.OrdinalIgnoreCase)){
                        IMAP_r_u_ServerStatus statusResponse = IMAP_r_u_ServerStatus.Parse(responseLine);
                        response = statusResponse;

                        // Process optional response-codes(7.2). ALERT,BADCHARSET,CAPABILITY,PARSE,PERMANENTFLAGS,READ-ONLY,
                        // READ-WRITE,TRYCREATE,UIDNEXT,UIDVALIDITY,UNSEEN                                
                        if(statusResponse.OptionalResponse != null){
                            if(statusResponse.OptionalResponse is IMAP_t_orc_PermanentFlags){
                                if(this.SelectedFolder != null){
                                    this.SelectedFolder.SetPermanentFlags(((IMAP_t_orc_PermanentFlags)statusResponse.OptionalResponse).Flags);
                                }
                            }
                            else if(statusResponse.OptionalResponse is IMAP_t_orc_ReadOnly){
                                if(this.SelectedFolder != null){
                                    this.SelectedFolder.SetReadOnly(true);
                                }
                            }
                            else if(statusResponse.OptionalResponse is IMAP_t_orc_ReadWrite){
                                if(this.SelectedFolder != null){
                                    this.SelectedFolder.SetReadOnly(true);
                                }
                            }
                            else if(statusResponse.OptionalResponse is IMAP_t_orc_UidNext){
                                if(this.SelectedFolder != null){
                                    this.SelectedFolder.SetUidNext(((IMAP_t_orc_UidNext)statusResponse.OptionalResponse).UidNext);
                                }
                            }
                            else if(statusResponse.OptionalResponse is IMAP_t_orc_UidValidity){
                                if(this.SelectedFolder != null){
                                    this.SelectedFolder.SetUidValidity(((IMAP_t_orc_UidValidity)statusResponse.OptionalResponse).Uid);
                                }
                            }
                            else if(statusResponse.OptionalResponse is IMAP_t_orc_Unseen){
                                if(this.SelectedFolder != null){
                                    this.SelectedFolder.SetFirstUnseen(((IMAP_t_orc_Unseen)statusResponse.OptionalResponse).SeqNo);
                                }
                            }
                            // We don't care about other response codes.                            
                        }

                        OnUntaggedStatusResponse((IMAP_r_u)response);
                    }
                    else if(word.Equals("NO",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_ServerStatus.Parse(responseLine);

                        OnUntaggedStatusResponse((IMAP_r_u)response);
                    }
                    else if(word.Equals("BAD",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_ServerStatus.Parse(responseLine);

                        OnUntaggedStatusResponse((IMAP_r_u)response);
                    }
                    else if(word.Equals("PREAUTH",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_ServerStatus.Parse(responseLine);

                        OnUntaggedStatusResponse((IMAP_r_u)response);
                    }
                    else if(word.Equals("BYE",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_ServerStatus.Parse(responseLine);

                        OnUntaggedStatusResponse((IMAP_r_u)response);
                    }

                    #endregion

                    #region Untagged server and mailbox status. RFC 3501 7.2.

                    // CAPABILITY,LIST,LSUB,STATUS,SEARCH,FLAGS

                    #region CAPABILITY

                    else if(word.Equals("CAPABILITY",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Capability.Parse(responseLine); 
                       
                        // Cache IMAP server capabilities.
                        m_pCapabilities = new List<string>();
                        m_pCapabilities.AddRange(((IMAP_r_u_Capability)response).Capabilities);
                    }

                    #endregion

                    #region LIST

                    else if(word.Equals("LIST",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_List.Parse(imapReader,cancellationToken);
                    }

                    #endregion

                    #region LSUB

                    else if(word.Equals("LSUB",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_LSub.Parse(imapReader,cancellationToken);
                    }

                    #endregion

                    #region STATUS

                    else if(word.Equals("STATUS",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_Status.Parse(imapReader,cancellationToken);
                    }

                    #endregion

                    #region SEARCH

                    else if(word.Equals("SEARCH",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Search.Parse(responseLine);
                    }

                    #endregion

                    #region FLAGS

                    else if(word.Equals("FLAGS",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Flags.Parse(responseLine);

                        if(m_pSelectedFolder != null){
                            m_pSelectedFolder.SetFlags(((IMAP_r_u_Flags)response).Flags);
                        }
                    }

                    #endregion

                    #endregion

                    #region Untagged mailbox size. RFC 3501 7.3.

                    // EXISTS,RECENT

                    else if(Net_Utils.IsInteger(word) && parts[2].Equals("EXISTS",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Exists.Parse(responseLine);

                        if(m_pSelectedFolder != null){
                            m_pSelectedFolder.SetMessagesCount(((IMAP_r_u_Exists)response).MessageCount);
                        }
                    }
                    else if(Net_Utils.IsInteger(word) && parts[2].Equals("RECENT",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Recent.Parse(responseLine);

                        if(m_pSelectedFolder != null){
                            m_pSelectedFolder.SetRecentMessagesCount(((IMAP_r_u_Recent)response).MessageCount);
                        }
                    }
                                        
                    #endregion

                    #region Untagged message status. RFC 3501 7.4.

                    // EXPUNGE,FETCH

                    else if(Net_Utils.IsInteger(word) && parts[2].Equals("EXPUNGE",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Expunge.Parse(responseLine);
                        OnMessageExpunged((IMAP_r_u_Expunge)response);
                    }
                    else if(Net_Utils.IsInteger(word) && parts[2].Equals("FETCH",StringComparison.InvariantCultureIgnoreCase)){                        
                        response = await IMAP_r_u_Fetch.ParseAsync(imapReader,fetchGetStoreStreamCallback,cancellationToken);
                    }

                    #endregion

                    #region Untagged acl realted. RFC 4314.

                    else if(word.Equals("ACL",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_Acl.ParseAsync(imapReader,cancellationToken);
                    }
                    else if(word.Equals("LISTRIGHTS",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_ListRights.ParseAsync(imapReader,cancellationToken);
                    }
                    else if(word.Equals("MYRIGHTS",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_MyRights.Parse(imapReader,cancellationToken);
                    }

                    #endregion

                    #region Untagged quota related. RFC 2087.

                    else if(word.Equals("QUOTA",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_Quota.Parse(imapReader,cancellationToken);
                    }
                    else if(word.Equals("QUOTAROOT",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_QuotaRoot.Parse(imapReader,cancellationToken);
                    }

                    #endregion

                    #region Untagged namespace related. RFC 2342.

                    else if(word.Equals("NAMESPACE",StringComparison.InvariantCultureIgnoreCase)){
                        response = await IMAP_r_u_Namespace.Parse(imapReader,cancellationToken);
                    }

                    #endregion

                    #region Untagged enable related. RFC 5161.

                    else if(word.Equals("ENABLED",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Enabled.Parse(responseLine);
                    }

                    #endregion

                    else if(word.Equals("BYE",StringComparison.InvariantCultureIgnoreCase)){
                        response = IMAP_r_u_Bye.Parse(responseLine);
                    }
                    
                    // TODO: Unknown

                    // Raise event 'UntaggedResponse'.
                    if(response != null){
                        OnUntaggedResponse((IMAP_r_u)response);
                    }
                }
                // Command continuation response.
                else if(responseLine.StartsWith("+")){
                    response = IMAP_r_ServerStatus.Parse(responseLine);
                }
                // Completion status response.
                else{
                    // Command response reading has completed.
                    response = IMAP_r_ServerStatus.Parse(responseLine);
                }                

                return response!;
            }
        }

        #endregion
        

        #region method SupportsCapability

        /// <summary>
        /// Gets if IMAP server supports the specified capability.
        /// </summary>
        /// <param name="capability">IMAP capability.</param>
        /// <returns>Return true if IMAP server supports the specified capability.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>capability</b> is null reference.</exception>
        private bool SupportsCapability(string capability)
        {
            if(capability == null){
                throw new ArgumentNullException("capability");
            }

            if(m_pCapabilities == null){
                return false;
            }
            else{
                foreach(string c in m_pCapabilities){
                    if(string.Equals(c.Split('=')[0],capability,StringComparison.OrdinalIgnoreCase)){
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the identity of the user authenticated for the current IMAP session,
        /// or <c>null</c> if no authentication has been performed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is set when an authentication command (such as <c>LOGIN</c> or a
        /// SASL mechanism) completes successfully. Until that point, the property
        /// remains <c>null</c>. The identity reflects the credentials used for the
        /// session and is not modified by subsequent IMAP operations.
        /// </para>
        /// <para>
        /// Accessing this property requires the client to be connected. If the client
        /// is not connected, an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// <para>
        /// If the IMAP client has been disposed, accessing this property will throw an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the IMAP client instance has been disposed.
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
        /// Gets the IMAP server greeting text received immediately after the TCP
        /// connection is established.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The greeting is the first line sent by the server upon connection, before
        /// any authentication or mailbox selection occurs. It typically contains an
        /// <c>OK</c>, <c>PREAUTH</c>, or <c>BYE</c> status along with optional
        /// capability information or server‑specific metadata.
        /// </para>
        /// <para>
        /// Accessing this property requires the client to be connected. If the client
        /// is not connected, an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// <para>
        /// If the IMAP client has been disposed, accessing this property will throw an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the IMAP client instance has been disposed.
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
        /// Gets the list of IMAP protocol capabilities advertised by the server for
        /// the current connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The capability list is populated from the server's <c>CAPABILITY</c>
        /// response, either from the initial greeting or from an explicit
        /// <c>CAPABILITY</c> command issued by the client. The values indicate which
        /// IMAP extensions, authentication mechanisms, and protocol features the
        /// server supports for the active session.
        /// </para>
        /// <para>
        /// Accessing this property requires the client to be connected. If the client
        /// is not connected, an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// <para>
        /// If the IMAP client has been disposed, accessing this property will throw an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// <para>
        /// If the server has not advertised any capabilities, the property returns an
        /// empty array rather than <c>null</c>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the IMAP client instance has been disposed.
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

                if(m_pCapabilities == null){
                    return new string[0];
                }

                return m_pCapabilities.ToArray(); 
            }
        }

        /// <summary>
        /// Gets the list of SASL authentication mechanisms advertised by the IMAP
        /// server for the current connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The returned array contains the SASL mechanism names extracted from
        /// capability tokens of the form <c>AUTH=mechanism</c>. These values are
        /// provided exactly as reported by the server and may include mechanisms such
        /// as <c>PLAIN</c>, <c>LOGIN</c>, <c>CRAM-MD5</c>, <c>DIGEST-MD5</c>,
        /// <c>XOAUTH2</c>, or other implementation‑specific extensions.
        /// </para>
        /// <para>
        /// The list is derived from the server's <c>CAPABILITY</c> response. If the
        /// server does not advertise any <c>AUTH=</c> tokens, the property returns an
        /// empty array rather than <c>null</c>.
        /// </para>
        /// <para>
        /// Accessing this property requires the client to be connected. If the client
        /// is not connected, an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// <para>
        /// If the IMAP client has been disposed, accessing this property will throw an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the IMAP client instance has been disposed.
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

                // Search AUTH entries.
                List<string> retVal = new List<string>();
                foreach(string feature in this.Capabilities){
                    if(feature.StartsWith("AUTH=",StringComparison.OrdinalIgnoreCase)){
                        // Syntax: AUTH=mechanism
                        retVal.Add(feature.Substring(5).Trim());
                    }
                }

                return retVal.ToArray();
            }
        }

        /// <summary>
        /// Gets the currently selected IMAP mailbox, or <c>null</c> if no mailbox
        /// has been selected for the active session.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is set when a <c>SELECT</c> or <c>EXAMINE</c> command completes
        /// successfully. Until that point, the property remains <c>null</c>. The
        /// returned <see cref="IMAP_Client_SelectedFolder"/> instance exposes all
        /// server‑reported mailbox metadata and provides methods for creating
        /// message sets within the selected mailbox.
        /// </para>
        /// <para>
        /// Accessing this property requires the client to be connected. If the client
        /// is not connected, an <see cref="InvalidOperationException"/> is thrown.
        /// </para>
        /// <para>
        /// If the IMAP client has been disposed, accessing this property will throw an
        /// <see cref="ObjectDisposedException"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the IMAP client instance has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        public IMAP_Client_SelectedFolder? SelectedFolder
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
				    throw new InvalidOperationException("You must connect first.");
			    }

                return m_pSelectedFolder; 
            }
        }

        #endregion

        #region Events implementation
        
        /// <summary>
        /// This event is raised when IMAP server sends untagged status response.
        /// </summary>
        public event EventHandler<EventArgs<IMAP_r_u>>? UntaggedStatusResponse = null;

        #region method OnUntaggedStatusResponse

        /// <summary>
        /// Raises <b>UntaggedStatusResponse</b> event.
        /// </summary>
        /// <param name="response">Untagged response.</param>
        private void OnUntaggedStatusResponse(IMAP_r_u response)
        {
            if(this.UntaggedStatusResponse != null){
                this.UntaggedStatusResponse(this,new EventArgs<IMAP_r_u>(response));
            }
        }

        #endregion

        /// <summary>
        /// Is raised when IMAP server sends any untagged response.
        /// </summary>
        /// <remarks>NOTE: This event may raised from thread pool thread, so UI event handlers need to use Invoke.</remarks>
        public event EventHandler<EventArgs<IMAP_r_u>>? UntaggedResponse = null;

        #region method OnUntaggedResponse

        /// <summary>
        /// Raises <b>UntaggedResponse</b> event.
        /// </summary>
        /// <param name="response">Untagged IMAP server response.</param>
        private void OnUntaggedResponse(IMAP_r_u response)
        {
            if(this.UntaggedResponse != null){
                this.UntaggedResponse(this,new EventArgs<IMAP_r_u>(response));
            }
        }

        #endregion
                
        /// <summary>
        /// This event is raised when IMAP server expunges message and sends EXPUNGE response.
        /// </summary>
        public event EventHandler<EventArgs<IMAP_r_u_Expunge>>? MessageExpunged = null;

        #region method OnMessageExpunged

        /// <summary>
        /// Raises <b>MessageExpunged</b> event.
        /// </summary>
        /// <param name="response">Expunge response.</param>
        private void OnMessageExpunged(IMAP_r_u_Expunge response)
        {
            if(this.MessageExpunged != null){
                this.MessageExpunged(this,new EventArgs<IMAP_r_u_Expunge>(response));
            }
        }

        #endregion

        #endregion
                
    }
}
