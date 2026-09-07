
using LumiSoft.Net.AUTH;
using LumiSoft.Net.DNS;
using LumiSoft.Net.DNS.Client;
using LumiSoft.Net.IO;
using LumiSoft.Net.Log;
using LumiSoft.Net.Mail;
using LumiSoft.Net.MIME;
using LumiSoft.Net.TCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace LumiSoft.Net.SMTP.Client
{
    /// <summary>
    /// Provides a full SMTP client implementation for sending email messages over
    /// SMTP or ESMTP. The class supports connection setup, greeting handling,
    /// capability discovery, optional TLS upgrade via STARTTLS, optional
    /// authentication, envelope commands, and message transmission.
    /// 
    /// <para>
    /// The client can be used in both simple one‑shot scenarios (via QuickSend and
    /// QuickSendAsync) or in advanced manual mode where each SMTP command is issued
    /// explicitly. It supports sending messages from streams or from
    /// <see cref="Mail_Message"/> instances.
    /// </para>
    /// 
    /// <para>
    /// The implementation follows RFC 5321 and related extensions, including EHLO
    /// feature parsing, SIZE support, and STARTTLS negotiation.
    /// </para>
    /// </summary>
    public class SMTP_Client : TCP_Client
    {
        private string?          m_LocalHostName      = null;
        private string?          m_RemoteHostName     = null;
        private string           m_GreetingText       = "";
        private bool             m_IsEsmtpSupported   = false;
        private List<string>     m_pEsmtpFeatures     = [];
        private string?          m_MailFrom           = null;
        private List<string>?    m_pRecipients        = null;
        private GenericIdentity? m_pAuthdUserIdentity = null;
        private long             m_MaxMessageSize     = -1;    
        
        /// <summary>
        /// Default constructor.
        /// </summary>
        public SMTP_Client()
        {
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
        /// Performs SMTP‑specific post‑connection initialization after the underlying TCP
        /// connection (and optional SSL/TLS negotiation) has completed. This method reads
        /// the server's initial greeting banner and validates that the server has accepted
        /// the connection with a <c>220</c> reply code.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <remarks>
        /// <para>
        /// According to RFC 5321 4.2, an SMTP server must send a greeting
        /// immediately after the connection is established. The greeting may consist of a
        /// single <c>220</c> line or a multi‑line <c>220‑</c> sequence followed by a final
        /// <c>220</c> line.
        /// </para>
        /// <para>
        /// If the greeting indicates success, this method stores the greeting text and
        /// initializes internal SMTP session state such as the ESMTP feature list and the
        /// per‑message recipient collection. If the server does not return a <c>220</c>
        /// reply code, an <see cref="SMTP_ClientException"/> is thrown.
        /// </para>
        /// <para>
        /// The base implementation in <see cref="TCP_Client"/> performs no actions; this
        /// override provides the SMTP‑specific behavior required to begin a valid SMTP
        /// session.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A task representing the asynchronous post‑connection operation.
        /// </returns>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server's initial greeting does not contain a <c>220</c> reply
        /// code, indicating that the SMTP session cannot proceed.
        /// </exception>
        protected override async ValueTask OnConnectedAsync(CancellationToken cancellationToken = default)
        {
            /* RFC 5321 4.2.
                Greeting = ( "220 " (Domain / address-literal) [ SP textstring ] CRLF ) /
                           ( "220-" (Domain / address-literal) [ SP textstring ] CRLF
                          *( "220-" [ textstring ] CRLF )
                             "220" [ SP textstring ] CRLF )

            */

            var smtpResponse = await ReadResponseAsync(cancellationToken);

            // SMTP server accepted connection, get greeting text.
            if(smtpResponse.ReplyCode == 220){
                m_GreetingText = string.Join("\r\n",smtpResponse.ReplyLines.Select((replyLine) => replyLine.Text));
                m_pEsmtpFeatures = new List<string>();
                m_pRecipients = new List<string>();
            }
            // SMTP server rejected connection.
            else{
                throw new SMTP_ClientException(smtpResponse);
            }
        }

        #endregion
                
        #region override method Disconnect

        /// <summary>
        /// Disconnects from the SMTP server and sends the <c>QUIT</c> command
        /// before closing the connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        /// <remarks>
        /// This override always performs a graceful disconnect by invoking
        /// <see cref="Disconnect(bool)"/> with <c>true</c>, causing the SMTP
        /// <c>QUIT</c> command to be sent before the connection is terminated.
        /// </remarks>
		public override void Disconnect()
		{
            Disconnect(true);
        }

        /// <summary>
        /// Disconnects from the SMTP server and optionally sends the <c>QUIT</c>
        /// command before closing the connection.
        /// </summary>
        /// <param name="sendQuit">
        /// If <c>true</c>, the method sends the SMTP <c>QUIT</c> command before
        /// terminating the connection.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
		public void Disconnect(bool sendQuit)
		{
            using var cts = new CancellationTokenSource(this.Timeout);

            DisconnectAsync(sendQuit,cts.Token).GetAwaiter().GetResult();
		}

        #endregion

        #region method DisconnectAsync

        /// <summary>
        /// Disconnects from the SMTP server and optionally sends the <c>QUIT</c> command
        /// before closing the connection.
        /// </summary>
        /// <param name="sendQuit">
        /// If <c>true</c>, the method sends the SMTP <c>QUIT</c> command before
        /// terminating the connection.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not currently connected.
        /// </exception>
        /// <remarks>
        /// When <paramref name="sendQuit"/> is <c>true</c>, the method attempts to send
        /// the <c>QUIT</c> command and read the server's final reply. Any exceptions
        /// raised during this final exchange are suppressed, and the connection is
        /// closed regardless.
        /// 
        /// All SMTP session state (greeting text, host names, ESMTP capabilities,
        /// sender and recipient information, and authenticated identity) is cleared
        /// before the underlying connection is closed.
        /// </remarks>
        public async ValueTask DisconnectAsync(bool sendQuit,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("SMTP client is not connected.");
            }

            try{            
                if(sendQuit){
                    await SendCommandLineAsync("QUIT\r\n",true,cancellationToken);

                    _= await ReadResponseAsync(cancellationToken);
                }
            }
            catch{
            }

            m_LocalHostName      = null;
            m_RemoteHostName     = null;
            m_GreetingText       = "";
            m_IsEsmtpSupported   = false;
            m_MailFrom           = null;
            m_pRecipients        = null;
            m_pAuthdUserIdentity = null;

            base.Disconnect(); 
        }

        #endregion

        #region method EhloHelo

        /// <summary>
        /// Synchronously sends the SMTP <c>EHLO</c> command using the specified host name
        /// and reads any ESMTP features advertised by the server. If the server does not
        /// accept <c>EHLO</c>, the method automatically falls back to the <c>HELO</c> command.
        /// </summary>
        /// <param name="hostName">
        /// The host to present to the server.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="hostName"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="hostName"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when both <c>EHLO</c> and <c>HELO</c> fail to receive a successful
        /// <c>250</c> reply from the server.
        /// </exception>
        /// <remarks>
        /// This method blocks the calling thread until the greeting sequence completes.
        /// When the server accepts <c>EHLO</c>, the asynchronous implementation extracts
        /// the server's greeting domain and stores all advertised ESMTP extension lines.
        /// If the server does not support <c>EHLO</c>, the method falls back to <c>HELO</c>
        /// to establish a basic SMTP session.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse EhloHelo(string hostName)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(hostName == null){
                throw new ArgumentNullException(nameof(hostName));
            }
            if(hostName == string.Empty){
                throw new ArgumentException("Argument 'hostName' value must be specified.",nameof(hostName));
            }

            using var cts = new CancellationTokenSource(this.Timeout);

            return EhloHeloAsync(hostName,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method EhloHeloAsync

        /// <summary>
        /// Sends the SMTP <c>EHLO</c> command using the specified host name and reads
        /// any ESMTP features advertised by the server. If the server does not accept
        /// <c>EHLO</c>, the method automatically falls back to the <c>HELO</c> command.
        /// </summary>
        /// <param name="hostName">
        /// The host name to present to the server.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="hostName"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="hostName"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when both <c>EHLO</c> and <c>HELO</c> fail to receive a successful
        /// <c>250</c> reply from the server.
        /// </exception>
        /// <remarks>
        /// <para>
        /// When the server accepts <c>EHLO</c>, this method extracts the server's
        /// greeting domain and reads all advertised ESMTP extension lines. These
        /// features are stored internally and can be used to determine server
        /// capabilities such as authentication methods, size limits, pipelining,
        /// and other SMTP extensions.
        /// </para>
        /// <para>
        /// If the server does not support <c>EHLO</c>, the method falls back to
        /// <c>HELO</c> to establish a basic SMTP session.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> EhloHeloAsync(string hostName,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(hostName == null){
                throw new ArgumentNullException(nameof(hostName));
            }
            if(hostName == string.Empty){
                throw new ArgumentException("Argument 'hostName' value must be specified.",nameof(hostName));
            }

            // NOTE: At frist we try EHLO command, if it fails we fallback to HELO.

            /* RFC 5321 4.1.1.1.
                ehlo        = "EHLO" SP ( Domain / address-literal ) CRLF  
             
                ehlo-ok-rsp = ( "250" SP Domain [ SP ehlo-greet ] CRLF )
                            / ( "250-" Domain [ SP ehlo-greet ] CRLF
                             *( "250-" ehlo-line CRLF )
                                "250" SP ehlo-line CRLF )

                helo        = "HELO" SP Domain CRLF
                                helo-ok-rsp = "250" SP Domain [ SP helo-greet ] CRLF
            */
            m_IsEsmtpSupported = false;
            m_MaxMessageSize = -1;
            m_pEsmtpFeatures.Clear();
            m_LocalHostName = hostName;

            await SendCommandLineAsync("EHLO " + hostName + "\r\n",true,cancellationToken);

            var smtpResponse = await ReadResponseAsync(cancellationToken);            
            if(smtpResponse.ReplyCode == 250){
                m_RemoteHostName = smtpResponse.ReplyLines[0].Text.Split(new char[]{' '},2)[0];
                m_IsEsmtpSupported = true;
                List<string> esmtpFeatures = new List<string>();
                // Skip domain line
                for(int i = 1;i < smtpResponse.ReplyLines.Length; i++){
                    esmtpFeatures.Add(smtpResponse.ReplyLines[i].Text);

                    if(smtpResponse.ReplyLines[i].Text.StartsWith("SIZE ",StringComparison.OrdinalIgnoreCase)){
                        m_MaxMessageSize = Convert.ToInt64(smtpResponse.ReplyLines[i].Text.Split(' ', 2)[1]);
                    }
                }
                m_pEsmtpFeatures = esmtpFeatures;
            }
            // EHLO failed, try HELO
            else{
                await SendCommandLineAsync("HELO " + hostName + "\r\n",true,cancellationToken);

                smtpResponse = await ReadResponseAsync(cancellationToken);
                if(smtpResponse.ReplyCode == 250){
                    m_RemoteHostName = smtpResponse.ReplyLines[0].Text.Split(new char[]{' '},2)[0];
                }
                else{
                    throw new SMTP_ClientException(smtpResponse);
                }
            }
            
            return smtpResponse;
        }

        #endregion

        #region method StartTls

        /// <summary>
        /// Synchronously sends the SMTP <c>STARTTLS</c> command and, if accepted by the server,
        /// upgrades the existing plaintext TCP connection to a secure TLS connection using
        /// <see cref="SslStream"/> and the provided <see cref="SslClientAuthenticationOptions"/>.
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
        /// These defaults maximize compatibility with older SMTP servers.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or if the connection is already secure.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server rejects the <c>STARTTLS</c> command.
        /// </exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown when the TLS handshake fails after the server accepts <c>STARTTLS</c>.
        /// </exception>
        /// <remarks>
        /// This method blocks the calling thread until the STARTTLS negotiation and TLS
        /// handshake complete.  
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse StartTls(SslClientAuthenticationOptions? sslOptions)
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

            using var cts = new CancellationTokenSource(this.Timeout);

            return StartTlsAsync(sslOptions,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method StartTlsAsync

        /// <summary>
        /// Sends the SMTP <c>STARTTLS</c> command and, if accepted by the server,
        /// upgrades the existing plaintext TCP connection to a secure TLS connection
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
        /// These defaults maximize compatibility with older SMTP servers.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or if the connection is already secure.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server rejects the <c>STARTTLS</c> command (reply codes other than 220).
        /// </exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown when the TLS handshake fails after the server accepts <c>STARTTLS</c>.
        /// </exception>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> StartTlsAsync(SslClientAuthenticationOptions? sslOptions,CancellationToken cancellationToken = default)
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

            /* RFC 3207 4.
                After STARTTLS completes, the client must treat the session as if it just connected.
              
                The format for the STARTTLS command is:

                STARTTLS

                with no parameters.

                After the client gives the STARTTLS command, the server responds with
                one of the following reply codes:

                220 Ready to start TLS
                501 Syntax error (no parameters allowed)
                454 TLS not available due to temporary reason
            */

            await SendCommandLineAsync("STARTTLS\r\n",true,cancellationToken);

            var smtpResponse = await ReadResponseAsync(cancellationToken);
            // STARTTLS accepted.
            if(smtpResponse.ReplyCode == 220){
                LogAddText("Starting TLS handshake.");

                await SwitchToSecureAsync(sslOptions,cancellationToken);

                LogAddText("TLS handshake completed sucessfully.");

                m_LocalHostName = null;
                m_MailFrom = null;
                m_pRecipients!.Clear();
            }
            else{
                throw new SMTP_ClientException(smtpResponse);
            }

            return smtpResponse;
        }

        #endregion

        #region method AuthGetStrongestMethod

        /// <summary>
        /// Selects the strongest SASL authentication mechanism supported by both the
        /// SMTP server and this client. Mechanisms are evaluated in the following
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
        /// Thrown if the SMTP server does not advertise any SASL mechanisms, or if
        /// none of the advertised mechanisms are supported by this client.
        /// </exception>
        public AUTH_SASL_Client AuthGetStrongestMethod(string userName,string password)
        {
            return AuthGetStrongestMethod(null,userName,password);
        }

        /// <summary>
        /// Selects the strongest SASL authentication mechanism supported by both the
        /// SMTP server and this client. Mechanisms are evaluated in the following
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
        /// Thrown if the SMTP server does not advertise any SASL mechanisms, or if
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
                throw new NotSupportedException("SMTP server does not support authentication.");
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
                return new AUTH_SASL_Client_DigestMd5("SMTP",this.RemoteEndPoint.Address.ToString(),userName,password);
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
                throw new NotSupportedException("We don't support any of the SMTP server authentication methods.");
            }
        }

        #endregion

        #region method Auth

        /// <summary>
        /// Performs SMTP authentication synchronously using the specified SASL
        /// mechanism. This method blocks until authentication completes.
        /// </summary>
        /// <param name="sasl">
        /// The SASL authentication mechanism to use. Any SASL client may be supplied.
        /// <see cref="AuthGetStrongestMethod(string?, string, string)"/> is the
        /// recommended helper, as it selects a strongest mechanism supported by the server.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or authentication has already
        /// completed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="sasl"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server returns a reply code indicating authentication
        /// failure.
        /// </exception>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse Auth(AUTH_SASL_Client sasl)
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
                throw new ArgumentNullException("sasl");
            }

            using var cts = new CancellationTokenSource(this.Timeout);

            return AuthAsync(sasl,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method AuthAsync

        /// <summary>
        /// Performs SMTP authentication using the specified SASL client mechanism.
        /// Sends the <c>AUTH</c> command and processes any challenge–response
        /// exchanges required by the mechanism.
        /// </summary>
        /// <param name="sasl">
        /// The SASL authentication mechanism to use. Any SASL client may be supplied.
        /// <see cref="AuthGetStrongestMethod(string?, string, string)"/> is the
        /// recommended helper, as it selects a strongest mechanism supported by the server.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or authentication has already
        /// completed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="sasl"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server returns a reply code other than <c>235</c> or
        /// <c>334</c>, indicating authentication failure.
        /// </exception>
        /// <remarks>
        /// If the SASL mechanism supports an initial client response, it is sent
        /// with the <c>AUTH</c> command. Otherwise, the method waits for the server's
        /// first challenge. The loop continues until a final <c>235</c> reply is
        /// received.
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> AuthAsync(AUTH_SASL_Client sasl,CancellationToken cancellationToken = default)
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

            /* RFC 4954 4. The AUTH Command.

                AUTH mechanism [initial-response]

                Arguments:
                    mechanism: A string identifying a [SASL] authentication mechanism.

                    initial-response: An optional initial client response.  If
                    present, this response MUST be encoded as described in Section
                    4 of [BASE64] or contain a single character "=".
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
                LogAddWrite(authCommand.Length,authCommand);
            #else
                LogAddWrite(authCommand.Length,"Client response sent.");
            #endif

            while(true){
                var smtpResponse = await ReadResponseAsync(cancellationToken);
                
                // Authentication suceeded.
                if(smtpResponse.ReplyCode == 235){
                    m_pAuthdUserIdentity = new GenericIdentity(sasl.UserName,sasl.Name);

                    return smtpResponse;
                }
                // Continue authenticating.
                else if(smtpResponse.ReplyCode == 334){
                    // 334 base64Data, we need to decode it and pass to SASL auth mechanism.

                    // Pass server response to SASL authentication.
                    byte[] saslResponse = sasl.Continue(Convert.FromBase64String(smtpResponse.ReplyLines[0].Text));
                    
                    string clientResponse;
                    // SASL auth requested canel. Canele reply is not encoded single token '='.
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
                    throw new SMTP_ClientException(smtpResponse);
                }
            }
        }

        #endregion

        #region method MailFrom

        /// <summary>
        /// Synchronously sends the SMTP <c>MAIL FROM</c> command using the specified
        /// sender address and an optional <c>SIZE</c> parameter, depending on the
        /// server's advertised capabilities.
        /// </summary>
        /// <param name="from">
        /// The envelope sender address to present to the server.
        /// </param>
        /// <param name="messageSize">
        /// The message size in bytes. A value of <c>-1</c> indicates that no
        /// <c>SIZE</c> parameter should be sent. The <c>SIZE</c> parameter is only
        /// included when the server supports the extension and the value is greater
        /// than zero.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="from"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="from"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>MAIL FROM</c> command failed.
        /// </exception>
        /// <remarks>
        /// This overload sends the <c>MAIL FROM</c> command without any delivery
        /// status notification parameters. It delegates to the full method that
        /// supports <c>RET</c> and <c>ENVID</c>, passing default values for both.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse MailFrom(string from,long messageSize)
        {
            return MailFrom(from,messageSize,SMTP_t_DSN_Ret.NotSpecified,null);
        }

        /// <summary>
        /// Synchronously sends the SMTP <c>MAIL FROM</c> command using the specified
        /// sender address and optional ESMTP parameters such as <c>SIZE</c>,
        /// <c>RET</c>, and <c>ENVID</c>, depending on the server's advertised
        /// capabilities.
        /// </summary>
        /// <param name="from">
        /// The envelope sender address to present to the server.
        /// </param>
        /// <param name="messageSize">
        /// The message size in bytes. A value of <c>-1</c> indicates that no
        /// <c>SIZE</c> parameter should be sent. The <c>SIZE</c> parameter is only
        /// included when the server supports the extension and the value is greater
        /// than zero.
        /// </param>
        /// <param name="ret">
        /// The delivery status notification return option. Used only when the
        /// server supports the <c>DSN</c> extension.
        /// </param>
        /// <param name="envid">
        /// An optional envelope identifier included in delivery status notifications.
        /// Used only when the server supports the <c>DSN</c> extension.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="from"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="from"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>MAIL FROM</c> command failed.
        /// </exception>
        /// <remarks>
        /// This method blocks the calling thread until the <c>MAIL FROM</c>
        /// operation completes. The asynchronous implementation constructs the
        /// command and conditionally appends ESMTP parameters based on the server's
        /// advertised capabilities. On success, the current envelope sender value is
        /// stored internally so subsequent recipient commands can be issued.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse MailFrom(string from,long messageSize,SMTP_t_DSN_Ret ret,string? envid)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            using var cts = new CancellationTokenSource(this.Timeout);
            
            return MailFromAsync(from,messageSize,ret,envid,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method MailFromAsync

        /// <summary>
        /// Sends the SMTP <c>MAIL FROM</c> command asynchronously using the specified
        /// sender address and an optional <c>SIZE</c> parameter, depending on the
        /// server's advertised capabilities.
        /// </summary>
        /// <param name="from">
        /// The envelope sender address to present to the server.
        /// </param>
        /// <param name="messageSize">
        /// The message size in bytes. A value of <c>-1</c> indicates that no
        /// <c>SIZE</c> parameter should be sent. The <c>SIZE</c> parameter is only
        /// included when the server supports the extension and the value is greater
        /// than zero.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>EhloHeloAsync</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="from"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="from"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>MAIL FROM</c> command failed.
        /// </exception>
        /// <remarks>
        /// This overload sends the <c>MAIL FROM</c> command without any delivery
        /// status notification parameters. It delegates to the full asynchronous
        /// method that supports <c>RET</c> and <c>ENVID</c>, passing default values
        /// for both.
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public ValueTask<SMTP_ServerResponse> MailFromAsync(string from,long messageSize,CancellationToken cancellationToken = default)
        {
            return MailFromAsync(from,messageSize,SMTP_t_DSN_Ret.NotSpecified,null,cancellationToken);
        }

        /// <summary>
        /// Sends the SMTP <c>MAIL FROM</c> command using the specified sender address
        /// and optional ESMTP parameters such as <c>SIZE</c>, <c>RET</c>, and
        /// <c>ENVID</c>, depending on the server's advertised capabilities.
        /// </summary>
        /// <param name="from">
        /// The envelope sender address to present to the server.
        /// </param>
        /// <param name="messageSize">
        /// The message size in bytes. A value of <c>-1</c> indicates that no
        /// <c>SIZE</c> parameter should be sent. The <c>SIZE</c> parameter is only
        /// included when the server supports the extension and the value is greater
        /// than zero.
        /// </param>
        /// <param name="ret">
        /// The delivery status notification return option. Used only when the
        /// server supports the <c>DSN</c> extension.
        /// </param>
        /// <param name="envid">
        /// An optional envelope identifier included in delivery status notifications.
        /// Used only when the server supports the <c>DSN</c> extension.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>EhloHeloAsync</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="from"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="from"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>MAIL FROM</c> command failed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method constructs the <c>MAIL FROM</c> command and conditionally
        /// appends ESMTP parameters based on the server's advertised capabilities.
        /// The <c>SIZE</c> parameter is included only when the server supports the
        /// message size extension and a positive size value is provided.
        /// </para>
        /// <para>
        /// The <c>RET</c> and <c>ENVID</c> parameters are included only when the
        /// server supports delivery status notifications via the <c>DSN</c> extension.
        /// </para>
        /// <para>
        /// On success, the current envelope sender value is stored internally so
        /// subsequent recipient commands can be issued.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> MailFromAsync(string from,long messageSize,SMTP_t_DSN_Ret ret,string? envid,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(m_LocalHostName == null){
                throw new InvalidOperationException("Call EhloHelo or EhloHeloAsync first.");
            }

            /* RFC 5321 4.1.1.2. MAIL
			    mail         = "MAIL FROM:" Reverse-path [SP Mail-parameters] CRLF
                Reverse-path = Path / "<>"
                Path         = "<" [ A-d-l ":" ] Mailbox ">"

			
			   RFC 1870 adds optional SIZE keyword support.
			        SIZE keyword may only be used if it's reported in EHLO command response.
			    Examples:
			        MAIL FROM:<ivx@lumisoft.ee> SIZE=1000
            
               RFC 3461 adds RET and ENVID paramters.
			*/

            // Build command.
            StringBuilder cmd = new StringBuilder();
            cmd.Append("MAIL FROM:<" + from + ">");
            if(SupportsCapability("SIZE") && messageSize > 0){
                cmd.Append(" SIZE=" + messageSize.ToString());
            }
            if(SupportsCapability("DSN") && ret == SMTP_t_DSN_Ret.FullMessage){
                cmd.Append(" RET=FULL");
            }
            else if(SupportsCapability("DSN") && ret == SMTP_t_DSN_Ret.Headers){
                cmd.Append(" RET=HDRS");
            }
            if(SupportsCapability("DSN") && !string.IsNullOrEmpty(envid)){
                cmd.Append(" ENVID=" + envid);
            }
            cmd.Append("\r\n");

            await SendCommandLineAsync(cmd.ToString(),true,cancellationToken);

            var smtpResponse = await ReadResponseAsync(cancellationToken);            
            if(smtpResponse.ReplyCode == 250){
                m_MailFrom = from;
            }
            // MAIL FROM failed.
            else{
                throw new SMTP_ClientException(smtpResponse);
            }

            return smtpResponse;
        }

        #endregion

        #region method RcptTo

        /// <summary>
        /// Synchronously sends the SMTP <c>RCPT TO</c> command using the specified
        /// recipient address without any delivery status notification parameters.
        /// </summary>
        /// <param name="to">
        /// The envelope recipient address to present to the server.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>MailFrom</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="to"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="to"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>RCPT TO</c> command failed.
        /// </exception>
        /// <remarks>
        /// This overload sends the <c>RCPT TO</c> command without any delivery
        /// status notification options. It delegates to the full method that
        /// supports <c>NOTIFY</c> and <c>ORCPT</c>, passing default values for both.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse RcptTo(string to)
        {
            return RcptTo(to,SMTP_t_DSN_Notify.NotSpecified,null);
        }

        /// <summary>
        /// Synchronously sends the SMTP <c>RCPT TO</c> command using the specified
        /// recipient address and optional delivery status notification parameters
        /// such as <c>NOTIFY</c> and <c>ORCPT</c>, depending on the server's
        /// advertised capabilities.
        /// </summary>
        /// <param name="to">
        /// The envelope recipient address to present to the server.
        /// </param>
        /// <param name="notify">
        /// The delivery status notification options for the recipient. Used only
        /// when the server supports the <c>DSN</c> extension.
        /// </param>
        /// <param name="orcpt">
        /// An optional original recipient value included in delivery status
        /// notifications. Used only when the server supports the <c>DSN</c>
        /// extension.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>MailFrom</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="to"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="to"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>RCPT TO</c> command failed.
        /// </exception>
        /// <remarks>
        /// This method blocks the calling thread until the <c>RCPT TO</c> operation
        /// completes. The asynchronous implementation constructs the command and
        /// conditionally appends ESMTP parameters based on the server's advertised
        /// capabilities. On success, the recipient address is added to the internal
        /// recipient collection.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse RcptTo(string to,SMTP_t_DSN_Notify notify,string? orcpt)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            using var cts = new CancellationTokenSource(this.Timeout);
            
            return RcptToAsync(to,notify,orcpt,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method RcptToAsync

        /// <summary>
        /// Sends the SMTP <c>RCPT TO</c> command asynchronously using the specified
        /// recipient address without any delivery status notification parameters.
        /// </summary>
        /// <param name="to">
        /// The envelope recipient address to present to the server.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>MailFromAsync</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="to"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="to"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>RCPT TO</c> command failed.
        /// </exception>
        /// <remarks>
        /// This overload sends the <c>RCPT TO</c> command without any delivery
        /// status notification options. It delegates to the full asynchronous
        /// method that supports <c>NOTIFY</c> and <c>ORCPT</c>, passing default
        /// values for both.
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public ValueTask<SMTP_ServerResponse> RcptToAsync(string to,CancellationToken cancellationToken = default)
        {
            return RcptToAsync(to,SMTP_t_DSN_Notify.NotSpecified,null,cancellationToken);
        }

        /// <summary>
        /// Sends the SMTP <c>RCPT TO</c> command using the specified recipient
        /// address and optional delivery status notification parameters such as
        /// <c>NOTIFY</c> and <c>ORCPT</c>, depending on the server's advertised
        /// capabilities.
        /// </summary>
        /// <param name="to">
        /// The envelope recipient address to present to the server.
        /// </param>
        /// <param name="notify">
        /// The delivery status notification options for the recipient. Used only
        /// when the server supports the <c>DSN</c> extension.
        /// </param>
        /// <param name="orcpt">
        /// An optional original recipient value included in delivery status
        /// notifications. Used only when the server supports the <c>DSN</c>
        /// extension.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected or if <c>MailFromAsync</c> has not
        /// been issued before calling <c>RcptToAsync</c>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="to"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="to"/> is an empty string.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>RCPT TO</c> command failed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method constructs the <c>RCPT TO</c> command and conditionally
        /// appends ESMTP parameters based on the server's advertised capabilities.
        /// The <c>NOTIFY</c> and <c>ORCPT</c> parameters are included only when the
        /// server supports delivery status notifications via the <c>DSN</c>
        /// extension.
        /// </para>
        /// <para>
        /// On success, the recipient address is added to the internal recipient
        /// collection so subsequent message delivery commands can be issued.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> RcptToAsync(string to,SMTP_t_DSN_Notify notify,string? orcpt,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(m_MailFrom == null){
                throw new InvalidOperationException("Call MailFrom or MailFromAsync first.");
            }
            if(string.IsNullOrEmpty(to)){
                throw new ArgumentException("Argument 'to' cannot be empty.",nameof(to));
            }

            /* RFC 5321 4.1.1.3. RCPT.
                rcpt = "RCPT TO:" ( "<Postmaster@" Domain ">" / "<Postmaster>" / Forward-path ) [SP Rcpt-parameters] CRLF

			    Examples:
			        RCPT TO:<ivar@lumisoft.ee>
            
                RFC 3461 adds NOTIFY and ORCPT parameters.
			*/

            StringBuilder cmd = new StringBuilder();
            cmd.Append("RCPT TO:<" + to + ">");
            if(SupportsCapability("DSN") && notify != SMTP_t_DSN_Notify.NotSpecified){
                cmd.Append(" NOTIFY=");
                bool first = true;
                if(notify == SMTP_t_DSN_Notify.Never){
                    cmd.Append("NEVER");
                }
                else if((notify & SMTP_t_DSN_Notify.Delay) != 0){
                    cmd.Append("DELAY");
                    first = false;
                }
                else if((notify & SMTP_t_DSN_Notify.Failure) != 0){
                    cmd.Append(first ? "FAILURE" : ",FAILURE");
                    first = false;
                }
                else if((notify & SMTP_t_DSN_Notify.Success) != 0){
                    cmd.Append(first ? "SUCCESS" : ",SUCCESS");
                }

                if(!string.IsNullOrEmpty(orcpt)){
                    cmd.Append(" ORCPT=" + orcpt);
                }
            }            
            cmd.Append("\r\n");

            await SendCommandLineAsync(cmd.ToString(),true,cancellationToken);

            var smtpResponse = await ReadResponseAsync(cancellationToken);            
            if(smtpResponse.ReplyCode == 250){
                if(!m_pRecipients!.Contains(to)){
                    m_pRecipients.Add(to);
                }
            }
            // RCPT TO failed.
            else{
                throw new SMTP_ClientException(smtpResponse);
            }

            return smtpResponse;
        }

        #endregion

        #region method SendMessage

        /// <summary>
        /// Sends the specified <see cref="Mail_Message"/> to the SMTP server using
        /// either the <c>DATA</c> command or the <c>BDAT</c> command when chunking
        /// is supported.
        /// </summary>
        /// <param name="message">
        /// The message object to serialize and send.
        /// </param>
        /// <param name="useBdatIfPossibe">
        /// If <c>true</c>, attempts to use <c>BDAT</c> when the server advertises
        /// the <c>CHUNKING</c> capability; otherwise falls back to <c>DATA</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="message"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>RcptTo</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the message size exceeds the server's allowed maximum.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply
        /// after sending the message.
        /// </exception>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse SendMessage(Mail_Message message,bool useBdatIfPossibe)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return SendMessageAsync(message,useBdatIfPossibe,cts.Token).GetAwaiter().GetResult();           
        }

        /// <summary>
        /// Sends a message to the SMTP server using the <c>DATA</c> command.
        /// The message is sent starting from the stream's current position.
        /// </summary>
        /// <param name="stream">
        /// The stream containing the message to send.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>RcptTo</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the message size exceeds the server's allowed maximum and
        /// the size can be determined from the stream.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply
        /// after sending the message.
        /// </exception>
        /// <remarks>
        /// This method always uses the <c>DATA</c> command.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse SendMessage(Stream stream)
        {
            return SendMessage(stream,false);
        }

        /// <summary>
        /// Sends a message to the SMTP server using either the <c>DATA</c> command
        /// or the <c>BDAT</c> command when chunking is supported. The message is
        /// sent starting from the stream's current position. This method blocks
        /// until the send operation completes.
        /// </summary>
        /// <param name="stream">
        /// The stream containing the message to send. When BDAT is used, the stream
        /// must be seekable so that the remaining length can be determined.
        /// </param>
        /// <param name="useBdatIfPossibe">
        /// If <c>true</c>, attempts to use <c>BDAT</c> when the server advertises
        /// the <c>CHUNKING</c> capability and the stream is seekable; otherwise
        /// falls back to <c>DATA</c>.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>RcptTo</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the message size exceeds the server's allowed maximum and
        /// the size can be determined from the stream.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply
        /// after sending the message.
        /// </exception>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse SendMessage(Stream stream,bool useBdatIfPossibe)
        {
            using var cts = new CancellationTokenSource(this.Timeout);

            return SendMessageAsync(stream,useBdatIfPossibe,cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method SendMessageAsync

        /// <summary>
        /// Serializes the specified <see cref="Mail_Message"/> into a stream and
        /// sends it to the SMTP server using either the <c>DATA</c> command or the
        /// <c>BDAT</c> command when chunking is supported.
        /// </summary>
        /// <param name="message">
        /// The message object to serialize and send.
        /// </param>
        /// <param name="useBdatIfPossibe">
        /// If <c>true</c>, attempts to use <c>BDAT</c> when the server advertises
        /// the <c>CHUNKING</c> capability; otherwise falls back to <c>DATA</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="message"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>RcptToAsync</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the message size exceeds the server's allowed maximum.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply
        /// after sending the message.
        /// </exception>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> SendMessageAsync(Mail_Message message,bool useBdatIfPossibe,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            using(var stream = new MemoryStreamEx(1000000)){
                message.ToStream(stream);
                stream.Position = 0;

                return await SendMessageAsync(stream,useBdatIfPossibe,cancellationToken);
            }
        }

        /// <summary>
        /// Sends a message to the SMTP server using either the <c>DATA</c> command
        /// or the <c>BDAT</c> command when chunking is supported. The message is
        /// sent starting from the stream's current position.
        /// </summary>
        /// <param name="stream">
        /// The stream containing the message to send. When BDAT is used, the stream
        /// must be seekable so that the remaining length can be determined.
        /// </param>
        /// <param name="useBdatIfPossibe">
        /// If <c>true</c>, attempts to use <c>BDAT</c> when the server advertises
        /// the <c>CHUNKING</c> capability and the stream is seekable; otherwise
        /// falls back to <c>DATA</c>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <c>RcptToAsync</c> has not been issued before calling this method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the message size exceeds the server's allowed maximum and
        /// the size can be determined from the stream.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply
        /// after sending the message.
        /// </exception>
        /// <remarks>
        /// When BDAT is used, the method sends a single chunk containing the bytes
        /// remaining in the stream and marks it as the last chunk. When DATA is
        /// used, the message is sent using period-terminated transfer as defined
        /// in RFC 5321.
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> SendMessageAsync(Stream stream,bool useBdatIfPossibe,CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }
            if(m_pRecipients!.Count == 0){
                throw new InvalidOperationException("Call RcptTo or RcptToAsync first.");
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }            
            if(m_MaxMessageSize > 0 && stream.CanSeek && (stream.Length - stream.Position) > m_MaxMessageSize){
                throw new DataSizeExceededException("Message size exceeds server allowed maximum.");
            }

            /* RFC 3030 2.
                bdat-cmd   ::= "BDAT" SP chunk-size [ SP end-marker ] CR LF
                chunk-size ::= 1*DIGIT
                end-marker ::= "LAST"

                Example:
                    C: BDAT <message-size> LAST<CRLF>
                    C: <message bytes>
                    S: 250 Message accepted<CRLF>

            */

            /* RFC 5321 4.1.1.4.
                The mail data are terminated by a line containing only a period, that
                is, the character sequence "<CRLF>.<CRLF>", where the first <CRLF> is
                actually the terminator of the previous line.
              
                Example:
			        C: DATA<CRLF>
			        S: 354 Start sending message, end with <crlf>.<crlf>.<CRLF>
			        C: send_message
			        C: .<CRLF>
                    S: 250 Ok<CRLF>
            */

            // Use BDAT command.
            if(useBdatIfPossibe && stream.CanSeek && SupportsCapability("CHUNKING")){
                long messageSize = stream.Length - stream.Position;

                // Send BDAT command and message.
                await SendCommandLineAsync("BDAT " + messageSize + " LAST\r\n",true,cancellationToken);
                await this.TcpStream!.WriteStreamAsync(stream,messageSize,int.MaxValue,CancellationToken.None);
                LogAddWrite(messageSize,"Wrote " + messageSize + " bytes.");

                var smtpResponse = await ReadResponseAsync(cancellationToken);
                // BDAT failed.
                if(!(smtpResponse.ReplyCode == 250)){
                    throw new SMTP_ClientException(smtpResponse);
                }

                return smtpResponse;
            }
            // Use DATA command.
            else{
                await SendCommandLineAsync("DATA\r\n",true,cancellationToken);

                var smtpResponse = await ReadResponseAsync(cancellationToken);
                // DATA failed.
                if(!(smtpResponse.ReplyCode == 354)){
                    throw new SMTP_ClientException(smtpResponse);
                }

                long count =await this.TcpStream!.WritePeriodTerminatedAsync(stream,int.MaxValue,32000,SizeExceededAction.JunkAndThrowException,cancellationToken);
                LogAddWrite(count,"Wrote " + count + " bytes.");

                smtpResponse = await ReadResponseAsync(cancellationToken);
                // DATA failed.
                if(!(smtpResponse.ReplyCode == 250)){
                    throw new SMTP_ClientException(smtpResponse);
                }

                return smtpResponse;
            }
        }

        #endregion

        #region method Rset

        /// <summary>
        /// Synchronously sends the SMTP <c>RSET</c> command to abort the current
        /// mail transaction and reset the server-side state without closing the
        /// connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>RSET</c> command failed.
        /// </exception>
        /// <remarks>
        /// This method blocks the calling thread until the reset operation completes.
        /// When the server accepts <c>RSET</c>, the asynchronous implementation clears
        /// the current <c>MAIL FROM</c> value and removes all stored recipients so a new
        /// mail transaction can begin cleanly.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse Rset()
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            using var cts = new CancellationTokenSource(this.Timeout);

            return RsetAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method RsetAsync

        /// <summary>
        /// Sends the SMTP <c>RSET</c> command to abort the current mail transaction
        /// and reset the server-side state without closing the connection.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server does not return a successful <c>250</c> reply,
        /// indicating that the <c>RSET</c> command failed.
        /// </exception>
        /// <remarks>
        /// When the server accepts <c>RSET</c>, this method clears the current
        /// <c>MAIL FROM</c> value and removes all stored recipients so a new
        /// mail transaction can begin cleanly.
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> RsetAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            /* RFC 5321 4.1.1.9.
                rset      = "RSET" CRLF
                rset-resp = "250 OK" CRLF
            */

            await SendCommandLineAsync("RSET\r\n",true,cancellationToken);

            var smtpResponse = await ReadResponseAsync(cancellationToken);            
            if(smtpResponse.ReplyCode == 250){
                m_MailFrom = null;
                m_pRecipients?.Clear();
            }
            // RSET failed.
            else{
                throw new SMTP_ClientException(smtpResponse);
            }

            return smtpResponse;
        }

        #endregion
        
        #region method Noop

        /// <summary>
        /// Synchronously sends the SMTP <c>NOOP</c> command to verify that the server
        /// is responsive and to keep the connection alive.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server returns a reply code other than <c>250</c>,
        /// indicating that the <c>NOOP</c> command failed.
        /// </exception>
        /// <remarks>
        /// The <c>NOOP</c> command is commonly used as a lightweight keep‑alive
        /// operation to prevent idle SMTP connections from timing out.
        /// </remarks>
        /// <returns>
        /// The SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public SMTP_ServerResponse Noop()
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            using var cts = new CancellationTokenSource(this.Timeout);

            return NoopAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region method NoopAsync

        /// <summary>
        /// Sends the SMTP <c>NOOP</c> command to verify that the server is responsive
        /// and to keep the connection alive.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client instance has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when the server returns a reply code other than <c>250</c>,
        /// indicating that the <c>NOOP</c> command failed.
        /// </exception>
        /// <remarks>
        /// The <c>NOOP</c> command is commonly used as a lightweight keep‑alive
        /// operation to prevent idle SMTP connections from timing out.
        /// </remarks>
        /// <returns>
        /// A task that completes with the SMTP reply produced by the remote endpoint.  
        /// The returned <see cref="SMTP_ServerResponse"/> contains all reply lines and
        /// the final reply code that represents the outcome of the operation.
        /// </returns>
        public async ValueTask<SMTP_ServerResponse> NoopAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(!this.IsConnected){
                throw new InvalidOperationException("You must connect first.");
            }

            /* RFC 5321 4.1.1.9.
                noop      = "NOOP" [ SP String ] CRLF
                noop-resp = "250 OK" CRLF
            */

            await SendCommandLineAsync("NOOP\r\n",true,cancellationToken);

            var smtpResponse = await ReadResponseAsync(cancellationToken);
            // NOOP failed.
            if(!(smtpResponse.ReplyCode == 250)){
                throw new SMTP_ClientException(smtpResponse);
            }

            return smtpResponse;
        }

        #endregion


        #region method SendCommandLineAsync
                
        /// <summary>
        /// Sends a single SMTP command line to the server.
        /// </summary>
        /// <param name="cmdLine">
        /// The SMTP command line to send.  
        /// The value must already be terminated with a CRLF sequence (<c>\r\n</c>).
        /// </param>
        /// <param name="log">Specifies whether the command is logged.</param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous write operation.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="cmdLine"/> or the underlying TCP stream is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="cmdLine"/> does not end with the required CRLF terminator.
        /// </exception>
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
        /// Reads an SMTP server reply consisting of one or more reply lines.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel the asynchronous read operation.
        /// </param>
        /// <returns>
        /// An array of <see cref="SMTP_t_ReplyLine"/> objects representing the complete
        /// multiline SMTP reply returned by the server.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the underlying TCP stream is null.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when the SMTP server closes the connection before a complete reply is received.
        /// </exception>
        /// <exception cref="DataSizeExceededException">
        /// Thrown when the server returns more than the allowed maximum number of multiline
        /// reply lines (100), indicating an excessively large or malformed reply.
        /// </exception>
        /// <remarks>
        /// This method reads SMTP reply lines until the final line is encountered, as indicated
        /// by the reply-line continuation marker defined in RFC 5321.  
        /// Each line is parsed into an <see cref="SMTP_t_ReplyLine"/> instance.  
        /// If the reply exceeds the configured safety limit of 100 lines, the operation is aborted
        /// and a <see cref="DataSizeExceededException"/> is thrown.
        /// </remarks>
        private async ValueTask<SMTP_ServerResponse> ReadResponseAsync(CancellationToken cancellationToken = default)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            ArgumentNullException.ThrowIfNull(this.TcpStream);

            List<SMTP_t_ReplyLine> replyLines = new List<SMTP_t_ReplyLine>();
            Memory<byte>           lineBuffer = new byte[8000];
            while(true){
                ReadLineResult responseline = await this.TcpStream.ReadLineAsync(lineBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
                // Server closed connection.
                if(responseline.BytesInBuffer == 0){
                    throw new IOException("SMTP server closed connection.");
                }
                else{
                    string line = responseline.LineUtf8 ?? "";

                    LogAddRead(responseline.BytesInBuffer,line);

                    SMTP_t_ReplyLine replyLine = SMTP_t_ReplyLine.Parse(line);
                    replyLines.Add(replyLine);                    

                    if(replyLine.IsLastLine){
                        break;
                    }
                    if(replyLines.Count > 100){
                        throw new DataSizeExceededException("SMTP server reached the 100 multiline reply‑line limit.");
                    }
                }
            } 

            return new SMTP_ServerResponse(replyLines.ToArray());
        }

        #endregion

        #region method SupportsCapability

        /// <summary>
        /// Gets if SMTP server supports the specified capability.
        /// </summary>
        /// <param name="capability">SMTP capability.</param>
        /// <returns>Return true if SMTP server supports the specified capability.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>capability</b> is null reference.</exception>
        private bool SupportsCapability(string capability)
        {
            if(capability == null){
                throw new ArgumentNullException("capability");
            }

            if(m_pEsmtpFeatures == null){
                return false;
            }
            else{
                foreach(string c in m_pEsmtpFeatures){
                    if(string.Equals(c.Split(' ')[0],capability,StringComparison.OrdinalIgnoreCase)){
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

         
        #region static method QuickSend

        /// <summary>
        /// Sends an SMTP message to a remote server using a fully automated sequence that
        /// performs connection establishment, optional TLS negotiation, optional
        /// authentication, and message transmission. This method provides
        /// a high‑level convenience wrapper around the <see cref="SMTP_Client"/> class,
        /// enabling one‑shot message delivery without manually issuing SMTP commands.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying TCP socket. When specified,
        /// the connection attempt is restricted to remote addresses matching the same
        /// <see cref="AddressFamily"/>.
        /// </param>
        /// <param name="localHost">
        /// Optional host name to present in the <c>EHLO</c>/<c>HELO</c> command.  
        /// When <c>null</c>, the machine's local host name is used.
        /// </param>
        /// <param name="host">
        /// The remote SMTP server host name or IP address. Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="port">
        /// The remote SMTP port number. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// Specifies which IP protocol to prefer when connecting (IPv4, IPv6, or Unspecified).
        /// 
        /// When <see cref="AddressFamily.Unspecified"/> is used, the client automatically
        /// chooses the appropriate protocol based on the server’s available addresses.
        /// </param>
        /// <param name="security">
        /// Defines how the connection should be secured.  
        /// <see cref="TcpClientSecurity.SSL"/> uses immediate SSL when connecting;  
        /// <see cref="TcpClientSecurity.TLS"/> upgrades the connection with STARTTLS;  
        /// <see cref="TcpClientSecurity.UseTlsIfSupported"/> enables TLS only if the server supports it.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used SSL or STARTTLS.  
        /// When <c>null</c>, the SMTP client applies a permissive legacy‑compatible
        /// configuration suitable for older servers.
        /// </param>
        /// <param name="userName">
        /// Optional user name for SMTP authentication. When provided together with
        /// <paramref name="password"/>, the strongest mutually supported SASL mechanism is
        /// selected automatically.
        /// </param>
        /// <param name="password">
        /// Optional password used for SMTP authentication.
        /// </param>
        /// <param name="from">
        /// The SMTP envelope sender address used in the <c>MAIL FROM</c> command.
        /// </param>
        /// <param name="to">
        /// The SMTP envelope recipient list used in repeated <c>RCPT TO</c> commands.
        /// Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="message">The mail message to send.
        /// </param>
        /// <param name="logger">
        /// Optional logger instance used to record protocol activity.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="host"/> is empty or when no recipients are specified.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="port"/> is outside the valid range 1–65535.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="to"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when any SMTP command (EHLO/HELO, STARTTLS, AUTH, MAIL FROM, RCPT TO,
        /// or DATA/BDAT) receives a non‑successful reply from the server.
        /// </exception>
        public static ValueTask QuickSend(
            IPEndPoint? localEP,
            string? localHost,
            string host,
            int port,
            AddressFamily addressFamily,
            TcpClientSecurity security,
            SslClientAuthenticationOptions? sslOptions,
            string? userName,
            string? password,
            string from,
            string[] to,
            Mail_Message message,
            Logger? logger)
        {
            return QuickSendAsync(localEP,localHost,host,port,addressFamily,security,sslOptions,userName,password,from,to,message,logger);
        }

        /// <summary>
        /// Sends an SMTP message to a remote server using a fully automated sequence that
        /// performs connection establishment, optional TLS negotiation, optional
        /// authentication, and message transmission. This method provides
        /// a high‑level convenience wrapper around the <see cref="SMTP_Client"/> class,
        /// enabling one‑shot message delivery without manually issuing SMTP commands.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying TCP socket. When specified,
        /// the connection attempt is restricted to remote addresses matching the same
        /// <see cref="AddressFamily"/>.
        /// </param>
        /// <param name="localHost">
        /// Optional host name to present in the <c>EHLO</c>/<c>HELO</c> command.  
        /// When <c>null</c>, the machine's local host name is used.
        /// </param>
        /// <param name="host">
        /// The remote SMTP server host name or IP address. Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="port">
        /// The remote SMTP port number. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// Specifies which IP protocol to prefer when connecting (IPv4, IPv6, or Unspecified).
        /// 
        /// When <see cref="AddressFamily.Unspecified"/> is used, the client automatically
        /// chooses the appropriate protocol based on the server’s available addresses.
        /// </param>
        /// <param name="security">
        /// Defines how the connection should be secured.  
        /// <see cref="TcpClientSecurity.SSL"/> uses immediate SSL when connecting;  
        /// <see cref="TcpClientSecurity.TLS"/> upgrades the connection with STARTTLS;  
        /// <see cref="TcpClientSecurity.UseTlsIfSupported"/> enables TLS only if the server supports it.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used SSL or STARTTLS.  
        /// When <c>null</c>, the SMTP client applies a permissive legacy‑compatible
        /// configuration suitable for older servers.
        /// </param>
        /// <param name="userName">
        /// Optional user name for SMTP authentication. When provided together with
        /// <paramref name="password"/>, the strongest mutually supported SASL mechanism is
        /// selected automatically.
        /// </param>
        /// <param name="password">
        /// Optional password used for SMTP authentication.
        /// </param>
        /// <param name="from">
        /// The SMTP envelope sender address used in the <c>MAIL FROM</c> command.
        /// </param>
        /// <param name="to">
        /// The SMTP envelope recipient list used in repeated <c>RCPT TO</c> commands.
        /// Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="message">
        /// The stream containing the message to send.  
        /// The message is sent starting from the stream’s current position.
        /// </param>
        /// <param name="logger">
        /// Optional logger instance used to record protocol activity.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="host"/> is empty or when no recipients are specified.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="port"/> is outside the valid range 1–65535.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="to"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when any SMTP command (EHLO/HELO, STARTTLS, AUTH, MAIL FROM, RCPT TO,
        /// or DATA/BDAT) receives a non‑successful reply from the server.
        /// </exception>
        public static ValueTask QuickSend(
            IPEndPoint? localEP,
            string? localHost,
            string host,
            int port,
            AddressFamily addressFamily,
            TcpClientSecurity security,
            SslClientAuthenticationOptions? sslOptions,
            string? userName,
            string? password,
            string from,
            string[] to,
            Stream message,
            Logger? logger)
        {
            return QuickSendAsync(localEP,localHost,host,port,addressFamily,security,sslOptions,userName,password,from,to,message,logger);
        }

        #endregion

        #region static method QuickSend

        /// <summary>
        /// Sends an SMTP message to a remote server using a fully automated sequence that
        /// performs connection establishment, optional TLS negotiation, optional
        /// authentication, and message transmission. This method provides
        /// a high‑level convenience wrapper around the <see cref="SMTP_Client"/> class,
        /// enabling one‑shot message delivery without manually issuing SMTP commands.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying TCP socket. When specified,
        /// the connection attempt is restricted to remote addresses matching the same
        /// <see cref="AddressFamily"/>.
        /// </param>
        /// <param name="localHost">
        /// Optional host name to present in the <c>EHLO</c>/<c>HELO</c> command.  
        /// When <c>null</c>, the machine's local host name is used.
        /// </param>
        /// <param name="host">
        /// The remote SMTP server host name or IP address. Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="port">
        /// The remote SMTP port number. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// Specifies which IP protocol to prefer when connecting (IPv4, IPv6, or Unspecified).
        /// 
        /// When <see cref="AddressFamily.Unspecified"/> is used, the client automatically
        /// chooses the appropriate protocol based on the server’s available addresses.
        /// </param>
        /// <param name="security">
        /// Defines how the connection should be secured.  
        /// <see cref="TcpClientSecurity.SSL"/> uses immediate SSL when connecting;  
        /// <see cref="TcpClientSecurity.TLS"/> upgrades the connection with STARTTLS;  
        /// <see cref="TcpClientSecurity.UseTlsIfSupported"/> enables TLS only if the server supports it.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used SSL or STARTTLS.  
        /// When <c>null</c>, the SMTP client applies a permissive legacy‑compatible
        /// configuration suitable for older servers.
        /// </param>
        /// <param name="userName">
        /// Optional user name for SMTP authentication. When provided together with
        /// <paramref name="password"/>, the strongest mutually supported SASL mechanism is
        /// selected automatically.
        /// </param>
        /// <param name="password">
        /// Optional password used for SMTP authentication.
        /// </param>
        /// <param name="from">
        /// The SMTP envelope sender address used in the <c>MAIL FROM</c> command.
        /// </param>
        /// <param name="to">
        /// The SMTP envelope recipient list used in repeated <c>RCPT TO</c> commands.
        /// Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="message">The mail message to send.
        /// </param>
        /// <param name="logger">
        /// Optional logger instance used to record protocol activity.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel any asynchronous network or I/O operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous send operation.  
        /// The task completes when the message has been fully transmitted and the SMTP
        /// session has been terminated.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="host"/> is empty or when no recipients are specified.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="port"/> is outside the valid range 1–65535.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="to"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when any SMTP command (EHLO/HELO, STARTTLS, AUTH, MAIL FROM, RCPT TO,
        /// or DATA/BDAT) receives a non‑successful reply from the server.
        /// </exception>
        public async static ValueTask QuickSendAsync(
            IPEndPoint? localEP,
            string? localHost,
            string host,
            int port,
            AddressFamily addressFamily,
            TcpClientSecurity security,
            SslClientAuthenticationOptions? sslOptions,
            string? userName,
            string? password,
            string from,
            string[] to,
            Mail_Message message,
            Logger? logger,
            CancellationToken cancellationToken = default)
        {
            if(message == null){
                throw new ArgumentNullException(nameof(message));
            }

            using(var stream = new MemoryStreamEx(1000000)){
                message.ToStream(stream);
                stream.Position = 0;

                await QuickSendAsync(
                    localEP,
                    localHost,
                    host,
                    port,
                    addressFamily,
                    security,
                    sslOptions,
                    userName,
                    password,
                    from,
                    to,
                    stream,
                    logger,
                    cancellationToken
                );
            }
        }

        /// <summary>
        /// Sends an SMTP message to a remote server using a fully automated sequence that
        /// performs connection establishment, optional TLS negotiation, optional
        /// authentication, and message transmission. This method provides
        /// a high‑level convenience wrapper around the <see cref="SMTP_Client"/> class,
        /// enabling one‑shot message delivery without manually issuing SMTP commands.
        /// </summary>
        /// <param name="localEP">
        /// Optional local endpoint to bind the underlying TCP socket. When specified,
        /// the connection attempt is restricted to remote addresses matching the same
        /// <see cref="AddressFamily"/>.
        /// </param>
        /// <param name="localHost">
        /// Optional host name to present in the <c>EHLO</c>/<c>HELO</c> command.  
        /// When <c>null</c>, the machine's local host name is used.
        /// </param>
        /// <param name="host">
        /// The remote SMTP server host name or IP address. Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="port">
        /// The remote SMTP port number. Must be between 1 and 65535.
        /// </param>
        /// <param name="addressFamily">
        /// Specifies which IP protocol to prefer when connecting (IPv4, IPv6, or Unspecified).
        /// 
        /// When <see cref="AddressFamily.Unspecified"/> is used, the client automatically
        /// chooses the appropriate protocol based on the server’s available addresses.
        /// </param>
        /// <param name="security">
        /// Defines how the connection should be secured.  
        /// <see cref="TcpClientSecurity.SSL"/> uses immediate SSL when connecting;  
        /// <see cref="TcpClientSecurity.TLS"/> upgrades the connection with STARTTLS;  
        /// <see cref="TcpClientSecurity.UseTlsIfSupported"/> enables TLS only if the server supports it.
        /// </param>
        /// <param name="sslOptions">
        /// Optional TLS configuration used SSL or STARTTLS.  
        /// When <c>null</c>, the SMTP client applies a permissive legacy‑compatible
        /// configuration suitable for older servers.
        /// </param>
        /// <param name="userName">
        /// Optional user name for SMTP authentication. When provided together with
        /// <paramref name="password"/>, the strongest mutually supported SASL mechanism is
        /// selected automatically.
        /// </param>
        /// <param name="password">
        /// Optional password used for SMTP authentication.
        /// </param>
        /// <param name="from">
        /// The SMTP envelope sender address used in the <c>MAIL FROM</c> command.
        /// </param>
        /// <param name="to">
        /// The SMTP envelope recipient list used in repeated <c>RCPT TO</c> commands.
        /// Cannot be <c>null</c> or empty.
        /// </param>
        /// <param name="message">
        /// The stream containing the message to send.  
        /// The message is sent starting from the stream’s current position.
        /// </param>
        /// <param name="logger">
        /// Optional logger instance used to record protocol activity.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that may be used to cancel any asynchronous network or I/O operation.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous send operation.  
        /// The task completes when the message has been fully transmitted and the SMTP
        /// session has been terminated.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="host"/> is empty or when no recipients are specified.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="port"/> is outside the valid range 1–65535.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="to"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="SMTP_ClientException">
        /// Thrown when any SMTP command (EHLO/HELO, STARTTLS, AUTH, MAIL FROM, RCPT TO,
        /// or DATA/BDAT) receives a non‑successful reply from the server.
        /// </exception>
        public async static ValueTask QuickSendAsync(
            IPEndPoint? localEP,
            string? localHost,
            string host,
            int port,
            AddressFamily addressFamily,
            TcpClientSecurity security,
            SslClientAuthenticationOptions? sslOptions,
            string? userName,
            string? password,
            string from,
            string[] to,
            Stream message,
            Logger? logger,
            CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrEmpty(host)){
                throw new ArgumentException("Argument 'host' cannot be null or empty.",nameof(host));
            }
            if(port <= 0 || port > 65535){
                throw new ArgumentOutOfRangeException(nameof(port),"Port must be between 1 and 65535.");
            }
            if(to == null){
                throw new ArgumentNullException("Argument 'to' cannot be null or empty.",nameof(to));
            }
            if(to.Length == 0){
                throw new ArgumentException("No recipients specified.",nameof(to));
            }
            if(message == null){
                throw new ArgumentNullException("Argument 'message' cannot be null or empty.",nameof(message));
            }

            long messageSize = message.CanSeek ? (message.Length - message.Position) : -1;

            using(SMTP_Client smtp = new SMTP_Client()){
                smtp.Logger = logger;
                await smtp.ConnectAsync(localEP,host,port,addressFamily,security == TcpClientSecurity.SSL,sslOptions,cancellationToken);
                await smtp.EhloHeloAsync(localHost != null ? localHost : Dns.GetHostName(),cancellationToken);
                if(security == TcpClientSecurity.TLS || (security == TcpClientSecurity.UseTlsIfSupported && smtp.SupportsCapability(SMTP_ServiceExtensions.STARTTLS))){
                    await smtp.StartTlsAsync(sslOptions,cancellationToken);
                    await smtp.EhloHeloAsync(localHost != null ? localHost : Dns.GetHostName(),cancellationToken);
                }
                if(!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password)){
                    await smtp.AuthAsync(smtp.AuthGetStrongestMethod(userName,password),cancellationToken);
                }
                await smtp.MailFromAsync(from,messageSize,cancellationToken);
                foreach(string t in to){
                    await smtp.RcptToAsync(t,cancellationToken);
                }
                await smtp.SendMessageAsync(message,true,cancellationToken);
            }
        }

        #endregion

        #region Properties Implementation

        /// <summary>
        /// Gets the host name that was presented to the SMTP server during the EHLO/HELO command.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        public string? LocalHostName
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }
                
                return m_LocalHostName; 
            }
        }

        /// <summary>
        /// Gets the SMTP server host name reported in the EHLO/HELO response.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        public string? RemoteHostName
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }

                return m_RemoteHostName; 
            }
        }

        /// <summary>
        /// Gets the greeting text sent by the SMTP server during connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
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
        /// Gets a value indicating whether the connected SMTP server supports ESMTP.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        public bool IsEsmtpSupported
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }

                return m_IsEsmtpSupported; 
            }
        }

        /// <summary>
        /// Gets the ESMTP extension lines advertised by the connected SMTP server.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        public string[] EsmtpFeatures
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }

                return m_pEsmtpFeatures.ToArray(); 
            }
        }

        /// <summary>
        /// Gets the SASL authentication mechanisms advertised by the connected SMTP server.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
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
                foreach(string feature in this.EsmtpFeatures){
                    string featureName = feature.Split(' ')[0];
                    if(string.Equals(featureName,SMTP_ServiceExtensions.AUTH,StringComparison.InvariantCultureIgnoreCase)){
                        // Remove AUTH<SP> and split authentication methods.
                        return feature.Substring(4).Trim().Split(' ');
                    }
                }

                return new string[0];
            }
        }

        /// <summary>
        /// Gets the maximum message size, in bytes, advertised by the SMTP server.
        /// A value of <c>-1</c> indicates that no size limit was reported.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
        /// </exception>
        public long MaxAllowedMessageSize
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(!this.IsConnected){
                    throw new InvalidOperationException("You must connect first.");
                }

                return m_MaxMessageSize; 
            }
        }

        /// <summary>
        /// Gets the authenticated user identity for the current SMTP session, or <c>null</c>
        /// if no authentication has been performed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the client has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the client is not connected.
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

        #endregion


        //------- OBSOLETE  


        #region static method QuickSend

        /// <summary>
        /// Sends specified mime message.
        /// </summary>
        /// <param name="message">Message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>message</b> is null.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSend(Mail_Message message)
        {
            if(message == null){
                throw new ArgumentNullException("message");
            }

            string from = "";
            if(message.From != null && message.From.Count > 0){
                from = ((Mail_t_Mailbox)message.From[0]).Address;
            }

            List<string> recipients = new List<string>();
            if(message.To != null){
				Mail_t_Mailbox[] addresses = message.To.Mailboxes;	
				foreach(Mail_t_Mailbox address in addresses){
					recipients.Add(address.Address);
				}
			}
			if(message.Cc != null){
				Mail_t_Mailbox[] addresses = message.Cc.Mailboxes;				
				foreach(Mail_t_Mailbox address in addresses){
					recipients.Add(address.Address);
				}
			}
			if(message.Bcc != null){
				Mail_t_Mailbox[] addresses = message.Bcc.Mailboxes;				
				foreach(Mail_t_Mailbox address in addresses){
					recipients.Add(address.Address);
				}

                // We must hide BCC
                message.Bcc.Clear();
			}

            foreach(string recipient in recipients){
                MemoryStream ms = new MemoryStream();
                message.ToStream(ms,new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.Q,Encoding.UTF8),Encoding.UTF8);
                ms.Position = 0;
                QuickSend(null,from,recipient,ms);
            }
        }

        /// <summary>
        /// Sends message directly to email domain. Domain email sever resolve order: MX recordds -> A reords if no MX.
        /// </summary>
        /// <param name="from">Sender email what is reported to SMTP server.</param>
        /// <param name="to">Recipient email.</param>
        /// <param name="message">Raw message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>from</b>,<b>to</b> or <b>message</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSend(string from,string to,Stream message)
        {
            QuickSend(null,from,to,message);
        }

        /// <summary>
        /// Sends message directly to email domain. Domain email sever resolve order: MX recordds -> A reords if no MX.
        /// </summary>
        /// <param name="localHost">Host name which is reported to SMTP server.</param>
        /// <param name="from">Sender email what is reported to SMTP server.</param>
        /// <param name="to">Recipient email.</param>
        /// <param name="message">Raw message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>from</b>,<b>to</b> or <b>message</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSend(string? localHost,string from,string to,Stream message)
        {
            if(from == null){
                throw new ArgumentNullException("from");
            }
            if(from != "" && !SMTP_Utils.IsValidAddress(from)){
                throw new ArgumentException("Argument 'from' has invalid value.");
            }
            if(to == null){
                throw new ArgumentNullException("to");
            }
            if(to == ""){
                throw new ArgumentException("Argument 'to' value must be specified.");
            }
            if(!SMTP_Utils.IsValidAddress(to)){
                throw new ArgumentException("Argument 'to' has invalid value.");
            }            
            if(message == null){
                throw new ArgumentNullException("message");
            }

            QuickSendSmartHost(localHost,Dns_Client.Static.GetEmailHosts(to)[0].HostName,25,false,from,new string[]{to},message);
        }

        #endregion

        #region static method QuickSendSmartHost

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="ssl">Specifies if connected via SSL.</param>
        /// <param name="message">Mail message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b> or <b>message</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string host,int port,bool ssl,Mail_Message message)
        {
            if(message == null){
                throw new ArgumentNullException("message");
            }

            QuickSendSmartHost(null,host,port,ssl,null,null,message);
        }

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="localHost">Host name which is reported to SMTP server. Value null means local computer name is used.</param>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="security">Specifies connection security.</param>
        /// <param name="userName">SMTP server user name. This value may be null, then authentication not used.</param>
        /// <param name="password">SMTP server password.</param>
        /// <param name="message">Mail message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b> or <b>message</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string? localHost,string host,int port,TcpClientSecurity security,string userName,string password,Mail_Message message)
        {
            QuickSendSmartHost(localHost,host,port,security == TcpClientSecurity.SSL,userName,password,message);
        }

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="localHost">Host name which is reported to SMTP server. Value null means local computer name is used.</param>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="ssl">Specifies if connected via SSL.</param>
        /// <param name="userName">SMTP server user name. This value may be null, then authentication not used.</param>
        /// <param name="password">SMTP server password.</param>
        /// <param name="message">Mail message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b> or <b>message</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string? localHost,string host,int port,bool ssl,string? userName,string? password,Mail_Message message)
        {
            if(message == null){
                throw new ArgumentNullException("message");
            }

            string from = "";
            if(message.From != null && message.From.Count > 0){
                from = ((Mail_t_Mailbox)message.From[0]).Address;
            }

            List<string> recipients = new List<string>();
            if(message.To != null){
				Mail_t_Mailbox[] addresses = message.To.Mailboxes;	
				foreach(Mail_t_Mailbox address in addresses){
					recipients.Add(address.Address);
				}
			}
			if(message.Cc != null){
				Mail_t_Mailbox[] addresses = message.Cc.Mailboxes;				
				foreach(Mail_t_Mailbox address in addresses){
					recipients.Add(address.Address);
				}
			}
			if(message.Bcc != null){
				Mail_t_Mailbox[] addresses = message.Bcc.Mailboxes;				
				foreach(Mail_t_Mailbox address in addresses){
					recipients.Add(address.Address);
				}

                // We must hide BCC
                message.Bcc.Clear();
			}

            MemoryStream ms = new MemoryStream();
            message.ToStream(ms,new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.Q,Encoding.UTF8),Encoding.UTF8);
            ms.Position = 0;
            QuickSendSmartHost(localHost,host,port,ssl,userName,password,from,recipients.ToArray(),ms);
        }

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="from">Sender email what is reported to SMTP server.</param>
        /// <param name="to">Recipients email addresses.</param>
        /// <param name="message">Raw message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b>,<b>from</b>,<b>to</b> or <b>message</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string host,int port,string from,string[] to,Stream message)
        {
            QuickSendSmartHost(null,host,port,false,null,null,from,to,message);
        }

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="ssl">Specifies if connected via SSL.</param>
        /// <param name="from">Sender email what is reported to SMTP server.</param>
        /// <param name="to">Recipients email addresses.</param>
        /// <param name="message">Raw message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b>,<b>from</b>,<b>to</b> or <b>stream</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string host,int port,bool ssl,string from,string[] to,Stream message)
        {
            QuickSendSmartHost(null,host,port,ssl,null,null,from,to,message);
        }

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="localHost">Host name which is reported to SMTP server. Value null means local computer name is used.</param>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="ssl">Specifies if connected via SSL.</param>
        /// <param name="from">Sender email what is reported to SMTP server.</param>
        /// <param name="to">Recipients email addresses.</param>
        /// <param name="message">Raw message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b>,<b>from</b>,<b>to</b> or <b>stream</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string? localHost,string host,int port,bool ssl,string from,string[] to,Stream message)
        {
            QuickSendSmartHost(localHost,host,port,ssl,null,null,from,to,message);
        }

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="localHost">Host name which is reported to SMTP server. Value null means local computer name is used.</param>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="ssl">Specifies if connected via SSL.</param>
        /// <param name="userName">SMTP server user name. This value may be null, then authentication not used.</param>
        /// <param name="password">SMTP server password.</param>
        /// <param name="from">Sender email what is reported to SMTP server.</param>
        /// <param name="to">Recipients email addresses.</param>
        /// <param name="message">Raw message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b>,<b>from</b>,<b>to</b> or <b>stream</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string? localHost,string host,int port,bool ssl,string? userName,string? password,string from,string[] to,Stream message)
        {
            QuickSendSmartHost(localHost,host,port,(ssl == true ? TcpClientSecurity.SSL : TcpClientSecurity.None),userName,password,from,to,message);
        }

        /// <summary>
        /// Sends message by using specified smart host.
        /// </summary>
        /// <param name="localHost">Host name which is reported to SMTP server. Value null means local computer name is used.</param>
        /// <param name="host">Host name or IP address.</param>
        /// <param name="port">Host port.</param>
        /// <param name="security">Specifies connection security.</param>
        /// <param name="userName">SMTP server user name. This value may be null, then authentication not used.</param>
        /// <param name="password">SMTP server password.</param>
        /// <param name="from">Sender email what is reported to SMTP server.</param>
        /// <param name="to">Recipients email addresses.</param>
        /// <param name="message">Raw message to send.</param>
        /// <exception cref="ArgumentNullException">Is raised when argument <b>host</b>,<b>from</b>,<b>to</b> or <b>stream</b> is null.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the method arguments has invalid value.</exception>
        /// <exception cref="SMTP_ClientException">Is raised when SMTP server returns error.</exception>
        [Obsolete("Use new QuickSend/QuickSendAsync instead. This method will be removed.")]
        public static void QuickSendSmartHost(string? localHost,string host,int port,TcpClientSecurity security,string? userName,string? password,string from,string[] to,Stream message)
        {
            if(host == null){
                throw new ArgumentNullException("host");
            }
            if(host == ""){
                throw new ArgumentException("Argument 'host' value may not be empty.");
            }
            if(port < 1){
                throw new ArgumentException("Argument 'port' value must be >= 1.");
            }
            if(from == null){
                throw new ArgumentNullException("from");
            }
            if(from != "" && !SMTP_Utils.IsValidAddress(from)){
                throw new ArgumentException("Argument 'from' has invalid value.");
            }
            if(to == null){
                throw new ArgumentNullException("to");
            }
            if(to.Length == 0){
                throw new ArgumentException("Argument 'to' must contain at least 1 recipient.");
            }
            foreach(string t in to){
                if(!SMTP_Utils.IsValidAddress(t)){
                    throw new ArgumentException("Argument 'to' has invalid value '" + t + "'.");
                }
            }
            if(message == null){
                throw new ArgumentNullException("message");
            }

            using(SMTP_Client smtp = new SMTP_Client()){
                smtp.Connect(host,port,security == TcpClientSecurity.SSL);
                if(security == TcpClientSecurity.TLS || (security == TcpClientSecurity.UseTlsIfSupported && smtp.SupportsCapability(SMTP_ServiceExtensions.STARTTLS))){
                    smtp.EhloHelo(localHost != null ? localHost : Dns.GetHostName());
                    smtp.StartTls(null);
                }
                smtp.EhloHelo(localHost != null ? localHost : Dns.GetHostName());
                if(!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password)){
                    smtp.Auth(smtp.AuthGetStrongestMethod(userName,password));
                }
                smtp.MailFrom(from,-1);
                foreach(string t in to){
                    smtp.RcptTo(t);
                }
                smtp.SendMessage(message);
            }
        }

        #endregion

    }
}
