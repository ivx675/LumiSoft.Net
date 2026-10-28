using LumiSoft.Net.AUTH;
using LumiSoft.Net.IMAP;
using LumiSoft.Net.IMAP.Server;
using LumiSoft.Net.IO;
using LumiSoft.Net.Log;
using LumiSoft.Net.Mail;
using LumiSoft.Net.TCP;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace IMAP_Server_Console 
{
    internal class Program 
    {
        #region class MemoryMessage

        internal sealed class MemoryMessage(
            long version,
            long uid,
            DateTime internalDate,
            DateTime? messageDate,
            string? subject,
            string? from,
            string? to,
            string? cc,
            string? bcc,
            string? body,
            long size,
            string[] flags,
            byte[] message)
        {
            public long Version { get; set; } = version;

            public long Uid { get; } = uid;

            public DateTime InternalDate { get; } = internalDate;

            public DateTime? MessageDate { get; } = messageDate;

            public string? Subject { get; } = subject;

            public string? From { get; } = from;

            public string? To { get; } = to;

            public string? Cc { get; } = cc;

            public string? Bcc { get; } = bcc;

            public string? Body { get; } = body;

            public long Size { get; } = size;

            public string[] Flags { get; set; } = flags;

            public byte[] Message { get; } = message;
        }

        #endregion

        private static List<MemoryMessage> m_pMessages    = [];
        private static long                m_InboxVersion = DateTime.Now.Ticks;
        private static long                m_UidNext      = 101;

        static async Task Main(string[] args)
        {
            try{
                m_pMessages = CreateTestMessages();

                IMAP_Server imapServer = new IMAP_Server();
                imapServer.Logger = new Logger();
                imapServer.Logger.WriteLog += (s,e) => { 
                    Console.WriteLine(e.LogEntry.EntryType + " " + e.LogEntry.Text);
                };
                imapServer.Error += (s, e) => {
                    Console.WriteLine("Error:" + e.Exception.ToString());
                };
                imapServer.ListenEndpoints = [
                    new TCP_ServerEndpoint("hostName",IPAddress.Any,143,TCP_ServerTlsMode.Explicit,CreateSelfSignedCert()),
                    new TCP_ServerEndpoint("hostName",IPAddress.Any,993,TCP_ServerTlsMode.Implicit,CreateSelfSignedCert())
                ];
                imapServer.SessionCreatedAsync += ImapServer_SessionCreated;
                imapServer.Start();

                Console.WriteLine("IMAP Server running.");
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
                Console.WriteLine("Sessions:" + imapServer.Sessions.Count);
                
                imapServer.Stop();
                imapServer.Dispose();
            }
            catch(Exception x){ 
                Console.WriteLine("Error:" + x.ToString());
            }
        }

        #region staitc method ImapServer_SessionCreated

        private static async Task ImapServer_SessionCreated(TCP_Server_e_SessionCreated<IMAP_Session> e)
        {
            e.Session.Capabilities.Clear();
            e.Session.Capabilities.AddRange([
                "IMAP4rev2",
                "NAMESPACE",
                "QUOTA",
              //"ACL",
                "IDLE",
                "ENABLE",
                "UTF8=ACCEPT",
                "SASL-IR",
                "UIDPLUS"
            ]);

            // Raised when error occurs in session.
            e.Session.ErrorAsync += async (eArgsError) => {
                Console.WriteLine("Session Error:" + eArgsError.Exception.ToString());
            };

            // Add SASL plain authentication.
            var sasPlain = new AUTH_SASL_ServerMechanism_Plain(true);
            sasPlain.Authenticate += (s,eArgs) => { 
                if(eArgs.UserName == "test" && eArgs.Password == "1234"){
                    eArgs.IsAuthenticated = true;
                }
            };
            e.Session.Authentications.Add(sasPlain.Name,sasPlain);
            
            // Raised when LOGIN authentication is received.
            e.Session.AuthLoginAsync += async (eArgsAuthUserPass) => {
                if(eArgsAuthUserPass.UserName == "test" && eArgsAuthUserPass.Password == "1234"){
                    eArgsAuthUserPass.Response = new IMAP_r_ServerStatus(eArgsAuthUserPass.Response.CommandTag,"OK","Authenticated successfully.");
                }
            };

            // Raised when a new IMAP session is established, before the greeting banner is sent.
            e.Session.StartedAsync += async (eArgsStarted) => {
                // Reject session.
                //eArgsStarted.Response = new IMAP_r_u_ServerStatus("NO",null,"Session rejected.");

                // Custom greeting text
                //eArgsStarted.Response = new IMAP_r_u_ServerStatus("OK",null,"Custom text.");
            };

            #region NamespaceAsync

            // Raised when a NAMESPACE command is received.
            e.Session.NamespaceAsync += async (eArgsNamespace) => {
                // Per IMAP rev2 RFC 9051 NAMESPACE is standard IMAP command.
                // By default RFC-compliant namespace response is provided and represents
                // a single personal namespace with an empty prefix and '/' hierarchy
                // delimiter, and no other-users or shared namespaces.

                // You can override default response here.
                //eArgsNamespace.NamespaceResponse = ...
            };

            #endregion

            #region ListAsync

            // Raised when a LIST command is received.
            e.Session.ListAsync += async (eArgsList) => {
                if(eArgsList.IsMatch("Inbox",'/')){
                    eArgsList.Folders.Add(new IMAP_r_u_List("Inbox",'/',[]));
                }
            };

            #endregion

            #region CreateAsync

            // Raised when a CREATE command is received.
            e.Session.CreateAsync += async (eArgsCreate) => {
                // TODO: UidValidity value create

                //eArgsCreate.Response = new IMAP_r_ServerStatus(eArgsCreate.Response.CommandTag,"OK","Folder created.");
            };

            #endregion

            #region DeleteAsync

            // Raised when a DELETE command is received.
            e.Session.DeleteAsync += async (eArgsDelete) => {
                //eArgsDelete.Response = new IMAP_r_ServerStatus(eArgsDelete.Response.CommandTag,"OK","Folder deleted.");
            };

            #endregion

            #region RenameAsync

            // Raised when a RENAME command is received.
            e.Session.RenameAsync += async (eArgsRename) => {
                //eArgsRename.Response = new IMAP_r_ServerStatus(eArgsRename.Response.CommandTag,"OK","Folder renamed.");
            };

            #endregion

            #region LSubAsync

            // Raised when a LSUB command is received.
            e.Session.LSubAsync += async (eArgsLSub) => {
                if(eArgsLSub.IsMatch("Inbox",'/')){
                    eArgsLSub.Folders.Add(new IMAP_r_u_LSub("Inbox",'/',[]));
                }
            };

            #endregion

            #region SubscribeAsync

            // Raised when a SUBSCRIBE command is received.
            e.Session.SubscribeAsync += async (eArgsSubscribe) => {
                //eArgsSubscribe.Response = new IMAP_r_ServerStatus(eArgsSubscribe.Response.CommandTag,"OK","Folder subscribed.");
            };

            #endregion

            #region UnsubscribeAsync

            // Raised when a UNSUBSCRIBE command is received.
            e.Session.UnsubscribeAsync += async (eArgsUnsubscribe) => {
                //eArgsUnsubscribe.Response = new IMAP_r_ServerStatus(eArgsUnsubscribe.Response.CommandTag,"OK","Folder unsubscribed.");
            };

            #endregion

            #region StatusAsync

            // Raised when a STATUS command is received.
            e.Session.StatusAsync += async (eArgsStatus) => {                
                if(string.Equals("Inbox",eArgsStatus.Folder,StringComparison.OrdinalIgnoreCase)){
                    int unseenCount = 0;
                    int deletedCount = 0;
                    long totalSize = 0;
                    long lastUid = 0;
                    foreach(var m in m_pMessages){
                        totalSize += m.Size;
                        lastUid = m.Uid;

                        if(!m.Flags.Contains("\\Seen", StringComparer.OrdinalIgnoreCase)){
                            unseenCount++;
                        }

                        if(m.Flags.Contains("\\Deleted", StringComparer.OrdinalIgnoreCase)){
                            deletedCount++;
                        }
                    }

                    eArgsStatus.Response = new IMAP_r_ServerStatus(eArgsStatus.Response.CommandTag,"OK","STATUS completed.");
                    eArgsStatus.UidValidity = 1010101;
                    eArgsStatus.UidNext = lastUid + 1;
                    eArgsStatus.MessagesCount = m_pMessages.Count;
                    eArgsStatus.UnseenCount = unseenCount;
                    eArgsStatus.DeletedCount = deletedCount;                    
                    eArgsStatus.Size = totalSize;                    
                }
            };

            #endregion

            #region SelectAsync

            // Raised when a SELECT command is received.
            e.Session.SelectAsync += async (eArgsSelect) => {
                if(string.Equals("Inbox",eArgsSelect.Folder,StringComparison.OrdinalIgnoreCase)){
                    eArgsSelect.Response = new IMAP_r_ServerStatus(eArgsSelect.Response.CommandTag,"OK",new IMAP_t_orc_ReadWrite(),"SELECT completed.");
                    eArgsSelect.Flags = ["\\Answered","\\Flagged","\\Deleted","\\Seen","\\Draft"];
                    eArgsSelect.PermanentFlags = ["\\Answered","\\Flagged","\\Deleted","\\Seen","\\Draft"];
                    eArgsSelect.UidValidity = 1010101;
                    eArgsSelect.Version = m_InboxVersion;

                    foreach(var message in m_pMessages){
                        eArgsSelect.MessagesInfo.Add(new IMAP_MessageInfo("internalID",message.Uid,message.Flags,message.Size,message.InternalDate));
                    }
                }
                else{
                    eArgsSelect.Response = new IMAP_r_ServerStatus(eArgsSelect.Response.CommandTag,"NO","No such mailbox.");
                }
            };

            #endregion

            #region ExamineAsync

            // Raised when a EXAMINE command is received.
            e.Session.ExamineAsync += async (eArgsExamine) => {
                if(string.Equals("Inbox",eArgsExamine.Folder,StringComparison.OrdinalIgnoreCase)){
                    eArgsExamine.Response = new IMAP_r_ServerStatus(eArgsExamine.Response.CommandTag,"OK",new IMAP_t_orc_ReadOnly(),"SELECT completed.");
                    eArgsExamine.Flags = ["\\Answered","\\Flagged","\\Deleted","\\Seen","\\Draft"];
                    eArgsExamine.UidValidity = 1010101;
                    eArgsExamine.Version = m_InboxVersion;

                    foreach(var message in m_pMessages){
                        eArgsExamine.MessagesInfo.Add(new IMAP_MessageInfo("internalID",message.Uid,message.Flags,message.Size,message.InternalDate));
                    }
                }
                else{
                    eArgsExamine.Response = new IMAP_r_ServerStatus(eArgsExamine.Response.CommandTag,"NO","No such mailbox.");
                }
            };

            #endregion

            #region FolderSyncAsync

            // Raised when IMAP session wants to know if folder messages count or existing messages flags has changed.
            e.Session.FolderSyncAsync += async (eArgsFoldrSync) => {
                var uids = new List<long>();
                var changedMessages = new List<IMAP_MessageInfo>();
                foreach(var message in m_pMessages){
                    uids.Add(message.Uid);
                    if(eArgsFoldrSync.KnownVersion < message.Version){
                        changedMessages.Add(new IMAP_MessageInfo(message.Uid.ToString(),message.Uid,message.Flags,message.Size,message.InternalDate));
                    }
                }
                
                eArgsFoldrSync.Version = m_InboxVersion;
                eArgsFoldrSync.Uids = uids.ToArray();
                eArgsFoldrSync.Messages = changedMessages.ToArray();
            };

            #endregion

            #region GetQuotaRootAsync

            // Raised when a GETQUOTAROOT command is received.
            e.Session.GetQuotaRootAsync += async (eArgsGetQuotaRoot) => {
                if(string.Equals("Inbox",eArgsGetQuotaRoot.Folder,StringComparison.OrdinalIgnoreCase)){
                    eArgsGetQuotaRoot.QuotaRootResponse = new IMAP_r_u_QuotaRoot(eArgsGetQuotaRoot.Folder,[""]);
                    eArgsGetQuotaRoot.QuotaResponses.Add(new IMAP_r_u_Quota("",[new IMAP_t_Quota_Entry("STORAGE",0,100000)]));
                    eArgsGetQuotaRoot.Response = new IMAP_r_ServerStatus(eArgsGetQuotaRoot.Response.CommandTag,"OK","GETQUOTAROOT completed.");
                }
                else{
                    eArgsGetQuotaRoot.Response = new IMAP_r_ServerStatus(eArgsGetQuotaRoot.Response.CommandTag,"NO","No such mailbox.");
                }
            };

            #endregion

            #region AppendBeginAsync AppendCancelAsync AppendCompleteAsync

            // Raised when a APPEND command starts processing.
            e.Session.AppendBeginAsync += async (eArgsAppendBegin) => {
                // NOTE: Real implementation should use non-memory stream here.
                eArgsAppendBegin.Stream = new MemoryStream();
            };

            // Raised when a APPEND command cancels(Session timeout,client closes connection, ...) processing.
            e.Session.AppendCancelAsync += async (eArgsAppendCancel) => {
                // Clean up, delete message, ...
                eArgsAppendCancel.Stream.Dispose();
            };

            // Raised when a APPEND command has completed processing.
            e.Session.AppendCompleteAsync += async (eArgsAppendComplete) => {
                m_InboxVersion = DateTime.Now.Ticks;
                var stream = (MemoryStream)eArgsAppendComplete.Stream;

                Mail_Message message = Mail_Message.ParseFromByte(stream.ToArray());
                m_pMessages.Add(
                    new MemoryMessage(
                        DateTime.Now.Ticks,
                        uid: m_UidNext++,
                        internalDate: eArgsAppendComplete.InternalDate ?? DateTime.Now,
                        messageDate: message.Date,
                        subject: message.Subject,
                        from: message.From?.ToString(),
                        to: message.To?.ToString(),
                        cc: message.Cc?.ToString(),
                        bcc: message.Bcc?.ToString(),
                        body: message.BodyText,
                        size: stream.Length,
                        flags: eArgsAppendComplete.Flags,
                        message: stream.ToArray()
                    )
                );

                eArgsAppendComplete.Stream.Dispose();
            };

            #endregion

            #region GetQuotaAsync

            // Raised when a GETQUOTA command is received.
            e.Session.GetQuotaAsync += async (eArgsGetQuota) => {
                if(string.Equals("",eArgsGetQuota.QuotaRoot,StringComparison.OrdinalIgnoreCase)){
                    long toalSize = 0;
                    foreach(var m in m_pMessages){
                        toalSize += m.Size;
                    }

                    eArgsGetQuota.QuotaResponses.Add(new IMAP_r_u_Quota("",[new IMAP_t_Quota_Entry("STORAGE",toalSize,100000)]));
                    eArgsGetQuota.Response = new IMAP_r_ServerStatus(eArgsGetQuota.Response.CommandTag,"OK","GETQUOTA completed.");
                }
                else{
                    eArgsGetQuota.Response = new IMAP_r_ServerStatus(eArgsGetQuota.Response.CommandTag,"NO","No such quota root.");
                }
            };

            #endregion

            #region GetAclAsync

            // Raised when a GETACL command is received.
            e.Session.GetAclAsync += async (eArgsGetAcl) => {
            };

            #endregion

            #region SetAclAsync

            // Raised when a SETACL command is received.
            e.Session.SetAclAsync += async (eArgsSetAcl) => {
            };

            #endregion

            #region DeleteAclAsync

            // Raised when a DELETEACL command is received.
            e.Session.DeleteAclAsync += async (eArgsDeleteAcl) => {
            };

            #endregion

            #region ListRightsAsync

            // Raised when a LISTRIGHTS command is received.
            e.Session.ListRightsAsync += async (eArgsListRights) => {
            };

            #endregion

            #region MyRightsAsync

            // Raised when a MYRIGHTS command is received.
            e.Session.MyRightsAsync += async (eArgsMyRights) => {
            };

            #endregion


            #region CloseAsync

            // Raised when a CLOSE command is received.
            e.Session.CloseAsync += async (eArgsClose) => {
                // Delete messages \Deleted flag set.
                if(!eArgsClose.IsReadOnly){
                    HashSet<long> uidsToDelete = new();

                    foreach(var messageInfo in eArgsClose.DeletedMessages){
                        uidsToDelete.Add(messageInfo.UID);
                    }

                    if(m_pMessages.RemoveAll(x => uidsToDelete.Contains(x.Uid)) > 0){
                        m_InboxVersion = DateTime.Now.Ticks;
                    }
                }
            };

            #endregion

            #region FetchAsync

            // Raised when a FETCH command is received.
            e.Session.FetchAsync += async (eArgsFetch) => {
                foreach(var message in eArgsFetch.Messages) {
                    MemoryStream?         messageStream = null;
                    IMAP_t_Envelope?      envelope      = null;
                    IMAP_t_BodyStructure? bodyStructure = null;
                    try{
                        if(eArgsFetch.RequiresMessageStream){
                            MemoryMessage m = m_pMessages.First(x => x.Uid == message.UID);
                            messageStream =  new MemoryStream(m.Message);
                        }

                        if(eArgsFetch.RequiresEnvelope){
                            // NOTE: ENVELOPE parsing is resource intensive operation. You should construct it once,
                            // when message is stored to mailbox and cache value.

                            MemoryMessage m = m_pMessages.First(x => x.Uid == message.UID);
                            Mail_Message mailMessage = Mail_Message.ParseFromByte(m.Message);
                            envelope = IMAP_t_Envelope.FromMessage(mailMessage);
                        }

                        if(eArgsFetch.RequiresBodyStructure){
                            // NOTE: BODYSTRUCTURE parsing is resource intensive operation. You should construct it once,
                            // when message is stored to mailbox and cache value.

                            MemoryMessage m = m_pMessages.First(x => x.Uid == message.UID);
                            Mail_Message mailMessage = Mail_Message.ParseFromByte(m.Message);
                            bodyStructure = IMAP_t_BodyStructure.FromMessage(mailMessage);
                        }

                        // Pass data to IMAP session.
                        await eArgsFetch.AddDataAsync(message,messageStream,envelope,bodyStructure);
                    }                    
                    finally{
                        messageStream?.Dispose();
                    }

                    // Set \Seen flags.
                    if(eArgsFetch.SetSeen){
                        MemoryMessage m = m_pMessages.First(x => x.Uid == message.UID);
                        if(!m.Flags.Contains("\\Seen",StringComparer.OrdinalIgnoreCase)){            
                            List<string> flags = new List<string>(m.Flags);
                            flags.Add("\\Seen");
                            m.Flags = flags.ToArray();                    
                        }
                    }
                }
            };

            #endregion

            #region SearchAsync

            // Raised when a SEARCH or UID SEARCH command is received.
            e.Session.SearchAsync += async (eArgsSearch) => {
                /*
                 * NOTE:
                 *
                 * This SEARCH implementation is intended for demonstration purposes.
                 * A real-world implementation should store searchable message
                 * metadata (UID, dates, size, flags, subject, sender, recipients,
                 * etc.) in a database and translate IMAP SEARCH criteria into a
                 * database query. Full-text search operations such as BODY and TEXT
                 * should use the storage engine's full-text indexing facilities.
                */

                var matchedUids = new List<long>();
                foreach(var m in m_pMessages){                    
                    if(SearchIsMatch(eArgsSearch.Criteria,m.Uid,m.InternalDate,m.MessageDate,m.Subject,m.From,m.To,m.Cc,m.Bcc,m.Body,m.Size,m.Flags)){
                        matchedUids.Add(m.Uid);
                    }
                }

                eArgsSearch.MatchedUids = matchedUids.ToArray();
            };

            #endregion

            #region StoreAsync

            // Raised when a STORE or UID STORE command is received.
            e.Session.StoreAsync += async (eArgsStore) => {
                if(string.Equals("Inbox",eArgsStore.Folder,StringComparison.OrdinalIgnoreCase)){
                    m_InboxVersion = DateTime.Now.Ticks;

                    foreach(var messageInfo in eArgsStore.Messages){
                        MemoryMessage message = m_pMessages.First(x => x.Uid == messageInfo.UID);

                        HashSet<string> flags = new(message.Flags,StringComparer.OrdinalIgnoreCase);

                        switch(eArgsStore.FlagsSetType)
                        {
                            case IMAP_Flags_SetType.Add:

                                foreach(string flag in eArgsStore.Flags){
                                    flags.Add(flag);
                                }

                                break;

                            case IMAP_Flags_SetType.Remove:

                                foreach(string flag in eArgsStore.Flags){
                                    flags.Remove(flag);
                                }

                                break;

                            case IMAP_Flags_SetType.Replace:

                                flags.Clear();

                                foreach(string flag in eArgsStore.Flags){
                                    flags.Add(flag);
                                }

                                break;
                        }

                        message.Version = DateTime.Now.Ticks;
                        message.Flags   = flags.ToArray();                       
                    }

                    eArgsStore.Response = new IMAP_r_ServerStatus(eArgsStore.Response.CommandTag,"OK","STORE completed.");
                }
            };

            #endregion

            #region CopyAsync

            // Raised when a COPY or UID COPY command is received.
            e.Session.CopyAsync += async (eArgsCopy) => {
                //eArgsCopy.Response = new IMAP_r_ServerStatus(eArgsCopy.Response.CommandTag,"OK","COPY completed.");
            };

            #endregion

            #region MoveAsync

            // Raised when a MOVE or UID MOVE command is received.
            e.Session.MoveAsync += async (eArgsMove) => {
                //eArgsMove.Response = new IMAP_r_ServerStatus(eArgsMove.Response.CommandTag,"OK","MOVE completed.");
            };

            #endregion

            #region ExpungeAsync

            // Raised when a EXPUNGE or UID EXPUNGE command is received.
            e.Session.ExpungeAsync += async (eArgsExpunge) => {
                foreach(var messageInfo in eArgsExpunge.Messages){
                    if(m_pMessages.RemoveAll(x => x.Uid == messageInfo.UID) > 0) {
                        m_InboxVersion = DateTime.Now.Ticks;
                    }
                }

                eArgsExpunge.Response = new IMAP_r_ServerStatus(eArgsExpunge.Response.CommandTag,"OK","EXPUNGE completed.");
            };

            #endregion
        }

        #endregion

        #region static method CreateSelfSignedCert

        private static X509Certificate2 CreateSelfSignedCert()
        {
            var dn = new X500DistinguishedName("CN=MyTestCert");

            using var rsa = RSA.Create(2048);

            var req = new CertificateRequest(
                dn,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Basic constraints: not a CA
            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));

            // Key usage: required for TLS server
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.KeyEncipherment,
                    false));

            // Subject Key Identifier
            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            // REQUIRED: Server Authentication EKU
            var eku = new OidCollection {
                new Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
            };
            req.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(eku, false));

            // REQUIRED: Subject Alternative Name (SAN)
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName("MyTestCert");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            req.CertificateExtensions.Add(sanBuilder.Build());

            var cert = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(1));

            // FIX: Export to PFX and re-import to resolve Windows Schannel ephemeral key limitations
            var pfxBytes = cert.Export(X509ContentType.Pfx);
    
            // Dispose the original ephemeral certificate to free resources
            cert.Dispose();

            cert = X509CertificateLoader.LoadPkcs12(
                pfxBytes,
                password: null,
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

            return cert;
        }

        #endregion

        #region static method SearchIsMatch

        private static bool SearchIsMatch(
            IMAP_t_Search_Key key,
            long uid,
            DateTime internalDate,
            DateTime? messageDate,
            string? subject,
            string? from,
            string? to,
            string? cc,
            string? bcc,
            string? body,
            long size,
            string[] flags
        )
        {
            switch(key)
            {
                case IMAP_t_Search_Key_All:
                    return true;

                case IMAP_t_Search_Key_Answered:
                    return flags.Contains("\\Answered");

                case IMAP_t_Search_Key_Unanswered:
                    return !flags.Contains("\\Answered");

                case IMAP_t_Search_Key_Deleted:
                    return flags.Contains("\\Deleted");

                case IMAP_t_Search_Key_Undeleted:
                    return !flags.Contains("\\Deleted");

                case IMAP_t_Search_Key_Draft:
                    return flags.Contains("\\Draft");

                case IMAP_t_Search_Key_Undraft:
                    return !flags.Contains("\\Draft");

                case IMAP_t_Search_Key_Flagged:
                    return flags.Contains("\\Flagged");

                case IMAP_t_Search_Key_Unflagged:
                    return !flags.Contains("\\Flagged");

                case IMAP_t_Search_Key_Seen:
                    return flags.Contains("\\Seen");

                case IMAP_t_Search_Key_Unseen:
                    return !flags.Contains("\\Seen");

                case IMAP_t_Search_Key_Keyword x:
                    return flags.Contains(x.Value,StringComparer.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_Unkeyword x:
                    return !flags.Contains(x.Value,StringComparer.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_Larger x:
                    return size > x.Value;

                case IMAP_t_Search_Key_Smaller x:
                    return size < x.Value;

                case IMAP_t_Search_Key_Before x:
                    return DateOnly.FromDateTime(internalDate) < x.Date;

                case IMAP_t_Search_Key_On x:
                    return DateOnly.FromDateTime(internalDate) == x.Date;

                case IMAP_t_Search_Key_Since x:
                    return DateOnly.FromDateTime(internalDate) >= x.Date;

                case IMAP_t_Search_Key_SentBefore x:
                    return messageDate != null && DateOnly.FromDateTime(messageDate.Value) < x.Date;

                case IMAP_t_Search_Key_SentOn x:
                    return messageDate != null && DateOnly.FromDateTime(messageDate.Value) == x.Date;

                case IMAP_t_Search_Key_SentSince x:
                    return messageDate != null && DateOnly.FromDateTime(messageDate.Value) >= x.Date;

                case IMAP_t_Search_Key_Subject x:
                    return (subject ?? "").Contains(x.Value,StringComparison.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_From x:
                    return (from ?? "").Contains(x.Value,StringComparison.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_To x:
                    return (to ?? "").Contains(x.Value,StringComparison.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_Cc x:
                    return (cc ?? "").Contains(x.Value,StringComparison.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_Bcc x:
                    return (bcc ?? "").Contains(x.Value,StringComparison.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_Body x:
                    return (body ?? "").Contains(x.Value,StringComparison.OrdinalIgnoreCase);

                case IMAP_t_Search_Key_Text x:
                {
                    string text =
                        (subject ?? "") + "\r\n" +
                        (from ?? "") + "\r\n" +
                        (to ?? "") + "\r\n" +
                        (cc ?? "") + "\r\n" +
                        (bcc ?? "") + "\r\n" +
                        (body ?? "");

                    return text.Contains(x.Value,StringComparison.OrdinalIgnoreCase);
                }

                case IMAP_t_Search_Key_Header:
                    // Not implemented in sample application.
                    return false;

                case IMAP_t_Search_Key_Uid x:
                    return x.Value.Contains(uid);

                case IMAP_t_Search_Key_Recent:
                    return false; // IMAP4rev2 compatibility.

                case IMAP_t_Search_Key_New:
                    return false; // IMAP4rev2 compatibility.

                case IMAP_t_Search_Key_Old:
                    return true; // IMAP4rev2 compatibility.

                case IMAP_t_Search_Key_Not x:
                    return !SearchIsMatch(
                        x.SearchKey,
                        uid,
                        internalDate,
                        messageDate,
                        subject,
                        from,
                        to,
                        cc,
                        bcc,
                        body,
                        size,
                        flags
                    );

                case IMAP_t_Search_Key_Or x:
                    return
                        SearchIsMatch(
                            x.SearchKey1,
                            uid,
                            internalDate,
                            messageDate,
                            subject,
                            from,
                            to,
                            cc,
                            bcc,
                            body,
                            size,
                            flags
                        )
                        ||
                        SearchIsMatch(
                            x.SearchKey2,
                            uid,
                            internalDate,
                            messageDate,
                            subject,
                            from,
                            to,
                            cc,
                            bcc,
                            body,
                            size,
                            flags
                        );

                case IMAP_t_Search_Key_Group x:

                    foreach(IMAP_t_Search_Key groupKey in x.Keys){
                        if(!SearchIsMatch(
                            groupKey,
                            uid,
                            internalDate,
                            messageDate,
                            subject,
                            from,
                            to,
                            cc,
                            bcc,
                            body,
                            size,
                            flags
                        )){
                            return false;
                        }
                    }

                    return true;

                default:
                    throw new NotSupportedException(
                        $"Search key '{key.GetType().Name}' is not supported."
                    );
            }
        }

        #endregion

        #region static method CreateTestMessages

        private static List<MemoryMessage> CreateTestMessages(int count = 100)
        {
            Random random = new Random();

            string[] availableFlags =
            [
                "\\Seen",
                "\\Answered",
                "\\Flagged",
                "\\Deleted",
                "\\Draft"
            ];

            List<MemoryMessage> messages = new List<MemoryMessage>();

            DateTime today = DateTime.UtcNow.Date;

            for(int i = 1; i <= count; i++)
            {
                // InternalDate: set to today with a random time.
                DateTime internalDate = today.AddHours(random.Next(0, 24)).AddMinutes(random.Next(60));
        
                // Message Date: random time within the last 5 days up to now.
                DateTime messageDate = DateTime.UtcNow.AddDays(-random.Next(0, 6)).AddHours(-random.Next(24)).AddMinutes(-random.Next(60));

                // Random flags.
                List<string> flags = new();
                foreach(string flag in availableFlags){
                    if(random.Next(2) == 0){
                        flags.Add(flag);
                    }
                }

                // Generate main text body.
                int lineCount = random.Next(1, 20);
                StringBuilder bodyBuilder = new StringBuilder();

                for(int line = 1; line <= lineCount; line++){
                    bodyBuilder.AppendLine($"This is line {line} of message {i}");
                }

                string bodyText = bodyBuilder.ToString();
                string attachmentText = $"This is the text attachment content for test message {i}.\r\nGenerated automatically.";
                string boundary = $"----=_NextPart_Boundary_{i}_{Guid.NewGuid():N}";
                string formattedDate = messageDate.ToString("r"); // RFC 1123 format (e.g., Sun, 21 Aug 2026 14:00:00 GMT)

                // Construct raw MIME multipart/mixed message
                string rawMessageString = 
                    $"From: user{i}@example.com\r\n" +
                    $"To: recipient@example.com\r\n" +
                    $"Subject: Test message {i}\r\n" +
                    $"Date: {formattedDate}\r\n" +
                    $"Message-ID: <msgid.{i}@xxx>\r\n" +
                    $"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n" +
                    $"\r\n" +
                    $"This is a multi-part message in MIME format.\r\n\r\n" +
                    // Part 1: Plain Text Body
                    $"--{boundary}\r\n" +
                    $"Content-Type: text/plain; charset=utf-8\r\n" +
                    $"Content-Transfer-Encoding: 7bit\r\n" +
                    $"\r\n" +
                    $"{bodyText}\r\n" +
                    // Part 2: Text Attachment
                    $"--{boundary}\r\n" +
                    $"Content-Type: text/plain; name=\"attachment_{i}.txt\"\r\n" +
                    $"Content-Disposition: attachment; filename=\"attachment_{i}.txt\"\r\n" +
                    $"Content-Transfer-Encoding: 7bit\r\n" +
                    $"\r\n" +
                    $"{attachmentText}\r\n" +
                    // End of multipart
                    $"--{boundary}--\r\n";

                byte[] rawMessage = Encoding.UTF8.GetBytes(rawMessageString);

                messages.Add(
                    new MemoryMessage(
                        DateTime.Now.Ticks,
                        uid: i,
                        internalDate: internalDate,
                        messageDate: messageDate,
                        subject: $"Test message {i}",
                        from: $"user{i}@example.com",
                        to: "recipient@example.com",
                        cc: i % 3 == 0 ? "cc@example.com" : null,
                        bcc: i % 5 == 0 ? "bcc@example.com" : null,
                        body: bodyText,
                        size: rawMessage.Length,
                        flags: flags.ToArray(),
                        message: rawMessage
                    )
                );
            }

            return messages;
        }

        #endregion
    }
}
