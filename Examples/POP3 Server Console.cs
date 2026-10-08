using LumiSoft.Net;
using LumiSoft.Net.AUTH;
using LumiSoft.Net.Log;
using LumiSoft.Net.POP3;
using LumiSoft.Net.POP3.Server;
using LumiSoft.Net.TCP;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace POP3_Server_Console 
{
    internal class Program 
    {
        private static Dictionary<string,byte[]> testMessages = new Dictionary<string, byte[]>();

        static async Task Main(string[] args)
        {
            try{
                // Create test messages.
                testMessages.Add(Guid.NewGuid().ToString().Substring(0,4),CreateTestMessage(1,10));
                testMessages.Add(Guid.NewGuid().ToString().Substring(0,4),CreateTestMessage(2,20));
                testMessages.Add(Guid.NewGuid().ToString().Substring(0,4),CreateTestMessage(3,30));
                testMessages.Add(Guid.NewGuid().ToString().Substring(0,4),CreateTestMessage(4,40));

                POP3_Server pop3Server = new POP3_Server();
                pop3Server.Logger = new Logger();
                pop3Server.Logger.WriteLog += (s,e) => { 
                    Console.WriteLine(e.LogEntry.EntryType + " " + e.LogEntry.Text);
                };
                pop3Server.Error += (s, e) => {
                    Console.WriteLine("Error:" + e.Exception.ToString());
                };
                pop3Server.ListenEndpoints = [
                    new TCP_ServerEndpoint("hostName",IPAddress.Any,110,TCP_ServerTlsMode.Explicit,CreateSelfSignedCert()),
                    new TCP_ServerEndpoint("hostName",IPAddress.Any,995,TCP_ServerTlsMode.Implicit,CreateSelfSignedCert())
                ];
                pop3Server.SessionCreatedAsync += Pop3Server_SessionCreated;
                pop3Server.Start();

                Console.WriteLine("POP3 Server running.");
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
                Console.WriteLine("Sessions:" + pop3Server.Sessions.Count);
                Console.ReadLine();
                
                pop3Server.Stop();
                pop3Server.Dispose();
            }
            catch(Exception x){ 
                Console.WriteLine("Error:" + x.ToString());
            }
        }

        #region staitc method Pop3Server_SessionCreated

        private static async Task Pop3Server_SessionCreated(TCP_Server_e_SessionCreated<POP3_Session> e)
        {
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

            // Raised when USER/PASS authentication is received.
            e.Session.AuthUserPassAsync += async (eArgsAuthUserPass) => {
                if(eArgsAuthUserPass.UserName == "test" && eArgsAuthUserPass.Password == "1234"){
                    eArgsAuthUserPass.Response = new POP3_ServerResponse("+OK",null,"Authenticated successfully.");
                }
            };

            // Raised when a new POP3 session is established, before the greeting banner is sent.
            e.Session.StartedAsync += async (eArgsStarted) => {
                // Reject session.
                //eArgsStarted.Response = new POP3_ServerResponse("-ERR",null,"Session rejected.");

                // Custom greeting text
                //eArgsStarted.Response = new POP3_ServerResponse("+OK",null,"domain Custom text.");
            };

            // Raised when user authentication is completed and the session needs to load the messages info for the authenticated user.
            e.Session.LoadMessagesInfoAsync += async (eArgsLoadMessagesInfo) => {
                foreach(var msgInfo in testMessages){
                    // Real implementation should return permanent unique ID for the message, and the message size in bytes.
                    eArgsLoadMessagesInfo.Messages.Add(new POP3_ServerMessage(msgInfo.Key,msgInfo.Value.Length));
                }
            };

            // Raised when TOP or RETR command is received, the session needs to provide a stream
            // containing the message data for the requested message.
            e.Session.GetMessageStreamAsync += async (eArgsGetMessageStream) => {
                eArgsGetMessageStream.MessageStream = new MemoryStream(testMessages[eArgsGetMessageStream.Message.UID]);
            };

            // Raised when POP3 session is about to be closed, messages marked for deletion must be deleted.
            e.Session.DeleteMessagesAsync += async (eArgsDeleteMessages) => {
                // You should delete messages from your storage here, eArgsDeleteMessages.Messages contains the list of messages to delete.
                foreach (var msg in eArgsDeleteMessages.Messages) {
                    Console.WriteLine("Message to delete: " + msg.UID);
                }
            };
        }

        #endregion

        #region static method CreateSelfSignedCert

        public static X509Certificate2 CreateSelfSignedCert()
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

        #region static method CreateTestMessage

        public static byte[] CreateTestMessage(int msgNumber,int contentLines)
        {
            var sb = new StringBuilder();

            // Minimal valid RFC‑822 headers
            sb.Append("From: test@example.com\r\n");
            sb.Append("To: user@example.com\r\n");
            sb.Append("Subject: Test Message " + msgNumber + "\r\n");
            sb.Append("Date: " + DateTime.UtcNow.ToString("r") + "\r\n");
            sb.Append("Message-ID: <test-" + Guid.NewGuid().ToString() + "@example.com>\r\n");
            sb.Append("\r\n"); // End of headers

            // Body
            for(int i = 0; i < contentLines; i++){
                if(i == 0) {
                    sb.Append(".");
                }
                sb.Append($"This is line {i + 1}\r\n");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion
    }
}
