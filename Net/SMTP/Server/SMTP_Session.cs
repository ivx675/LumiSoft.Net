using LumiSoft.Net.AUTH;
using LumiSoft.Net.IO;
using LumiSoft.Net.TCP;
using System.IO;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// This class implements SMTP session. Defined RFC 5321.
    /// </summary>
    public class SMTP_Session : TCP_ServerSession
    {
        private Dictionary<string,AUTH_SASL_ServerMechanism> m_pAuthentications;
        private int                                          m_BadCommands      = 0;
        private int                                          m_Transactions     = 0;
        private bool                                         m_SessionRejected  = false;
        private string?                                      m_EhloHost         = null;
        private GenericIdentity?                             m_pUser            = null;
        private SMTP_t_MailFrom?                             m_pFrom            = null;
        private Dictionary<string,SMTP_t_RcptTo>             m_pTo;
        private Stream?                                      m_pMessageStream   = null;
        private int                                          m_BDatReadedCount  = 0;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SMTP_Session()
        {
            m_pAuthentications = new Dictionary<string,AUTH_SASL_ServerMechanism>(StringComparer.OrdinalIgnoreCase);
            m_pTo = new Dictionary<string,SMTP_t_RcptTo>(StringComparer.OrdinalIgnoreCase);
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resource being used.
        /// </summary>
        public override void Dispose()
        {
            if(this.IsDisposed){
                return;
            }

            base.Dispose();

            m_pUser            = null;
            m_pFrom            = null;
            if(m_pMessageStream != null){
                m_pMessageStream.Dispose();
                m_pMessageStream = null;
            }

            this.StartedAsync = null;
            this.EhloAsync = null;
            this.HeloAsync = null;
            this.MailFromAsync = null;
            this.RcptToAsync = null;
            this.MessageStoringBeginAsync = null;
            this.MessageStoringCancelAsync = null;
            this.MessageStoringCompleteAsync = null;            
        }

        #endregion
    

        #region override method Start

        /// <summary>
        /// Starts session processing.
        /// </summary>
        protected override void Start()
        {
            base.Start();

            RunAsync();
        }

        #endregion

        #region override method OnErrorAsync

        /// <summary>
        /// Is called when session has processing error.
        /// </summary>
        /// <param name="x">Exception happened.</param>
        protected override async Task OnErrorAsync(Exception x)
        {
            if(this.IsDisposed){
                return;
            }
            if(x == null){
                return;
            }

            try{
                LogAddText("Exception: " + x.Message);

                // IO Error.
                if(x is IOException || x is SocketException){
                    Disconnect();
                }
                // Unknown error.
                else{
                    // Raise SMTP_Server.Error event.
                    await base.OnErrorAsync(x);

                    // Try to send "500 Internal server error."
                    try{
                        string text = "Internal server error.";
                        await SendResponseAsync(new SMTP_ServerResponse(500,null,text));
                    }
                    catch{                        
                    }

                    Disconnect();
                }
            }
            catch{
            }
        }

        #endregion

        #region override method OnTimeoutAsync

        /// <summary>
        /// Is called wen session idle timeout happens.
        /// </summary>
        protected override async Task OnTimeoutAsync()
        {
            try{
                if(m_pMessageStream != null){
                    // Raise MessageStoringCancelAsync event.
                    if(this.MessageStoringCancelAsync != null){
                        var eArgs = new SMTP_e_MessageStoringCancel(this,m_pMessageStream);
                        await this.MessageStoringCancelAsync(eArgs);
                    }
                }

                string text = "Idle timeout, closing connection.";
                var sendTask =  SendResponseAsync(new SMTP_ServerResponse(421,null,text));
                await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch{
                // Skip errors.
            }

            _= base.OnTimeoutAsync();
        }

        #endregion


        #region method RunAsync

        internal async void RunAsync()
        {
            /* RFC 5321 3.1.
                The SMTP protocol allows a server to formally reject a mail session
                while still allowing the initial connection as follows: a 554
                response MAY be given in the initial connection opening message
                instead of the 220.  A server taking this approach MUST still wait
                for the client to send a QUIT (see Section 4.1.1.10) before closing
                the connection and SHOULD respond to any intervening commands with
                "503 bad sequence of commands".  Since an attempt to make an SMTP
                connection to such a system is probably in error, a server returning
                a 554 response on connection opening SHOULD provide enough
                information in the reply text to facilitate debugging of the sending
                system.
            */

            try{
                string greetingText = this.Server.GreetingText;
                if(string.IsNullOrEmpty(greetingText)){
                    greetingText = Net_Utils.GetLocalHostName(this.LocalHostName) + " ESMTP Service ready.";
                }

                // Create default server response, user can override it in event.
                var response = new SMTP_ServerResponse(220,null,greetingText);

                // Raise event StartedAsync.
                if(this.StartedAsync != null){
                    SMTP_e_Started eArgs = new SMTP_e_Started(this,response);
                    await this.StartedAsync(eArgs);

                    response = eArgs.Response;
                }

                // Session rejected flag, so we respond "503 bad sequence of commands" any command except QUIT.
                if(!response.IsSuccess){
                    m_SessionRejected = true;
                }

                await SendResponseAsync(response);
                
                // Command loop, while QUIT or fatal error happens.
                while(!this.IsDisposed){
                    // Read command line.
                    ReadLineResult responseline = await this.TcpStream.ReadLineAsync(new byte[8000],SizeExceededAction.JunkAndThrowException);
                    // Server closed connection.
                    if(responseline.BytesInBuffer == 0){
                        LogAddText("The remote host '" + this.RemoteEndPoint?.ToString() + "' closed connection.");
                        Dispose();

                        break;
                    }                    
                    string line = responseline.LineUtf8 ?? "";

                    LogAddRead(responseline.BytesInBuffer,line);
                                        
                    string[] cmd_args = line.Split(new char[]{' '},2);
                    string   cmd      = cmd_args[0].ToUpperInvariant();
                    string   args     = cmd_args.Length == 2 ? cmd_args[1] : "";

                    // RFC 5321 3.1.
                    if(m_SessionRejected && cmd != "QUIT"){
                        await SendResponseAsync(new SMTP_ServerResponse(554,null,"Session rejected."));
                
                        continue;
                    }

                    if(cmd == "EHLO"){
                        await _EhloAsync(args);
                    }
                    else if(cmd == "HELO"){
                        await _HeloAsync(args);
                    }
                    else if(cmd == "STARTTLS"){
                        await StartTlsAsync(args);
                    }
                    else if(cmd == "AUTH"){
                        await AuthAsync(args);
                    }
                    else if(cmd == "MAIL"){
                        await _MailAsync(args);
                    }
                    else if(cmd == "RCPT"){
                        await _RcptAsync(args);
                    }
                    else if(cmd == "DATA"){  
                        await DataAsync(args);
                    }
                    else if(cmd == "BDAT"){
                        await BdatAsync(args);
                    }
                    else if(cmd == "RSET"){
                        await RsetAsync(args);
                    }
                    else if(cmd == "NOOP"){
                        await NoopAsync(args);
                    }
                    else if(cmd == "QUIT"){
                        await QuitAsync(args);
                    }
                    else{
                        m_BadCommands++;

                        // Maximum allowed bad commands exceeded.
                        if(this.Server.MaxBadCommands != 0 && m_BadCommands > this.Server.MaxBadCommands){
                            await SendResponseAsync(new SMTP_ServerResponse(421,null,$"Too many bad commands, closing connection."));
                            await DisconnectAsync();
                            
                            break;
                        }
                        
                        await SendResponseAsync(new SMTP_ServerResponse(502,null,$"Error: command '{cmd}' not recognized."));
                    }
                }
            }
            catch(Exception x){
                await OnErrorAsync(x);
            }
        }

        #endregion
        

        #region method EhloAsync

        private async Task _EhloAsync(string cmdText)
        {
            /* RFC 5321 4.1.1.1.
                ehlo           = "EHLO" SP ( Domain / address-literal ) CRLF

                ehlo-ok-rsp    = ( "250" SP Domain [ SP ehlo-greet ] CRLF )
                                 / ( "250-" Domain [ SP ehlo-greet ] CRLF
                                 *( "250-" ehlo-line CRLF )
                                 "250" SP ehlo-line CRLF )

                ehlo-greet     = 1*(%d0-9 / %d11-12 / %d14-127)
                                 ; string of any characters other than CR or LF

                ehlo-line      = ehlo-keyword *( SP ehlo-param )

                ehlo-keyword   = (ALPHA / DIGIT) *(ALPHA / DIGIT / "-")
                                ; additional syntax of ehlo-params depends on ehlo-keyword

                ehlo-param     = 1*(%d33-126)
                                ; any CHAR excluding <SP> and all control characters (US-ASCII 0-31 and 127 inclusive)
            */
            if(string.IsNullOrEmpty(cmdText) || cmdText.Split(' ').Length != 1){
                string text = "Syntax error, syntax: \"EHLO\" SP hostname CRLF";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }
            if(m_pFrom != null){
                string text = "Bad sequence of commands: EHLO not allowed during a mail transaction.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
             
            List<SMTP_t_ReplyLine> ehloLines = new List<SMTP_t_ReplyLine>();
            ehloLines.Add(new SMTP_t_ReplyLine(250,Net_Utils.GetLocalHostName(this.LocalHostName)));
            if(Supports(SMTP_ServiceExtensions.PIPELINING)){
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions.PIPELINING));
            }
            if(Supports(SMTP_ServiceExtensions.SIZE)){
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions.SIZE + " " + this.Server.MaxMessageSize));
            }
            if(Supports(SMTP_ServiceExtensions.STARTTLS) && !this.IsSecureConnection && this.Certificate != null){
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions.STARTTLS));
            }
            if(Supports(SMTP_ServiceExtensions._8BITMIME)){
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions._8BITMIME));
            }
            if(Supports(SMTP_ServiceExtensions.BINARYMIME)){                
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions.BINARYMIME));
            }
            if(Supports(SMTP_ServiceExtensions.CHUNKING)){
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions.CHUNKING));
            }
            if(Supports(SMTP_ServiceExtensions.DSN)){
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions.DSN));
            }
            
            List<string> sasl = new List<string>();
            foreach(AUTH_SASL_ServerMechanism authMechanism in this.Authentications.Values){
                if(!authMechanism.RequireSSL || (authMechanism.RequireSSL && this.IsSecureConnection)){
                    sasl.Add(authMechanism.Name);
                }
            }
            if(sasl.Count > 0){
                ehloLines.Add(new SMTP_t_ReplyLine(250,SMTP_ServiceExtensions.AUTH + " " + string.Join(' ',sasl)));
            }
            
            // Create default server response, user can override it in event.
            var response = new SMTP_ServerResponse(ehloLines.ToArray());

            // Raise event EhloAsync.
            if(this.EhloAsync != null){
                SMTP_e_Ehlo eArgs = new SMTP_e_Ehlo(this,cmdText,response);
                await this.EhloAsync(eArgs);

                response = eArgs.Response;
            }

            // EHLO accepted.
            if(response.IsSuccess){
                m_EhloHost = cmdText;

                /* RFC 5321 4.1.4.
                    An EHLO command MAY be issued by a client later in the session.  If
                    it is issued after the session begins and the EHLO command is
                    acceptable to the SMTP server, the SMTP server MUST clear all buffers
                    and reset the state exactly as if a RSET command had been issued.  In
                    other words, the sequence of RSET followed immediately by EHLO is
                    redundant, but not harmful other than in the performance cost of
                    executing unnecessary commands.
                */
                Reset();
            }

            await SendResponseAsync(response);
        }

        #endregion

        #region method HeloAsync

        private async Task _HeloAsync(string cmdText)
        {
            /* RFC 5321 4.1.1.1.
                helo     = "HELO" SP Domain CRLF
            
                response = "250" SP Domain [ SP ehlo-greet ] CRLF
            */
            if(string.IsNullOrEmpty(cmdText) || cmdText.Split(' ').Length != 1){
                string text = "Syntax error, syntax: \"HELO\" SP hostname CRLF";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }
            if(m_pFrom != null){
                string text = "Bad sequence of commands: HELO not allowed during a mail transaction.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }

            // Create default server response, user can override it in event.
            var response = new SMTP_ServerResponse(220,null,Net_Utils.GetLocalHostName(this.LocalHostName));

            // Raise event EhloAsync.
            if(this.HeloAsync != null){
                SMTP_e_Helo eArgs = new SMTP_e_Helo(this,cmdText,response);
                await this.HeloAsync(eArgs);

                response = eArgs.Response;
            }

            // HELO accepted.
            if(response.IsSuccess){
                m_EhloHost = cmdText;

                /* RFC 5321 4.1.4.
                    An EHLO command MAY be issued by a client later in the session.  If
                    it is issued after the session begins and the EHLO command is
                    acceptable to the SMTP server, the SMTP server MUST clear all buffers
                    and reset the state exactly as if a RSET command had been issued.  In
                    other words, the sequence of RSET followed immediately by EHLO is
                    redundant, but not harmful other than in the performance cost of
                    executing unnecessary commands.
                */
                Reset();
            }

            await SendResponseAsync(response);
        }

        #endregion

        #region method StartTlsAsync

        private async Task StartTlsAsync(string cmdText)
        {
            /* RFC 3207 section 4.2 — STARTTLS Command

               The STARTTLS command requests that the SMTP session be upgraded
               from a cleartext connection to a TLS-protected connection.  The
               server MUST advertise the STARTTLS extension in response to EHLO
               before the client may issue this command.

               STARTTLS is valid only before any mail transaction commands
               (MAIL, RCPT, DATA) and only while the connection is not already
               protected by TLS.  Once a TLS layer is active, servers MUST NOT
               advertise STARTTLS and clients MUST NOT issue it.

               Upon receiving STARTTLS, the server MUST respond with:
                   "220 Ready to start TLS"
               after which the TLS handshake begins immediately.  No further
               SMTP commands may be sent until TLS negotiation completes.

               After TLS is successfully established, the client MUST send EHLO
               again.  All SMTP state prior to STARTTLS is discarded.

               Syntax:
                   starttls = "STARTTLS" CRLF
            */

            
            if(!string.IsNullOrEmpty(cmdText)){
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),"Syntax error: No parameters allowed."));

                return;
            }
            if(this.IsSecureConnection){
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,1),"STARTTLS not allowed after TLS negotiation."));

                return;
            }            
            if(this.AuthenticatedUserIdentity != null || this.m_pFrom != null){
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,1),"Bad sequence of commands: STARTTLS not permitted in current state."));

                return;
            }
            if(this.Certificate == null){
                await SendResponseAsync(new SMTP_ServerResponse(550,new SMTP_t_EnhancedStatusCode(5,5,3),"TLS not available: Server has no SSL certificate."));

                return;
            }

            await SendResponseAsync(new SMTP_ServerResponse(220,null,"Ready to start TLS."));

            try{
                await SwitchToSecureAsync();

                // Log
                LogAddText("TLS negotiation completed successfully.");

                m_EhloHost = "";
                Reset();
            }
            catch(Exception x){
                // Log
                LogAddText("TLS negotiation failed: " + x.Message + ".");

                await DisconnectAsync();
            }
        }

        #endregion

        #region method AuthAsync

        private async Task AuthAsync(string cmdText)
        {
            /* RFC 4954 
			    AUTH mechanism [initial-response]

                Arguments:
                    mechanism: A string identifying a [SASL] authentication mechanism.

                    initial-response: An optional initial client response.  If
                    present, this response MUST be encoded as described in Section
                    4 of [BASE64] or contain a single character "=".

                Restrictions:
                    After an AUTH command has been successfully completed, no more
                    AUTH commands may be issued in the same session.  After a
                    successful AUTH command completes, a server MUST reject any
                    further AUTH commands with a 503 reply.

                    The AUTH command is not permitted during a mail transaction.
                    An AUTH command issued during a mail transaction MUST be
                    rejected with a 503 reply.
             
                A server challenge is sent as a 334 reply with the text part
                containing the [BASE64] encoded string supplied by the SASL
                mechanism.  This challenge MUST NOT contain any text other
                than the BASE64 encoded challenge.
             
                In SMTP, a server challenge that contains no data is defined 
                as a 334 reply with no text part. Note that there is still a space 
                following the reply code, so the complete response line is "334 ".
             
                If the client wishes to cancel the authentication exchange, 
                it issues a line with a single "*". If the server receives 
                such a response, it MUST reject the AUTH command by sending a 501 reply.
			*/
            
            if(this.Authentications.Count == 0){
                string text = "AUTH not supported.";
                await SendResponseAsync(new SMTP_ServerResponse(502,new SMTP_t_EnhancedStatusCode(5,5,1),text));

				return;
			}
			if(this.IsAuthenticated){
                string text = "Already authenticated.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,0),text));

				return;
			}
            if(m_pFrom != null){                
                string text = "Authentication not permitted during mail transaction.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,1),text));

				return;
            }

            #region Parse parameters

            string[] arguments = cmdText.Split(' ');
            if(arguments.Length > 2){
                string text = "Syntax error.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }
            byte[] initialClientResponse = [];
            if(arguments.Length == 2){
                if(arguments[1] == "="){
                    // Skip.
                }
                else{
                    try{
                        initialClientResponse = Convert.FromBase64String(arguments[1]);
                    }
                    catch{
                        string text = "Syntax error: Parameter 'initial-response' value must be BASE64 or contain a single character '='.\".";
                        await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                        return;
                    }
                }
            }
            string mechanism = arguments[0];

            #endregion

            
            AUTH_SASL_ServerMechanism? auth = null;
            if(!m_pAuthentications.TryGetValue(mechanism,out auth)){
                string text = "Unrecognized authentication type.";
                await SendResponseAsync(new SMTP_ServerResponse(504,new SMTP_t_EnhancedStatusCode(5,5,4),text));

                return;
            }

            if(auth.RequireSSL && !this.IsSecureConnection){
                string text = "Encryption required for requested authentication mechanism.";
                await SendResponseAsync(new SMTP_ServerResponse(538,new SMTP_t_EnhancedStatusCode(5,7,11),text));

                return;
            }

            byte[] clientResponse = initialClientResponse;            
            auth.Reset();
            while(true){
                byte[]? serverResponse = auth.Continue(clientResponse);
                // Authentication completed.
                if(auth.IsCompleted){
                    if(auth.IsAuthenticated){
                        m_pUser = new GenericIdentity(auth.UserName,"SASL-" + auth.Name);

                        string text = "Authentication succeeded.";
                        await SendResponseAsync(new SMTP_ServerResponse(235,new SMTP_t_EnhancedStatusCode(2,7,0),text));
                    }
                    else{
                        string text = "Authentication credentials invalid.";
                        await SendResponseAsync(new SMTP_ServerResponse(535,new SMTP_t_EnhancedStatusCode(5,7,8),text));
                    }
                    break;
                }
                // Authentication continues.
                else{
                    ArgumentNullException.ThrowIfNull(serverResponse);

                    // Send server challenge.
                    if(serverResponse.Length == 0){
                        await SendResponseAsync(new SMTP_ServerResponse(334,null,""));
                    }
                    else{
                        await SendResponseAsync(new SMTP_ServerResponse(334,null,Convert.ToBase64String(serverResponse)));
                    }

                    // Read client response. 
                    var readLineResult = await this.TcpStream.ReadLineAsync(new byte[8000],SizeExceededAction.JunkAndThrowException);
                    string? clientResponseStr = readLineResult.LineUtf8;
                    if(clientResponseStr == null){
                        LogAddText("Client closed connection.");

                        throw new IOException("Client closed connection.");
                    }
                    
                    // Log
                    #if DEBUG
                        LogAddRead(readLineResult.BytesInBuffer,clientResponseStr);
                    #else
                        LogAddRead(readLineResult.BytesInBuffer,"Client response recieved.");
                    #endif                    
                   
                    // Client canceled authentication.
                    if(clientResponseStr == "*"){
                        string text = "Authentication canceled.";
                        await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,7,0),text));

                        return;
                    }
                    // We have base64 client response, decode it.
                    else{
                        try{
                            clientResponse = Convert.FromBase64String(clientResponseStr);
                        }
                        catch{
                            string text = "Invalid client response.";
                            await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                            return;
                        }
                    }
                }
            }
        }

        #endregion

        #region method MailAsync

        private async Task _MailAsync(string cmdText)
        {
            /*
                MAIL FROM command syntax (RFC 5321 + RFC 3461 DSN extensions):
                  MAIL FROM:<reverse-path> [SP mail-parameters] CRLF

                reverse-path:
                  - Must be enclosed in angle brackets.
                  - May be empty (<>) for the null sender.
                  - Contains the sender mailbox or address-literal.

                mail-parameters (optional):
                  - SIZE=<number>        // RFC 1870
                  - BODY=<type>          // 7BIT, 8BITMIME, BINARYMIME
                  - AUTH=<address>       // RFC 2554
                  - RET=<type>           // FULL or HDRS (RFC 3461)
                  - ENVID=<string>       // DSN envelope identifier (RFC 3461)
                  - Additional parameters may appear depending on supported extensions.

                parameter rules:
                  - Must follow a space after the closing '>'.
                  - Each parameter is keyword=value with no spaces around '='.
                  - RET must be FULL or HDRS.
                  - ENVID must not contain spaces.
                  - If a parameter is syntactically valid but not recognized or not
                    implemented by the server → reply with 555 (RFC 5321).

                error handling:
                  - Malformed or missing reverse-path → 501 Syntax error.
                  - Unsupported or unimplemented mail-parameters → 555 parameters not recognized.
                  - Wrong command sequence (MAIL already active) → 503 Bad sequence.

                examples:
                  OK:  MAIL FROM:<user@example.com>
                  OK:  MAIL FROM:<>                     // null sender
                  OK:  MAIL FROM:<user@example.com> SIZE=12345 RET=FULL ENVID=abc123
            */

            // RFC 5321 4.1.4.
            if(string.IsNullOrEmpty(m_EhloHost)){
                string text = "Bad sequence of commands: send EHLO/HELO first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            // RFC 5321 4.1.4.
            if(m_pFrom != null){
                string text = "Bad sequence of commands: MAIL FROM already issued.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            // RFC 3030 BDAT.
            if(m_pMessageStream != null){
                string text = "Bad sequence of commands: BDAT command is pending.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            if(this.Server.MaxTransactions != 0 && m_Transactions >= this.Server.MaxTransactions){
                string text = "Too many mail transactions in this session";
                await SendResponseAsync(new SMTP_ServerResponse(452,new SMTP_t_EnhancedStatusCode(4,5,3),text));

                return;
            }

            
            if(cmdText.ToUpper().StartsWith("FROM:")){
                // Remove FROM: from command text.
                cmdText = cmdText.Substring(5).Trim();
            }
            else{
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }

            string         address = "";
            int?           size    = null;
            string?        body    = null;
            SMTP_t_DSN_Ret ret     = SMTP_t_DSN_Ret.NotSpecified;
            string?        envID   = null;

            // Mailbox not between <>.
            if(!cmdText.StartsWith("<") || cmdText.IndexOf('>') == -1){
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }
            // Parse mailbox.
            else{
                address = cmdText.Substring(1,cmdText.IndexOf('>') - 1).Trim();
                cmdText = cmdText.Substring(cmdText.IndexOf('>') + 1).Trim();

                bool isEmpty = string.IsNullOrEmpty(address);

                // MAIL FROM can be empty (<>) OR must have valid email syntax
                if(!isEmpty && !IsValidEmailSyntax(address)){
                    string text = "Syntax error: Sender address format is invalid (e.g., user@domain.com).";
                    await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,4),text));

                    return;
                }
            }

            #region Parse parameters
                                    
            string[] parameters = string.IsNullOrEmpty(cmdText) ? new string[0] : cmdText.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
            foreach(string parameter in parameters){
                string[] name_value = parameter.Split(new char[]{'='},2);
                if(name_value.Length != 2){
                    string text = "Syntax error in parameters or arguments.";
                    await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                    return;
                }
                string name  = name_value[0].ToUpper();
                string value = name_value[1];

                // SIZE
                if(name == "SIZE" && Supports(SMTP_ServiceExtensions.SIZE)){
                    // RFC 1870.
                    //  size-value ::= 1*20DIGIT
                    if(int.TryParse(value,out int sizeParsed)){
                        size = sizeParsed;
                    }
                    else{                        
                        string text = "Syntax error in parameters or arguments.";
                        await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                        return;
                    }

                    // Message size exceeds maximum allowed message size.
                    if(size > this.Server.MaxMessageSize){
                        string text = "Message size exceeds the server’s configured maximum.";
                        await SendResponseAsync(new SMTP_ServerResponse(552,new SMTP_t_EnhancedStatusCode(5,3,4),text));
                        
                        return;
                    }
                }
                // BODY
                else if(name == "BODY"){
                    // RFC 1652.
                    //  body-value ::= "7BIT" / "8BITMIME" / "BINARYMIME"
                    //
                    // BINARYMIME - defined in RFC 3030.
                    if(value.ToUpper() != "7BIT" && value.ToUpper() != "8BITMIME" && value.ToUpper() != "BINARYMIME"){
                        string text = "Syntax error in parameters or arguments.";
                        await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                        return;
                    }
                    body = name_value[1].ToUpper();
                }
                // RET
                else if(name == "RET" && Supports(SMTP_ServiceExtensions.DSN)){
                    // RFC 3461 4.3.
                    //  ret-value = "FULL" / "HDRS"
                    if(value.ToUpper() == "FULL"){
                        ret = SMTP_t_DSN_Ret.FullMessage;
                    }
                    else if(value.ToUpper() == "HDRS"){
                        ret = SMTP_t_DSN_Ret.Headers;
                    }
                    else{
                        string text = "Syntax error in parameters or arguments.";
                        await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                        return;
                    }
                }
                // ENVID
                else if(name == "ENVID" && Supports(SMTP_ServiceExtensions.DSN)){
                    // RFC 3461 4.4.
                    //  envid-parameter = "ENVID=" xtext

                    envID = value;
                }
                // AUTH
                else if(name == "AUTH"){
                }
                // Unsupported parameter.
                else{
                    string text = "Parameter not recognized or not supported.";
                    await SendResponseAsync(new SMTP_ServerResponse(555,new SMTP_t_EnhancedStatusCode(5,5,4),text));

                    return;
                }
            }

            #endregion

            var from     = new SMTP_t_MailFrom(address,size,body,ret,envID);
            var response = new SMTP_ServerResponse(250,null,"OK.");

            // Raise event MailFromAsync.
            if(this.MailFromAsync != null){
                SMTP_e_MailFrom eArgs = new SMTP_e_MailFrom(this,from,response);
                await this.MailFromAsync(eArgs);

                response = eArgs.Response;
            }

            // MAIL accepted.
            if(response.IsSuccess){
                m_pFrom = from;
                m_Transactions++;
            }

            await SendResponseAsync(response);
        }

        #endregion

        #region method RcptAsync

        private async Task _RcptAsync(string cmdText)
        {
            /*
                RCPT TO command syntax (RFC 5321 + RFC 3461 DSN extensions):
                  RCPT TO:<forward-path> [SP rcpt-parameters] CRLF

                forward-path:
                  - Must be enclosed in angle brackets.
                  - Contains the recipient mailbox or address-literal.
                  - May include source routes (deprecated; MUST be rejected or ignored).

                rcpt-parameters (optional):
                  - NOTIFY=<dsn-notify>      // RFC 3461
                  - ORCPT=<dsn-orcpt>        // RFC 3461
                  - Additional parameters may appear depending on supported extensions.

                parameter rules:
                  - Must follow a space after the closing '>'.
                  - Each parameter is keyword=value with no spaces around '='.
                  - NOTIFY may contain: NEVER / SUCCESS / FAILURE / DELAY
                    * Multiple values separated by commas.
                    * NEVER cannot be combined with other values.
                  - ORCPT must be of the form: <type>;<address>
                    * Example: ORCPT=rfc822;user@example.com
                  - If a parameter is syntactically valid but not recognized or not
                    implemented by the server → reply with 555 (RFC 5321).

                error handling:
                  - Malformed or missing forward-path → 501 Syntax error.
                  - Unsupported or unimplemented rcpt-parameters → 555 parameters not recognized.
                  - Wrong command sequence (MAIL not yet issued) → 503 Bad sequence.
                  - Recipient rejected by policy or local rules → 550 Requested action not taken.
                  - Temporary failure (e.g., mailbox unavailable) → 450 Requested action not taken.

                examples:
                  OK:  RCPT TO:<user@example.com>
                  OK:  RCPT TO:<user@example.com> NOTIFY=SUCCESS,FAILURE
                  OK:  RCPT TO:<user@example.com> ORCPT=rfc822;alias@example.net
            */

            // RFC 5321 4.1.4.
            if(string.IsNullOrEmpty(m_EhloHost)){                
                string text = "Bad sequence of commands: send EHLO/HELO first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            // RFC 5321 4.1.4.
            if(m_pFrom == null){     
                string text = "Bad sequence of commands: send 'MAIL FROM:' first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            // RFC 3030 BDAT.
            if(m_pMessageStream != null){     
                string text = "Bad sequence of commands: BDAT command is pending.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }


            if(cmdText.ToUpper().StartsWith("TO:")){
                // Remove TO: from command text.
                cmdText = cmdText.Substring(3).Trim();
            }
            else{
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }

            string            address = "";
            SMTP_t_DSN_Notify notify  = SMTP_t_DSN_Notify.NotSpecified;
            string?           orcpt   = null;

            // Mailbox not between <>.
            if(!cmdText.StartsWith("<") || cmdText.IndexOf('>') == -1){
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }
            // Parse mailbox.
            else{
                address = cmdText.Substring(1,cmdText.IndexOf('>') - 1).Trim();
                cmdText = cmdText.Substring(cmdText.IndexOf('>') + 1).Trim();

                // RCPT TO CANNOT BE EMPTY and must have valid email syntax
                if (string.IsNullOrEmpty(address) || !IsValidEmailSyntax(address)){
                    string text = "Syntax error: Recipient address format is invalid or empty.";
                    await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,4),text));

                    return;
                }
            }
            if(address == string.Empty){
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }

            #region Parse parameters

            string[] parameters = string.IsNullOrEmpty(cmdText) ? new string[0] : cmdText.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
            foreach(string parameter in parameters){
                string[] name_value = parameter.Split(new char[]{'='},2);
                if(name_value.Length != 2){
                    string text = "Syntax error in parameters or arguments.";
                    await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                    return;
                }
                string name  = name_value[0].ToUpper();
                string value = name_value[1];

                // NOTIFY
                if(name == "NOTIFY" && Supports(SMTP_ServiceExtensions.DSN)){
                    /* RFC 1891 5.1.
                        notify-esmtp-value  = "NEVER" / 1#notify-list-element
                        notify-list-element = "SUCCESS" / "FAILURE" / "DELAY"
                      
                        a. Multiple notify-list-elements, separated by commas, MAY appear in a
                           NOTIFY parameter; however, the NEVER keyword MUST appear by itself.
                    */
                    string[] notifyItems = value.ToUpper().Split(',');
                    foreach(string notifyItem in notifyItems){
                        if(notifyItem.Trim().ToUpper() == "NEVER"){
                            notify |= SMTP_t_DSN_Notify.Never;
                        }
                        else if(notifyItem.Trim().ToUpper() == "SUCCESS"){
                            notify |= SMTP_t_DSN_Notify.Success;
                        }
                        else if(notifyItem.Trim().ToUpper() == "FAILURE"){
                            notify |= SMTP_t_DSN_Notify.Failure;
                        }
                        else if(notifyItem.Trim().ToUpper() == "DELAY"){
                            notify |= SMTP_t_DSN_Notify.Delay;
                        }
                        // Invalid or not supported notify item.
                        else{
                            string text = "Syntax error in parameters or arguments.";
                            await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                            return;
                        }

                        if(notify.HasFlag(SMTP_t_DSN_Notify.Never) && notify != SMTP_t_DSN_Notify.Never){
                            string text = "Syntax error in parameters or arguments.";
                            await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                            return;
                        }
                    }
                }
                // ORCPT
                else if(name == "ORCPT" && Supports(SMTP_ServiceExtensions.DSN)){
                    orcpt = value;

                    int sepIndex = value.IndexOf(';');
                    if(sepIndex <= 0 || sepIndex == value.Length - 1){
                        string text = "Syntax error in parameters or arguments.";
                        await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                        return;
                    }
                }
                // Unsupported parameter.
                else{
                    string text = "Parameter not recognized.";
                    await SendResponseAsync(new SMTP_ServerResponse(555,new SMTP_t_EnhancedStatusCode(5,5,4),text));

                    return;
                }
            }

            #endregion

            // Maximum allowed recipients exceeded.
            if(m_pTo.Count >= this.Server.MaxRecipients){
                string text = "Too many recipients.";
                await SendResponseAsync(new SMTP_ServerResponse(452,new SMTP_t_EnhancedStatusCode(4,5,3),text));

                return;
            }
            // Recipient already specified.
            if(m_pTo.ContainsKey(address)){
                string text = "Recipient already specified.";
                await SendResponseAsync(new SMTP_ServerResponse(452,new SMTP_t_EnhancedStatusCode(4,5,3),text));

                return;
            }

            var to       = new SMTP_t_RcptTo(address,notify,orcpt);
            var response = new SMTP_ServerResponse(250,null,"OK.");

            // Raise event MailFromAsync.
            if(this.RcptToAsync != null){
                SMTP_e_RcptTo eArgs = new SMTP_e_RcptTo(this,to,response);
                await this.RcptToAsync(eArgs);

                response = eArgs.Response;
            }

            // RCPT accepted.
            if(response.IsSuccess){
                if(!m_pTo.ContainsKey(address)){
                    m_pTo.Add(address,to);
                }
            }  

            await SendResponseAsync(response);
        }

        #endregion

        #region method DataAsync

        private async Task DataAsync(string cmdText)
        {
            /*
                DATA command syntax (RFC 5321 + RFC 3030 CHUNKING interaction):

                  DATA CRLF
                  <message content> CRLF "." CRLF

                semantics:
                  - DATA initiates the transfer of message content unless BDAT is used.
                  - The server MUST reply with 354 to signal readiness to receive data.
                  - The message ends when a line containing only "." is received.
                  - If CHUNKING (BDAT) is active, DATA MUST NOT be used (503).

                reply codes:
                  354 Start mail input; end with <CRLF>.<CRLF>
                  250 OK — message accepted for delivery
                  451 Requested action aborted: local error in processing
                  452 Requested action not taken: insufficient system storage
                  552 Requested mail action aborted: exceeded storage allocation
                  554 Transaction failed (policy rejection)

                error handling:
                  - No MAIL FROM issued → 503 Bad sequence of commands
                  - No RCPT TO issued → 503 Bad sequence of commands
                  - BDAT in progress → 503 Bad sequence of commands
                  - Message size exceeds server maximum → 552 5.3.4
                  - Temporary storage failure → 452 4.5.3
                  - Policy rejection → 554 5.7.1
                  - Local processing error → 451 4.3.0

                notes:
                  - DATA and BDAT are mutually exclusive; once BDAT is used, DATA is invalid.
                  - The server must treat DATA and BDAT as equivalent message-delivery
                    mechanisms; both feed into the same message-storing pipeline.
                  - The final response after storing the message is identical for DATA and BDAT.
            */

            if(string.IsNullOrEmpty(m_EhloHost)){                
                string text = "Bad sequence of commands: send EHLO/HELO first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            if(m_pFrom == null){     
                string text = "Bad sequence of commands: send 'MAIL FROM:' first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            if(m_pTo.Count == 0){     
                string text = "Bad sequence of commands: send 'RCPT TO:' first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            if(m_pMessageStream != null){     
                string text = "Bad sequence of commands: BDAT command is pending.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }

            // DATA may not have arguments.
            if(cmdText.Length > 0){
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }

            await SendResponseAsync(new SMTP_ServerResponse(354,null,"Start mail input; end with <CRLF>.<CRLF>."));
            
            // Raise MessageStoringBeginAsync event.
            if(this.MessageStoringBeginAsync != null){
                var eArgs = new SMTP_e_MessageStoringBegin(this);
                await this.MessageStoringBeginAsync(eArgs);

                m_pMessageStream = eArgs.StoreStream;                
            }
            // User didn't specify stream, usedefault stream.
            if(m_pMessageStream == null){
                m_pMessageStream = new MemoryStreamEx(128000);
            }

            try{
                // Add Received: header.
                byte[] recevived = CreateReceivedHeader();
                await m_pMessageStream.WriteAsync(recevived);

                int msgSize = await this.TcpStream.ReadPeriodTerminatedAsync(m_pMessageStream,this.Server.MaxMessageSize,32000,SizeExceededAction.JunkAndThrowException);
                LogAddRead(msgSize,$"Message received: {msgSize} bytes.");
            }
            catch(Exception x){
                // Raise MessageStoringCancelAsync event.
                if(this.MessageStoringCancelAsync != null){
                    var eArgs = new SMTP_e_MessageStoringCancel(this,m_pMessageStream);
                    await this.MessageStoringCancelAsync(eArgs);        
                }

                if(x is IncompleteDataException){
                    LogAddText("Disposing SMTP session, remote endpoint closed socket.");
                    await DisconnectAsync();
                }
                else if(x is LineSizeExceededException){
                    await SendResponseAsync(new SMTP_ServerResponse(552,null,"Line too long."));
                }
                else if(x is DataSizeExceededException){
                    await SendResponseAsync(new SMTP_ServerResponse(552,null,"Too much mail data."));
                }
                else{
                    LogAddText("Disposing SMTP session, fatal error:" + x.Message);
                    await OnErrorAsync(x);
                    await DisconnectAsync();
                }                

                Reset();

                return;
            }

            var response = new SMTP_ServerResponse(250,null,"OK — message accepted for delivery");

            // Raise MessageStoringCompleteAsync event.
            if(this.MessageStoringCompleteAsync != null){
                var eArgs = new SMTP_e_MessageStoringComplete(this,m_pMessageStream,response);
                await this.MessageStoringCompleteAsync(eArgs);

                response = eArgs.Response;
            }
                        
            await SendResponseAsync(response);

            Reset();
        }

        #endregion

        #region method BdatAsync

        private async Task BdatAsync(string cmdText)
        {
            /*
                BDAT (RFC 3030) Chunked Transfer Notes:

                - BDAT <size> [LAST] declares the exact octet count of the following chunk.
                - Server MUST read exactly <size> bytes; BDAT does not use CRLF.CRLF termination.
                - The LAST keyword marks the final chunk; server finalizes the message afterward.
                - BDAT and DATA MUST NOT be mixed in the same SMTP transaction.
                - After each BDAT (except LAST), server replies: 250 OK.
                - After BDAT LAST, server replies: 250 OK or an appropriate error.

                Syntax:
                    BDAT <octet-count> [LAST]

                Example:
                    C: EHLO client.example
                    S: 250-CHUNKING
                       250 OK

                    C: BDAT 1024
                    S: 250 OK

                    C: <1024 bytes of binary data>

                    C: BDAT 512 LAST
                    S: 250 OK

                    C: <512 bytes of binary data>

                    (Message is now complete)
            */

            if(!Supports(SMTP_ServiceExtensions.CHUNKING)){
                string text = "Unrecognized command.";
                await SendResponseAsync(new SMTP_ServerResponse(500,new SMTP_t_EnhancedStatusCode(5,5,1),text));

                return;
            }
            if(string.IsNullOrEmpty(m_EhloHost)){                
                string text = "Bad sequence of commands: send EHLO/HELO first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            if(m_pFrom == null){     
                string text = "Bad sequence of commands: send 'MAIL FROM:' first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }
            if(m_pTo.Count == 0){     
                string text = "Bad sequence of commands: send 'RCPT TO:' first.";
                await SendResponseAsync(new SMTP_ServerResponse(503,new SMTP_t_EnhancedStatusCode(5,5,3),text));

                return;
            }

            int chunkSize = 0;
            bool last     = false;
            string[] args = cmdText.Split(' ');
            if(cmdText == string.Empty || args.Length > 2){
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }
            if(!int.TryParse(args[0],out chunkSize)){
                string text = "Syntax error in parameters or arguments.";
                await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                return;
            }
            if(args.Length == 2){
                if(args[1].ToUpperInvariant() != "LAST"){
                    string text = "Syntax error in parameters or arguments.";
                    await SendResponseAsync(new SMTP_ServerResponse(501,new SMTP_t_EnhancedStatusCode(5,5,2),text));

                    return;
                }
                last = true;
            }

            // First BDAT block in transaction.
            if(m_pMessageStream == null){
                // Raise MessageStoringBeginAsync event.
                if(this.MessageStoringBeginAsync != null){
                    var eArgs = new SMTP_e_MessageStoringBegin(this);
                    await this.MessageStoringBeginAsync(eArgs);

                    m_pMessageStream = eArgs.StoreStream;                
                }
                // User didn't specify stream, usedefault stream.
                if(m_pMessageStream == null){
                    m_pMessageStream = new MemoryStreamEx(128000);
                }

                // RFC 5321.4.4 trace info.
                byte[] recevived = CreateReceivedHeader();
                await m_pMessageStream.WriteAsync(recevived);
            }

            Stream storeStream = m_pMessageStream;
            // Maximum allowed message size exceeded, junk all incoming data.
            if((m_BDatReadedCount + chunkSize) > this.Server.MaxMessageSize){
                storeStream = new JunkingStream();
            }

            // Read BDAT chunk.
            this.TcpStream.ReadFixedCount(storeStream,chunkSize);
            m_BDatReadedCount += chunkSize;

            // Maximum allowed message size exceeded.
            if(m_BDatReadedCount > this.Server.MaxMessageSize){
                await SendResponseAsync(new SMTP_ServerResponse(552,null,"Too much mail data."));

                // Raise MessageStoringCancelAsync event.
                if(this.MessageStoringCancelAsync != null){
                    var eArgs = new SMTP_e_MessageStoringCancel(this,m_pMessageStream);
                    await this.MessageStoringCancelAsync(eArgs);        
                }

                // According RFC 3030, client should send RSET and we must wait it and reject transaction commands.
                // If we reset internally, so all transaction commands will be blocked. 
                Reset();
                
                return;
            }
            
            var response = new SMTP_ServerResponse(250,null,"Ok.");

            if(last){
                // Raise MessageStoringCompleteAsync event.
                if(this.MessageStoringCompleteAsync != null){
                    var eArgs = new SMTP_e_MessageStoringComplete(this,m_pMessageStream,response);
                    await this.MessageStoringCompleteAsync(eArgs);

                    response = eArgs.Response;
                }

                // According RFC 3030, client should send RSET and we must wait it and reject transaction commands.
                // If we reset internally, so all transaction commands will be blocked. 
                Reset();
            }
            
            await SendResponseAsync(response); 
        }

        #endregion

        #region method RsetAsync

        private async Task RsetAsync(string cmdText)
        {
            /* RFC 5321 4.1.1.5.
                This command specifies that the current mail transaction will be
                aborted.  Any stored sender, recipients, and mail data MUST be
                discarded, and all buffers and state tables cleared.  The receiver
                MUST send a "250 OK" reply to a RSET command with no arguments.  A
                reset command may be issued by the client at any time.  It is
                effectively equivalent to a NOOP (i.e., it has no effect) if issued
                immediately after EHLO, before EHLO is issued in the session, after
                an end of data indicator has been sent and acknowledged, or
                immediately before a QUIT.  An SMTP server MUST NOT close the
                connection as the result of receiving a RSET; that action is reserved
                for QUIT (see Section 4.1.1.10).
            */
            
            if(m_pMessageStream != null){
                // Raise MessageStoringCancelAsync event.
                if(this.MessageStoringCancelAsync != null){
                    var eArgs = new SMTP_e_MessageStoringCancel(this,m_pMessageStream);
                    await this.MessageStoringCancelAsync(eArgs);        
                }
            }

            Reset();

            await SendResponseAsync(new SMTP_ServerResponse(250,null,"OK."));
        }

        #endregion

        #region method NoopAsync

        private async Task NoopAsync(string cmdText)
        {
            /* RFC 5321 4.1.1.9.
                This command does not affect any parameters or previously entered
                commands.  It specifies no action other than that the receiver send a
                "250 OK" reply.

                This command has no effect on the reverse-path buffer, the forward-
                path buffer, or the mail data buffer, and it may be issued at any
                time.  If a parameter string is specified, servers SHOULD ignore it.

                Syntax:
                    noop = "NOOP" [ SP String ] CRLF
            */

            await SendResponseAsync(new SMTP_ServerResponse(250,null,"OK."));
        }

        #endregion

        #region method QuitAsync

        private async Task QuitAsync(string cmdText)
        {
            /* RFC 5321 4.1.1.10.
                This command specifies that the receiver MUST send a "221 OK" reply,
                and then close the transmission channel.
              
                The QUIT command may be issued at any time.  Any current uncompleted
                mail transaction will be aborted.
            
                quit = "QUIT" CRLF
            */

            try{
                string text = "<" + Net_Utils.GetLocalHostName(this.LocalHostName) + "> Service closing transmission channel.";
                await SendResponseAsync(new SMTP_ServerResponse([new SMTP_t_ReplyLine(221,text)]));                              
            }
            catch{
            }
            
            await DisconnectAsync();
        }

        #endregion


        private bool IsValidEmailSyntax(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return false;
            }

            int atIndex = address.IndexOf('@');
    
            // Must have '@', not at the start or end, and only one '@'
            if (atIndex > 0 && atIndex < address.Length - 1 && address.IndexOf('@', atIndex + 1) == -1)
            {
                string localPart = address.Substring(0, atIndex);
                string domainPart = address.Substring(atIndex + 1);

                // Basic domain hygiene: must contain a dot, not start/end with dot or hyphen, no spaces
                bool hasDot = domainPart.Contains('.');
                bool validDomainEdges = !domainPart.StartsWith(".") && !domainPart.EndsWith(".") && 
                                        !domainPart.StartsWith("-") && !domainPart.EndsWith("-");
                bool noSpaces = !address.Contains(' ') && !address.Contains('\t');

                if (hasDot && validDomainEdges && noSpaces)
                {
                    return true;
                }
            }

            return false;
        }

        #region method Reset

        /// <summary>
        /// Does reset as specified in RFC 5321.
        /// </summary>
        private void Reset()
        {
            if(this.IsDisposed){
                return;
            }

            m_pFrom = null;
            m_pTo.Clear();                    
            m_pMessageStream = null;
            m_BDatReadedCount = 0;
        }

        #endregion

        #region method Supports

        /// <summary>
        /// Determines whether the SMTP session's server advertises support for the
        /// specified extension or capability.
        /// </summary>
        /// <param name="feature">
        /// The SMTP extension or capability token to check.
        /// </param>
        /// <returns>
        /// <c>true</c> if the server lists the specified feature; otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="feature"/> is <c>null</c>.
        /// </exception>
        private bool Supports(string feature)
        {
            if(feature == null){
                throw new ArgumentNullException(nameof(feature));
            }

            return this.Server.Extentions.Contains(feature,StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region method CreateReceivedHeader

        /// <summary>
        /// Creates "Received:" header field. For more info see RFC 5321.4.4.
        /// </summary>
        /// <returns>Returns "Received:" header field.</returns>
        private byte[] CreateReceivedHeader()
        {
            /* 5321 4.4. Trace Information.
                When an SMTP server receives a message for delivery or further
                processing, it MUST insert trace ("time stamp" or "Received")
                information at the beginning of the message content, as discussed in
                Section 4.1.1.4.

               RFC 4954.7. Additional Requirements on Servers.
                As described in Section 4.4 of [SMTP], an SMTP server that receives a
                message for delivery or further processing MUST insert the
                "Received:" header field at the beginning of the message content.
                This document places additional requirements on the content of a
                generated "Received:" header field.  Upon successful authentication,
                a server SHOULD use the "ESMTPA" or the "ESMTPSA" [SMTP-TT] (when
                appropriate) keyword in the "with" clause of the Received header
                field.
               
               http://www.iana.org/assignments/mail-parameters
                ESMTP                SMTP with Service Extensions               [RFC5321]
                ESMTPA               ESMTP with SMTP AUTH                       [RFC3848]
                ESMTPS               ESMTP with STARTTLS                        [RFC3848]
                ESMTPSA              ESMTP with both STARTTLS and SMTP AUTH     [RFC3848]
            */

            ArgumentNullException.ThrowIfNull(this.RemoteEndPoint);

            LumiSoft.Net.Mail.Mail_h_Received received = new LumiSoft.Net.Mail.Mail_h_Received(this.EhloHost!,Net_Utils.GetLocalHostName(this.LocalHostName),DateTime.Now);
            received.From_TcpInfo = new LumiSoft.Net.Mail.Mail_t_TcpInfo(this.RemoteEndPoint.Address,null);
            received.Via = "TCP";
            if(!this.IsAuthenticated && !this.IsSecureConnection){
                received.With = "ESMTP";
            }
            else if(this.IsAuthenticated && !this.IsSecureConnection){
                received.With = "ESMTPA";
            }
            else if(!this.IsAuthenticated && this.IsSecureConnection){
                received.With = "ESMTPS";
            }
            else if(this.IsAuthenticated && this.IsSecureConnection){
                received.With = "ESMTPSA";
            }
            
            return Encoding.UTF8.GetBytes(received.ToString());
        }

        #endregion

        #region method SendResponseAsync

        /// <summary>
        /// Sends the specified SMTP server response to the remote endpoint by writing
        /// its formatted reply lines to the underlying TCP stream and recording the
        /// outgoing data in the session log.
        /// </summary>
        /// <param name="response">
        /// The <see cref="SMTP_ServerResponse"/> instance containing the reply lines
        /// to transmit to the peer.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        internal async Task SendResponseAsync(SMTP_ServerResponse response)
        {
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            string cmdLine = response.ToString();            
            LogAddWrite(Encoding.UTF8.GetByteCount(cmdLine),cmdLine.TrimEnd());
            await this.TcpStream.WriteLineAsync(cmdLine);
        }

        #endregion


        #region mehtod LogAddRead

        /// <summary>
        /// Logs read operation.
        /// </summary>
        /// <param name="size">Number of bytes readed.</param>
        /// <param name="text">Log text.</param>
        public void LogAddRead(long size,string text)
        {
            try{
                if(this.Server.Logger != null){
                    this.Server.Logger.AddRead(
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
        public void LogAddWrite(long size,string text)
        {
            try{
                if(this.Server.Logger != null){
                    this.Server.Logger.AddWrite(
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
        public void LogAddText(string text)
        {
            try{
                if(this.Server.Logger != null){
                    this.Server.Logger.AddText(
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
        public void LogAddException(string text,Exception x)
        {
            try{
                if(this.Server.Logger != null){
                    this.Server.Logger.AddException(
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


        #region Properties implementation

        /// <summary>
        /// Gets the SMTP server instance that owns this session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public new SMTP_Server Server
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return (SMTP_Server)base.Server;
            }
        }

        /// <summary>
        /// Gets the collection of supported SASL authentication mechanisms for
        /// this SMTP server. The returned dictionary maps mechanism names to
        /// their corresponding <see cref="AUTH_SASL_ServerMechanism"/> instances.
        /// Applications may add or remove mechanisms from this collection to
        /// customize the server's available authentication methods.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public Dictionary<string,AUTH_SASL_ServerMechanism> Authentications
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pAuthentications; 
            }
        }

        /// <summary>
        /// Gets the number of invalid or unrecognized SMTP commands received
        /// during this session. This counter is incremented whenever the client
        /// issues a syntactically incorrect command or a command that is not
        /// permitted in the current session state.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public int BadCommands
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_BadCommands; 
            }
        }

        /// <summary>
        /// Gets the number of mail transactions processed during this SMTP
        /// session. A transaction begins with a successful MAIL FROM command
        /// and ends after the message is fully processed (either accepted or
        /// rejected).
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public int Transactions
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_Transactions; 
            }
        }
                
        /// <summary>
        /// Gets the host name reported by the client in the EHLO or HELO command.
        /// Returns <c>null</c> if the client has not issued EHLO or HELO yet.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public string? EhloHost
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_EhloHost; 
            }
        }

        /// <summary>
        /// Gets the identity of the user authenticated for this SMTP session,
        /// or <c>null</c> if no authentication has been performed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public override GenericIdentity? AuthenticatedUserIdentity
        {
	        get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

		        return m_pUser;
	        }
        }
        
        /// <summary>
        /// Gets the value supplied by the client in the MAIL FROM command.
        /// Returns <c>null</c> if MAIL FROM has not been issued in the current
        /// transaction.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public SMTP_t_MailFrom? From
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pFrom; 
            }
        }

        /// <summary>
        /// Gets the collection of recipient addresses supplied by the client
        /// through RCPT TO commands. Returns an array containing all accepted
        /// recipients for the current mail transaction. If no RCPT TO command
        /// has been issued yet, the array is empty.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public SMTP_t_RcptTo[] To
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                lock(m_pTo){
                    SMTP_t_RcptTo[] retVal = new SMTP_t_RcptTo[m_pTo.Count];
                    m_pTo.Values.CopyTo(retVal,0);

                    return retVal;
                }
            }
        }

        #endregion

        #region Events implementation

        /// <summary>
        /// Occurs when a new SMTP session is created, before the server sends
        /// its initial 220 greeting banner.  This event allows the application
        /// to inspect the session, customize the greeting <see cref="SMTP_ServerResponse"/>,
        /// or reject the connection entirely by returning an error response.
        /// </summary>
        /// <remarks>
        /// The event is raised immediately after the TCP connection is accepted
        /// and before any protocol-level data is transmitted to the client.
        /// Handlers may replace the default greeting banner or return a non-2xx
        /// reply to terminate the session.
        /// </remarks>
        public event Func<SMTP_e_Started,Task>? StartedAsync = null;

        /// <summary>
        /// Occurs when the server receives the EHLO command from the client.
        /// The event is raised after the command syntax has been validated but
        /// before the server sends its 250 reply lines.  Handlers may inspect
        /// the session, modify the default <see cref="SMTP_ServerResponse"/>,
        /// add or remove advertised SMTP extensions, or reject the EHLO command
        /// by returning an error response.
        /// </summary>
        /// <remarks>
        /// The event provides full access to the parsed EHLO domain and the
        /// server-generated reply lines.  Applications may customize the
        /// advertised capabilities (for example, conditionally exposing AUTH
        /// mechanisms only when TLS is active) or override the response entirely.
        /// </remarks>
        public event Func<SMTP_e_Ehlo,Task>? EhloAsync = null;

        /// <summary>
        /// Occurs when the server receives the HELO command from the client.
        /// The event is raised after the command syntax has been validated but
        /// before the server sends its 250 reply.  Handlers may inspect the
        /// session, modify the default <see cref="SMTP_ServerResponse"/>, or
        /// reject the HELO command by returning an error response.
        /// </summary>
        /// <remarks>
        /// HELO is the simple greeting command defined by RFC 5321.  Unlike EHLO,
        /// it does not negotiate extensions.  Applications may use this event to
        /// customize the greeting reply or enforce connection policies before the
        /// session proceeds.
        /// </remarks>
        public event Func<SMTP_e_Helo,Task>? HeloAsync = null;

        /// <summary>
        /// Occurs when the server receives the MAIL FROM command. The event is
        /// raised after the command syntax and address have been parsed but
        /// before the server sends its reply. Handlers may inspect the sender
        /// address, apply acceptance policies, modify the default
        /// <see cref="SMTP_ServerResponse"/>, or reject the MAIL FROM command
        /// by returning an error response.
        /// </summary>
        /// <remarks>
        /// The MAIL FROM command initializes a new message transaction. This
        /// event provides access to the envelope sender, optional parameters
        /// (such as SIZE or BODY), and the current SMTP session state. The
        /// application may enforce sender validation, rate limits, size limits,
        /// or other policies before the transaction proceeds.
        /// </remarks>
        public event Func<SMTP_e_MailFrom,Task>? MailFromAsync = null;

        /// <summary>
        /// Occurs when the server receives the RCPT TO command. The event is
        /// raised after the command syntax, address, and parameters have been
        /// parsed but before the server sends its reply. Handlers may inspect
        /// the recipient address, apply acceptance policies, modify the default
        /// <see cref="SMTP_ServerResponse"/>, or reject the RCPT TO command by
        /// returning an error response.
        /// </summary>
        /// <remarks>
        /// The RCPT TO command adds a recipient to the current message
        /// transaction. This event provides access to the envelope recipient,
        /// optional parameters (such as NOTIFY or ORCPT), and the current SMTP
        /// session state. Applications may enforce recipient validation,
        /// forwarding rules, relay restrictions, quota limits, or other policies
        /// before the recipient is accepted.
        /// </remarks>
        public event Func<SMTP_e_RcptTo,Task>? RcptToAsync = null;

        /// <summary>
        /// Occurs when the server is about to begin receiving message content
        /// for a DATA or BDAT command. The event is raised after the command
        /// has been accepted and before any message data is read. No server
        /// reply is sent at this stage, and the message transfer cannot be
        /// aborted here. Handlers may inspect the session and initialize
        /// storage resources in preparation for reading the message content.
        /// </summary>
        /// <remarks>
        /// For DATA, this event is raised immediately after the server has sent
        /// the 354 reply and before the first byte of message data is read.
        /// For BDAT, it is raised after the BDAT command is parsed and accepted.
        /// The application may allocate buffers, open storage streams, or perform
        /// other initialization tasks required for processing the incoming data.
        /// </remarks>
        public event Func<SMTP_e_MessageStoringBegin,Task>? MessageStoringBeginAsync = null;

        /// <summary>
        /// Occurs when the server aborts a DATA or BDAT message transfer before
        /// completion. This event is raised if the client closes the connection,
        /// the session times out while reading message data, or the incoming
        /// message exceeds the server's configured maximum size. The transfer
        /// cannot continue once this event is raised.
        /// </summary>
        /// <remarks>
        /// The event is triggered during the message data phase, after the server
        /// has begun reading content but before a successful terminating sequence
        /// (CRLF.CRLF for DATA or the final BDAT chunk) is received. Handlers may
        /// inspect any partial message data that was collected and perform cleanup
        /// or logging. The server will send the appropriate SMTP error reply after
        /// the handler returns.
        /// </remarks>
        public event Func<SMTP_e_MessageStoringCancel,Task>? MessageStoringCancelAsync = null;

        /// <summary>
        /// Occurs when the server has successfully finished receiving all message
        /// content for a DATA or BDAT command. The event is raised after the
        /// terminating sequence (CRLF.CRLF for DATA or the final BDAT chunk) has
        /// been fully read and before the server sends its final reply. Handlers
        /// may inspect the completed message data, perform final processing, or
        /// reject the message by returning a non-success <see cref="SMTP_ServerResponse"/>.
        /// </summary>
        /// <remarks>
        /// This event indicates a normal, successful end of the message data
        /// phase. For DATA, it is raised after the terminating dot sequence is
        /// received. For BDAT, it is raised after the last chunk marked with
        /// the LAST flag has been completely read. Applications may finalize
        /// storage, commit the message to a queue, update metadata, or reject
        /// the message before the server sends its final acceptance response.
        /// </remarks>
        public event Func<SMTP_e_MessageStoringComplete,Task>? MessageStoringCompleteAsync = null;
        
        #endregion

    }
}
