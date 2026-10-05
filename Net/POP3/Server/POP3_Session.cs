using LumiSoft.Net.AUTH;
using LumiSoft.Net.IO;
using LumiSoft.Net.SMTP;
using LumiSoft.Net.SMTP.Server;
using LumiSoft.Net.TCP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace LumiSoft.Net.POP3.Server
{
    /// <summary>
    /// This class implements POP3 server session. Defined RFC 1939.
    /// </summary>
    public class POP3_Session : TCP_ServerSession
    {
        private Dictionary<string,AUTH_SASL_ServerMechanism>  m_pAuthentications;
        private bool                                          m_SessionRejected  = false;
        private int                                           m_BadCommands      = 0;
        private string?                                       m_UserName         = null;
        private GenericIdentity?                              m_pUser            = null;
        private List<POP3_ServerMessage>                      m_pMessages;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public POP3_Session()
        {
            m_pAuthentications = new Dictionary<string,AUTH_SASL_ServerMechanism>(StringComparer.OrdinalIgnoreCase);
            m_pMessages = new List<POP3_ServerMessage>();
        }


        #region override method Start

        /// <summary>
        /// Starts session processing.
        /// </summary>
        protected override void Start()
        {
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

                // Permanent error.
                if(x is IOException || x is SocketException){
                    Dispose();
                }
                // Unknown error.
                else{
                    // Raise POP3_Server.Error event.
                    await base.OnErrorAsync(x);

                    // Try to send "500 Internal server error."
                    try{
                        string text = "Internal server error.";
                        await SendResponseAsync(new POP3_ServerResponse("-ERR",null,text));
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
        /// This method is called when specified session times out.
        /// </summary>
        /// <remarks>
        /// This method allows inhereted classes to report error message to connected client.
        /// Session will be disconnected after this method completes.
        /// </remarks>
        protected override async Task OnTimeoutAsync()
        {
            try{
                string text = "Idle timeout, closing connection.";
                var sendTask =  SendResponseAsync(new POP3_ServerResponse("-ERR",null,text));
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
            /* RFC 1939 4.
                Once the TCP connection has been opened by a POP3 client, the POP3
                server issues a one line greeting.  This can be any positive
                response.  An example might be:

                    S:  +OK POP3 server ready
            */

            try{
                var response = new POP3_ServerResponse("+OK",null,Net_Utils.GetLocalHostName(this.LocalHostName) + " POP3 server ready.");
                if(!string.IsNullOrEmpty(this.Server.GreetingText)){
                    response = new POP3_ServerResponse("+OK",null,this.Server.GreetingText);
                }

                // Raise event StartedAsync.
                if(this.StartedAsync != null){
                    POP3_e_Started eArgs = new POP3_e_Started(this,response);
                    await this.StartedAsync(eArgs);

                    response = eArgs.Response;
                }

                await SendResponseAsync(response);

                // Session rejected flag, so we respond "-ERR Session rejected." any command except QUIT.
                if(!response.IsSuccess){
                    m_SessionRejected = true;
                }

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

                    string[] cmd_args = line.Split(new char[]{' '},2);
                    string   cmd      = cmd_args[0].ToUpperInvariant();
                    string   args     = cmd_args.Length == 2 ? cmd_args[1] : "";

                    // Hide password from log.
                    if(cmd == "PASS"){
                        LogAddRead(responseline.BytesInBuffer,"PASS <***REMOVED***>");
                    }
                    else{
                        LogAddRead(responseline.BytesInBuffer,line);
                    }

                    if(cmd == "STLS"){
                        await StlsAsync(args);
                    }
                    else if(cmd == "USER"){
                        await UserAsync(args);
                    }
                    else if(cmd == "PASS"){
                        await PassAsync(args);
                    }
                    else if(cmd == "AUTH"){
                        await AuthAsync(args);
                    }
                    else if(cmd == "STAT"){
                        await StatAsync(args);
                    }
                    else if(cmd == "LIST"){
                        await ListAsync(args);
                    }
                    else if(cmd == "UIDL"){
                        await UidlAsync(args);
                    }
                    else if(cmd == "TOP"){
                        await TopAsync(args);
                    }
                    else if(cmd == "RETR"){
                        await RetrAsync(args);
                    }
                    else if(cmd == "DELE"){
                        await DeleAsync(args);
                    }
                    else if(cmd == "RSET"){
                        await ResetAsync(args);
                    }
                    else if(cmd == "NOOP"){
                        await NoopAsync(args);
                    }
                    else if(cmd == "CAPA"){
                        await CapaAsync(args);
                    }
                    else if(cmd == "QUIT"){
                        await QuitAsync(args);
                    }
                    else{
                         m_BadCommands++;

                         // Maximum allowed bad commands exceeded.
                         if(this.Server.MaxBadCommands != 0 && m_BadCommands > this.Server.MaxBadCommands){
                             response = new POP3_ServerResponse("-ERR",null,"Too many bad commands, closing connection.");
                             await SendResponseAsync(response);
                             Disconnect();

                             return;
                         }
                            
                         response = new POP3_ServerResponse("-ERR",null,"Error: command '" + cmd + "' not recognized.");
                         await SendResponseAsync(response);
                    }
                }
            }
            catch(Exception x){
                OnError(x);
            }
        }

        #endregion


        #region method StlsAsync

        private async Task StlsAsync(string cmdText)
        {
            /* RFC 2595 4. POP3 STARTTLS extension.
                 Arguments: none

                 Restrictions:
                     Only permitted in AUTHORIZATION state.

                 Discussion:
                     A TLS negotiation begins immediately after the CRLF at the
                     end of the +OK response from the server.  A -ERR response
                     MAY result if a security layer is already active.  Once a
                     client issues a STLS command, it MUST NOT issue further
                     commands until a server response is seen and the TLS
                     negotiation is complete.

                     The STLS command is only permitted in AUTHORIZATION state
                     and the server remains in AUTHORIZATION state, even if
                     client credentials are supplied during the TLS negotiation.
                     The AUTH command [POP-AUTH] with the EXTERNAL mechanism
                     [SASL] MAY be used to authenticate once TLS client
                     credentials are successfully exchanged, but servers
                     supporting the STLS command are not required to support the
                     EXTERNAL mechanism.

                     Once TLS has been started, the client MUST discard cached
                     information about server capabilities and SHOULD re-issue
                     the CAPA command.  This is necessary to protect against
                     man-in-the-middle attacks which alter the capabilities list
                     prior to STLS.  The server MAY advertise different
                     capabilities after STLS.

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

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }
            if(this.IsAuthenticated){
                var response = new POP3_ServerResponse("-ERR",null,"This command is only valid in AUTHORIZATION state (RFC 2595 4).");
                await SendResponseAsync(response);

                return;
            }
            if(this.IsSecureConnection){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Connection is already secure.");
                await SendResponseAsync(response);

                return;
            }
            if(this.Certificate == null){
                var response = new POP3_ServerResponse("-ERR",null,"TLS not available: Server has no SSL certificate.");
                await SendResponseAsync(response);

                return;
            }

            await SendResponseAsync(new POP3_ServerResponse("+OK",null,"Ready to start TLS."));

            try{
                await SwitchToSecureAsync();

                // Log
                LogAddText("TLS negotiation completed successfully.");
            }
            catch(Exception x){
                // Log
                LogAddText("TLS negotiation failed: " + x.Message + ".");

                Disconnect();
            }
        }

        #endregion

        #region method UserAsync

        private async Task UserAsync(string cmdText)
        {
            /* RFC 1939 7. USER
			    Arguments:
				    a string identifying a mailbox (required), which is of
				    significance ONLY to the server
				
			    NOTE:
				    If the POP3 server responds with a positive
				    status indicator ("+OK"), then the client may issue
				    either the PASS command to complete the authentication,
				    or the QUIT command to terminate the POP3 session.			 
			*/

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }
            if(this.IsAuthenticated){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Already authenticated."));

                return;
            }
            if(m_UserName != null){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"User name already specified."));

                return;
            }
            if(string.IsNullOrEmpty(cmdText)){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"-ERR Error in arguments."));

                return;
            }

            m_UserName = cmdText;

            await SendResponseAsync(new POP3_ServerResponse("+OK",null,"User name OK."));
        }

        #endregion

        #region method PassAsync

        private async Task PassAsync(string cmdText)
        {
            /* RFC 1939 7. PASS
			Arguments:
				a server/mailbox-specific password (required)
				
			Restrictions:
				may only be given in the AUTHORIZATION state immediately
				after a successful USER command
				
			NOTE:
				When the client issues the PASS command, the POP3 server
				uses the argument pair from the USER and PASS commands to
				determine if the client should be given access to the
				appropriate maildrop.
				
			Possible Responses:
				+OK maildrop locked and ready
				-ERR invalid password
				-ERR unable to lock maildrop
						
			*/

            if(m_SessionRejected){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected."));

                return;
            }
            if(this.IsAuthenticated){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Already authenticated."));

                return;
            }
            if(m_UserName == null){                
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Specify user name first."));

                return;
            }
            if(string.IsNullOrEmpty(cmdText)){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Error in arguments."));

                return;
            }

            var response = new POP3_ServerResponse("-ERR",null,"Authentication failed.");
            POP3_e_AuthUserPass eArgsAuth = new POP3_e_AuthUserPass(this,m_UserName,cmdText,response);
            // Raise event AuthUserPassAsync, if no event handler specified, we can't authenticate user.
            if (this.AuthUserPassAsync != null){
                await this.AuthUserPassAsync(eArgsAuth);
                response = eArgsAuth.Response;
            }                        
            if(response.IsSuccess){
                m_pUser = new GenericIdentity(m_UserName,"POP3-USER/PASS");

                // Get mailbox messages info.
                var eArgsLoad = new POP3_e_LoadMessagesInfo(this);
                // Raise event LoadMessagesInfoAsync.
                if (this.LoadMessagesInfoAsync != null){                    
                    await this.LoadMessagesInfoAsync(eArgsLoad);
                }
                int seqNo = 1;
                foreach(POP3_ServerMessage message in eArgsLoad.Messages){
                    message.SequenceNumber = seqNo++;
                    m_pMessages.Add(message);
                }
            }

            await SendResponseAsync(response);

            m_UserName = null;
        }

        #endregion

        #region method AuthAsync

        private async Task AuthAsync(string cmdText)
        {
            /*
             POP3 AUTH command — RFC 5034 (SASL Authentication for POP3).

             AUTH with no parameters:
               - Client requests the list of SASL mechanisms supported by the server.
               - Server replies with a multi-line response:
                   <mechanism>
                   <mechanism>
                   .
               - No authentication attempt is made in this form.

             AUTH <mechanism>:
               - Begins SASL authentication using the specified mechanism.
               - Server may send an initial challenge depending on the mechanism.
               - Client and server exchange SASL data until authentication succeeds or fails.

             State rules:
               - AUTH is valid only in the AUTHORIZATION state.
               - On successful SASL authentication, the session transitions to TRANSACTION.
               - On failure, the session remains in AUTHORIZATION.

             Interaction with USER/PASS:
               - AUTH replaces USER/PASS when SASL is used.
               - If the client issues AUTH, it must complete SASL authentication before
                 attempting USER/PASS.
               - Supported SASL mechanisms may be advertised via CAPA (e.g., "SASL PLAIN").

             Error handling:
               - If the mechanism is unsupported, the server must return an error.
               - If SASL negotiation fails, the server must return an error and remain
                 in AUTHORIZATION.

             Notes:
               - AUTH is an extension defined in RFC 5034, not part of RFC 1939.
               - Mechanism names are case-insensitive.
               - Server must not send message data during SASL negotiation.
            */

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }
            if(this.IsAuthenticated){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Already authenticated."));

                return;
            }

            string mechanism = cmdText;
            if(string.IsNullOrEmpty(mechanism)){
                StringBuilder resp = new StringBuilder();
                resp.Append("+OK\r\n");
                foreach(AUTH_SASL_ServerMechanism authMechanism in m_pAuthentications.Values){
                    if(!authMechanism.RequireSSL || (authMechanism.RequireSSL && this.IsSecureConnection)){
                        resp.Append(authMechanism.Name + "\r\n");
                    }                    
                }
                resp.Append(".\r\n");

                string responseString = resp.ToString();            
                LogAddWrite(Encoding.UTF8.GetByteCount(responseString),responseString.TrimEnd());
                await this.TcpStream.WriteLineAsync(responseString);

                return;
            }

            AUTH_SASL_ServerMechanism? auth = null;
            if(!m_pAuthentications.TryGetValue(mechanism,out auth)){
                string text = "Unrecognized authentication type.";
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,text));

                return;
            }

            if(auth.RequireSSL && !this.IsSecureConnection){
                string text = "Encryption required for requested authentication mechanism.";
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,text));

                return;
            }

            byte[] clientResponse = [];
            auth.Reset();
            while(true){
                byte[]? serverResponse = auth.Continue(clientResponse);
                // Authentication completed.
                if(auth.IsCompleted){
                    if(auth.IsAuthenticated){
                        m_pUser = new GenericIdentity(auth.UserName,"SASL-" + auth.Name);

                        // Get mailbox messages info.
                        var eArgsLoad = new POP3_e_LoadMessagesInfo(this);
                        // Raise event LoadMessagesInfoAsync.
                        if (this.LoadMessagesInfoAsync != null){                    
                            await this.LoadMessagesInfoAsync(eArgsLoad);
                        }
                        int seqNo = 1;
                        foreach(POP3_ServerMessage message in eArgsLoad.Messages){
                            message.SequenceNumber = seqNo++;
                            m_pMessages.Add(message);
                        }

                        await SendResponseAsync(new POP3_ServerResponse("+OK",null,"Authentication succeeded."));
                    }
                    else{
                        await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Authentication credentials invalid."));
                    }
                    break;
                }
                // Authentication continues.
                else{
                    ArgumentNullException.ThrowIfNull(serverResponse);

                    // Send server challenge.
                    if (serverResponse.Length == 0){
                        await SendResponseAsync(new POP3_ServerResponse("+",null,""));
                    }
                    else{
                        await SendResponseAsync(new POP3_ServerResponse("+",null,Convert.ToBase64String(serverResponse)));
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
                        await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Authentication canceled."));

                        return;
                    }
                    // We have base64 client response, decode it.
                    else{
                        try{
                            clientResponse = Convert.FromBase64String(clientResponseStr);
                        }
                        catch{
                            await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Invalid client response."));

                            return;
                        }
                    }
                }
            }
        }

        #endregion


        #region method StatAsync

        private async Task StatAsync(string cmdText)
        {
            /* RFC 1939 5. STAT
			NOTE:
				The positive response consists of "+OK" followed by a single
				space, the number of messages in the maildrop, a single
				space, and the size of the maildrop in octets.
				
				Note that messages marked as deleted are not counted in
				either total.
			 
			Example:
				C: STAT
				S: +OK 2 320
			*/

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }
            if(!this.IsAuthenticated){
                var response = new POP3_ServerResponse("-ERR",null,"Authentication required.");
                await SendResponseAsync(response);

                return;
            }

            // Calculate count and total size in bytes, exclude marked for deletion messages.
            int count = 0;
            int size  = 0;
            foreach(POP3_ServerMessage msg in m_pMessages){
                if(!msg.IsMarkedForDeletion){
                    count++;
                    size += msg.Size;
                }
            }

            await SendResponseAsync(new POP3_ServerResponse("+OK", null,count + " " + size));
        }

        #endregion

        #region method ListAsync

        private async Task ListAsync(string cmdText)
        {
            /* RFC 1939 5. LIST
			Arguments:
				a message-number (optional), which, if present, may NOT
				refer to a message marked as deleted
			 
			NOTE:
				If an argument was given and the POP3 server issues a
				positive response with a line containing information for
				that message.

				If no argument was given and the POP3 server issues a
				positive response, then the response given is multi-line.
				
				Note that messages marked as deleted are not listed.
			
			Examples:
				C: LIST
				S: +OK 2 messages (320 octets)
				S: 1 120				
				S: 2 200
				S: .
				...
				C: LIST 2
				S: +OK 2 200
				...
				C: LIST 3
				S: -ERR no such message, only 2 messages in maildrop
			 
			*/

            if(m_SessionRejected){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected."));

                return;
            }
            if(!this.IsAuthenticated){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Authentication required."));

                return;
            }

            string[] args = cmdText.Split(' ');

            // List whole mailbox.
            if(string.IsNullOrEmpty(cmdText)){
                // Calculate count and total size in bytes, exclude marked for deletion messages.
                int count = 0;
                int size  = 0;
                foreach(POP3_ServerMessage msg in m_pMessages){
                    if(!msg.IsMarkedForDeletion){
                        count++;
                        size += msg.Size;
                    }
                }

                await SendResponseAsync(new POP3_ServerResponse("+OK",null,count + " messages (" + size + " bytes)."));

                StringBuilder listResponse = new StringBuilder();
                foreach(POP3_ServerMessage msg in m_pMessages){
                    listResponse.Append(msg.SequenceNumber + " " + msg.Size + "\r\n");
                }
                listResponse.Append(".");

                await this.TcpStream.WriteLineAsync(listResponse.ToString());
            }
            // Single message info listing.
            else{
                if(args.Length > 1 || !int.TryParse(args[0], out int messageNumber)){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Error in arguments."));

                    return;
                }
                if(messageNumber < 1){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR", null, "Error in arguments."));
                    return;
                }

                POP3_ServerMessage? msg = messageNumber <= m_pMessages.Count ? m_pMessages[messageNumber - 1] : null;
                if(msg != null){
                    // Block messages marked for deletion.
                    if(msg.IsMarkedForDeletion){
                        await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Invalid operation: Message marked for deletion."));

                        return;
                    }

                    await SendResponseAsync(new POP3_ServerResponse("+OK",null, msg.SequenceNumber + " " + msg.Size));
                }
                else{
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"no such message."));
                }
            }
        }

        #endregion

        #region method UidlAsync

        private async Task UidlAsync(string cmdText)
        {
            /* RFC 1939 UIDL [msg]
			Arguments:
			    a message-number (optional), which, if present, may NOT
				refer to a message marked as deleted
				
			NOTE:
				If an argument was given and the POP3 server issues a positive
				response with a line containing information for that message.

				If no argument was given and the POP3 server issues a positive
				response, then the response given is multi-line.  After the
				initial +OK, for each message in the maildrop, the POP3 server
				responds with a line containing information for that message.	
				
			Examples:
				C: UIDL
				S: +OK
				S: 1 whqtswO00WBw418f9t5JxYwZ
				S: 2 QhdPYR:00WBw1Ph7x7
				S: .
				...
				C: UIDL 2
				S: +OK 2 QhdPYR:00WBw1Ph7x7
				...
				C: UIDL 3
				S: -ERR no such message
			*/

            if(m_SessionRejected){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected."));

                return;
            }
            if(!this.IsAuthenticated){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Authentication required."));

                return;
            }

            string[] args = cmdText.Split(' ');

            // List whole mailbox.
            if(string.IsNullOrEmpty(cmdText)){
                // Calculate count and total size in bytes, exclude marked for deletion messages.
                int count = 0;
                foreach(POP3_ServerMessage msg in m_pMessages){
                    if(!msg.IsMarkedForDeletion){
                        count++;
                    }
                }

                await SendResponseAsync(new POP3_ServerResponse("+OK",null,count + " messages."));

                StringBuilder uidlLresponse = new StringBuilder();
                foreach(POP3_ServerMessage msg in m_pMessages){
                    uidlLresponse.Append(msg.SequenceNumber + " " + msg.UID + "\r\n");
                }
                uidlLresponse.Append(".");

                await this.TcpStream.WriteLineAsync(uidlLresponse.ToString());
            }
            // Single message info listing.
            else{
                if(args.Length > 1 || !int.TryParse(args[0], out int messageNumber)){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Error in arguments."));

                    return;
                }
                if(messageNumber < 1){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR", null, "Error in arguments."));

                    return;
                }

                POP3_ServerMessage? msg = messageNumber <= m_pMessages.Count ? m_pMessages[messageNumber - 1] : null;
                if(msg != null){
                    // Block messages marked for deletion.
                    if(msg.IsMarkedForDeletion){
                        await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Invalid operation: Message marked for deletion."));

                        return;
                    }

                    await SendResponseAsync(new POP3_ServerResponse("+OK",null,msg.SequenceNumber + " " + msg.UID));
                }
                else{
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"no such message."));
                }
            }
        }

        #endregion

        #region method TopAsync

        private async Task TopAsync(string cmdText)
        {
            /* RFC 1939 7. TOP
			    Arguments:
				    a message-number (required) which may NOT refer to to a
				    message marked as deleted, and a non-negative number
				    of lines (required)
		
			    NOTE:
				    If the POP3 server issues a positive response, then the
				    response given is multi-line.  After the initial +OK, the
				    POP3 server sends the headers of the message, the blank
				    line separating the headers from the body, and then the
				    number of lines of the indicated message's body, being
				    careful to byte-stuff the termination character (as with
				    all multi-line responses).
			
			    Examples:
				    C: TOP 1 10
				    S: +OK
				    S: <the POP3 server sends the headers of the
					    message, a blank line, and the first 10 lines
					    of the body of the message>
				    S: .
                    ...
				    C: TOP 100 3
				    S: -ERR no such message
			 
			*/

            if(m_SessionRejected){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected."));

                return;
            }
            if(!this.IsAuthenticated){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Authentication required."));

                return;
            }

            string[] args = cmdText.Split(' ');

            if(args.Length != 2 || !int.TryParse(args[0], out int messageNumber) || !int.TryParse(args[1], out int lineCount)){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Error in arguments."));

                return;
            }
            if(messageNumber < 1) {
                await SendResponseAsync(new POP3_ServerResponse("-ERR", null, "Error in arguments."));

                return;
            }
            if(lineCount < 0) {
                await SendResponseAsync(new POP3_ServerResponse("-ERR", null, "Error in arguments."));

                return;
            }
            
            POP3_ServerMessage? msg = messageNumber <= m_pMessages.Count ? m_pMessages[messageNumber - 1] : null;
            if(msg != null){
                // Block messages marked for deletion.
                if(msg.IsMarkedForDeletion){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Invalid operation: Message marked for deletion."));

                    return;
                }

                POP3_e_GetMessageStream e = new POP3_e_GetMessageStream(this,msg);
                // Raise event GetMessageStreamAsync.
                if (this.GetMessageStreamAsync != null) {
                    await this.GetMessageStreamAsync(e);
                }

                // User didn't provide us message stream, assume that message deleted(for example by IMAP during this POP3 session).
                if(e.MessageStream == null){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"No such message."));
                }
                else{
                    await SendResponseAsync(new POP3_ServerResponse("+OK",null,"Top of message follows."));

                    using var sourceStream = new SmartStream(e.MessageStream,true);

                    // Send message header to client.
                    long bytesStored = await sourceStream.ReadHeaderAsync(this.TcpStream,int.MaxValue,32000,SizeExceededAction.JunkAndThrowException);
                    // Send header terminator line.
                    await this.TcpStream.WriteAsync(new byte[]{(byte)'\r',(byte)'\n'}).ConfigureAwait(false);
                    bytesStored += 2;

                    Memory<byte> buffer         = new Memory<byte>(new byte[32000]);
                    buffer.Span[0] = (byte)'.'; 
                    Memory<byte> readBuffer     = buffer.Slice(1); // Reserve first byte for additional '.'.
                    bool         lastLineCRLF   = true;
                    for(int i = 0; i < lineCount; i++){
                        ReadLineResult result = await sourceStream.ReadLineAsync(readBuffer,SizeExceededAction.ThrowException).ConfigureAwait(false);

                        // We reached end of stream, no more data.
                        if(result.BytesInBuffer == 0){  
                            break;
                        }

                        Memory<byte> lineBuffer;
                        // Period handled line. If line starts with period '.', additional period is added.
                        if(result.LineBytesInBuffer > 0 && buffer.Span[1] == (byte)'.'){
                            // buffer[0] already contains '.'
                            lineBuffer = buffer.Slice(0,result.BytesInBuffer + 1);
                    
                        }
                        // Normal line.
                        else{
                            lineBuffer = buffer.Slice(1,result.BytesInBuffer);
                        }

                        await this.TcpStream.WriteAsync(lineBuffer).ConfigureAwait(false);
                        bytesStored += lineBuffer.Length;

                        lastLineCRLF = false;
                        if(lineBuffer.Length >= 2 && lineBuffer.Span[lineBuffer.Length - 2] == (byte)'\r' && lineBuffer.Span[lineBuffer.Length - 1] == (byte)'\n'){
                            lastLineCRLF = true;
                        }
                    }

                    // Add .CRLF termintqor.
                    if(lastLineCRLF){
                        Memory<byte> crlfDotTerminator = new byte[]{(byte)'.',(byte)'\r',(byte)'\n'};
                        await this.TcpStream.WriteAsync(crlfDotTerminator).ConfigureAwait(false);
                        bytesStored += crlfDotTerminator.Length;
                    }
                    // Last line not including CRLF, add CRLF.CRLF terminator.
                    else{
                        Memory<byte> crlfDotTerminator = new byte[]{(byte)'\r',(byte)'\n',(byte)'.',(byte)'\r',(byte)'\n'};
                        await this.TcpStream.WriteAsync(crlfDotTerminator).ConfigureAwait(false);
                        bytesStored += crlfDotTerminator.Length;
                    }
                    
                    LogAddWrite(bytesStored, "Wrote top of message(" + bytesStored + " bytes).");
                }
            }
            else{
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"No such message."));
            }
        }

        #endregion

        #region method RetrAsync

        private async Task RetrAsync(string cmdText)
        {
            /* RFC 1939 5. RETR
			    Arguments:
				    a message-number (required) which may NOT refer to a
				    message marked as deleted
			 
			    NOTE:
				    If the POP3 server issues a positive response, then the
				    response given is multi-line.  After the initial +OK, the
				    POP3 server sends the message corresponding to the given
				    message-number, being careful to byte-stuff the termination
				    character (as with all multi-line responses).
				
			    Example:
				    C: RETR 1
				    S: +OK 120 octets
				    S: <the POP3 server sends the entire message here>
				    S: .
			
			*/

            if(m_SessionRejected){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected."));

                return;
            }
            if(!this.IsAuthenticated){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Authentication required."));

                return;
            }

            string[] args = cmdText.Split(' ');

            if(args.Length != 1 || !int.TryParse(args[0],out int messageNumber)){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Error in arguments."));

                return;
            }
            if(messageNumber < 1) {
                await SendResponseAsync(new POP3_ServerResponse("-ERR", null, "Error in arguments."));

                return;
            }

            POP3_ServerMessage? msg = messageNumber <= m_pMessages.Count ? m_pMessages[messageNumber - 1] : null;
            if(msg != null){
                // Block messages marked for deletion.
                if(msg.IsMarkedForDeletion){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Invalid operation: Message marked for deletion."));

                    return;
                }

                POP3_e_GetMessageStream e = new POP3_e_GetMessageStream(this,msg);
                // Raise event GetMessageStreamAsync.
                if (this.GetMessageStreamAsync != null) {
                    await this.GetMessageStreamAsync(e);
                }

                // User didn't provide us message stream, assume that message deleted(for example by IMAP during this POP3 session).
                if (e.MessageStream == null){
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"No such message."));
                }
                else{
                    try{
                        await SendResponseAsync(new POP3_ServerResponse("+OK",null,"Message follows."));

                        long countWritten = await this.TcpStream.WritePeriodTerminatedAsync(e.MessageStream,int.MaxValue,32000,SizeExceededAction.JunkAndThrowException);
                        LogAddWrite(countWritten,"Wrote message(" + countWritten + " bytes).");                        
                    }
                    finally{
                        e.MessageStream.Dispose();
                    }                   
                }
            }
            else{
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"No such message."));
            }
        }

        #endregion

        #region method DeleAsync

        private async Task DeleAsync(string cmdText)
        {
            /* RFC 1939 5. DELE
			    Arguments:
				    a message-number (required) which may NOT refer to a
				    message marked as deleted
			 
			    NOTE:
				    The POP3 server marks the message as deleted.  Any future
				    reference to the message-number associated with the message
				    in a POP3 command generates an error.  The POP3 server does
				    not actually delete the message until the POP3 session
				    enters the UPDATE state.
			*/

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }
            if(!this.IsAuthenticated){
                var response = new POP3_ServerResponse("-ERR",null,"Authentication required.");
                await SendResponseAsync(response);

                return;
            }

            string[] args = cmdText.Split(' ');

            if(args.Length != 1 || !int.TryParse(args[0], out int messageNumber)){
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Error in arguments."));

                return;
            }
            if(messageNumber < 1){
                await SendResponseAsync(new POP3_ServerResponse("-ERR", null, "Error in arguments."));

                return;
            }

            POP3_ServerMessage? msg = messageNumber <= m_pMessages.Count ? m_pMessages[messageNumber - 1] : null;
            if(msg != null){  
                if(!msg.IsMarkedForDeletion){
                    msg.SetIsMarkedForDeletion(true);

                    await SendResponseAsync(new POP3_ServerResponse("+OK",null,"Message marked for deletion."));
                }
                else{
                    await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"Message already marked for deletion."));
                }
            }
            else{
                await SendResponseAsync(new POP3_ServerResponse("-ERR",null,"No such message."));
            }
        }

        #endregion

        #region method ResetAsync

        private async Task ResetAsync(string cmdText)
        {
            /* RFC 1939 5. RSET
			Discussion:
				If any messages have been marked as deleted by the POP3
				server, they are unmarked.  The POP3 server then replies
				with a positive response.
			*/

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }
            if(!this.IsAuthenticated){
                var response = new POP3_ServerResponse("-ERR",null,"Authentication required.");
                await SendResponseAsync(response);

                return;
            }

            // Unmark messages marked for deletion.
            foreach(POP3_ServerMessage msg in m_pMessages){
                msg.SetIsMarkedForDeletion(false);
            }

            await SendResponseAsync(new POP3_ServerResponse("+OK",null,""));
        }

        #endregion
        

        #region method NoopAsync

        private async Task NoopAsync(string cmdText)
        {
            /* RFC 1939 5. NOOP
			    NOTE:
				    The POP3 server does nothing, it merely replies with a
				    positive response.
			*/

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }

            await SendResponseAsync(new POP3_ServerResponse("+OK",null,""));
        }

        #endregion

        #region method CapaAsync

        private async Task CapaAsync(string cmdText)
        {
            /* RFC 2449 5.  The CAPA Command
			
				The POP3 CAPA command returns a list of capabilities supported by the
				POP3 server.  It is available in both the AUTHORIZATION and
				TRANSACTION states.

				A capability description MUST document in which states the capability
				is announced, and in which states the commands are valid.

				Capabilities available in the AUTHORIZATION state MUST be announced
				in both states.

				If a capability is announced in both states, but the argument might
				differ after authentication, this possibility MUST be stated in the
				capability description.

				(These requirements allow a client to issue only one CAPA command if
				it does not use any TRANSACTION-only capabilities, or any
				capabilities whose values may differ after authentication.)

				If the authentication step negotiates an integrity protection layer,
				the client SHOULD reissue the CAPA command after authenticating, to
				check for active down-negotiation attacks.

				Each capability may enable additional protocol commands, additional
				parameters and responses for existing commands, or describe an aspect
				of server behavior.  These details are specified in the description
				of the capability.
				
				Section 3 describes the CAPA response using [ABNF].  When a
				capability response describes an optional command, the <capa-tag>
				SHOULD be identical to the command keyword.  CAPA response tags are
				case-insensitive.

				CAPA

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

            if(m_SessionRejected){
                var response = new POP3_ServerResponse("-ERR",null,"Bad sequence of commands: Session rejected.");
                await SendResponseAsync(response);

                return;
            }

            StringBuilder capaResponse = new StringBuilder();
			capaResponse.Append("+OK Capability list follows\r\n");
			capaResponse.Append("PIPELINING\r\n");
			capaResponse.Append("UIDL\r\n");
			capaResponse.Append("TOP\r\n");

            StringBuilder sasl = new StringBuilder();
            foreach(AUTH_SASL_ServerMechanism authMechanism in this.Authentications.Values){
                if(!authMechanism.RequireSSL || (authMechanism.RequireSSL && this.IsSecureConnection)){
                    sasl.Append(authMechanism.Name + " ");
                }
            }
            if(sasl.Length > 0){
                capaResponse.Append("SASL " + sasl.ToString().Trim() + "\r\n");
            }

            if(!this.IsSecureConnection && this.Certificate != null){
                capaResponse.Append("STLS\r\n");
            }
			capaResponse.Append(".\r\n");

            string capaResponseString = capaResponse.ToString();            
            LogAddWrite(Encoding.UTF8.GetByteCount(capaResponseString),capaResponseString.TrimEnd());
            await this.TcpStream.WriteLineAsync(capaResponseString);
        }

        #endregion

        #region method QuitAsync

        private async Task QuitAsync(string cmdText)
        {
            /* RFC 1939 6. QUIT
			   NOTE:
                When the client issues the QUIT command from the TRANSACTION state,
				the POP3 session enters the UPDATE state.  (Note that if the client
				issues the QUIT command from the AUTHORIZATION state, the POP3
				session terminates but does NOT enter the UPDATE state.)

				If a session terminates for some reason other than a client-issued
				QUIT command, the POP3 session does NOT enter the UPDATE state and
				MUST not remove any messages from the maildrop.
             
				The POP3 server removes all messages marked as deleted
				from the maildrop and replies as to the status of this
				operation.  If there is an error, such as a resource
				shortage, encountered while removing messages, the
				maildrop may result in having some or none of the messages
				marked as deleted be removed.  In no case may the server
				remove any messages not marked as deleted.

				Whether the removal was successful or not, the server
				then releases any exclusive-access lock on the maildrop
				and closes the TCP connection.
			*/

            try{                
                if(this.IsAuthenticated){
                    // Delete messages marked for deletion.
                    List<POP3_ServerMessage> messagesToDelete = new List<POP3_ServerMessage>();
                    foreach (POP3_ServerMessage msg in m_pMessages){
                        if(msg.IsMarkedForDeletion){
                            messagesToDelete.Add(msg);
                        }
                    }

                    // Raise event DeleteMessagesAsync.
                    if (messagesToDelete.Count > 0 && this.DeleteMessagesAsync != null){
                        await this.DeleteMessagesAsync(new POP3_e_DeleteMessages(this,messagesToDelete.ToArray()));
                    }
                }

                await SendResponseAsync(new POP3_ServerResponse("+OK",null,"POP3 server signing off."));                
            }
            catch{
            }
            Disconnect();
            Dispose();
        }

        #endregion

        

        #region method SendResponseAsync

        /// <summary>
        /// Sends the specified POP3 server response to the remote endpoint and recording the
        /// outgoing data in the session log.
        /// </summary>
        /// <param name="response">
        /// The <see cref="POP3_ServerResponse"/> instance containing the response
        /// to transmit to the peer.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        internal async Task SendResponseAsync(POP3_ServerResponse response)
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
        /// Logs specified text.
        /// </summary>
        /// <param name="text">text to log.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>text</b> is null reference.</exception>
        public void LogAddText(string text)
        {
            if(text == null){
                throw new ArgumentNullException("text");
            }

            // Log
            if(this.Server.Logger != null){
                this.Server.Logger.AddText(this.ID,text);
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the POP3 server instance that owns this session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if this object has been disposed and the property is accessed.
        /// </exception>
        public new POP3_Server Server
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return (POP3_Server)base.Server;
            }
        }

        /// <summary>
        /// Gets the collection of supported SASL authentication mechanisms for
        /// this POP3 server. The returned dictionary maps mechanism names to
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
        /// Gets the number of invalid or unrecognized POP3 commands received
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
        /// Gets the identity of the user authenticated for this POP3 session,
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

        #endregion

        #region Events implementation
        
        /// <summary>
        /// Raised when a new POP3 session is created, before the server sends its initial
        /// greeting banner. Event handlers may customize the greeting or reject the
        /// connection by modifying the provided <see cref="POP3_ServerResponse"/>.
        /// </summary>
        public event Func<POP3_e_Started,Task>? StartedAsync = null;

        /// <summary>
        /// Raised when the server requires USER/PASS authentication for the current POP3 session.
        /// This event is triggered after receiving the USER and PASS commands and before entering
        /// the TRANSACTION state.
        /// </summary>
        public event Func<POP3_e_AuthUserPass,Task>? AuthUserPassAsync = null;

        /// <summary>
        /// Raised after successful USER/PASS or AUTH authentication when the server needs
        /// to retrieve message metadata (sizes and UIDLs) for the authenticated POP3 session.
        /// Handlers must populate the message list so the server can enter the TRANSACTION
        /// state and serve STAT,LIST, UIDL, RETR, TOP and DELE commands.
        /// </summary>
        public event Func<POP3_e_LoadMessagesInfo,Task>? LoadMessagesInfoAsync = null;

        /// <summary>
        /// Event that is raised when the POP3 server needs to obtain a readable
        /// message stream for a specific message. This event is used by both the
        /// <c>RETR</c> and <c>TOP</c> commands, allowing the server to expose a
        /// single retrieval mechanism regardless of how much of the message the
        /// client intends to read.
        /// 
        /// The event handler must provide a stream containing the full RFC‑822
        /// message (headers followed by body). The POP3 server will read from the
        /// returned stream according to the command semantics:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <c>RETR</c> — the server reads the entire stream and sends the complete
        /// message to the client.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>TOP</c> — the server reads headers and the requested number of body
        /// lines, then stops reading.
        /// </description>
        /// </item>
        /// </list>
        /// 
        /// The handler is responsible for opening the stream and ensuring it
        /// remains readable for the duration of the operation. The server will
        /// dispose the stream when message transmission is complete.
        /// </summary>
        /// <remarks>
        /// Implementations may return any readable <see cref="Stream"/> including
        /// file streams, memory streams, or custom streaming sources. The stream
        /// must contain the message in canonical CRLF format and must not perform
        /// dot‑stuffing; the POP3 server applies dot‑stuffing when sending data to
        /// the client.
        /// </remarks>
        public event Func<POP3_e_GetMessageStream,Task>? GetMessageStreamAsync = null;

        /// <summary>
        /// Raised when the POP3 session ends with a successful <c>QUIT</c> command
        /// and the server requires the host application to delete all messages that
        /// were marked for deletion during the TRANSACTION state.
        /// </summary>
        public event Func<POP3_e_DeleteMessages,Task>? DeleteMessagesAsync = null;
        
        #endregion
    }
}
