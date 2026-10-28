using LumiSoft.Net.AUTH;
using LumiSoft.Net.IO;
using LumiSoft.Net.Mail;
using LumiSoft.Net.MIME;
using LumiSoft.Net.TCP;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace LumiSoft.Net.IMAP.Server
{
    /// <summary>
    /// This class implements IMAP server session. Defined RFC 9051.
    /// </summary>
    public class IMAP_Session : TCP_ServerSession
    {
        #region class _SelectedFolder

        /// <summary>
        /// Represents mailbox state maintained by an IMAP session for the
        /// currently selected mailbox.
        /// </summary>
        /// <remarks>
        /// This class maintains the session's cached mailbox state
        /// including mailbox metadata, message information, mailbox
        /// version, and UID tracking information required for IMAP
        /// operations such as FETCH, SEARCH, EXPUNGE, and mailbox
        /// synchronization.
        /// </remarks>
        private class _SelectedFolder
        {
            private string                 m_Folder;
            private bool                   m_IsReadOnly    = false;
            private List<IMAP_MessageInfo> m_pMessages;
            private long                   m_MaxUid = 0;

            /// <summary>
            /// Initializes a new <see cref="_SelectedFolder"/> instance.
            /// </summary>
            /// <param name="folder">
            /// The selected mailbox name.
            /// </param>
            /// <param name="version">
            /// The mailbox version associated with the selected mailbox.
            /// </param>
            /// <param name="isReadOnly">
            /// Specifies whether the mailbox was selected in read-only mode.
            /// </param>
            /// <param name="messages">
            /// The initial mailbox message information cache.
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="folder"/> or
            /// <paramref name="messages"/> is
            /// <see langword="null"/>.
            /// </exception>
            public _SelectedFolder(string folder,long version,bool isReadOnly,List<IMAP_MessageInfo> messages)
            {
                if(folder == null){
                    throw new ArgumentNullException(nameof(folder));
                }
                if(messages == null){
                    throw new ArgumentNullException(nameof(messages));
                }

                m_Folder     = folder;
                Version      = version;
                m_IsReadOnly = isReadOnly;
                m_pMessages  = messages;

                if(messages.Count > 0){
                    m_MaxUid = messages[messages.Count - 1].UID;
                }
            }
                        

            #region method Filter

            /// <summary>
            /// Gets messages matching the specified sequence set.
            /// </summary>
            /// <param name="uid">
            /// Specifies whether the sequence set contains UIDs or IMAP
            /// sequence numbers.
            /// </param>
            /// <param name="seqSet">
            /// The sequence set to match.
            /// </param>
            /// <returns>
            /// An array containing all matching messages.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="seqSet"/> is
            /// <see langword="null"/>.
            /// </exception>
            internal IMAP_MessageInfo[] Filter(bool uid,IMAP_t_SeqSet seqSet)
            {
                if(seqSet == null){
                    throw new ArgumentNullException("seqSet");
                }

                List<IMAP_MessageInfo> retVal = new List<IMAP_MessageInfo>();
                for(int i=0;i<m_pMessages.Count;i++){
                    IMAP_MessageInfo msgInfo = m_pMessages[i];                    
                    if(uid){
                        if(seqSet.Contains(msgInfo.UID)){
                            retVal.Add(msgInfo);
                        }
                    }
                    else{
                        if(seqSet.Contains(i + 1)){
                            retVal.Add(msgInfo);
                        }
                    }
                }

                return retVal.ToArray();
            }

            #endregion

            #region method AddMessage

            /// <summary>
            /// Adds the specified message to the mailbox cache.
            /// </summary>
            /// <param name="message">
            /// The message information to add.
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="message"/> is
            /// <see langword="null"/>.
            /// </exception>
            internal void AddMessage(IMAP_MessageInfo message)
            {
                if(message == null){
                    throw new ArgumentNullException("message");
                }

                m_MaxUid = message.UID;

                m_pMessages.Add(message);
            }

            #endregion

            #region method RemoveMessage

            /// <summary>
            /// Removes the specified message from the mailbox cache.
            /// </summary>
            /// <param name="message">
            /// The message to remove.
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="message"/> is
            /// <see langword="null"/>.
            /// </exception>
            internal void RemoveMessage(IMAP_MessageInfo message)
            {
                if(message == null){
                    throw new ArgumentNullException("message");
                }

                m_pMessages.Remove(message);
            }

            #endregion

            #region method GetMessage

            /// <summary>
            /// Gets the message with the specified UID.
            /// </summary>
            /// <param name="uid">
            /// The UID of the message to retrieve.
            /// </param>
            /// <returns>
            /// The matching <see cref="IMAP_MessageInfo"/> instance if found;
            /// otherwise, <see langword="null"/>.
            /// </returns>
            public IMAP_MessageInfo? GetMessage(long uid)
            {
                foreach(IMAP_MessageInfo msgInfo in m_pMessages){
                    if(msgInfo.UID == uid){
                        return msgInfo;
                    }
                }
                
                return null;
            }

            #endregion

            #region method GetSeqNo

            /// <summary>
            /// Gets the IMAP 1-based sequence number of the specified
            /// message.
            /// </summary>
            /// <param name="msgInfo">
            /// The message whose sequence number is requested.
            /// </param>
            /// <returns>
            /// The IMAP 1-based sequence number.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="msgInfo"/> is
            /// <see langword="null"/>.
            /// </exception>
            internal int GetSeqNo(IMAP_MessageInfo msgInfo)
            {
                if(msgInfo == null){
                    throw new ArgumentNullException("msgInfo");
                }

                return m_pMessages.IndexOf(msgInfo) + 1;
            }

            /// <summary>
            /// Gets the IMAP 1-based sequence number of the specified
            /// message.
            /// </summary>
            /// <param name="uid">
            /// The UID of the message.
            /// </param>
            /// <returns>
            /// The IMAP 1-based sequence number if the message exists;
            /// otherwise, -1.
            /// </returns>
            internal int GetSeqNo(long uid)
            {
                for(int i=0;i<m_pMessages.Count;i++){
                    if(m_pMessages[i].UID == uid){
                        return i + 1;
                    }
                }

                return -1;
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets the name of the selected mailbox.
            /// </summary>
            public string Folder
            {
                get{ return m_Folder; }
            }

            /// <summary>
            /// Gets a value indicating whether the mailbox is open in
            /// read-only mode.
            /// </summary>
            public bool IsReadOnly
            {
                get{ return m_IsReadOnly; }
            }

            /// <summary>
            /// Gets or sets the mailbox version associated with the selected
            /// mailbox.
            /// </summary>
            /// <remarks>
            /// The IMAP session uses this value to determine whether the
            /// mailbox state has changed and requires synchronization.
            /// </remarks>
            internal long Version
            {
                get;
                set;
            }

            /// <summary>
            /// Gets the messages currently known to exist in the selected
            /// mailbox.
            /// </summary>
            /// <remarks>
            /// The returned collection represents the mailbox state
            /// currently cached by the IMAP session and is used for
            /// message lookup, sequence number calculation,
            /// synchronization, and generation of mailbox update
            /// responses.
            /// </remarks>
            internal IMAP_MessageInfo[] Messages
            {
                get{ return m_pMessages.ToArray(); }
            }

            /// <summary>
            /// Gets the highest UID ever observed by the selected mailbox
            /// cache.
            /// </summary>
            /// <remarks>
            /// This value is used to validate mailbox synchronization data
            /// and detect newly added messages.
            /// <para>
            /// A newly discovered message that does not already exist in the
            /// session cache is generally expected to have a UID greater than
            /// this value.
            /// </para>
            /// <para>
            /// Unlike sequence numbers, IMAP UIDs are monotonically
            /// increasing. Consequently, this value is not reduced when
            /// messages are removed from the mailbox.
            /// </para>
            /// </remarks>
            internal long MaxUid
            {
                get => m_MaxUid;
            }

            #endregion
        }

        #endregion

        private Dictionary<string,AUTH_SASL_ServerMechanism> m_pAuthentications;
        private bool                                         m_SessionRejected  = false;
        private int                                          m_BadCommands      = 0;
        private List<string>                                 m_pCapabilities;
        private char                                         m_FolderSeparator  = '/';
        private GenericIdentity?                             m_pUser            = null;
        private _SelectedFolder?                             m_pSelectedFolder  = null;
        private bool                                         m_Utf8Enabled      = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public IMAP_Session()
        {
            m_pAuthentications = new Dictionary<string,AUTH_SASL_ServerMechanism>(StringComparer.CurrentCultureIgnoreCase);

            m_pCapabilities = new List<string>();
            m_pCapabilities.AddRange(new string[]{"IMAP4rev2","NAMESPACE","QUOTA","ACL","IDLE","ENABLE","UTF8=ACCEPT","SASL-IR","UIDPLUS"});
        }

        #region override method Dispose

        /// <summary>
        /// Cleans up any resource being used.
        /// </summary>
        public override void Dispose()
        {
            if(this.IsDisposed){
                return;
            }
            base.Dispose();

            m_pUser = null;
            m_pSelectedFolder = null;

            // Release events
            this.StartedAsync        = null;
            this.AuthLoginAsync      = null;
            this.NamespaceAsync      = null;
            this.ListAsync           = null;
            this.CreateAsync         = null;
            this.DeleteAsync         = null;
            this.RenameAsync         = null;
            this.LSubAsync           = null;
            this.SubscribeAsync      = null;
            this.UnsubscribeAsync    = null;
            this.StatusAsync         = null;
            this.SelectAsync         = null;
            this.ExamineAsync        = null;
            this.FolderSyncAsync     = null;
            this.AppendBeginAsync    = null;
            //this.AppendCancelAsync   = null; // Don't release it, otherwise pending APPEND message can't be canceled.
            this.AppendCompleteAsync = null;
            this.GetQuotaRootAsync   = null;
            this.GetQuotaAsync       = null;
            this.GetAclAsync         = null;
            this.SetAclAsync         = null;
            this.DeleteAclAsync      = null;
            this.ListRightsAsync     = null;
            this.MyRightsAsync       = null;
            this.FetchAsync          = null;
            this.SearchAsync         = null;
            this.StoreAsync          = null;
            this.CopyAsync           = null;
            this.MoveAsync           = null;
            this.ExpungeAsync        = null;
        }

        #endregion

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
                    // Raise IMAP_Server.Error event.
                    await base.OnErrorAsync(x);

                    // Try to send "* BAD Internal server error."
                    try{
                        await SendResponseAsync(new IMAP_r_u_ServerStatus("BAD","Internal server error: " + x.Message),true);
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
                var sendTask =  SendResponseAsync(new IMAP_r_u_Bye("Idle timeout, closing connection."),true);
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
            /* RFC 9051 7.1.1. Greeting text.
                The untagged form is also used as one of three possible greetings
                at connection startup.

                Example:    S: * OK IMAP4rev2 server ready
            */

            try{
                IMAP_r_u_ServerStatus? response = null;
                if(string.IsNullOrEmpty(this.Server.GreetingText)){
                    response = new IMAP_r_u_ServerStatus("OK","<" + Net_Utils.GetLocalHostName(this.LocalHostName) + "> IMAP server ready.");
                }
                else{
                    response = new IMAP_r_u_ServerStatus("OK",this.Server.GreetingText);
                }
                
                // Raise event StartedAsync.
                if(this.StartedAsync != null){
                    IMAP_e_Started eArgs = new IMAP_e_Started(this,response);
                    await this.StartedAsync(eArgs);

                    response = eArgs.Response;
                }
                
                await SendResponseAsync(response,true);

                // Session rejected flag, so we respond "NO Session rejected." any command except LOGOUT.
                if(!string.Equals(response.ResponseCode,"OK",StringComparison.OrdinalIgnoreCase)){
                    m_SessionRejected = true;
                }

                // Command loop, while QUIT or fatal error happens.
                while(!this.IsDisposed){
                    // Read command line.
                    ReadLineResult readLineResult = await this.TcpStream.ReadLineAsync(new byte[8000],SizeExceededAction.JunkAndThrowException);
                    // Server closed connection.
                    if(readLineResult.BytesInBuffer == 0){
                        LogAddText("The remote host '" + this.RemoteEndPoint?.ToString() + "' closed connection.");
                        Dispose();

                        break;
                    }                    
                    string line = readLineResult.LineUtf8 ?? "";

                    string[] cmd_args = line.Split(new char[]{' '},3);
                    if(cmd_args.Length < 2){
                        await SendResponseAsync(new IMAP_r_u_ServerStatus("BAD","Error: Command '" + line + "' not recognized."),true);

                        continue;
                    }
                    string   cmdTag   = cmd_args[0];
                    string   cmd      = cmd_args[1].ToUpperInvariant();
                    string   args     = cmd_args.Length == 3 ? cmd_args[2] : "";

                    // Log.
                    if(this.Server.Logger != null){
                        bool isDebug = false;
                        #if DEBUG
                            isDebug = true;
                        #endif

                        // Hide password from log.
                        if(!isDebug && cmd == "LOGIN"){  
                            LogAddRead(readLineResult.BytesInBuffer,line.Substring(0,line.LastIndexOf(' ')) + " <***REMOVED***>");
                        }
                        // Hide client initial response from log.
                        else if (!isDebug && cmd == "AUTHENTICATE" && args.Split(' ').Length > 1){
                            LogAddRead(readLineResult.BytesInBuffer,line.Substring(0,line.LastIndexOf(' ')) + " <***REMOVED***>");
                        }
                        else{
                            LogAddRead(readLineResult.BytesInBuffer,line);
                        }
                    }

                    if(cmd == "STARTTLS"){                    
                        await StartTlsAsync(cmdTag,args);
                    }
                    else if(cmd == "LOGIN"){
                        await LoginAsync(cmdTag,args);
                    }
                    else if(cmd == "AUTHENTICATE"){
                        await AuthenticateAsync(cmdTag,args);
                    }
                    else if(cmd == "NAMESPACE"){
                        await _NamespaceAsync(cmdTag,args);
                    }
                    else if(cmd == "LIST"){
                        await _ListAsync(cmdTag,args);
                    }
                    else if(cmd == "CREATE"){
                        await _CreateAsync(cmdTag,args);
                    }
                    else if(cmd == "DELETE"){
                        await _DeleteAsync(cmdTag,args);
                    }
                    else if(cmd == "RENAME"){
                        await _RenameAsync(cmdTag,args);
                    }
                    else if(cmd == "LSUB"){
                        await _LSub(cmdTag,args);
                    }
                    else if(cmd == "SUBSCRIBE"){
                        await _SubscribeAsync(cmdTag,args);
                    }
                    else if(cmd == "UNSUBSCRIBE"){
                        await _UnsubscribeAsync(cmdTag,args);
                    }
                    else if(cmd == "STATUS"){
                        await _StatusAsync(cmdTag,args);
                    }
                    else if(cmd == "SELECT"){
                        await _SelectAsync(cmdTag,args);
                    }
                    else if(cmd == "EXAMINE"){
                        await _ExamineAsync(cmdTag,args);
                    }
                    else if(cmd == "APPEND"){
                        await _AppendAsync(cmdTag,args);
                    }
                    else if(cmd == "GETQUOTAROOT"){
                        await _GetQuotaRootAsync(cmdTag,args);
                    }
                    else if(cmd == "GETQUOTA"){
                        await _GetQuotaAsync(cmdTag,args);
                    }
                    else if(cmd == "GETACL"){
                        await _GetAcl(cmdTag,args);
                    }
                    else if(cmd == "SETACL"){
                        await _SetAclAsync(cmdTag,args);
                    }
                    else if(cmd == "DELETEACL"){
                        await _DeleteAclAsync(cmdTag,args);
                    }
                    else if(cmd == "LISTRIGHTS"){
                        await _ListRightsAsync(cmdTag,args);
                    }
                    else if(cmd == "MYRIGHTS"){
                        await _MyRightsAsync(cmdTag,args);
                    }
                    else if(cmd == "ENABLE"){
                        await _EnableAsync(cmdTag,args);
                    }
                    else if(cmd == "CHECK"){
                        await _CheckAsync(cmdTag,args);
                    }
                    else if(cmd == "CLOSE"){
                        await _CloseAsync(cmdTag,args);
                    }
                    else if(cmd == "FETCH"){
                        await _FetchAsync(false,cmdTag,args);
                    }
                    else if(cmd == "SEARCH"){
                        await _SearchAsync(false,cmdTag,args);
                    }
                    else if(cmd == "STORE"){
                        await _StoreAsync(false,cmdTag,args);
                    }
                    else if(cmd == "COPY"){
                        await _CopyAsync(false,cmdTag,args);
                    }
                    else if(cmd == "MOVE"){
                        await _MoveAsync(false,cmdTag,args);
                    }
                    else if(cmd == "UID"){
                        await _UidAsync(cmdTag,args);
                    }
                    else if(cmd == "EXPUNGE"){
                        await _ExpungeAsync(false,cmdTag,args);
                    }
                    else if(cmd == "IDLE"){
                        await _IdleAsync(cmdTag,args);
                    }
                    else if(cmd == "CAPABILITY"){
                        await CapabilityAsync(cmdTag,args);
                    }
                    else if(cmd == "NOOP"){
                        await NoopAsync(cmdTag,args);
                    }
                    else if(cmd == "LOGOUT"){
                        await LogoutAsync(cmdTag,args);
                        
                        break;
                    }
                    else{
                        m_BadCommands++;

                        // Maximum allowed bad commands exceeded.
                        if(this.Server.MaxBadCommands != 0 && m_BadCommands > this.Server.MaxBadCommands){
                            await SendResponseAsync(new IMAP_r_u_ServerStatus("BYE","Too many bad commands, closing transmission channel."),true);
                            await DisconnectAsync();

                            break;
                        }
                   
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error: Command '" + cmd + "' not recognized."),true);
                    }
                }
            }
            catch(Exception x){
                await OnErrorAsync(x);
            }
        }

        #endregion


        #region method StartTlsAsync

        private async Task StartTlsAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.2.1 STARTTLS Command

             Arguments:
                 none

             Responses:
                 no specific response for this command

             Result:
                 OK  - begin TLS negotiation
                 BAD - command unknown or arguments invalid

             A TLS negotiation begins immediately after the CRLF at the end
             of the tagged OK response from the server.

             After issuing STARTTLS, the client MUST NOT send further IMAP
             commands until TLS negotiation has completed.

             The server remains in the Not Authenticated state after the TLS
             negotiation completes. Client certificates exchanged during TLS
             negotiation may subsequently be used by authentication mechanisms
             such as SASL EXTERNAL.

             The tagged OK response SHOULD contain a CAPABILITY response code
             advertising the capabilities available after TLS has been
             established. Clients SHOULD use these capabilities instead of any
             capabilities obtained before STARTTLS.

             Example:

                 C: a001 CAPABILITY
                 S: * CAPABILITY IMAP4rev2 STARTTLS LOGINDISABLED
                 S: a001 OK CAPABILITY completed

                 C: a002 STARTTLS
                 S: a002 OK [CAPABILITY IMAP4rev2 AUTH=PLAIN] Begin TLS negotiation
                 <TLS negotiation>

                 C: a003 LOGIN joe password
                 S: a003 OK LOGIN completed

             References:
                 RFC 9051 §6.2.1 STARTTLS Command
                 RFC 8446 TLS 1.3
             */

            if(m_SessionRejected){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command rejected: Session rejected."),true);

                return;
            }
            if(this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","This command is only valid in the non-authenticated state."),true);

                return;
            }
            if(this.IsSecureConnection){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","TLS is already active."),true);

                return;
            }
            if(this.Certificate == null){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","TLS not available: No server certificate configured."),true);

                return;
            }

            List<string> capabilities = new List<string>();
            foreach(string c in m_pCapabilities){
                capabilities.Add(c);
            }
            foreach(AUTH_SASL_ServerMechanism auth in this.Authentications.Values){
                capabilities.Add("AUTH=" + auth.Name);
            }
            
            await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK",new IMAP_t_orc_Capability(capabilities.ToArray()),"Begin TLS negotiation now."),true);
            
            try{
                await SwitchToSecureAsync();

                // Log
                LogAddText("TLS negotiation completed successfully.");
            }
            catch(Exception x){
                LogAddException(x);
                Disconnect();
            }
        }

        #endregion

        #region method LoginAsync

        private async Task LoginAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.2.3 LOGIN Command

             Arguments:
                 user name
                 password

             Responses:
                 no specific responses for this command

             Result:
                 OK - login completed, authenticated state entered
                 NO - authentication failed
                 BAD - command unknown or arguments invalid

             The LOGIN command authenticates a client using a user name and
             plaintext password supplied as command arguments.

             A server may include a CAPABILITY response code in the tagged OK
             response to advertise the capabilities available after successful
             authentication.

             Example:

                 C: A001 LOGIN SMITH SESAME
                 S: A001 OK LOGIN completed

             Security Considerations:

                 The LOGIN command transmits credentials in plaintext within the
                 IMAP protocol stream. Use of TLS is strongly recommended to
                 protect credentials from disclosure.

                 Server implementations may restrict or disable LOGIN on
                 unprotected connections as a local security policy.

             References:

                 RFC 9051 §6.2.3 LOGIN Command
            */
            
            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(m_SessionRejected){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command rejected: Session rejected."),true);

                return;
            }
            if(this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Already authenticated."),true);

                return;
            }
            if(SupportsCap("LOGINDISABLED")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","LOGIN command disabled. Use AUTHENTICATE instead."),true);

                return;
            }
            if(string.IsNullOrEmpty(cmdText)){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            string userName;
            string password;
            try{
                userName = await imapReader.ReadStringAsync() ?? "";

                if(imapReader.PeekIs('{')){
                    bool log = false;
                    #if DEBUG
                        log = true;
                    #endif

                    password = Encoding.UTF8.GetString(await imapReader.ReadLiteralAsync(log));
                }
                else{
                    password = await imapReader.ReadStringAsync() ?? "";
                }

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Authentication failed.");

            // Raise AuthLoginAsync event.
            if(this.AuthLoginAsync != null){
                IMAP_e_AuthLogin eArgs = new IMAP_e_AuthLogin(this,userName,password,response);
                await this.AuthLoginAsync(eArgs);

                response = eArgs.Response;
            }

            if(response.IsSuccess){
                m_pUser = new GenericIdentity(userName,"IMAP-LOGIN");
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method AuthenticateAsync

        private async Task AuthenticateAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.2.2 AUTHENTICATE Command

             Arguments:
                 SASL authentication mechanism name

             Responses:
                 continuation request ("+") during authentication exchange

             Result:
                 OK  - authentication completed, authenticated state entered
                 NO  - authentication failed or mechanism unsupported
                 BAD - command unknown, arguments invalid, or authentication
                       exchange cancelled

             The AUTHENTICATE command initiates a SASL authentication exchange
             between the client and server using the specified authentication
             mechanism.

             If the server supports the requested mechanism, authentication data
             is exchanged using server continuation requests and client responses.
             Authentication data is transmitted as Base64-encoded strings.

             A client may cancel an authentication exchange by sending a line
             containing a single '*'. The server then rejects the AUTHENTICATE
             command.

             The SASL service name for IMAP is:

                 "imap"

             SASL Initial Response
             ---------------------

             If the server advertises the SASL-IR capability, mechanisms that
             support an initial client response may include that response in the
             AUTHENTICATE command itself, avoiding an extra challenge/response
             round trip.

             Security Layers
             ---------------

             Some SASL mechanisms can negotiate a security layer. If a security
             layer is negotiated, it takes effect immediately following
             completion of the authentication exchange.

             Capability Advertisement
             -------------------------

             Supported SASL mechanisms are advertised using CAPABILITY response
             entries of the form:

                 AUTH=<mechanism>

             Examples:

                 AUTH=PLAIN
                 AUTH=SCRAM-SHA-256
                 AUTH=EXTERNAL

             A server may include a CAPABILITY response code in the tagged OK
             response of a successful AUTHENTICATE command to advertise the
             capabilities available after authentication.

             Authorization Identity
             ----------------------

             Any authorization identity supplied by the SASL mechanism is
             interpreted by the server as the identity whose privileges the
             client is requesting.

             Example:

                 C: A001 AUTHENTICATE PLAIN
                 S: +
                 C: AHNtaXRoAHNlY3JldA==
                 S: A001 OK AUTHENTICATE completed

             References:

                 RFC 9051 §6.2.2 AUTHENTICATE Command
                 RFC 4422 SASL
            */

            if(m_SessionRejected){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command rejected: Session rejected."),true);

                return;
            }
            if(this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Already authenticated."),true);

                return;
            }

            #region Parse parameters

            string[] arguments = cmdText.Split(' ');
            if(arguments.Length > 2){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }
            byte[] initialClientResponse = new byte[0];
            if(arguments.Length == 2){
                if(arguments[1] == "="){
                    // Skip.
                }
                else{
                    try{
                        initialClientResponse = Convert.FromBase64String(arguments[1]);
                    }
                    catch{
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Syntax error: Parameter 'initial-response' value must be BASE64 or contain a single character '='."),true);

                        return;
                    }
                }
            }
            string mechanism = arguments[0];

            #endregion
                        
            if(!this.Authentications.ContainsKey(mechanism)){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authentication mechanism is not supported."),true);

                return;
            }

            byte[] clientResponse = initialClientResponse;
            AUTH_SASL_ServerMechanism auth = this.Authentications[mechanism];            
            auth.Reset();
            while(true){
                byte[]? serverResponse = auth.Continue(clientResponse);
                // Authentication completed.
                if(auth.IsCompleted){
                    if(auth.IsAuthenticated){
                        m_pUser = new GenericIdentity(auth.UserName,"SASL-" + auth.Name);
                        
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","Authentication succeeded."),true);
                    }
                    else{
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authentication failed."),true);
                    }

                    break;
                }
                // Authentication continues.
                else{
                    ArgumentNullException.ThrowIfNull(serverResponse);

                    // Send server challenge.
                    if (serverResponse.Length == 0){
                        await SendResponseAsync(new IMAP_r_ServerStatus("+",""),true);
                    }
                    else{
                        await SendResponseAsync(new IMAP_r_ServerStatus("+",Convert.ToBase64String(serverResponse)),true);
                    }

                    // Read client response. 
                    var readLineResult = this.TcpStream.ReadLine(new byte[8000],SizeExceededAction.JunkAndThrowException);
                    if(readLineResult.BytesInBuffer == 0){
                        LogAddText("Client closed connection.");

                        throw new IOException("Client closed connection.");
                    }
                    string clientResponseStr = readLineResult.LineUtf8 ?? "";

                    // Log
                    #if DEBUG
                        LogAddRead(readLineResult.BytesInBuffer,clientResponseStr);
                    #else
                        LogAddRead(readLineResult.BytesInBuffer,"Client response recieved.");
                    #endif


                    // Client canceled authentication.
                    if(clientResponseStr == "*"){
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authentication canceled."),true);

                        return;
                    }
                    // We have base64 client response, decode it.
                    else{
                        try{
                            clientResponse = Convert.FromBase64String(clientResponseStr);
                        }
                        catch{
                            await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Invalid client response."),true);

                            return;
                        }
                    }
                }
            }
        }

        #endregion


        #region method _NamespaceAsync

        private async Task _NamespaceAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.3.10. NAMESPACE Command

                Arguments:  none

                Responses:  REQUIRED untagged response:
                                NAMESPACE

                Result:     OK  - command completed
                            NO  - can't complete command
                            BAD - arguments invalid

                The NAMESPACE command causes a single untagged NAMESPACE
                response to be returned. The response contains the prefix and
                hierarchy delimiter for the server's Personal Namespace(s),
                Other Users' Namespace(s), and Shared Namespace(s).

                The response contains NIL for any namespace class that is not
                available.

                Example:

                    C: A001 NAMESPACE
                    S: * NAMESPACE (("" "/")) NIL (("Public Folders/" "/"))
                    S: A001 OK NAMESPACE command completed
            */

            if(!this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(cmdText.Length > 0) {
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"OK","NAMESPACE command completed.");
            
            // Raise NamespaceAsync event.
            if(this.NamespaceAsync != null){
                IMAP_e_Namespace eArgs = new IMAP_e_Namespace(this,response);
                await NamespaceAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    await SendResponseAsync(eArgs.NamespaceResponse,true);
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _ListAsync

        private async Task _ListAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 7.2.2. LIST Command

                Arguments:  reference name
                            mailbox pattern

                Responses:  untagged LIST responses

                Result:     OK  - LIST completed
                            NO  - unable to perform LIST operation
                            BAD - command unknown or arguments invalid

                The LIST command returns information about mailboxes matching
                the supplied reference name and mailbox pattern.

                Response format:

                    * LIST (<attributes>) <hierarchy-delimiter> <mailbox-name>

                Mailbox names are returned as IMAP strings:

                    - quoted string, when representable
                    - literal, when required

                UTF-8 mailbox names are supported natively by IMAP4rev2.
                However, mailbox-name encoding in LIST responses depends on the
                session's negotiated UTF-8 capabilities:

                    UTF-8 enabled:
                        Return mailbox names as UTF-8.

                    UTF-8 not enabled:
                        Return non-ASCII mailbox names using modified UTF-7 for
                        interoperability with legacy clients.

                Common mailbox attributes:

                    \HasChildren
                    \HasNoChildren
                    \Marked
                    \Unmarked
                    \Noselect
                    \Subscribed
                    \Remote

                Wildcards:

                    '%' matches a single hierarchy level.

                    '*' matches zero or more hierarchy levels.

                Examples:

                    C: A001 LIST "" "*"
                    S: * LIST (\HasNoChildren) "/" INBOX
                    S: * LIST (\HasChildren) "/" Archive
                    S: A001 OK LIST completed

                    C: A002 LIST "" "%"
                    S: * LIST (\HasNoChildren) "/" INBOX
                    S: * LIST (\HasChildren) "/" Archive
                    S: A002 OK LIST completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string refName;
            string folder;            
            try{
                refName = await imapReader.ReadMailboxAsync() ?? "";
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){                
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"OK","LIST Completed.");

            // Folder separator request.
            if(folder == string.Empty){
                await SendResponseAsync(new IMAP_r_u_List(m_FolderSeparator),true);
            }
            else{
                // Raise ListAsync event.
                if(this.ListAsync != null){
                    IMAP_e_List eArgs = new IMAP_e_List(this,refName,folder,response);
                    await ListAsync(eArgs);

                    response = eArgs.Response;

                    if(response.IsSuccess){
                        foreach(IMAP_r_u_List r in eArgs.Folders){
                            await SendResponseAsync(r,true);
                        }
                    }
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _CreateAsync

        private async Task _CreateAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.3.3 CREATE Command

             Arguments:
                 mailbox name

             Responses:
                 no specific responses for this command

             Result:
                 OK - mailbox created
                 NO - mailbox cannot be created
                 BAD - command unknown or arguments invalid

             The CREATE command creates a mailbox with the specified name.

             An OK response is returned only if a new mailbox is successfully
             created. Attempting to create an existing mailbox or INBOX fails
             with a tagged NO response.

             If the mailbox name ends with the hierarchy delimiter, the client
             indicates its intention to create child mailboxes beneath that
             mailbox. Servers that do not require this declaration MUST ignore
             the trailing delimiter. The mailbox is created without the trailing
             delimiter.

             If the hierarchy delimiter appears within the mailbox name,
             the server MAY create any required superior hierarchy names
             automatically.

             Examples:

                 C: A003 CREATE Projects/
                 S: A003 OK CREATE completed

                 C: A004 CREATE Projects/IMAP
                 S: A004 OK CREATE completed

             If a mailbox is recreated after deletion, UID assignment rules
             and UIDVALIDITY requirements apply as described by RFC 9051.

             References:

                 RFC 9051 §6.3.3 CREATE Command
                 RFC 9051 §2.3.1.1 UIDVALIDITY
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string folder;            
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise CreateAsync event.
            if(this.CreateAsync != null){
                IMAP_e_Create eArgs = new IMAP_e_Create(this,folder,response);
                await CreateAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _DeleteAsync

        private async Task _DeleteAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.3.4 DELETE Command

             Arguments:
                 mailbox name

             Responses:
                 no specific responses for this command

             Result:
                 OK - mailbox deleted
                 NO - mailbox cannot be deleted
                 BAD - command unknown or arguments invalid

             The DELETE command permanently removes the specified mailbox.

             An OK response is returned only if the mailbox is successfully
             deleted. Attempting to delete a mailbox that does not exist or
             attempting to delete INBOX results in a tagged NO response.

             Deleting a mailbox does not delete any inferior mailboxes. A server
             MUST NOT remove child mailboxes when processing a DELETE command.

             If the deleted mailbox has child mailboxes, the server may preserve
             the mailbox name as a non-selectable hierarchy node as required by
             its mailbox hierarchy implementation.

             UID assignment and UIDVALIDITY requirements apply if a mailbox with
             the same name is subsequently recreated.

             Examples:

                 C: A005 DELETE Projects
                 S: A005 OK DELETE completed

                 C: A006 DELETE INBOX
                 S: A006 NO DELETE failed

             References:

                 RFC 9051 §6.3.4 DELETE Command
                 RFC 9051 §2.3.1.1 UIDVALIDITY
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string folder;            
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise DeleteAsync event.
            if(this.DeleteAsync != null){
                IMAP_e_Delete eArgs = new IMAP_e_Delete(this,folder,response);
                await DeleteAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _RenameAsync

        private async Task _RenameAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.3.5 RENAME Command

             Arguments:
                 existing mailbox name
                 new mailbox name

             Responses:
                 no specific responses for this command

             Result:
                 OK - mailbox renamed
                 NO - mailbox cannot be renamed
                 BAD - command unknown or arguments invalid

             The RENAME command changes the name of an existing mailbox.

             An OK response is returned only if the mailbox is renamed
             successfully. It is an error to rename a mailbox that does not
             exist or to rename a mailbox to a name that already exists.

             If the mailbox has child mailboxes, those child mailboxes MUST
             also be renamed to preserve the mailbox hierarchy.

             If required by the mailbox hierarchy, the server MAY create any
             superior hierarchy names needed to complete the operation.

             Renaming INBOX is permitted and has special semantics. Messages in
             INBOX are moved to the new mailbox and a new, empty INBOX remains
             available. Child mailboxes of INBOX are unaffected.

             UID assignment and UIDVALIDITY requirements apply if the original
             mailbox name is subsequently recreated.

             Examples:

                 C: A007 RENAME Projects ArchivedProjects
                 S: A007 OK RENAME completed

                 C: A008 RENAME INBOX OldInbox
                 S: A008 OK RENAME completed

             References:

                 RFC 9051 §6.3.5 RENAME Command
                 RFC 9051 §2.3.1.1 UIDVALIDITY
            */
            
            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string currentFolder;
            string newFolder;
            try{
                currentFolder  = await imapReader.ReadMailboxAsync() ?? "";
                newFolder      = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise DeleteAsync event.
            if(this.RenameAsync != null){
                IMAP_e_Rename eArgs = new IMAP_e_Rename(this,currentFolder,newFolder,response);
                await RenameAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _LSub

        private async Task _LSub(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.3.9 LSUB Command

             Arguments:
                 reference name
                 mailbox pattern

             Responses:
                 untagged LSUB responses

             Result:
                 OK - LSUB completed
                 NO - unable to list subscriptions
                 BAD - command unknown or arguments invalid

             The LSUB command returns the set of subscribed mailboxes that
             match the supplied reference name and mailbox pattern.

             Zero or more untagged LSUB responses are returned. The arguments
             have the same meaning as those used by the LIST command.

             A server may return mailbox attributes that differ from those
             returned by LIST. If mailbox attributes differ, LIST responses are
             considered authoritative for mailbox state.

             A subscription may exist for a mailbox that no longer exists.
             Servers MUST NOT automatically remove such subscriptions.

             Wildcards:

                 '%' matches a single hierarchy level.
                 '*' matches zero or more hierarchy levels.

             Examples:

                 C: A002 LSUB "#news." "comp.mail.*"
                 S: * LSUB () "." #news.comp.mail.mime
                 S: * LSUB () "." #news.comp.mail.misc
                 S: A002 OK LSUB completed

                 C: A003 LSUB "#news." "comp.%"
                 S: * LSUB (\Noselect) "." #news.comp.mail
                 S: A003 OK LSUB completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string refName;
            string folder;
            try{
                refName = await imapReader.ReadMailboxAsync() ?? "";
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"OK","LSUB Completed.");

            // Raise LSubAsync event.
            if(this.LSubAsync != null){
                IMAP_e_LSub eArgs = new IMAP_e_LSub(this,refName,folder,response);
                await LSubAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    foreach(IMAP_r_u_LSub r in eArgs.Folders){
                        await SendResponseAsync(r,true);
                    }
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _SubscribeAsync

        private async Task _SubscribeAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.3.6 SUBSCRIBE Command

             Arguments:
                 mailbox name

             Responses:
                 no specific responses for this command

             Result:
                 OK - subscription created
                 NO - unable to subscribe to mailbox
                 BAD - command unknown or arguments invalid

             The SUBSCRIBE command adds the specified mailbox to the user's
             subscription list. Subscribed mailboxes are returned by the LSUB
             command.

             An OK response is returned only if the subscription operation
             succeeds.

             A server may validate whether the specified mailbox exists before
             creating the subscription. However, a subscription may remain even
             if the corresponding mailbox no longer exists.

             Servers MUST NOT automatically remove an existing subscription
             solely because the mailbox does not currently exist.

             Example:

                 C: A002 SUBSCRIBE #news.comp.mail.mime
                 S: A002 OK SUBSCRIBE completed

             References:

                 RFC 9051 §6.3.6 SUBSCRIBE Command
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string folder;
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise SubscribeAsync event.
            if(this.SubscribeAsync != null){
                IMAP_e_Subscribe eArgs = new IMAP_e_Subscribe(this,folder,response);
                await SubscribeAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _UnsubscribeAsync

        private async Task _UnsubscribeAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.3.8. UNSUBSCRIBE Command

                Arguments:  mailbox name

                Responses:  no specific responses for this command

                Result:     OK  - unsubscribe completed
                            NO  - unsubscribe failure: can't unsubscribe that name
                            BAD - command unknown or arguments invalid

                The UNSUBSCRIBE command removes the specified mailbox name from
                the server's set of "active" or "subscribed" mailboxes as returned
                by the LIST (SUBSCRIBED) command.

                This command returns a tagged OK response if the unsubscription
                is successful or if the mailbox is not subscribed.

                Example:

                    C: A002 UNSUBSCRIBE #news.comp.mail.mime
                    S: A002 OK UNSUBSCRIBE completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string folder;
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise UnsubscribeAsync event.
            if(this.UnsubscribeAsync != null){
                IMAP_e_Unsubscribe eArgs = new IMAP_e_Unsubscribe(this,folder,response);
                await UnsubscribeAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _StatusAsync

        private async Task _StatusAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.3.11. STATUS Command

                Arguments:  mailbox name
                            status data item names

                Responses:  untagged STATUS response

                Result:     OK  - STATUS completed
                            NO  - STATUS failure: no status for that mailbox
                            BAD - command unknown or arguments invalid

                The STATUS command requests the status of the specified mailbox
                without selecting it. The command does not change the currently
                selected mailbox and does not affect the state of any messages in
                the queried mailbox.

                Unlike the LIST command, the STATUS command does not accept
                wildcards and is not guaranteed to be fast. Some implementations
                may need to perform additional mailbox access in order to obtain
                the requested status information.

                IMAP4rev2 (RFC 9051) defines the following status data items:

                    MESSAGES
                        The number of messages in the mailbox.

                    UIDNEXT
                        The next unique identifier value of the mailbox.

                    UIDVALIDITY
                        The unique identifier validity value of the mailbox.

                    UNSEEN
                        The number of messages that do not have the \Seen flag.

                    DELETED
                        The number of messages that have the \Deleted flag.

                    SIZE
                        The total size, in bytes, of all messages in the mailbox.

                For compatibility with IMAP4rev1 (RFC 3501), clients may also
                request:

                    RECENT
                        The number of messages with the \Recent flag set.
                        This status data item is obsolete in IMAP4rev2.

                Servers MUST return only the status data items requested by the
                client.

                Example:

                    C: A042 STATUS blurdybloop (UIDNEXT MESSAGES)
                    S: * STATUS blurdybloop (MESSAGES 231 UIDNEXT 44292)
                    S: A042 OK STATUS completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string   folder;
            string[] statusItems;
            try{
                folder = await imapReader.ReadMailboxAsync() ?? "";

                statusItems = imapReader.ReadParenthesized().Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
                foreach(string statusItem in statusItems){
                    if(string.Equals("MESSAGES",statusItem,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                    else if(string.Equals("UIDNEXT",statusItem,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                    else if(string.Equals("UIDVALIDITY",statusItem,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                    else if(string.Equals("UNSEEN",statusItem,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                    else if(string.Equals("DELETED",statusItem,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                    else if(string.Equals("SIZE",statusItem,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                    else if(string.Equals("RECENT",statusItem,StringComparison.OrdinalIgnoreCase)){
                        continue;
                    }
                    else{
                        await imapReader.DrainRemainingCommandAsync();
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                        return;
                    }
                }

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }
            
            var response = new IMAP_r_ServerStatus(cmdTag,"NO","STATUS information unavailable");
            
            // Raise StatusAsync event.
            IMAP_e_Status eArgs = new IMAP_e_Status(this,folder,response);
            if(this.StatusAsync != null){
                
                await StatusAsync(eArgs);

                response = eArgs.Response;
            }

            if(response.IsSuccess){
                await SendResponseAsync(
                    new IMAP_r_u_Status(
                        folder,
                        statusItems.Contains("MESSAGES",StringComparer.OrdinalIgnoreCase) ? eArgs.MessagesCount : null,
                        statusItems.Contains("UIDNEXT",StringComparer.OrdinalIgnoreCase) ? eArgs.UidNext : null,
                        statusItems.Contains("UIDVALIDITY",StringComparer.OrdinalIgnoreCase) ? eArgs.UidValidity : null,
                        statusItems.Contains("UNSEEN",StringComparer.OrdinalIgnoreCase) ? eArgs.UnseenCount : null,
                        statusItems.Contains("DELETED",StringComparer.OrdinalIgnoreCase) ? eArgs.DeletedCount : null,
                        statusItems.Contains("SIZE",StringComparer.OrdinalIgnoreCase) ? eArgs.Size : null,
                        statusItems.Contains("RECENT",StringComparer.OrdinalIgnoreCase) ? 0 : null
                    ),
                    true
                );
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _SelectAsync

        private async Task _SelectAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.3.1. SELECT Command

                Arguments:  mailbox name
                            optional SELECT parameters

                Responses:  REQUIRED untagged responses:
                                FLAGS
                                EXISTS

                            REQUIRED OK response codes:
                                UIDVALIDITY
                                UIDNEXT

                Result:     OK  - mailbox selected, now in selected state
                            NO  - mailbox cannot be selected
                            BAD - command unknown or arguments invalid

                The SELECT command selects a mailbox so that messages in the
                mailbox can be accessed. Before returning a tagged OK response,
                the server sends mailbox state information describing the selected
                mailbox.

                    FLAGS
                        The flags defined in the mailbox.

                    <n> EXISTS
                        The number of messages in the mailbox.

                    OK [UIDVALIDITY <n>]
                        The unique identifier validity value of the mailbox.

                    OK [UIDNEXT <n>]
                        The next unique identifier value.

                The server SHOULD also return:

                    OK [PERMANENTFLAGS (<flags>)]
                        The set of flags that the client may change permanently.

                Only one mailbox can be selected at a time on a connection.
                Selecting a mailbox automatically deselects any previously
                selected mailbox.

                If the mailbox permits modifications, the tagged OK response
                SHOULD contain the READ-WRITE response code.

                If the mailbox is selected read-only, the tagged OK response
                MUST contain the READ-ONLY response code.

                Example:

                    C: A142 SELECT INBOX
                    S: * 172 EXISTS
                    S: * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)
                    S: * OK [UIDVALIDITY 3857529045] UIDs valid
                    S: * OK [UIDNEXT 4392] Predicted next UID
                    S: * OK [PERMANENTFLAGS (\Deleted \Seen \*)]
                    S: A142 OK [READ-WRITE] SELECT completed
            */            

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
                        
            // Unselect folder if any selected.
            if(m_pSelectedFolder != null){
                m_pSelectedFolder = null;
            }

            string folder;
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","SELECT not implemented.");
            
            // Raise SelectAsync event.
            if(this.SelectAsync != null){
                IMAP_e_Select eArgs = new IMAP_e_Select(this,folder,response);
                await SelectAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    await SendResponseAsync(new IMAP_r_u_Exists(eArgs.MessagesInfo.Count),true);
                    await SendResponseAsync(new IMAP_r_u_Flags(eArgs.Flags),true);
                    await SendResponseAsync(new IMAP_r_u_ServerStatus("OK",new IMAP_t_orc_UidValidity(eArgs.UidValidity),""),true);
                    await SendResponseAsync(new IMAP_r_u_ServerStatus("OK",new IMAP_t_orc_UidNext(eArgs.UidNext),""),true);
                    await SendResponseAsync(new IMAP_r_u_ServerStatus("OK",new IMAP_t_orc_PermanentFlags(eArgs.PermanentFlags),""),true);

                    m_pSelectedFolder = new _SelectedFolder(folder,eArgs.Version,response.OptionalResponse is IMAP_t_orc_ReadOnly,eArgs.MessagesInfo);                    
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _ExamineAsync

        private async Task _ExamineAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.3.2. EXAMINE Command

                Arguments:  mailbox name

                Responses:  REQUIRED untagged responses:
                                FLAGS
                                EXISTS

                            REQUIRED OK response codes:
                                UIDVALIDITY
                                UIDNEXT

                Result:     OK  - mailbox examined, now in selected state
                            NO  - mailbox cannot be examined
                            BAD - command unknown or arguments invalid

                The EXAMINE command is identical to SELECT except that the mailbox
                is selected in read-only mode.

                No changes to the permanent state of the mailbox are permitted
                through an EXAMINE session.

                Before returning a tagged OK response, the server sends mailbox
                state information describing the selected mailbox.

                    FLAGS
                        The flags defined in the mailbox.

                    <n> EXISTS
                        The number of messages in the mailbox.

                    OK [UIDVALIDITY <n>]
                        The unique identifier validity value of the mailbox.

                    OK [UIDNEXT <n>]
                        The next unique identifier value.

                The server SHOULD also return:

                    OK [PERMANENTFLAGS (<flags>)]
                        The set of flags that can be changed permanently. For a
                        read-only mailbox this will typically be empty.

                The tagged OK response to a successful EXAMINE command MUST
                contain the [READ-ONLY] response code.

                Example:

                    C: A932 EXAMINE blurdybloop
                    S: * 17 EXISTS
                    S: * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)
                    S: * OK [UIDVALIDITY 3857529045] UIDs valid
                    S: * OK [UIDNEXT 4392] Predicted next UID
                    S: * OK [PERMANENTFLAGS ()]
                    S: A932 OK [READ-ONLY] EXAMINE completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
                        
            // Unselect folder if any selected.
            if(m_pSelectedFolder != null){
                m_pSelectedFolder = null;
            }

            string folder;
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","EXAMINE not implemented.");
            
            // Raise ExamineAsync event.
            if(this.ExamineAsync != null){
                IMAP_e_Examine eArgs = new IMAP_e_Examine(this,folder,response);
                await ExamineAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    await SendResponseAsync(new IMAP_r_u_Exists(eArgs.MessagesInfo.Count),true);
                    await SendResponseAsync(new IMAP_r_u_Flags(eArgs.Flags),true);
                    await SendResponseAsync(new IMAP_r_u_ServerStatus("OK",new IMAP_t_orc_UidValidity(eArgs.UidValidity),""),true);
                    await SendResponseAsync(new IMAP_r_u_ServerStatus("OK",new IMAP_t_orc_UidNext(eArgs.UidNext),""),true);
                    await SendResponseAsync(new IMAP_r_u_ServerStatus("OK",new IMAP_t_orc_PermanentFlags([]),""),true);

                    m_pSelectedFolder = new _SelectedFolder(folder,eArgs.Version,response.OptionalResponse is IMAP_t_orc_ReadOnly,eArgs.MessagesInfo);
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _AppendAsync

        private async Task _AppendAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.3.12. APPEND Command

                Arguments:  mailbox name
                            OPTIONAL flag list
                            OPTIONAL date-time string
                            message literal

                Responses:  no specific responses for this command

                Result:     OK  - APPEND completed
                            NO  - unable to append the message
                            BAD - command unknown or arguments invalid

                The APPEND command appends the specified message to the end of the
                destination mailbox.

                The message literal should contain a valid RFC 5322 message.
                The server may accept messages that do not contain all required
                header fields, for example draft messages.

                If a flag list is supplied, the specified flags are assigned to the
                appended message. Otherwise, the message is created with no flags
                set unless server policy specifies otherwise.

                If a date-time value is supplied, it becomes the INTERNALDATE of
                the appended message. Otherwise, the current date and time are used.

                If APPEND fails for any reason, the mailbox must remain unchanged;
                partial appends are not permitted.

                If the destination mailbox does not exist, the server MUST return
                a NO response. If mailbox creation might succeed, the server SHOULD
                include the [TRYCREATE] response code.

                If the destination mailbox is currently selected, the server SHOULD
                notify connected clients of the new message using the appropriate
                untagged mailbox update responses.

                Servers supporting UIDPLUS (RFC 4315) may include an APPENDUID
                response code in the tagged OK response.

                Example:

                    C: A003 APPEND saved-messages (\Seen) "07-Feb-1994 21:52:25 -0800" {310}
                    S: + Ready for literal data
                    C: Date: Mon, 7 Feb 1994 21:52:25 -0800 (PST)
                    C: From: Fred Foobar <foobar@Blurdybloop.COM>
                    C: Subject: afternoon meeting
                    C: To: mooch@owatagu.siam.edu
                    C: Message-Id: <B27397-0100000@Blurdybloop.COM>
                    C: MIME-Version: 1.0
                    C: Content-Type: text/plain; charset=US-ASCII
                    C:
                    C: Hello Joe, do you think we can meet at 3:30 tomorrow?
                    C:
                    S: A003 OK APPEND completed

                Note: APPEND is not a message delivery mechanism because it does
                not provide a way to supply SMTP envelope information.
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string    folder;
            string[]  flags         = [];
            DateTime? dateTime      = null;
            int       size          = 0;
            Stream?   messageStream = null;
            try{
                folder = await imapReader.ReadMailboxAsync() ?? "";
                
                // We have optional flag list.
                if(imapReader.PeekIs('(')){
                    flags = imapReader.ReadParenthesized().Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);
                }
                // We have optional date-time.
                if(!imapReader.PeekIs('{')){
                    try{
                        dateTime = IMAP_Utils.ParseDate(await imapReader.ReadStringAsync() ?? "");
                    }
                    catch{
                        throw new ParseException("Invalid date-time.");
                    }
                }
                size = imapReader.PeekLiteralSize();
                                
                // Raise AppendBeginAsync event.
                if(this.AppendBeginAsync != null){
                    IMAP_e_AppendBegin eArgs = new IMAP_e_AppendBegin(this,folder,flags,dateTime,size);
                    await AppendBeginAsync(eArgs);

                    if(eArgs.RejectResponse != null){
                        await imapReader.DrainRemainingCommandAsync();
                        await SendResponseAsync(eArgs.RejectResponse,true);

                        return;
                    }                    

                    messageStream = eArgs.Stream;
                }

                if(messageStream == null){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Unable to append message."),true);

                    return;
                }
                                
                await imapReader.ReadLiteralAsync(messageStream);

                if(imapReader.HasMore){
                    throw new ParseException("Error in arguments.");
                }                
            }
            catch(Exception x){
                if(messageStream != null && this.AppendCancelAsync != null){
                    IMAP_e_AppendCancel eArgs = new IMAP_e_AppendCancel(this,messageStream);
                    await AppendCancelAsync(eArgs);
                }

                if(x is ParseException){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);
                }

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"OK","APPEND completed.");

            // Raise AppendCompleteAsync event.
            if(this.AppendCompleteAsync != null){
                IMAP_e_AppendComplete eArgs = new IMAP_e_AppendComplete(this,folder,flags,dateTime,size,messageStream,response);
                await AppendCompleteAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _GetQuotaRootAsync

        private async Task _GetQuotaRootAsync(string cmdTag,string cmdText)
        {
            /* RFC 9208 4.1.2. GETQUOTAROOT Command

                Arguments:  mailbox name

                Responses:  untagged QUOTAROOT response
                            untagged QUOTA response(s)

                Result:     OK  - GETQUOTAROOT completed
                            NO  - mailbox doesn't exist or access denied
                            BAD - command unknown or arguments invalid

                The GETQUOTAROOT command takes a mailbox name and returns the list
                of quota roots associated with that mailbox in a QUOTAROOT response.
                For each returned quota root, the server also returns a QUOTA
                response containing the current resource usage and limits.

                Example:

                    C: A003 GETQUOTAROOT INBOX
                    S: * QUOTAROOT INBOX ""
                    S: * QUOTA "" (STORAGE 10 512)
                    S: A003 OK GETQUOTAROOT completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!SupportsCap("QUOTA")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported."),true);

                return;
            }
            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            
            string folder;
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise GetQuotaRootAsync event.
            if(this.GetQuotaRootAsync != null){
                IMAP_e_GetQuotaRoot eArgs = new IMAP_e_GetQuotaRoot(this,folder,response);
                await GetQuotaRootAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    if(eArgs.QuotaRootResponse == null){
                        response = new IMAP_r_ServerStatus(cmdTag,"NO","Mailbox has no quota root.");
                    }
                    else{
                        await SendResponseAsync(eArgs.QuotaRootResponse,true);
                        foreach(IMAP_r_u_Quota r in eArgs.QuotaResponses){
                            await SendResponseAsync(r,true);
                        }
                    }
                }
            }            

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _GetQuotaAsync

        private async Task _GetQuotaAsync(string cmdTag,string cmdText)
        {
            /* RFC 9208 4.1.1. GETQUOTA Command

                Arguments:  quota root

                Responses:  untagged QUOTA response

                Result:     OK  - GETQUOTA completed
                            NO  - no such quota root or access denied
                            BAD - command unknown or arguments invalid

                The GETQUOTA command takes the name of a quota root and returns the
                current resource usage and limits associated with that quota root in
                an untagged QUOTA response.

                A server may support one or more quota resources, such as STORAGE,
                MESSAGE, MAILBOX, or ANNOTATION-STORAGE.

                Example:

                    C: A003 GETQUOTA ""
                    S: * QUOTA "" (STORAGE 10 512)
                    S: A003 OK GETQUOTA completed

            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!SupportsCap("QUOTA")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported."),true);

                return;
            }
            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string quotaRoot;
            try{
                quotaRoot  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise GetQuotaAsync event.
            if(this.GetQuotaAsync != null){
                IMAP_e_GetQuota eArgs = new IMAP_e_GetQuota(this,quotaRoot,response);
                await GetQuotaAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    foreach(IMAP_r_u_Quota r in eArgs.QuotaResponses){
                        await SendResponseAsync(r,true);
                    }
                }
            }            

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _GetAcl

        private async Task _GetAcl(string cmdTag,string cmdText)
        {
            /* RFC 4314 3.3. GETACL Command

                Arguments:  mailbox name

                Responses:  untagged ACL response

                Result:     OK  - GETACL completed
                            NO  - unable to retrieve ACL information
                            BAD - arguments invalid

                The GETACL command returns the access control list of the
                specified mailbox in an untagged ACL response.

                Some implementations may permit multiple forms of an identifier
                to refer to the same IMAP account. Such implementations often
                store identifiers in a canonical form. Consequently, an ACL
                response returned by GETACL may contain an identifier that differs
                from the one originally supplied in a corresponding SETACL command.

                Example:

                    C: A002 GETACL INBOX
                    S: * ACL INBOX Fred rwipslda
                    S: A002 OK GETACL completed

                    ... Multiple users

                    S: * ACL INBOX Fred rwipslda test rwipslda

                    ... No rights for Fred

                    S: * ACL INBOX Fred "" test rwipslda
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!SupportsCap("ACL")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported."),true);

                return;
            }
            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string folder;
            try{
                folder  = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise GetAclAsync event.
            if(this.GetAclAsync != null){
                IMAP_e_GetAcl eArgs = new IMAP_e_GetAcl(this,folder,response);
                await GetAclAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    if(eArgs.AclResponse == null){
                        response = new IMAP_r_ServerStatus(cmdTag,"NO","ACL information unavailable.");
                    }
                    else{
                        await SendResponseAsync(eArgs.AclResponse,true);
                    }
                }
            }            

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _SetAclAsync

        private async Task _SetAclAsync(string cmdTag,string cmdText)
        {
            /* RFC 4314 3.1. SETACL Command

                Arguments:  mailbox name
                            identifier
                            access right modification

                Responses:  no specific responses for this command

                Result:     OK  - SETACL completed
                            NO  - unable to modify the ACL
                            BAD - arguments invalid

                The SETACL command modifies the access control list of the
                specified mailbox for the specified identifier.

                The access right modification argument consists of an optional
                plus ("+") or minus ("-") prefix followed by zero or more rights
                characters.

                    +rights
                        Adds the specified rights to the identifier's existing
                        rights.

                    -rights
                        Removes the specified rights from the identifier's existing
                        rights.

                    rights
                        Replaces any existing rights for the identifier.

                An unrecognized right MUST cause the command to fail with a BAD
                response. Servers MUST NOT silently ignore unsupported or
                unrecognized rights.

                Example:

                    C: A035 SETACL INBOX/Drafts John lrQswicda
                    S: A035 BAD Uppercase rights are not allowed

                    C: A036 SETACL INBOX/Drafts John lrqswicda
                    S: A036 BAD The q right is not supported
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!SupportsCap("ACL")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported."),true);

                return;
            }
            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }

            string folder;
            string identifier;
            string rights;
            try{
                folder     = await imapReader.ReadMailboxAsync() ?? "";
                identifier = await imapReader.ReadStringAsync() ?? "";
                rights     = await imapReader.ReadStringAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise SetAclAsync event.
            if(this.SetAclAsync != null){
                IMAP_e_SetAcl eArgs = new IMAP_e_SetAcl(this,folder,identifier,rights,response);
                await SetAclAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _DeleteAclAsync

        private async Task _DeleteAclAsync(string cmdTag,string cmdText)
        {
                        /* RFC 4314 3.2. DELETEACL Command

                Arguments:  mailbox name
                            identifier

                Responses:  no specific responses for this command

                Result:     OK  - DELETEACL completed
                            NO  - unable to delete the ACL entry
                            BAD - arguments invalid

                The DELETEACL command removes any <identifier, rights> pair for
                the specified identifier from the access control list of the
                specified mailbox.

                If the identifier does not have an ACL entry for the mailbox,
                the server MAY treat the operation as successful.

                Example:

                    C: B001 GETACL INBOX
                    S: * ACL INBOX Fred rwipslxetad -Fred wetd $team w
                    S: B001 OK GETACL completed

                    C: B002 DELETEACL INBOX Fred
                    S: B002 OK DELETEACL completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!SupportsCap("ACL")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported."),true);

                return;
            }
            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            
            string folder;
            string identifier;
            try{
                folder     = await imapReader.ReadMailboxAsync() ?? "";
                identifier = await imapReader.ReadStringAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise DeleteAclAsync event.
            if(this.DeleteAclAsync != null){
                IMAP_e_DeleteAcl eArgs = new IMAP_e_DeleteAcl(this,folder,identifier,response);
                await DeleteAclAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _ListRightsAsync

        private async Task _ListRightsAsync(string cmdTag,string cmdText)
        {
            /* RFC 4314 3.4. LISTRIGHTS Command

                Arguments:  mailbox name
                            identifier

                Responses:  untagged LISTRIGHTS response

                Result:     OK  - LISTRIGHTS completed
                            NO  - unable to retrieve rights information
                            BAD - arguments invalid

                The LISTRIGHTS command takes a mailbox name and an identifier and
                returns information about the rights that may be granted to the
                identifier in the mailbox ACL.

                Some implementations may permit multiple forms of an identifier to
                refer to the same IMAP account. A LISTRIGHTS response MUST return
                the identifier exactly as supplied by the client, allowing the
                client to correlate the response with the command.

                The LISTRIGHTS response contains:

                    mailbox-name
                        The mailbox whose rights are being queried.

                    identifier
                        The identifier supplied by the client.

                    required-rights
                        Rights that are always granted to the identifier and cannot
                        be removed.

                    optional-rights...
                        Rights that may be granted to the identifier.

                Example:

                    C: A001 LISTRIGHTS ~/Mail/saved smith
                    S: * LISTRIGHTS ~/Mail/saved smith la r swicdkxte
                    S: A001 OK LISTRIGHTS completed

                Example:

                    C: A005 LISTRIGHTS archive/imap anyone
                    S: * LISTRIGHTS archive/imap anyone ""
                       l r s w i p k x t e c d a 0 1 2 3 4 5 6 7 8 9
                    S: A005 OK LISTRIGHTS completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!SupportsCap("ACL")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported."),true);

                return;
            }
            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            
            string folder;
            string identifier;
            try{
                folder     = await imapReader.ReadMailboxAsync() ?? "";
                identifier = await imapReader.ReadStringAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise ListRightsAsync event.
            if(this.ListRightsAsync != null){
                IMAP_e_ListRights eArgs = new IMAP_e_ListRights(this,folder,identifier,response);
                await ListRightsAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _MyRightsAsync

        private async Task _MyRightsAsync(string cmdTag,string cmdText)
        {
            /* RFC 4314 3.5. MYRIGHTS Command

                Arguments:  mailbox name

                Responses:  untagged MYRIGHTS response

                Result:     OK  - MYRIGHTS completed
                            NO  - unable to determine mailbox rights
                            BAD - arguments invalid

                The MYRIGHTS command returns the set of rights that the
                authenticated user has for the specified mailbox in an
                untagged MYRIGHTS response.

                The rights returned in the response represent the effective
                rights available to the user for the mailbox.

                Example:

                    C: A003 MYRIGHTS INBOX
                    S: * MYRIGHTS INBOX rwiptsldaex
                    S: A003 OK MYRIGHTS completed
            */

            
            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!SupportsCap("ACL")){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported."),true);

                return;
            }
            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            
            string folder;
            try{
                folder = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise MyRightsAsync event.
            if(this.MyRightsAsync != null){
                IMAP_e_MyRights eArgs = new IMAP_e_MyRights(this,folder,response);
                await MyRightsAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _EnableAsync

        private async Task _EnableAsync(string cmdTag,string cmdText)
        {            
            /* RFC 5161 3.1. ENABLE Command

                Arguments:  capability names

                Responses:  untagged ENABLED response

                Result:     OK  - requested capabilities enabled
                            BAD - no arguments or invalid syntax

                The ENABLE command requests activation of one or more capabilities
                that require explicit client opt-in. Once enabled, a capability
                remains enabled for the lifetime of the IMAP connection and cannot
                be disabled.

                For each requested capability:

                    - If the capability is unknown to the server, it is ignored.

                    - If the capability is known but cannot be enabled using the
                      ENABLE command, it is ignored.

                    - If the capability is supported and can be enabled, it is
                      activated for the duration of the connection.

                If one or more capabilities are successfully enabled, the server
                MUST return an untagged ENABLED response listing the capabilities
                enabled by the command.

                The ENABLE command is only valid in the authenticated state before
                a mailbox has been selected.

                Multiple ENABLE commands may be issued during a session. The effect
                is additive; capabilities enabled by previous ENABLE commands remain
                enabled.

                Executing ENABLE does not alter the server's CAPABILITY response.
                A CAPABILITY command issued after ENABLE returns the same capability
                list as one issued before ENABLE.

                Example:

                    C: A1 ENABLE UTF8=ACCEPT
                    S: * ENABLED UTF8=ACCEPT
                    S: A1 OK ENABLE completed

                Example:

                    C: A2 ENABLE CONDSTORE
                    S: * ENABLED CONDSTORE
                    S: A2 OK ENABLE completed
            */

            if(!this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(string.IsNullOrEmpty(cmdText)){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            foreach(string capa in cmdText.Split(' ')){
                if(string.Equals("UTF8=ACCEPT",capa,StringComparison.OrdinalIgnoreCase)){
                    m_Utf8Enabled = true;
                    await SendResponseAsync(new IMAP_r_u_Enabled(new string[]{"UTF8=ACCEPT"}),true);
                }
                // Ignore as specification says.
                //else{
                //}
            }

            await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","ENABLE command completed."),true);
        }

        #endregion


        #region method _CheckAsync

        private async Task _CheckAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.1. CHECK Command

                Arguments:  none

                Responses:  no specific responses for this command

                Result:     OK  - CHECK completed
                            BAD - command unknown or arguments invalid

                The CHECK command requests a checkpoint of the currently selected
                mailbox. A checkpoint refers to any implementation-dependent
                housekeeping associated with the mailbox that is not normally
                performed as part of each command.

                A checkpoint may require a non-trivial amount of time to complete.
                If a server implementation has no such housekeeping requirements,
                CHECK is equivalent to NOOP.

                There is no guarantee that an EXISTS untagged response will be sent
                as a result of CHECK. Clients should use NOOP, IDLE, or other
                appropriate mechanisms to check for mailbox changes.

                Example:

                    C: FXXZ CHECK
                    S: FXXZ OK CHECK completed
            */

            if(!this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }
            if(cmdText.Length > 0) {
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","CHECK completed."),true);
        }

        #endregion

        #region method _CloseAsync

        private async Task _CloseAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.4. CLOSE Command

                Arguments:  none

                Responses:  no specific responses for this command

                Result:     OK  - CLOSE completed; session returned to the
                                 authenticated state
                            BAD - command unknown or arguments invalid

                The CLOSE command removes all messages marked with the
                \Deleted flag from the currently selected mailbox and returns
                the session from the selected state to the authenticated state.

                No untagged EXPUNGE responses are sent.

                If the mailbox was opened in read-only mode (for example using
                EXAMINE), no messages are removed and the command succeeds.

                SELECT, EXAMINE, and LOGOUT implicitly close the currently
                selected mailbox without performing an expunge.

                CLOSE is typically more efficient than issuing EXPUNGE before
                SELECT or LOGOUT because no untagged EXPUNGE responses need to
                be generated.

                Example:

                    C: A341 CLOSE
                    S: A341 OK CLOSE completed
            */

            if(!this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }
            if(cmdText.Length > 0) {
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"OK","Close completed.");
            
            // Raise CloseAsync event.
            if(this.CloseAsync != null){
                var deletedMessages = new List<IMAP_MessageInfo>();
                foreach(var msgInfo in m_pSelectedFolder.Messages){
                    if(msgInfo.ContainsFlag("\\Deleted")){
                        deletedMessages.Add(msgInfo);
                    }
                }

                IMAP_e_Close eArgs = new IMAP_e_Close(this,m_pSelectedFolder.Folder,m_pSelectedFolder.IsReadOnly,deletedMessages.ToArray(),response);
                await CloseAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _FetchAsync

        private async Task _FetchAsync(bool uid,string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.5. FETCH Command

                Arguments:  sequence set
                            message data items or macro

                Responses:  untagged FETCH responses

                Result:     OK  - FETCH completed
                            NO  - unable to fetch the requested data
                            BAD - command unknown or arguments invalid

                The FETCH command retrieves message data from the selected
                mailbox.

                Data items may be specified either individually or as a
                parenthesized list.

                IMAP4rev2 defines the following FETCH macros:

                    ALL
                        Equivalent to:
                        (FLAGS INTERNALDATE RFC822.SIZE ENVELOPE)

                    FAST
                        Equivalent to:
                        (FLAGS INTERNALDATE RFC822.SIZE)

                    FULL
                        Equivalent to:
                        (FLAGS INTERNALDATE RFC822.SIZE
                         ENVELOPE BODYSTRUCTURE)

                Defined FETCH data items:

                    BODY
                        Non-extensible body structure.

                    BODY[<section>]

                    BODY[<section>]<offset.length>

                    BODY.PEEK[<section>]

                    BODYSTRUCTURE
                        MIME body structure.

                    ENVELOPE
                        Parsed message envelope.

                    FLAGS
                        Message flags.

                    INTERNALDATE
                        Message internal date.

                    RFC822
                        Complete message.

                    RFC822.HEADER
                        Message header.

                    RFC822.SIZE
                        Message size in octets.

                    RFC822.TEXT
                        Message text body.

                    UID
                        Message UID.

                Common BODY section specifiers:

                    HEADER
                    HEADER.FIELDS
                    HEADER.FIELDS.NOT
                    MIME
                    TEXT

                Section specifiers may be prefixed by one or more MIME part
                numbers such as:

                    1
                    2
                    3.1
                    4.2.TEXT
                    4.1.MIME

                BODY[] and BODY.PEEK[] support retrieval of the complete
                message, specific MIME parts, MIME headers, message headers,
                and message text bodies.

                Partial fetches are supported using the syntax:

                    BODY[<section>]<offset.length>

                BODY.PEEK does not implicitly set the \Seen flag.

                For UID FETCH commands, all generated FETCH responses must
                include the UID data item, even when UID was not explicitly
                requested.

                Note that the number appearing immediately after "*" in an
                untagged FETCH response is always a message sequence number,
                not a UID.

                Example:

                    C: A654 FETCH 2:4
                       (FLAGS BODY.PEEK[HEADER.FIELDS (DATE FROM)])
                    S: * 2 FETCH (...)
                    S: * 3 FETCH (...)
                    S: * 4 FETCH (...)
                    S: A654 OK FETCH completed

                Example:

                    C: A655 UID FETCH 100:102 FLAGS
                    S: * 2 FETCH (FLAGS (\Seen) UID 100)
                    S: * 3 FETCH (FLAGS (\Seen) UID 101)
                    S: * 4 FETCH (FLAGS (\Seen) UID 102)
                    S: A655 OK UID FETCH completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }

            IMAP_t_SeqSet? seqSet = null;
            try{                
                seqSet = IMAP_t_SeqSet.Parse(imapReader.ReadAtom());
            }
            catch{
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            #region Parse data-items

            var dataItems            = new Dictionary<string,IMAP_t_Fetch_i>();
            var requireMessage       = false;
            var requireEnvelope      = false;
            var requireBodyStructure = false;
            var setSeen              = false;

            if(!imapReader.PeekIs('(')){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }
            imapReader.ReadChar();

            // Parse data-items.
            while(true){
                // Unterminated fetch data items list.
                if(!imapReader.HasMore){
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
                if(imapReader.PeekIs(')')){
                    imapReader.ReadChar();

                    break;
                }

                #region ALL

                if(imapReader.PeekIs("ALL")){
                    dataItems.TryAdd("FLAGS",new IMAP_t_Fetch_i_Flags());
                    dataItems.TryAdd("INTERNALDATE",new IMAP_t_Fetch_i_InternalDate());
                    dataItems.TryAdd("RFC822.SIZE",new IMAP_t_Fetch_i_Rfc822Size());
                    dataItems.TryAdd("ENVELOPE",new IMAP_t_Fetch_i_Envelope());
                }

                #endregion

                #region FAST

                else if(imapReader.PeekIs("FAST")){
                    dataItems.TryAdd("FLAGS",new IMAP_t_Fetch_i_Flags());
                    dataItems.TryAdd("INTERNALDATE",new IMAP_t_Fetch_i_InternalDate());
                    dataItems.TryAdd("RFC822.SIZE",new IMAP_t_Fetch_i_Rfc822Size());
                }

                #endregion

                #region FULL

                else if(imapReader.PeekIs("FULL")){                    
                    dataItems.TryAdd("FLAGS",new IMAP_t_Fetch_i_Flags());
                    dataItems.TryAdd("INTERNALDATE",new IMAP_t_Fetch_i_InternalDate());
                    dataItems.TryAdd("RFC822.SIZE",new IMAP_t_Fetch_i_Rfc822Size());
                    dataItems.TryAdd("ENVELOPE",new IMAP_t_Fetch_i_Envelope());
                    dataItems.TryAdd("BODYSTRUCTURE",new IMAP_t_Fetch_i_BodyStructure());
                }

                #endregion

                #region BODYSTRUCTURE

                else if(imapReader.PeekIs("BODYSTRUCTURE")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("BODYSTRUCTURE",new IMAP_t_Fetch_i_BodyStructure());
                    requireBodyStructure = true;
                }

                #endregion

                #region BODY[<section>]<<partial>> and BODY.PEEK[<section>]<<partial>>

                else if(imapReader.PeekIs("BODY[") || imapReader.PeekIs("BODY.PEEK[")){
                    if(imapReader.PeekIs("BODY[")){
                        setSeen = true;
                    }

                    try{
                        IMAP_t_Fetch_i_Body item = await IMAP_t_Fetch_i_Body.ParseAsync(imapReader);
                        dataItems.TryAdd(item.GetBodySignature(),item);
                    }
                    catch(ParseException x){
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments." + x.Message),true);

                        return;
                    }
                    requireMessage = true;
                }

                #endregion
                
                #region BODY

                else if(imapReader.PeekIs("BODY")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("BODYSTRUCTURE",new IMAP_t_Fetch_i_BodyStructure());
                    requireBodyStructure = true;
                }

                #endregion

                #region ENVELOPE

                else if(imapReader.PeekIs("ENVELOPE")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("ENVELOPE",new IMAP_t_Fetch_i_Envelope());
                    requireEnvelope = true;
                }

                #endregion
                
                #region FLAGS

                else if(imapReader.PeekIs("FLAGS")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("FLAGS",new IMAP_t_Fetch_i_Flags());
                }

                #endregion

                #region INTERNALDATE

                else if(imapReader.PeekIs("INTERNALDATE")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("INTERNALDATE",new IMAP_t_Fetch_i_InternalDate());
                }

                #endregion

                #region RFC822.HEADER

                else if(imapReader.PeekIs("RFC822.HEADER")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("RFC822.HEADER",new IMAP_t_Fetch_i_Rfc822Header());
                    requireMessage = true;
                    setSeen = true;
                }

                #endregion

                #region RFC822.SIZE

                else if(imapReader.PeekIs("RFC822.SIZE")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("RFC822.SIZE",new IMAP_t_Fetch_i_Rfc822Size());
                }

                #endregion

                #region RFC822.TEXT

                else if(imapReader.PeekIs("RFC822.TEXT")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("RFC822.TEXT",new IMAP_t_Fetch_i_Rfc822Text());
                    requireMessage = true;
                    setSeen = true;
                }

                #endregion

                #region RFC822

                else if(imapReader.PeekIs("RFC822")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("RFC822",new IMAP_t_Fetch_i_Rfc822());
                    requireMessage = true;
                    setSeen = true;
                }

                #endregion

                #region UID

                else if(imapReader.PeekIs("UID")){
                    imapReader.ReadAtom();
                    dataItems.TryAdd("UID",new IMAP_t_Fetch_i_Uid());
                }

                #endregion

                #region Unknown data-item.

                else{
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments: Unknown FETCH data-item."),true);

                    return;
                }

                #endregion
            }

            #endregion

            // UID FETCH must always return UID data-item, even if user didn't request it.
            if(uid){
                dataItems.TryAdd("UID",new IMAP_t_Fetch_i_Uid());
            }

            await SyncFolderAsync();

            byte[] readBuffer = new byte[16000];
            FetchDataAsyncCallback addDataCallback = async (
                IMAP_MessageInfo messageInfo,
                Stream? messageStream,
                IMAP_t_Envelope? envelope,
                IMAP_t_BodyStructure? bodyStructure) =>
            {   
                long streamStartPosition = messageStream != null ? messageStream.Position : 0;

                StringBuilder reponseBuffer = new StringBuilder();
                reponseBuffer.Append("* " + m_pSelectedFolder.GetSeqNo(messageInfo) + " FETCH (");
                                
                bool isFirst = true;
                // Return requested data-items for the returned message.
                foreach(IMAP_t_Fetch_i dataItem in dataItems.Values){                                      
                    // Add data-items separator.
                    if(!isFirst){
                        reponseBuffer.Append(" ");
                    }
                    isFirst = false;

                    #region BODY[<section>]<<partial>> and BODY.PEEK[<section>]<<partial>>

                    if(dataItem is IMAP_t_Fetch_i_Body body){
                        messageStream!.Position = streamStartPosition;

                        Stream? streamToSend = null;
                        long    bytesToSend  = 0;
                        // BODY[]
                        if(string.IsNullOrEmpty(body.Section)){
                            streamToSend = messageStream;
                            bytesToSend  = streamToSend.Length - streamToSend.Position;
                        }
                        // BODY[HEADER]
                        else if (string.Equals(body.Section,"HEADER",StringComparison.OrdinalIgnoreCase)){
                            var (headerLength,missingTerminatorBytes) = await FindHeaderEndAsync(messageStream, readBuffer);
                            if(missingTerminatorBytes.Length == 0){
                                messageStream.Position = streamStartPosition;
                                streamToSend = messageStream;
                                bytesToSend = headerLength;
                            }
                            // Header not properly terminated, add missing terminator.
                            else{
                                MemoryStreamEx ms = new MemoryStreamEx(64000);
    
                                // Position back at the start of the message/header
                                messageStream.Position = streamStartPosition;
    
                                // Copy exactly headerLength bytes into the memory stream
                                byte[] buffer = new byte[8000];
                                long bytesToCopy = headerLength;
    
                                while(bytesToCopy > 0){
                                    int countToRead = (int)Math.Min(buffer.Length, bytesToCopy);
                                    int read = await messageStream.ReadAsync(buffer,0,countToRead);
                                    if(read == 0){
                                        throw new IOException("Fetch message source stream closed unexpectedly.");
                                    }
        
                                    await ms.WriteAsync(buffer,0,read);
                                    bytesToCopy -= read;
                                }
    
                                // Append the patch/missing terminator bytes (e.g., missing \r\n)
                                await ms.WriteAsync(missingTerminatorBytes,0,missingTerminatorBytes.Length);
    
                                // Reset position to the beginning so it's ready to be read/sent
                                ms.Position = 0;
    
                                streamToSend = ms;
                                bytesToSend = ms.Length;
                            }
                        }
                        // BODY[TEXT]
                        else if (string.Equals(body.Section,"TEXT",StringComparison.OrdinalIgnoreCase)){
                            await FindHeaderEndAsync(messageStream,readBuffer);

                            streamToSend = messageStream;
                            bytesToSend  = streamToSend.Length - streamToSend.Position;
                        }
                        else{
                            Mail_Message? message = null;
                            try{
                                message = Mail_Message.ParseFromStream(messageStream);
                            }
                            catch{
                                streamToSend = null;
                            }

                            // Get specified MIME part.
                            MIME_Entity? entity = message != null ? GetMimeEntity(message,body.PartSpecifier!) : null;
                            if(entity != null){

                                #region HEADER

                                if (string.Equals(body.SectionType,"HEADER",StringComparison.OrdinalIgnoreCase)){
                                    streamToSend = new MemoryStreamEx(64000);

                                    entity.Header.ToStream(streamToSend,new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.B,Encoding.UTF8),Encoding.UTF8,false);
                                    // All header fetches must include header terminator(CRLF).
                                    if(streamToSend.Length > 0){
                                        await streamToSend.WriteAsync(new byte[]{(byte)'\r',(byte)'\n'});
                                    }
                                    streamToSend.Position = 0;                                    
                                    bytesToSend  = streamToSend.Length - streamToSend.Position;
                                }

                                #endregion

                                #region HEADER.FIELDS

                                else if (string.Equals(body.SectionType,"HEADER.FIELDS",StringComparison.OrdinalIgnoreCase)){
                                    streamToSend = new MemoryStreamEx(64000);

                                    foreach(string filedName in (body.HeaderFields ?? [])){
                                        MIME_h[] fields = entity.Header[filedName];
                                        if(fields != null){
                                            foreach(MIME_h field in fields){
                                                byte[] fieldBytes = Encoding.UTF8.GetBytes(field.ToString(new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.B,Encoding.UTF8),Encoding.UTF8));
                                                await streamToSend.WriteAsync(fieldBytes);
                                            }
                                        }
                                    }
                                    // All header fetches must include header terminator(CRLF).
                                    if(streamToSend.Length > 0){
                                        await streamToSend.WriteAsync(new byte[]{(byte)'\r',(byte)'\n'});
                                    }
                                    streamToSend.Position = 0;
                                    bytesToSend  = streamToSend.Length - streamToSend.Position;
                                }

                                #endregion

                                #region HEADER.FIELDS.NOT

                                else if (string.Equals(body.SectionType,"HEADER.FIELDS.NOT",StringComparison.OrdinalIgnoreCase)){
                                    streamToSend = new MemoryStreamEx(64000);

                                    foreach(MIME_h field in entity.Header){
                                        bool contains = false;
                                        foreach(string fieldName in (body.HeaderFieldsNot ?? [])){
                                            if(string.Equals(field.Name,fieldName,StringComparison.OrdinalIgnoreCase)){
                                                contains = true;
                                                break;
                                            }
                                        }

                                        if(!contains){
                                            byte[] fieldBytes = Encoding.UTF8.GetBytes(field.ToString(new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.B,Encoding.UTF8),Encoding.UTF8));
                                            await streamToSend.WriteAsync(fieldBytes);
                                        }
                                    }
                                    // All header fetches must include header terminator(CRLF).
                                    if(streamToSend.Length > 0){
                                        await streamToSend.WriteAsync(new byte[]{(byte)'\r',(byte)'\n'});
                                    }
                                    streamToSend.Position = 0;
                                    bytesToSend  = streamToSend.Length - streamToSend.Position;
                                }

                                #endregion

                                #region MIME

                                else if (string.Equals(body.SectionType,"MIME",StringComparison.OrdinalIgnoreCase)){
                                    streamToSend = new MemoryStreamEx(64000);

                                    entity.Header.ToStream(streamToSend,new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.B,Encoding.UTF8),Encoding.UTF8,false);
                                    // All header fetches must include header terminator(CRLF).
                                    if(streamToSend.Length > 0){
                                        await streamToSend.WriteAsync(new byte[]{(byte)'\r',(byte)'\n'});
                                    }
                                    streamToSend.Position = 0;                                    
                                    bytesToSend  = streamToSend.Length - streamToSend.Position;
                                }

                                #endregion

                                #region TEXT

                                else if (string.Equals(body.SectionType,"TEXT",StringComparison.OrdinalIgnoreCase)){
                                    streamToSend = new MemoryStreamEx(64000);

                                    entity.Body?.ToStream(streamToSend,new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.B,Encoding.UTF8),Encoding.UTF8,false);
                                    streamToSend.Position = 0;                                    
                                    bytesToSend  = streamToSend.Length - streamToSend.Position;
                                }

                                #endregion

                                #region part-number only

                                else{
                                    streamToSend = new MemoryStreamEx(64000);

                                    entity.Body?.ToStream(streamToSend,new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.B,Encoding.UTF8),Encoding.UTF8,false);
                                    streamToSend.Position = 0;                                    
                                    bytesToSend  = streamToSend.Length - streamToSend.Position;
                                }

                                #endregion
                            }
                        }

                        // All data wanted.
                        if(body.Offset == null){
                            if(streamToSend != null){
                                reponseBuffer.Append("BODY[" + body.Section + "] {" + bytesToSend + "}\r\n");
                                await SendResponseAsync(reponseBuffer.ToString(),true);
                                reponseBuffer = new StringBuilder();
                        
                                await this.TcpStream.WriteStreamExactlyAsync(streamToSend,bytesToSend);
                                LogAddWrite(bytesToSend,"Wrote " + bytesToSend + " bytes.");
                            }
                            else{
                                reponseBuffer.Append("BODY[" + body.Section + "] NIL");
                            }
                        }
                        // Partial data wanted.
                        else{
                            if(streamToSend != null){
                                if(body.Offset > bytesToSend){
                                    bytesToSend = 0;
                                }
                                else{
                                    streamToSend.Position = (long)body.Offset;

                                    bytesToSend = bytesToSend - (long)body.Offset;
                                    if(body.MaxCount != null){
                                        bytesToSend = Math.Min(bytesToSend,(long)body.MaxCount);
                                    }
                                }

                                reponseBuffer.Append("BODY[" + body.Section + "]<" + body.Offset + "> {" + bytesToSend + "}\r\n");
                                await SendResponseAsync(reponseBuffer.ToString(),true);
                                reponseBuffer = new StringBuilder();
                        
                                if(bytesToSend > 0){
                                    await this.TcpStream.WriteStreamExactlyAsync(streamToSend,bytesToSend);
                                }
                                LogAddWrite(bytesToSend,"Wrote " + bytesToSend + " bytes.");
                            }
                            else{
                                reponseBuffer.Append("BODY[" + body.Section + "] NIL");
                            }
                        }

                        if(streamToSend is MemoryStreamEx){
                            _= streamToSend.DisposeAsync();
                        }
                    }

                    #endregion

                    #region BODYSTRUCTURE

                    else if(dataItem is IMAP_t_Fetch_i_BodyStructure){
                        reponseBuffer.Append("BODYSTRUCTURE " + bodyStructure!.ToString());
                    }

                    #endregion

                    #region ENVELOPE

                    else if(dataItem is IMAP_t_Fetch_i_Envelope){
                        reponseBuffer.Append("ENVELOPE " + envelope!.ToString());
                    }

                    #endregion

                    #region FLAGS

                    else if(dataItem is IMAP_t_Fetch_i_Flags){
                        reponseBuffer.Append("FLAGS (" + string.Join(' ',messageInfo.Flags) + ")");
                    }

                    #endregion

                    #region INTERNALDATE

                    else if(dataItem is IMAP_t_Fetch_i_InternalDate){
                        reponseBuffer.Append("INTERNALDATE \"" + IMAP_Utils.DateTimeToString(messageInfo.InternalDate) + "\"");
                    }

                    #endregion

                    #region RFC822

                    else if(dataItem is IMAP_t_Fetch_i_Rfc822){
                        messageStream!.Position = streamStartPosition;
                        long messageSize = messageStream.Length - messageStream.Position;

                        reponseBuffer.Append("RFC822 {" + messageSize + "}\r\n");
                        await SendResponseAsync(reponseBuffer.ToString(),true);
                        reponseBuffer = new StringBuilder();
                        
                        await this.TcpStream.WriteStreamExactlyAsync(messageStream,messageSize);
                        LogAddWrite(messageSize,"Wrote " + messageSize + " bytes.");
                    }

                    #endregion

                    #region RFC822.HEADER

                    else if(dataItem is IMAP_t_Fetch_i_Rfc822Header){
                        messageStream!.Position = streamStartPosition;
                        var (headerLength,missingTerminatorBytes) = await FindHeaderEndAsync(messageStream, readBuffer);
                        
                        messageStream!.Position = streamStartPosition;
                        long countToSend = headerLength + missingTerminatorBytes.Length;
                        reponseBuffer.Append("RFC822.HEADER {" + countToSend  + "}\r\n");
                        await SendResponseAsync(reponseBuffer.ToString(),true);
                        reponseBuffer = new StringBuilder();

                        await this.TcpStream.WriteStreamExactlyAsync(messageStream,countToSend);
                        // Header not properly terminated, send missing part of CRLFCRLF,
                        if(missingTerminatorBytes.Length > 0){
                            await this.TcpStream.WriteAsync(missingTerminatorBytes);
                        }
                        LogAddWrite(countToSend,"Wrote " + countToSend + " bytes.");
                    }

                    #endregion

                    #region RFC822.SIZE

                    else if(dataItem is IMAP_t_Fetch_i_Rfc822Size){
                        reponseBuffer.Append("RFC822.SIZE " + messageInfo.Size);
                    }

                    #endregion

                    #region RFC822.TEXT

                    else if(dataItem is IMAP_t_Fetch_i_Rfc822Text){
                        messageStream!.Position = streamStartPosition;
                        await FindHeaderEndAsync(messageStream,readBuffer);

                        long countToSend = messageStream.Length - messageStream.Position;
                        reponseBuffer.Append("RFC822.TEXT {" + countToSend  + "}\r\n");
                        await SendResponseAsync(reponseBuffer.ToString(),true);
                        reponseBuffer = new StringBuilder();

                        if(countToSend > 0){
                            await this.TcpStream.WriteStreamExactlyAsync(messageStream,countToSend);
                        }
                        LogAddWrite(countToSend,"Wrote " + countToSend + " bytes.");
                    }

                    #endregion

                    #region UID

                    else if(dataItem is IMAP_t_Fetch_i_Uid){ 
                        reponseBuffer.Append("UID " + messageInfo.UID);
                    }

                    #endregion
                }

                reponseBuffer.Append(")\r\n");            
                await SendResponseAsync(reponseBuffer.ToString(),true);

                if(setSeen && !m_pSelectedFolder.IsReadOnly){
                    messageInfo.UpdateFlags(IMAP_Flags_SetType.Add,["\\Seen"]);
                }
            };

            var response = new IMAP_r_ServerStatus(cmdTag,"OK","FETCH completed.");
                        
            // We have all needed data in message info.
            if(!requireMessage && !requireEnvelope && !requireBodyStructure){
                foreach(IMAP_MessageInfo msgInfo in m_pSelectedFolder.Filter(uid,seqSet)){
                    await addDataCallback(msgInfo,null,null,null);
                }
            }
            // Request messages data.
            else{
                IMAP_e_Fetch fetchEArgs = new IMAP_e_Fetch(
                    this,
                    m_pSelectedFolder.Filter(uid,seqSet),
                    requireMessage,
                    requireEnvelope,
                    requireBodyStructure,
                    setSeen && !m_pSelectedFolder.IsReadOnly,
                    response,
                    addDataCallback
                );

                // Raise FetchAsync event.
                if(FetchAsync != null){
                    await FetchAsync(fetchEArgs);

                    response = fetchEArgs.Response;
                }
            }
            
            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _SearchAsync

        private async Task _SearchAsync(bool uid,string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.4. SEARCH Command

                Arguments:  OPTIONAL RETURN options
                            OPTIONAL CHARSET specification
                            one or more search keys

                Responses:  REQUIRED untagged SEARCH response
                            OPTIONAL ESEARCH response

                Result:     OK  - SEARCH completed
                            NO  - unable to perform the requested search
                            BAD - command unknown or arguments invalid

                The SEARCH command searches the selected mailbox for messages
                matching the supplied search criteria.

                Multiple search keys are combined using logical AND.
                Parenthesized search keys may be combined with NOT and OR to
                form more complex search expressions.

                Unless ESEARCH is in effect, matching messages are returned as
                message sequence numbers in an untagged SEARCH response.

                The UID SEARCH command returns matching message UIDs instead
                of message sequence numbers.

                The optional CHARSET argument specifies the character set used
                by search strings. If the requested charset is not supported,
                the server should return a NO response and may include a
                BADCHARSET response code.

                Text matching is case-insensitive.

                Common search keys:

                    <sequence-set>
                        Messages with sequence numbers matching the specified
                        sequence set.

                    ALL
                        All messages in the mailbox.

                    ANSWERED
                        Messages with the \Answered flag set.

                    BCC <string>
                        Messages whose BCC header contains the specified string.

                    BEFORE <date>
                        Messages whose INTERNALDATE is earlier than the
                        specified date.

                    BODY <string>
                        Messages whose message body contains the specified
                        string.

                    CC <string>
                        Messages whose CC header contains the specified string.

                    DELETED
                        Messages with the \Deleted flag set.

                    DRAFT
                        Messages with the \Draft flag set.

                    FLAGGED
                        Messages with the \Flagged flag set.

                    FROM <string>
                        Messages whose FROM header contains the specified
                        string.

                    HEADER <field-name> <string>
                        Messages whose specified header contains the specified
                        string.

                    KEYWORD <flag>
                        Messages with the specified keyword set.

                    LARGER <n>
                        Messages larger than the specified number of octets.

                    NOT <search-key>
                        Messages that do not match the specified search key.

                    ON <date>
                        Messages whose INTERNALDATE matches the specified date.

                    OR <search-key1> <search-key2>
                        Messages matching either search key.

                    SEEN
                        Messages with the \Seen flag set.

                    SENTBEFORE <date>
                        Messages whose Date header is earlier than the
                        specified date.

                    SENTON <date>
                        Messages whose Date header matches the specified date.

                    SENTSINCE <date>
                        Messages whose Date header is on or after the
                        specified date.

                    SINCE <date>
                        Messages whose INTERNALDATE is on or after the
                        specified date.

                    SMALLER <n>
                        Messages smaller than the specified number of octets.

                    SUBJECT <string>
                        Messages whose Subject header contains the specified
                        string.

                    TEXT <string>
                        Messages whose headers or body contain the specified
                        string.

                    TO <string>
                        Messages whose TO header contains the specified string.

                    UID <sequence-set>
                        Messages whose UIDs match the specified UID set.

                    UNANSWERED
                        Messages without the \Answered flag.

                    UNDELETED
                        Messages without the \Deleted flag.

                    UNDRAFT
                        Messages without the \Draft flag.

                    UNFLAGGED
                        Messages without the \Flagged flag.

                    UNKEYWORD <flag>
                        Messages without the specified keyword.

                    UNSEEN
                        Messages without the \Seen flag.

                Compatibility search keys:

                    NEW
                        Messages with the \Recent flag set and without the
                        \Seen flag.

                    OLD
                        Messages without the \Recent flag set.

                    RECENT
                        Messages with the \Recent flag set.

                IMAP4rev2 removes the \Recent message flag. The NEW, OLD,
                and RECENT search keys are supported only for interoperability
                with legacy IMAP4rev1 clients.
                Compatibility search keys NEW, OLD, and RECENT are evaluated
                as if no messages possess the obsolete \Recent flag.

                Example:

                    C: A282 SEARCH FLAGGED SINCE 1-Feb-2024 NOT FROM "Smith"
                    S: * SEARCH 2 84 882
                    S: A282 OK SEARCH completed

                Example:

                    C: A283 SEARCH TEXT "string not in mailbox"
                    S: * SEARCH
                    S: A283 OK SEARCH completed

                Example:

                    C: A284 UID SEARCH UNSEEN
                    S: * SEARCH 101 102 105
                    S: A284 OK SEARCH completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }
           
            IMAP_t_Search_Key_Group criteria;
            try{
                if(imapReader.PeekIs("CHARSET")){
                    string charset  = await imapReader.ReadStringAsync() ?? "";
                    if(!(string.Equals(charset,"US-ASCII",StringComparison.OrdinalIgnoreCase) || string.Equals(charset,"UTF-8",StringComparison.OrdinalIgnoreCase))){
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO",new IMAP_t_orc_BadCharset(["US-ASCII","UTF-8"]),"Unsupported charset."),true);

                        return;
                    }
                }

                criteria = await IMAP_t_Search_Key_Group.ParseAsync(imapReader);
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            #region Determine search mode. Local(use cache data) or raise SearchAsync event

            bool localSearch = true;

            List<IMAP_t_Search_Key> keys = new List<IMAP_t_Search_Key>();
            keys.Add(criteria);

            int index = 0;
            while(index < keys.Count){
                IMAP_t_Search_Key current = keys[index++];

                switch(current)
                {
                    case IMAP_t_Search_Key_Bcc:
                    case IMAP_t_Search_Key_Body:
                    case IMAP_t_Search_Key_Cc:
                    case IMAP_t_Search_Key_From:
                    case IMAP_t_Search_Key_Header:
                    case IMAP_t_Search_Key_Subject:
                    case IMAP_t_Search_Key_Text:
                    case IMAP_t_Search_Key_To:
                    case IMAP_t_Search_Key_SentBefore:
                    case IMAP_t_Search_Key_SentOn:
                    case IMAP_t_Search_Key_SentSince:
                        localSearch = false;
                        break;

                    case IMAP_t_Search_Key_Not notKey:
                        keys.Add(notKey.SearchKey);
                        break;

                    case IMAP_t_Search_Key_Or orKey:
                        keys.Add(orKey.SearchKey1);
                        keys.Add(orKey.SearchKey2);
                        break;

                    case IMAP_t_Search_Key_Group group:
                        keys.AddRange(group.Keys);
                        break;
                }

                if(!localSearch){
                    break;
                }
            }

            #endregion

            await SyncFolderAsync();

            if(localSearch){
                List<long> matched = new List<long>();
                foreach(var message in m_pSelectedFolder.Messages){
                    if(SearchIsMatch(message,criteria)){
                        if(uid){
                            matched.Add(message.UID);
                        }
                        else{
                            matched.Add(m_pSelectedFolder.GetSeqNo(message));
                        }
                    }
                }

                await SendResponseAsync(new IMAP_r_u_Search(matched.ToArray()),matched.Count < 50);
                if(matched.Count > 50){
                    LogAddWrite(0,$"* SEARCH [{matched.Count} matches]");
                }
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","SEARCH completed."),true);
            }
            else{
                var response = new IMAP_r_ServerStatus(cmdTag,"OK","SEARCH completed.");
                
                // Raise SearchAsync event.
                if(this.SearchAsync != null){
                    IMAP_e_Search eArgs = new IMAP_e_Search(this,m_pSelectedFolder.Folder,criteria,response);
                    await SearchAsync(eArgs);

                    response = eArgs.Response;

                    if(response.IsSuccess){
                        List<long> matched = new List<long>();
                        if(uid){
                            matched.AddRange(eArgs.MatchedUids);
                        }
                        else{
                            foreach(long matchedUid in eArgs.MatchedUids){
                                int seqNo = m_pSelectedFolder.GetSeqNo(matchedUid);
                                if(seqNo > 0){
                                    matched.Add(seqNo);
                                }
                            }
                        }

                        await SendResponseAsync(new IMAP_r_u_Search(matched.ToArray()),matched.Count < 50);
                        if(matched.Count > 50){
                            LogAddWrite(0,$"* SEARCH [{matched.Count} matches]");
                        }
                    }
                }

                await SendResponseAsync(response,true);
            } 
        }

        #endregion

        #region method _StoreAsync

        private async Task _StoreAsync(bool uid,string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.6. STORE Command

                Arguments:  sequence set
                            message data item name
                            value for message data item

                Responses:  untagged FETCH responses

                Result:     OK  - STORE completed
                            NO  - unable to store the requested data
                            BAD - command unknown or arguments invalid

                The STORE command alters data associated with one or more
                messages in the selected mailbox.

                Unless the ".SILENT" suffix is specified, the server returns
                the updated data using untagged FETCH responses.

                Regardless of whether the ".SILENT" suffix is used, the server
                should notify clients of externally observed flag changes using
                the appropriate untagged FETCH responses.

                The following STORE data items are defined:

                    FLAGS <flag-list>

                        Replaces the message flags with the supplied flag list.
                        The updated flags are returned using untagged FETCH
                        responses.

                    FLAGS.SILENT <flag-list>

                        Equivalent to FLAGS but suppresses the untagged FETCH
                        responses normally generated by the STORE command.

                    +FLAGS <flag-list>

                        Adds the supplied flags to the existing message flags.
                        The updated flags are returned using untagged FETCH
                        responses.

                    +FLAGS.SILENT <flag-list>

                        Equivalent to +FLAGS but suppresses the untagged FETCH
                        responses normally generated by the STORE command.

                    -FLAGS <flag-list>

                        Removes the supplied flags from the existing message
                        flags. The updated flags are returned using untagged
                        FETCH responses.

                    -FLAGS.SILENT <flag-list>

                        Equivalent to -FLAGS but suppresses the untagged FETCH
                        responses normally generated by the STORE command.

                Example:

                    C: A003 STORE 2:4 +FLAGS (\Deleted)
                    S: * 2 FETCH (FLAGS (\Deleted \Seen))
                    S: * 3 FETCH (FLAGS (\Deleted))
                    S: * 4 FETCH (FLAGS (\Deleted \Flagged \Seen))
                    S: A003 OK STORE completed

                Example:

                    C: A004 UID STORE 100:102 FLAGS.SILENT (\Seen)
                    S: A004 OK STORE completed
            */

            /* RFC 9051 UID Command Semantics

                For UID STORE, all FETCH responses generated by the command
                must include the UID message data item.

                Example:

                    C: A003 UID STORE 100 +FLAGS (\Seen)
                    S: * 5 FETCH (FLAGS (\Seen) UID 100)
                    S: A003 OK STORE completed

                This requirement applies regardless of whether the STORE
                operation targets a single message or multiple messages.
            */
            
            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }
 
            IMAP_t_SeqSet      seqSet;
            IMAP_Flags_SetType setType;
            string[]           flags;
            bool               silent = false;
            try{
                try{
                    seqSet = IMAP_t_SeqSet.Parse(await imapReader.ReadStringAsync() ?? "");
                }
                catch{
                    throw new ParseException("Error in arguments.");
                }

                string dataitem = await imapReader.ReadStringAsync() ?? "";
                if(string.Equals(dataitem,"FLAGS",StringComparison.OrdinalIgnoreCase)){
                    setType = IMAP_Flags_SetType.Replace;
                }
                else if(string.Equals(dataitem,"FLAGS.SILENT",StringComparison.OrdinalIgnoreCase)){
                    setType = IMAP_Flags_SetType.Replace;
                    silent = true;
                }
                else if(string.Equals(dataitem,"+FLAGS",StringComparison.OrdinalIgnoreCase)){
                    setType = IMAP_Flags_SetType.Add;
                }
                else if(string.Equals(dataitem,"+FLAGS.SILENT",StringComparison.OrdinalIgnoreCase)){
                    setType = IMAP_Flags_SetType.Add;
                    silent = true;
                }
                else if(string.Equals(dataitem,"-FLAGS",StringComparison.OrdinalIgnoreCase)){
                    setType = IMAP_Flags_SetType.Remove;
                }
                else if(string.Equals(dataitem,"-FLAGS.SILENT",StringComparison.OrdinalIgnoreCase)){
                    setType = IMAP_Flags_SetType.Remove;
                    silent = true;
                }
                else{
                    throw new ParseException("Error in arguments.");
                }

                flags = imapReader.ReadParenthesized().Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries);

                if(imapReader.HasMore){
                    throw new ParseException("Error in arguments.");
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise StoreAsync event.
            if(this.StoreAsync != null){
                await SyncFolderAsync();

                var messages = m_pSelectedFolder.Filter(uid,seqSet);

                IMAP_e_Store eArgs = new IMAP_e_Store(this,m_pSelectedFolder.Folder,setType,flags,messages,response);
                await StoreAsync(eArgs);

                response = eArgs.Response;

                if(response.IsSuccess){
                    foreach(var message in messages){
                        // Update local message info flags value.
                        message.UpdateFlags(setType,flags.ToArray());

                        if(!silent){
                            if(uid){
                                await SendResponseAsync(
                                    new IMAP_r_u_Fetch(
                                        m_pSelectedFolder.GetSeqNo(message),
                                        new IMAP_t_Fetch_r_i[]{
                                            new IMAP_t_Fetch_r_i_Flags(message.Flags),
                                            new IMAP_t_Fetch_r_i_Uid(message.UID)
                                        }
                                    ),
                                    true
                                );
                            }
                            else{
                                await SendResponseAsync(
                                    new IMAP_r_u_Fetch(
                                        m_pSelectedFolder.GetSeqNo(message),
                                        new IMAP_t_Fetch_r_i[]{
                                            new IMAP_t_Fetch_r_i_Flags(message.Flags)
                                        }
                                    ),
                                    true
                                );
                            }
                        }
                    }
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _CopyAsync

        private async Task _CopyAsync(bool uid,string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.8. COPY Command

                Arguments:  sequence set
                            mailbox name

                Responses:  no specific responses for this command

                Result:     OK  - COPY completed
                            NO  - unable to copy the specified message(s)
                            BAD - command unknown or arguments invalid

                The COPY command copies the specified message(s) to the end of
                the destination mailbox.

                The flags and INTERNALDATE of the source message(s) should be
                preserved in the copied message(s).

                If the destination mailbox does not exist, the server should
                return a NO response and should not automatically create the
                mailbox. Unless it is certain that the mailbox cannot be created,
                the server should include the [TRYCREATE] response code.

                If the COPY operation fails for any reason, the destination
                mailbox must be restored to its state before the COPY attempt.
                Partial copies are not permitted.

                Servers supporting UIDPLUS (RFC 4315) may include a COPYUID
                response code in the final OK response.

                Example:

                    C: A003 COPY 2:4 MEETING
                    S: A003 OK COPY completed

                Example (UIDPLUS):

                    C: A004 UID COPY 100:102 Archive
                    S: A004 OK [COPYUID 3857529045 100:102 500:502]
                       COPY completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }

            IMAP_t_SeqSet seqSet;
            string        folder;            
            try{                
                seqSet = IMAP_t_SeqSet.Parse(await imapReader.ReadStringAsync() ?? "");
                folder = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise CopyAsync event.
            if(this.CopyAsync != null){
                await SyncFolderAsync();
                
                IMAP_e_Copy eArgs = new IMAP_e_Copy(this,uid,seqSet,m_pSelectedFolder.Folder,folder,m_pSelectedFolder.Filter(uid,seqSet),response);
                await CopyAsync(eArgs);

                response = eArgs.Response;
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _MoveAsync

        private async Task _MoveAsync(bool uid,string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.8. MOVE Command

                Originally specified by RFC 6851 and incorporated into
                IMAP4rev2 by RFC 9051.

                Arguments:  sequence set
                            mailbox name

                Responses:  untagged EXPUNGE responses
                            untagged VANISHED responses (QRESYNC)

                Result:     OK  - MOVE completed
                            NO  - unable to move the specified message(s)
                            BAD - command unknown or arguments invalid

                The MOVE command moves the specified message(s) from the
                currently selected mailbox to the specified destination
                mailbox.

                The flags and INTERNALDATE of the source message(s) should
                be preserved in the moved message(s).

                The MOVE command is functionally equivalent to performing:

                    COPY
                    STORE +FLAGS.SILENT (\Deleted)
                    EXPUNGE

                However, MOVE is performed atomically. The server must ensure
                that each message is either moved successfully or remains in
                the source mailbox. Intermediate states must not be visible to
                clients.

                If the destination mailbox does not exist, the server should
                return a NO response. Unless it is certain that the mailbox
                cannot be created, the server should include the [TRYCREATE]
                response code in the tagged NO response.

                If the server supports UIDPLUS (RFC 4315), the tagged OK
                response should include a COPYUID response code providing the
                UID mapping between source and destination messages.

                When messages are removed from the selected mailbox as a result
                of a successful MOVE operation, appropriate EXPUNGE responses
                must be generated. Servers supporting QRESYNC may return
                VANISHED responses instead.

                Example:

                    C: A003 MOVE 2:4 Archive
                    S: * 2 EXPUNGE
                    S: * 2 EXPUNGE
                    S: * 2 EXPUNGE
                    S: A003 OK MOVE completed

                Example (UIDPLUS):

                    C: A004 UID MOVE 100:102 Archive
                    S: * 100 EXPUNGE
                    S: * 101 EXPUNGE
                    S: * 102 EXPUNGE
                    S: A004 OK [COPYUID 3857529045 100:102 500:502]
                       MOVE completed
            */

            var imapReader = new _IMAP_Reader(cmdText,new byte[8000],this);

            if(!this.IsAuthenticated){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }

            IMAP_t_SeqSet seqSet;
            string        folder;            
            try{                
                seqSet = IMAP_t_SeqSet.Parse(await imapReader.ReadStringAsync() ?? "");
                folder = await imapReader.ReadMailboxAsync() ?? "";

                if(imapReader.HasMore){
                    await imapReader.DrainRemainingCommandAsync();
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            catch(ParseException){
                await imapReader.DrainRemainingCommandAsync();
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            var response = new IMAP_r_ServerStatus(cmdTag,"NO","Command not supported.");
            
            // Raise MoveAsync event.
            if(this.MoveAsync != null){
                await SyncFolderAsync();
               
                var messagesToCopy = m_pSelectedFolder.Filter(uid,seqSet);

                IMAP_e_Move eArgs = new IMAP_e_Move(this,uid,seqSet,m_pSelectedFolder.Folder,folder,messagesToCopy,response);
                await MoveAsync(eArgs);

                response = eArgs.Response;

                // Generate EXPUNGE foreach copied message and remove these messages from current folder.
                if(response.IsSuccess){
                    foreach(var message in messagesToCopy) {
                        int seqNo = m_pSelectedFolder.GetSeqNo(message);
                        m_pSelectedFolder.RemoveMessage(message);

                        await SendResponseAsync(new IMAP_r_u_Expunge(seqNo),true);                        
                    }
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _UidAsync

        private async Task _UidAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.8. UID Command

                Arguments:  command name
                            command arguments

                Responses:  untagged FETCH responses
                            untagged SEARCH responses

                Result:     OK  - UID command completed
                            NO  - UID command failed
                            BAD - command unknown or arguments invalid

                The UID command causes the specified command to operate on
                message UIDs rather than message sequence numbers.

                IMAP4rev2 defines the following UID commands:

                    UID COPY
                    UID EXPUNGE
                    UID FETCH
                    UID MOVE
                    UID SEARCH
                    UID STORE

                Unless otherwise specified, sequence-set arguments supplied
                to a UID command are interpreted as UID sets.

                Non-existent UIDs are ignored without error. Consequently,
                a UID command may complete successfully without affecting any
                messages.

                UID SEARCH uses the same search criteria as SEARCH. However,
                matching UIDs are returned in the SEARCH response instead of
                message sequence numbers.

                For UID FETCH and UID STORE, all untagged FETCH responses
                generated by the command must include the UID data item,
                regardless of whether UID was explicitly requested.

                Note that the number appearing immediately after "*" in an
                untagged FETCH response is always a message sequence number,
                not a UID.

                Example:

                    C: A999 UID FETCH 4827313:4828442 FLAGS
                    S: * 23 FETCH (FLAGS (\Seen) UID 4827313)
                    S: * 24 FETCH (FLAGS (\Seen) UID 4827943)
                    S: * 25 FETCH (FLAGS (\Seen) UID 4828442)
                    S: A999 OK UID FETCH completed

                Example:

                    C: A100 UID SEARCH UNSEEN
                    S: * SEARCH 4827313 4827943 4828442
                    S: A100 OK UID SEARCH completed
            */

            if(!this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }

            string[] cmd_cmtText = cmdText.Split(new char[]{' '},2);
                        
            if(string.Equals(cmd_cmtText[0],"COPY",StringComparison.InvariantCultureIgnoreCase)){
                await _CopyAsync(true,cmdTag,cmd_cmtText[1]);
            }
            else if(string.Equals(cmd_cmtText[0],"FETCH",StringComparison.InvariantCultureIgnoreCase)){
                await _FetchAsync(true,cmdTag,cmd_cmtText[1]);
            }
            else if(string.Equals(cmd_cmtText[0],"STORE",StringComparison.InvariantCultureIgnoreCase)){
                await _StoreAsync(true,cmdTag,cmd_cmtText[1]);
            }
            else if(string.Equals(cmd_cmtText[0],"SEARCH",StringComparison.InvariantCultureIgnoreCase)){
                await _SearchAsync(true,cmdTag,cmd_cmtText[1]);
            }
            else if(string.Equals(cmd_cmtText[0],"EXPUNGE",StringComparison.InvariantCultureIgnoreCase)){
                await _ExpungeAsync(true,cmdTag,cmd_cmtText[1]);
            }
            else{
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);
            }
        }

        #endregion

        #region method _ExpungeAsync

        private async Task _ExpungeAsync(bool uid,string cmdTag,string cmdText)
        {
            /* RFC 9051 6.4.3. EXPUNGE Command

                Arguments:  none

                Responses:  untagged EXPUNGE responses
                            untagged VANISHED responses (QRESYNC)

                Result:     OK  - EXPUNGE completed
                            NO  - unable to expunge messages
                            BAD - command unknown or arguments invalid

                The EXPUNGE command permanently removes all messages marked
                with the \Deleted flag from the currently selected mailbox.

                Before returning an OK response, the server must send an
                untagged EXPUNGE response for each message removed from the
                selected mailbox.

                EXPUNGE responses use message sequence numbers. Since sequence
                numbers are renumbered after each expunge, clients must process
                EXPUNGE responses in the order received.

                If the selected mailbox is read-only, for example opened by
                EXAMINE, no messages may be removed.

                Example:

                    C: A202 EXPUNGE
                    S: * 3 EXPUNGE
                    S: * 3 EXPUNGE
                    S: * 5 EXPUNGE
                    S: * 8 EXPUNGE
                    S: A202 OK EXPUNGE completed

                In this example, messages 3, 4, 7, and 11 were marked with
                the \Deleted flag before the EXPUNGE command was issued.
            */

            /* RFC 4315 2.1 UID EXPUNGE Command

                Arguments:  UID sequence set

                Responses:  untagged EXPUNGE responses
                            untagged VANISHED responses (QRESYNC)

                Result:     OK  - UID EXPUNGE completed
                            NO  - unable to expunge messages
                            BAD - command unknown or arguments invalid

                The UID EXPUNGE command permanently removes messages that:

                    - have the \Deleted flag set, and
                    - match the supplied UID sequence set.

                Messages marked with \Deleted that are not included in the
                supplied UID set must not be removed.

                UID EXPUNGE is intended for disconnected clients to safely
                remove only messages previously marked for deletion by that
                client.

                Before returning an OK response, the server must send an
                untagged EXPUNGE response for each removed message.

                Example:

                    C: A301 UID EXPUNGE 100:150
                    S: * 3 EXPUNGE
                    S: * 5 EXPUNGE
                    S: A301 OK UID EXPUNGE completed
            */

            if(!this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }

            IMAP_t_SeqSet? seqSet = null;
            if(uid){
                try{
                    seqSet = IMAP_t_SeqSet.Parse(cmdText);
                }
                catch{
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }
            else{
                if(cmdText.Length > 0) {
                    await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                    return;
                }
            }            

            var response = new IMAP_r_ServerStatus(cmdTag,"OK","EXPUNGE completed.");
            
            // Raise ExpungeAsync event.
            if(this.ExpungeAsync != null){
                await SyncFolderAsync();
               
                IMAP_MessageInfo[] messagesToDelete;
                if(uid){
                     messagesToDelete = m_pSelectedFolder.Filter(uid,seqSet!).Where(item => item.ContainsFlag("\\Deleted")).ToArray();
                }
                else{
                    messagesToDelete = m_pSelectedFolder.Messages.Where(item => item.ContainsFlag("\\Deleted")).ToArray();
                }

                IMAP_e_Expunge eArgs = new IMAP_e_Expunge(this,uid,seqSet,m_pSelectedFolder.Folder,messagesToDelete,response);
                await ExpungeAsync(eArgs);

                response = eArgs.Response;

                // Generate EXPUNGE foreach deleted message and remove these messages from current folder.
                if(response.IsSuccess){
                    foreach(var message in messagesToDelete){
                        int seqNo = m_pSelectedFolder.GetSeqNo(message);
                        m_pSelectedFolder.RemoveMessage(message);

                        await SendResponseAsync(new IMAP_r_u_Expunge(seqNo),true);                        
                    }
                }
            }

            await SendResponseAsync(response,true);
        }

        #endregion

        #region method _IdleAsync

        private async Task _IdleAsync(string cmdTag,string cmdText)
        {
            /* RFC 9051 6.3.13. IDLE Command

                Originally specified by RFC 2177 and incorporated into
                IMAP4rev2.

                Arguments:  none

                Responses:  continuation request ("+")
                            untagged mailbox update responses

                Result:     OK  - IDLE completed after receipt of DONE
                            NO  - IDLE cannot be started at this time
                            BAD - command unknown or arguments invalid

                The IDLE command allows a client to request immediate mailbox
                update notifications without polling.

                After receiving the IDLE command, the server issues a
                continuation response:

                    + idling

                While IDLE is active, the server may send untagged responses
                such as:

                    EXISTS
                    EXPUNGE
                    FETCH
                    LIST
                    STATUS
                    VANISHED (QRESYNC)

                and other mailbox update notifications permitted by the
                protocol.

                The client terminates IDLE by sending:

                    DONE

                Upon receiving DONE, the server may send any queued untagged
                responses and must immediately return the tagged completion
                response for the IDLE command.

                While the server is waiting for DONE, the client must not send
                any command other than DONE.

                Servers may terminate idle clients according to local inactivity
                policies. Clients are generally expected to terminate and
                reissue IDLE periodically to avoid inactivity timeouts.

                Example:

                    C: A001 SELECT INBOX
                    S: * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)
                    S: * 3 EXISTS
                    S: * OK [UIDVALIDITY 1]
                    S: A001 OK [READ-WRITE] SELECT completed

                    C: A002 IDLE
                    S: + idling

                    ... new mail arrives ...

                    S: * 4 EXISTS

                    C: DONE
                    S: A002 OK IDLE completed

                Example:

                    C: A003 IDLE
                    S: + idling

                    ... another client deletes a message ...

                    S: * 3 EXPUNGE
                    S: * 3 EXISTS

                    C: DONE
                    S: A003 OK IDLE completed
            */

            if(!this.IsAuthenticated){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","Authenticate first."),true);

                return;
            }
            if(m_pSelectedFolder == null){
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"NO","No mailbox selected."),true);

                return;
            }
            if(cmdText.Length > 0) {
                await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"BAD","Error in arguments."),true);

                return;
            }

            await SendResponseAsync(new IMAP_r_ServerStatus("+","idling"),true);

            using SemaphoreSlim idleSync = new(1,1);
            using var timer = new System.Threading.Timer(
                async  _ =>  {
                    if(!await idleSync.WaitAsync(0)){
                        return;
                    }

                    try{
                        await SyncFolderAsync();
                    }
                    catch(Exception x){
                        LogAddException(x);
                    }
                    finally{
                        idleSync.Release();
                    }
                },
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30)
            );
                        
            while(true){
                var readLineResult = await this.TcpStream.ReadLineAsync(new byte[8000],SizeExceededAction.JunkAndThrowException);
                if(readLineResult.BytesInBuffer == 0){
                    LogAddText("The remote host '" + this.RemoteEndPoint?.ToString() + "' closed connection.");

                    break;
                }

                string line = readLineResult.LineUtf8 ?? "";

                LogAddRead(readLineResult.BytesInBuffer,line);

                if(string.Equals(line,"DONE",StringComparison.OrdinalIgnoreCase)){
                    timer.Dispose();
                    await idleSync.WaitAsync();

                    try{
                        await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","IDLE terminated."),true);
                    }
                    finally{
                        idleSync.Release();
                    }

                    break;
                }
            }
        }

        #endregion


        #region method CapabilityAsync

        private async Task CapabilityAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.1.1 CAPABILITY Command

             Arguments:
                 none

             Responses:
                 REQUIRED untagged response: CAPABILITY

             Result:
                 OK  - CAPABILITY completed
                 BAD - command unknown or arguments invalid

             The CAPABILITY command requests a listing of capabilities supported
             by the server. The server MUST send a single untagged CAPABILITY
             response before the tagged OK response.

             Example:

                 C: A001 CAPABILITY
                 S: * CAPABILITY IMAP4rev2 STARTTLS AUTH=PLAIN IDLE ENABLE
                 S: A001 OK CAPABILITY completed

             Capability names beginning with "AUTH=" advertise supported SASL
             authentication mechanisms.

             Capabilities may vary depending on session state (for example,
             before and after successful authentication or TLS negotiation).

             RFC 9051 requires support for the IMAP4rev2 capability. Additional
             capabilities advertise optional protocol extensions and server
             features.

             References:

                 RFC 9051 §6.1.1 CAPABILITY Command
                 RFC 9051 §7.2.1 CAPABILITY Response
             */

            List<string> capabilities = new List<string>();
            if(!this.IsSecureConnection && this.Certificate != null){
                capabilities.Add("STARTTLS");
            }
            foreach(string c in m_pCapabilities){
                capabilities.Add(c);
            }
            foreach(AUTH_SASL_ServerMechanism auth in this.Authentications.Values){
                capabilities.Add("AUTH=" + auth.Name);
            }

            await SendResponseAsync(new IMAP_r_u_Capability(capabilities.ToArray()),true);
            await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","CAPABILITY completed."),true);
        }

        #endregion

        #region method NoopAsync

        private async Task NoopAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.1.2 NOOP Command

             Arguments:
                 none

             Responses:
                 no specific responses for this command

             Result:
                 OK  - NOOP completed
                 BAD - command unknown or arguments invalid

             The NOOP command always succeeds and performs no action.

             Because any command can return untagged status updates, NOOP is
             commonly used to poll for mailbox changes during periods of client
             inactivity and can also be used to reset server inactivity timers.

             A NOOP command may cause the server to send untagged EXISTS,
             EXPUNGE, FETCH, STATUS, or other mailbox updates before the tagged
             OK response.

             Example:

                 C: A002 NOOP
                 S: A002 OK NOOP completed

                 ...

                 C: A047 NOOP
                 S: * 22 EXPUNGE
                 S: * 23 EXISTS
                 S: * 14 FETCH (FLAGS (\Seen \Deleted))
                 S: A047 OK NOOP completed

             References:

                 RFC 9051 §6.1.2 NOOP Command
            */

            if(m_pSelectedFolder != null){
                await SyncFolderAsync();
            }

            await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","NOOP Completed."),true);
        }

        #endregion

        #region method LogoutAsync

        private async Task LogoutAsync(string cmdTag,string cmdText)
        {
            /*
             RFC 9051 §6.1.3 LOGOUT Command

             Arguments:
                 none

             Responses:
                 REQUIRED untagged response: BYE

             Result:
                 OK  - LOGOUT completed
                 BAD - command unknown or arguments invalid

             The LOGOUT command informs the server that the client wishes
             to terminate the session.

             The server MUST send an untagged BYE response before the tagged
             OK response and then close the network connection.

             Example:

                 C: A023 LOGOUT
                 S: * BYE IMAP4rev2 Server logging out
                 S: A023 OK LOGOUT completed
                 (connection closed)

             References:

                 RFC 9051 §6.1.3 LOGOUT Command
            */

            await SendResponseAsync(new IMAP_r_u_ServerStatus("BYE","IMAP Server logging out."),true);
            await SendResponseAsync(new IMAP_r_ServerStatus(cmdTag,"OK","LOGOUT completed."),true);
            await DisconnectAsync();
            Dispose();
        }

        #endregion


        #region method SendResponseAsync

        internal async Task SendResponseAsync(string response,bool log)
        {
            if(log){
                LogAddWrite(Encoding.UTF8.GetByteCount(response),response.TrimEnd());
            }
            await this.TcpStream.WriteLineAsync(response);
        }

        internal async Task SendResponseAsync(IMAP_r response,bool log)
        {
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            string cmdLine = response.ToString(m_Utf8Enabled)!;
            if(log){
                LogAddWrite(Encoding.UTF8.GetByteCount(cmdLine),cmdLine.TrimEnd());
            }
            await this.TcpStream.WriteLineAsync(cmdLine);
        }

        #endregion

        #region method LogAddRead

        /// <summary>
        /// Logs read operation.
        /// </summary>
        /// <param name="size">Number of bytes read.</param>
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
            
            try{
                if(this.Server.Logger != null){
                    this.Server.Logger.AddText(
                        this.ID,
                        this.AuthenticatedUserIdentity,
                        text,                        
                        this.LocalEndPoint,
                        this.RemoteEndPoint
                    );
                }
            }
            catch{
            }
        }

        #endregion

        #region method LogAddException

        /// <summary>
        /// Logs specified exception.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>exception</b> is null reference.</exception>
        public void LogAddException(Exception exception)
        {
            if(exception == null){
                throw new ArgumentNullException("exception");
            }
            
            try{
                if(this.Server.Logger != null){
                    this.Server.Logger.AddException(
                        this.ID,
                        this.AuthenticatedUserIdentity,
                        exception.Message,                        
                        this.LocalEndPoint,
                        this.RemoteEndPoint,
                        exception
                    );
                }
            }
            catch{
            }
        }

        #endregion

        #region method SyncFolderAsync

        /// <summary>
        /// Synchronizes the currently selected mailbox and sends any
        /// detected mailbox changes to the client.
        /// </summary>
        /// <remarks>
        /// This method compares the mailbox state supplied through the
        /// <see cref="FolderSyncAsync"/> event with the session's cached
        /// mailbox state. Differences are translated into the
        /// appropriate IMAP mailbox update responses.
        /// </remarks>
        private async Task SyncFolderAsync()
        {
            if(m_pSelectedFolder == null){
                return;
            }

            if(FolderSyncAsync != null){
                IMAP_e_FolderSync eArgs = new IMAP_e_FolderSync(this,m_pSelectedFolder.Folder,m_pSelectedFolder.Version);
                await FolderSyncAsync(eArgs);

                if(m_pSelectedFolder.Version != eArgs.Version){
                    m_pSelectedFolder.Version = eArgs.Version;

                    // Check deleted messages, send "* n EXPUNGE" for each deleted message.                    
                    var newUids = new HashSet<long>(eArgs.Uids);
                    foreach(IMAP_MessageInfo msgInfo in m_pSelectedFolder.Messages){
                        if(!newUids.Contains(msgInfo.UID)){
                            await SendResponseAsync(new IMAP_r_u_Expunge(m_pSelectedFolder.GetSeqNo(msgInfo)),true);
                            m_pSelectedFolder.RemoveMessage(msgInfo);
                        }
                    }

                    // Check message flags update and new messages.
                    foreach(IMAP_MessageInfo newMsgInfo in eArgs.Messages){
                        IMAP_MessageInfo? currentMsgInfo = m_pSelectedFolder.GetMessage(newMsgInfo.UID);
                        // Existing message,check if message flags changed.
                        if(currentMsgInfo != null){
                            if(!currentMsgInfo.FlagsEqual(newMsgInfo.Flags)){
                                currentMsgInfo.UpdateFlags(IMAP_Flags_SetType.Replace,newMsgInfo.Flags);

                                await SendResponseAsync(
                                    new IMAP_r_u_Fetch(m_pSelectedFolder.GetSeqNo(currentMsgInfo),
                                    [new IMAP_t_Fetch_r_i_Flags(currentMsgInfo.Flags)]),
                                    true);
                            }
                        }
                        // New message.
                        else{
                            if(m_pSelectedFolder.MaxUid < newMsgInfo.UID){
                                m_pSelectedFolder.AddMessage(newMsgInfo);
                            } 
                            else{
                                Exception x = new InvalidOperationException(
                                    $"Folder synchronization returned a new message with UID '{newMsgInfo.UID}' " +
                                    $"which is lower than the highest known UID '{m_pSelectedFolder.MaxUid}'."
                                );

                                LogAddException(x);
                            }
                        }
                    }

                    // Send EXISTS
                    await SendResponseAsync(new IMAP_r_u_Exists(newUids.Count),true);
                }
            }
        }

        #endregion

        #region method SupportsCap

        /// <summary>
        /// Gets if session supports specified capability.
        /// </summary>
        /// <param name="capability">Capability name.</param>
        /// <returns>Returns true if session supports specified capability.</returns>
        private bool SupportsCap(string capability)
        {
            foreach(string c in m_pCapabilities){
                if(string.Equals(c,capability,StringComparison.InvariantCultureIgnoreCase)){
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region method GetMimeEntity

        /// <summary>
		/// Gets specified mime entity. Returns null if specified mime entity doesn't exist.
		/// </summary>
		/// <param name="message">Mail message.</param>
		/// <param name="partNumber">MIME part-number specifier. Nested mime entities are pointed by '.'. 
		/// For example: 1,1.1,2.1, ... .</param>
		/// <returns></returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>message</b> is null reference.</exception>
		public MIME_Entity? GetMimeEntity(Mail_Message message,string partNumber)
		{
            if(message == null){
                throw new ArgumentNullException("message");
            }
            			
			// For single part message there is only one entity with value 1.
			// Example:
			//		header
			//		entity -> 1
			
			// For multipart message, entity counting starts from MainEntity.ChildEntities
			// Example:
			//		header
			//		multipart/mixed
			//			text/plain  -> 1
			//			application/pdf  -> 2
			//          ...

            // TODO: nested rfc 822 message

            if(partNumber == string.Empty){
                return message;
            }

			// Single part
			if(message.ContentType == null || message.ContentType.Type.ToLower() != "multipart"){
				if(Convert.ToInt32(partNumber) == 1){
					return message;
				}
				else{
					return null;
				}
			}
			// multipart
			else{                
				MIME_Entity entity = message;
				string[] parts = partNumber.Split('.');
				foreach(string part in parts){
					int mEntryNo = Convert.ToInt32(part) - 1; // Enitites are zero base, mimeEntitySpecifier is 1 based.
                    if(entity.Body is MIME_b_Multipart){
                        MIME_b_Multipart multipart = (MIME_b_Multipart)entity.Body;
                        if(mEntryNo > -1 && mEntryNo < multipart.BodyParts.Count){
						    entity = multipart.BodyParts[mEntryNo];
					    }
                        else{
                            return null;
                        }
                    }
			        else{
                        return null;
                    }
				}

				return entity;
			}			
		}

		#endregion

        #region method SearchIsMatch

        /// <summary>
        /// Determines whether the specified message matches the specified
        /// SEARCH key.
        /// </summary>
        private bool SearchIsMatch(IMAP_MessageInfo message,IMAP_t_Search_Key key)
        {
            switch(key)
            {
                case IMAP_t_Search_Key_All:
                    return true;

                case IMAP_t_Search_Key_Answered:
                    return message.ContainsFlag("\\Answered");

                case IMAP_t_Search_Key_Unanswered:
                    return !message.ContainsFlag("\\Answered");

                case IMAP_t_Search_Key_Deleted:
                    return message.ContainsFlag("\\Deleted");

                case IMAP_t_Search_Key_Undeleted:
                    return !message.ContainsFlag("\\Deleted");

                case IMAP_t_Search_Key_Draft:
                    return message.ContainsFlag("\\Draft");

                case IMAP_t_Search_Key_Undraft:
                    return !message.ContainsFlag("\\Draft");

                case IMAP_t_Search_Key_Flagged:
                    return message.ContainsFlag("\\Flagged");

                case IMAP_t_Search_Key_Unflagged:
                    return !message.ContainsFlag("\\Flagged");

                case IMAP_t_Search_Key_Seen:
                    return message.ContainsFlag("\\Seen");

                case IMAP_t_Search_Key_Unseen:
                    return !message.ContainsFlag("\\Seen");

                case IMAP_t_Search_Key_Keyword x:
                    return message.ContainsFlag(x.Value);

                case IMAP_t_Search_Key_Unkeyword x:
                    return !message.ContainsFlag(x.Value);

                case IMAP_t_Search_Key_Larger x:
                    return message.Size > x.Value;

                case IMAP_t_Search_Key_Smaller x:
                    return message.Size < x.Value;

                case IMAP_t_Search_Key_Before x:
                    return DateOnly.FromDateTime(message.InternalDate) < x.Date;

                case IMAP_t_Search_Key_On x:
                    return DateOnly.FromDateTime(message.InternalDate) == x.Date;

                case IMAP_t_Search_Key_Since x:
                    return DateOnly.FromDateTime(message.InternalDate) >= x.Date;

                case IMAP_t_Search_Key_SeqSet x:
                    return x.Value.Contains(m_pSelectedFolder?.GetSeqNo(message) ?? 0);

                case IMAP_t_Search_Key_Uid x:
                    return x.Value.Contains(message.UID);

                case IMAP_t_Search_Key_Recent:
                    return false;

                case IMAP_t_Search_Key_New:
                    return false;

                case IMAP_t_Search_Key_Old:
                    return true;

                case IMAP_t_Search_Key_Not x:
                    return !SearchIsMatch(message,x.SearchKey);

                case IMAP_t_Search_Key_Or x:
                    return
                        SearchIsMatch(message,x.SearchKey1) ||
                        SearchIsMatch(message,x.SearchKey2);

                case IMAP_t_Search_Key_Group x:
                    foreach(IMAP_t_Search_Key k in x.Keys){
                        if(!SearchIsMatch(message,k)){
                            return false;
                        }
                    }

                    return true;

                default:
                    throw new NotSupportedException(
                        $"Search key '{key.GetType().Name}' is not supported by local cache search."
                    );
            }
        }

        #endregion

        #region method FindHeaderEndAsync

        /// <summary>
        /// Asynchronously scans an email stream to determine the header length using a zero-allocation 
        /// overlap buffer pattern, positions the stream precisely at the start of the body, and computes 
        /// any missing RFC 5322 terminator bytes if the headers are truncated.
        /// </summary>
        /// <param name="messageStream">The source stream containing the email, positioned at the start of the headers.</param>
        /// <param name="buffer">
        /// A reusable byte array buffer used for chunked reading and cross-chunk overlap search. 
        /// Must be at least 4 bytes in length (3 bytes for overlap tracking + at least 1 byte per read).
        /// </param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item><description><see langword="HeaderLength"/>: The length of the header block, including its terminator (or full stream length if truncated).</description></item>
        /// <item><description><see langword="MissingTerminatorBytes"/>: Any padding bytes (<c>\r\n</c> or <c>\r\n\r\n</c>) required to ensure the output stream adheres strictly to RFC 5322.</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="buffer"/> is null or less than 4 bytes long.</exception>
        private async Task<(long HeaderLength, byte[] MissingTerminatorBytes)> FindHeaderEndAsync(Stream messageStream,byte[] buffer)
        {
            if(buffer == null || buffer.Length < 4){
                throw new ArgumentException("Buffer must be at least 4 bytes (3 bytes overlap + at least 1 read byte).", nameof(buffer));
            }

            int chunkSize = buffer.Length - 3;
            long originalPosition = messageStream.Position;
            long headerLength = -1;
            byte[] missingTerminatorBytes = Array.Empty<byte>();

            int bytesRead;
            long currentScanOffset = originalPosition;

            // Strict RFC 5322 header terminator
            byte[] crlfcrlf = [(byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n'];

            byte lastByte1 = 0, lastByte2 = 0;

            while((bytesRead = await messageStream.ReadAsync(buffer.AsMemory(3, chunkSize)).ConfigureAwait(false)) > 0){
                int totalValidLength = 3 + bytesRead;
                ReadOnlySpan<byte> searchSpan = buffer.AsSpan(0, totalValidLength);

                // Vectorized check for standard \r\n\r\n
                int index = searchSpan.IndexOf(crlfcrlf);
                if(index != -1){
                    headerLength = ((currentScanOffset - 3) + index + crlfcrlf.Length) - originalPosition;

                    break;
                }

                // Save final bytes for fallback tracking
                if (searchSpan.Length >= 2){
                    lastByte1 = searchSpan[searchSpan.Length - 2];
                    lastByte2 = searchSpan[searchSpan.Length - 1];
                }

                currentScanOffset += bytesRead;
                searchSpan.Slice(searchSpan.Length - 3, 3).CopyTo(buffer.AsSpan(0, 3));
            }

            // Fallback if no valid \r\n\r\n terminator was found all the way to EOF
            if(headerLength == -1){
                headerLength = messageStream.Length - originalPosition;
                messageStream.Position = originalPosition + headerLength;

                // Check if it ends with a partial CRLF or nothing, and patch to valid \r\n\r\n
                if(lastByte1 == '\r' && lastByte2 == '\n'){
                    missingTerminatorBytes = [(byte)'\r', (byte)'\n'];
                }
                else{
                    missingTerminatorBytes = [(byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n'];
                }
            }
            else{
                messageStream.Position = originalPosition + headerLength;
            }

            return (headerLength, missingTerminatorBytes);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP server that owns this session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this session has been disposed.
        /// </exception>
        public new IMAP_Server Server
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return (IMAP_Server)base.Server;
            }
        }

        /// <summary>
        /// Gets the SASL authentication mechanisms available to this
        /// session.
        /// </summary>
        /// <remarks>
        /// The key is the SASL mechanism name and the value is the
        /// corresponding server-side SASL mechanism implementation.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this session has been disposed.
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
        /// Gets the number of invalid or unrecognized commands
        /// received during this session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this session has been disposed.
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
        /// Gets the identity of the authenticated user.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null"/> if the session has not yet
        /// successfully authenticated.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this session has been disposed.
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
        /// Gets the IMAP capabilities advertised by this session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this session has been disposed.
        /// </exception>
        public List<string> Capabilities
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pCapabilities; 
            }
        }

        /// <summary>
        /// Gets the name of the currently selected mailbox.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null"/> if no mailbox is currently
        /// selected.
        /// </remarks>
        public string? SelectedFolderName
        {
            get{ 
                if(m_pSelectedFolder == null){
                    return null;
                }
                else{
                    return m_pSelectedFolder.Folder; 
                }
            }
        }

        #endregion

        #region Events implementation

        /// <summary>
        /// Raised when a new IMAP session is created, before the server sends its initial
        /// greeting banner. Event handlers may customize the greeting or reject the
        /// connection by modifying the provided <see cref="IMAP_r_u_ServerStatus"/>.
        /// </summary>
        public event Func<IMAP_e_Started,Task>? StartedAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a LOGIN command and requires
        /// validation of the credentials supplied by the client. Event handlers
        /// must examine the provided <see cref="IMAP_e_AuthLogin"/> instance and
        /// set an appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether authentication succeeded or failed.
        /// </summary>
        public event Func<IMAP_e_AuthLogin,Task>? AuthLoginAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a NAMESPACE command and
        /// requires namespace information. Event handlers may replace the
        /// default <see cref="IMAP_r_u_Namespace"/> response and must set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the request was processed successfully or why it failed.
        /// </summary>
        /// <remarks>
        /// A default RFC-compliant namespace response is provided and represents
        /// a single personal namespace with an empty prefix and '/' hierarchy
        /// delimiter, and no other-users or shared namespaces.
        /// </remarks>
        public event Func<IMAP_e_Namespace,Task>? NamespaceAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a LIST command and requires
        /// the application to provide the folders that match the requested
        /// reference name and mailbox filter.
        /// </summary>
        public event Func<IMAP_e_List,Task>? ListAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a CREATE command. Event
        /// handlers must examine the provided <see cref="IMAP_e_Create"/>
        /// instance, create the requested mailbox if permitted, and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response describing
        /// the result of the operation.
        /// </summary>
        public event Func<IMAP_e_Create,Task>? CreateAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a DELETE command and requires
        /// deletion of the mailbox specified by the client. Event handlers must
        /// examine the provided <see cref="IMAP_e_Delete"/> instance and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the mailbox was deleted successfully or why the request failed.
        /// </summary>
        public event Func<IMAP_e_Delete,Task>? DeleteAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a RENAME command and requires
        /// renaming a mailbox. Event handlers must examine the provided
        /// <see cref="IMAP_e_Rename"/> instance and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// mailbox was renamed successfully or why the request failed.
        /// </summary>
        public event Func<IMAP_e_Rename,Task>? RenameAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes an LSUB command and requires
        /// retrieval of subscribed mailboxes matching the reference name and
        /// mailbox pattern supplied by the client. Event handlers must examine
        /// the provided <see cref="IMAP_e_LSub"/> instance and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response describing the result of
        /// the operation.
        /// </summary>
        public event Func<IMAP_e_LSub,Task>? LSubAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a SUBSCRIBE command and
        /// requires creation of a mailbox subscription. Event handlers must
        /// examine the provided <see cref="IMAP_e_Subscribe"/> instance and set
        /// an appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the subscription was created successfully or why the request
        /// failed.
        /// </summary>
        public event Func<IMAP_e_Subscribe,Task>? SubscribeAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes an UNSUBSCRIBE command and
        /// requires removal of a mailbox subscription. Event handlers must
        /// examine the provided <see cref="IMAP_e_Unsubscribe"/> instance and set
        /// an appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the request was processed successfully or why it failed.
        ///
        /// In accordance with RFC 9051, a successful response should also be
        /// returned when the specified mailbox is not currently subscribed.
        /// </summary>
        public event Func<IMAP_e_Unsubscribe,Task>? UnsubscribeAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a STATUS command and
        /// requires mailbox status information. Event handlers must examine
        /// the provided <see cref="IMAP_e_Status"/> instance, populate the
        /// available status data items, and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// request was processed successfully or why it failed.
        /// </summary>
        /// <remarks>
        /// IMAP servers include only the STATUS data items explicitly requested
        /// by the client. The server is responsible for selecting which populated
        /// values are returned in the STATUS response.
        /// </remarks>
        public event Func<IMAP_e_Status,Task>? StatusAsync = null;
         
        /// <summary>
        /// Is raised when an IMAP session processes a SELECT command and
        /// requires mailbox information. Event handlers must examine the
        /// provided <see cref="IMAP_e_Select"/> instance, populate
        /// <see cref="IMAP_e_Select.MessagesInfo"/>, and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// mailbox was selected successfully or why the request failed.
        /// </summary>
        /// <remarks>
        /// Event handlers should populate
        /// <see cref="IMAP_e_Select.MessagesInfo"/> with metadata for all
        /// messages currently contained in the mailbox. The server caches this
        /// information and uses it to service subsequent mailbox operations
        /// such as FETCH, STORE, SEARCH, COPY, EXPUNGE, and sequence number to
        /// UID mapping.
        /// </remarks>
        public event Func<IMAP_e_Select,Task>? SelectAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes an EXAMINE command and
        /// requires mailbox information. Event handlers must examine the
        /// provided <see cref="IMAP_e_Examine"/> instance, populate
        /// <see cref="IMAP_e_Examine.MessagesInfo"/>, and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// mailbox was opened successfully in read-only mode or why the
        /// request failed.
        /// </summary>
        /// <remarks>
        /// Event handlers should populate
        /// <see cref="IMAP_e_Examine.MessagesInfo"/> with metadata for all
        /// messages currently contained in the mailbox. The server caches this
        /// information and uses it to service subsequent mailbox operations
        /// such as FETCH, SEARCH, and sequence number to UID mapping.
        /// <para>
        /// A successful response should normally contain a
        /// <see cref="IMAP_t_orc_ReadOnly"/> response code.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_Examine,Task>? ExamineAsync = null;

        /// <summary>
        /// Is raised when the IMAP session synchronizes the state of the
        /// selected mailbox.
        /// </summary>
        /// <remarks>
        /// This event allows an application to determine whether the
        /// mailbox has changed since the previous synchronization and, if
        /// necessary, supply the current mailbox state.
        /// <para>
        /// The mailbox version known to the IMAP session is available
        /// through the <see cref="IMAP_e_FolderSync.KnownVersion"/>
        /// property.
        /// </para>
        /// <para>
        /// If the current mailbox version differs from
        /// <see cref="IMAP_e_FolderSync.KnownVersion"/>, the event handler
        /// should set <see cref="IMAP_e_FolderSync.Version"/> to the
        /// current mailbox version and provide the complete mailbox UID
        /// set through <see cref="IMAP_e_FolderSync.Uids"/>.
        /// </para>
        /// <para>
        /// Message information supplied through
        /// <see cref="IMAP_e_FolderSync.Messages"/> may contain either the
        /// complete mailbox snapshot or only messages that have been added
        /// or modified since the previous synchronization.
        /// </para>
        /// <para>
        /// The IMAP session uses the supplied information to detect
        /// message additions, removals, and flag changes and generates
        /// the appropriate mailbox update responses.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_FolderSync,Task>? FolderSyncAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes an APPEND command and
        /// before the message literal is read from the client. Event handlers
        /// may validate the operation, reject the APPEND request by setting
        /// <see cref="IMAP_e_AppendBegin.RejectResponse"/>, or provide a
        /// writable destination stream through
        /// <see cref="IMAP_e_AppendBegin.Stream"/> into which the incoming
        /// message will be stored.
        /// </summary>
        /// <remarks>
        /// This event is intended for mailbox validation, permission checks,
        /// quota checks, and allocation of message storage before any message
        /// data is received from the client.
        /// </remarks>
        public event Func<IMAP_e_AppendBegin,Task>? AppendBeginAsync = null;

        /// <summary>
        /// Is raised when an APPEND operation is cancelled and the message
        /// cannot be stored successfully.
        /// </summary>
        /// <remarks>
        /// Typical causes include:
        /// <list type="bullet">
        /// <item><description>Client connection closed during literal transfer.</description></item>
        /// <item><description>Session timeout.</description></item>
        /// <item><description>I/O or storage failure.</description></item>
        /// <item><description>Unexpected processing error.</description></item>
        /// </list>
        /// This event allows applications to perform cleanup of temporary
        /// files, streams, or other resources associated with the failed
        /// APPEND operation.
        /// </remarks>
        public event Func<IMAP_e_AppendCancel,Task>? AppendCancelAsync = null;

        /// <summary>
        /// Is raised when an APPEND message literal has been successfully
        /// received from the client and written to the destination stream.
        /// Event handlers may finalize message storage, update mailbox
        /// metadata, and set the final <see cref="IMAP_r_ServerStatus"/>
        /// response that will be returned to the client.
        /// </summary>
        /// <remarks>
        /// Event handlers should set an appropriate OK or NO response. For
        /// successful APPEND operations, servers supporting the UIDPLUS
        /// extension (RFC 4315) should include an
        /// <see cref="IMAP_t_orc_AppendUid"/> optional response code in the
        /// returned OK response.
        /// </remarks>
        public event Func<IMAP_e_AppendComplete,Task>? AppendCompleteAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a GETQUOTAROOT command and
        /// requires quota root information for a mailbox. Event handlers must
        /// examine the provided <see cref="IMAP_e_GetQuotaRoot"/> instance,
        /// populate <see cref="IMAP_e_GetQuotaRoot.QuotaRootResponse"/> and the
        /// corresponding <see cref="IMAP_e_GetQuotaRoot.QuotaResponses"/>
        /// collection, and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// request was processed successfully or why it failed.
        /// </summary>
        /// <remarks>
        /// For each quota root listed in
        /// <see cref="IMAP_e_GetQuotaRoot.QuotaRootResponse"/>, a corresponding
        /// <see cref="IMAP_r_u_Quota"/> response should be added to
        /// <see cref="IMAP_e_GetQuotaRoot.QuotaResponses"/>.
        /// </remarks>
        public event Func<IMAP_e_GetQuotaRoot,Task>? GetQuotaRootAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a GETQUOTA command and
        /// requires quota information for a specific quota root. Event handlers
        /// must examine the provided <see cref="IMAP_e_GetQuota"/> instance,
        /// populate the <see cref="IMAP_e_GetQuota.QuotaResponses"/> collection
        /// with the requested quota information, and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// request was processed successfully or why it failed.
        /// </summary>
        public event Func<IMAP_e_GetQuota,Task>? GetQuotaAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a GETACL command and
        /// requires access control information for a mailbox. Event handlers
        /// must examine the provided <see cref="IMAP_e_GetAcl"/> instance,
        /// populate <see cref="IMAP_e_GetAcl.AclResponse"/>, and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the request was processed successfully or why it failed.
        /// </summary>
        /// <remarks>
        /// A successful GETACL operation should provide an ACL response
        /// describing the access rights associated with the requested mailbox.
        /// </remarks>
        public event Func<IMAP_e_GetAcl,Task>? GetAclAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a SETACL command and
        /// requires modification of mailbox access rights. Event handlers must
        /// examine the provided <see cref="IMAP_e_SetAcl"/> instance and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the access control list was modified successfully or why the
        /// request failed.
        /// </summary>
        /// <remarks>
        /// The <see cref="IMAP_e_SetAcl.Rights"/> property contains the rights
        /// modification string supplied by the client. Depending on its format,
        /// the specified rights are added to, removed from, or replace the
        /// identifier's existing access rights.
        /// </remarks>
        public event Func<IMAP_e_SetAcl,Task>? SetAclAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a DELETEACL command and
        /// requires removal of an ACL entry from a mailbox. Event handlers
        /// must examine the provided <see cref="IMAP_e_DeleteAcl"/> instance
        /// and set an appropriate <see cref="IMAP_r_ServerStatus"/> response
        /// indicating whether the ACL entry was removed successfully or why
        /// the request failed.
        /// </summary>
        public event Func<IMAP_e_DeleteAcl,Task>? DeleteAclAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a LISTRIGHTS command and
        /// requires information about the rights that may be granted to an
        /// identifier for a mailbox. Event handlers must examine the provided
        /// <see cref="IMAP_e_ListRights"/> instance, populate
        /// <see cref="IMAP_e_ListRights.ListRightsResponse"/>, and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the request was processed successfully or why it failed.
        /// </summary>
        public event Func<IMAP_e_ListRights,Task>? ListRightsAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a MYRIGHTS command and
        /// requires the effective access rights of the authenticated user for
        /// a mailbox. Event handlers must examine the provided
        /// <see cref="IMAP_e_MyRights"/> instance, populate
        /// <see cref="IMAP_e_MyRights.MyRightsResponse"/>, and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the request was processed successfully or why it failed.
        /// </summary>
        public event Func<IMAP_e_MyRights,Task>? MyRightsAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a CLOSE command.
        /// Event handlers should perform any required mailbox cleanup,
        /// including permanent removal of messages marked with the
        /// \Deleted flag when the mailbox is writable.
        /// </summary>
        /// <remarks>
        /// A successful CLOSE operation normally returns an OK response.
        /// Mailboxes opened in read-only mode must not have messages
        /// expunged.
        /// </remarks>
        public event Func<IMAP_e_Close,Task>? CloseAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a FETCH command and
        /// requires the application to supply message data.
        /// </summary>
        /// <remarks>
        /// The messages included in the FETCH operation are available
        /// through the <see cref="IMAP_e_Fetch.Messages"/> property.
        /// <para>
        /// The IMAP session determines which information is required to
        /// satisfy the FETCH request and exposes the requirements through
        /// the <see cref="IMAP_e_Fetch.RequiresMessageStream"/>,
        /// <see cref="IMAP_e_Fetch.RequiresEnvelope"/>, and
        /// <see cref="IMAP_e_Fetch.RequiresBodyStructure"/> properties.
        /// </para>
        /// <para>
        /// Event handlers should call
        /// <see cref="IMAP_e_Fetch.AddDataAsync(IMAP_MessageInfo,Stream?,IMAP_t_Envelope?,IMAP_t_BodyStructure?)"/>
        /// for each message that is to be included in the FETCH response.
        /// </para>
        /// <para>
        /// A handler may provide:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// The complete RFC 5322 message stream.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// A parsed <see cref="IMAP_t_Envelope"/> instance.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// A parsed <see cref="IMAP_t_BodyStructure"/> instance.
        /// </description>
        /// </item>
        /// </list>
        /// <para>
        /// Envelope and body structure data may be supplied from an
        /// application cache, avoiding the need to parse them from the
        /// message stream for each FETCH operation.
        /// </para>
        /// <para>
        /// If <see cref="IMAP_e_Fetch.SetSeen"/> is
        /// <see langword="true"/>, the event handler must persist the
        /// corresponding <c>\Seen</c> flag update for the fetched
        /// messages.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_Fetch,Task>? FetchAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a SEARCH command and
        /// requires the application to evaluate search criteria that cannot
        /// be satisfied using the selected mailbox cache alone.
        /// </summary>
        /// <remarks>
        /// The parsed SEARCH expression is available through the
        /// <see cref="IMAP_e_Search.Criteria"/> property.
        /// <para>
        /// Event handlers should populate the
        /// <see cref="IMAP_e_Search.MatchedUids"/> property with the UIDs of
        /// all messages matching the supplied search criteria.
        /// </para>
        /// <para>
        /// The IMAP server automatically converts returned UIDs to message
        /// sequence numbers when processing a SEARCH command. For UID SEARCH
        /// commands, the returned UIDs are sent directly to the client.
        /// </para>
        /// <para>
        /// Event handlers must assign an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// search operation completed successfully or why it failed.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_Search,Task>? SearchAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a STORE command and
        /// requires one or more message flags to be updated. Event handlers
        /// must examine the provided <see cref="IMAP_e_Store"/> instance and
        /// set an appropriate <see cref="IMAP_r_ServerStatus"/> response
        /// indicating whether the operation completed successfully or why it
        /// failed.
        /// </summary>
        /// <remarks>
        /// The <see cref="IMAP_e_Store.Messages"/> property contains the
        /// messages affected by the operation.
        /// <para>
        /// The <see cref="IMAP_e_Store.FlagsSetType"/> property specifies
        /// whether the supplied flags replace, add to, or remove existing
        /// message flags.
        /// </para>
        /// <para>
        /// The flags supplied by the client are available through the
        /// <see cref="IMAP_e_Store.Flags"/> property.
        /// </para>
        /// <para>
        /// When a successful response is returned, the IMAP server updates
        /// the cached mailbox state and automatically generates any required
        /// FETCH responses associated with the STORE operation.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_Store,Task>? StoreAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a COPY command and
        /// requires one or more messages to be copied from a source mailbox
        /// to a destination mailbox. Event handlers must examine the
        /// provided <see cref="IMAP_e_Copy"/> instance and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the copy operation completed successfully or why it
        /// failed.
        /// </summary>
        /// <remarks>
        /// The original sequence set supplied by the client is available
        /// through the <see cref="IMAP_e_Copy.SequenceSet"/> property.
        /// <para>
        /// The <see cref="IMAP_e_Copy.IsUidCopy"/> property indicates whether
        /// the operation originated from a COPY or UID COPY command.
        /// </para>
        /// <para>
        /// The <see cref="IMAP_e_Copy.Messages"/> property contains the
        /// messages resolved from the supplied sequence set. Each
        /// <see cref="IMAP_MessageInfo"/> contains the application's
        /// internal message identifier together with the cached IMAP message
        /// metadata for the current session.
        /// </para>
        /// <para>
        /// For servers supporting the UIDPLUS extension (RFC 4315),
        /// successful copy operations should include an
        /// <see cref="IMAP_t_orc_CopyUid"/> optional response code in the
        /// returned OK response.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_Copy,Task>? CopyAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes a MOVE command and
        /// requires one or more messages to be moved from a source mailbox
        /// to a destination mailbox. Event handlers must examine the
        /// provided <see cref="IMAP_e_Move"/> instance and set an
        /// appropriate <see cref="IMAP_r_ServerStatus"/> response indicating
        /// whether the move operation completed successfully or why it
        /// failed.
        /// </summary>
        /// <remarks>
        /// The original sequence set supplied by the client is available
        /// through the <see cref="IMAP_e_Move.SequenceSet"/> property.
        /// <para>
        /// The <see cref="IMAP_e_Move.IsUidMove"/> property indicates whether
        /// the operation originated from a MOVE or UID MOVE command.
        /// </para>
        /// <para>
        /// The <see cref="IMAP_e_Move.Messages"/> property contains the
        /// messages resolved from the supplied sequence set. Each
        /// <see cref="IMAP_MessageInfo"/> contains the application's
        /// internal message identifier together with the cached IMAP message
        /// metadata for the current session.
        /// </para>
        /// <para>
        /// When the handler returns a successful response, the IMAP server
        /// automatically generates the required EXPUNGE responses for the
        /// moved messages and updates the selected mailbox state.
        /// </para>
        /// <para>
        /// For servers supporting the UIDPLUS extension (RFC 4315),
        /// successful move operations should include an
        /// <see cref="IMAP_t_orc_CopyUid"/> optional response code in the
        /// returned OK response.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_Move,Task>? MoveAsync = null;

        /// <summary>
        /// Is raised when an IMAP session processes an EXPUNGE or UID EXPUNGE
        /// command and requires one or more messages to be permanently removed
        /// from the selected mailbox. Event handlers must examine the provided
        /// <see cref="IMAP_e_Expunge"/> instance and set an appropriate
        /// <see cref="IMAP_r_ServerStatus"/> response indicating whether the
        /// expunge operation completed successfully or why it failed.
        /// </summary>
        /// <remarks>
        /// The <see cref="IMAP_e_Expunge.Messages"/> property contains the
        /// messages selected for removal.
        /// <para>
        /// When <see cref="IMAP_e_Expunge.IsUidExpunge"/> is
        /// <see langword="true"/>, the operation originated from a UID
        /// EXPUNGE command and the original UID sequence set is available
        /// through the <see cref="IMAP_e_Expunge.SequenceSet"/> property.
        /// </para>
        /// <para>
        /// When a successful response is returned, the IMAP server
        /// automatically generates the required EXPUNGE responses and updates
        /// the selected mailbox state.
        /// </para>
        /// </remarks>
        public event Func<IMAP_e_Expunge,Task>? ExpungeAsync = null;

        #endregion
    }
}
