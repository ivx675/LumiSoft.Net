using LumiSoft.Net;
using LumiSoft.Net.Log;
using LumiSoft.Net.AUTH;
using LumiSoft.Net.TCP;
using LumiSoft.Net.SMTP;
using LumiSoft.Net.SMTP.Server;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SMTP_Server_Console 
{
    internal class Program 
    {
        static async Task Main(string[] args)
        {
            try{
                SMTP_Server smtpServer = new SMTP_Server();
                smtpServer.MaxMessageSize = 10 * 1000 * 1000;
                smtpServer.Logger = new Logger();
                smtpServer.Logger.WriteLog += (s,e) => { 
                    Console.WriteLine(e.LogEntry.EntryType + " " + e.LogEntry.Text);
                };
                smtpServer.Error += (s, e) => {
                    Console.WriteLine("Error:" + e.Exception.ToString());
                };
                smtpServer.ListenEndpoints = [
                    new TCP_ServerEndpoint("hostName",IPAddress.Any,25,TCP_ServerTlsMode.Explicit,CreateSelfSignedCert()),
                    new TCP_ServerEndpoint("hostName",IPAddress.Any,465,TCP_ServerTlsMode.Implicit,CreateSelfSignedCert())
                ];
                smtpServer.SessionCreatedAsync += SmtpServer_SessionCreated;
                smtpServer.Start();

                Console.WriteLine("SMTP Server running.");
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
                Console.WriteLine("Sessions:" + smtpServer.Sessions.Count);
                Console.ReadLine();
                
                smtpServer.Stop();
                smtpServer.Dispose();
            }
            catch(Exception x){ 
                Console.WriteLine("Error:" + x.ToString());
            }
        }


        #region static method SmtpServer_SessionCreated

        private static async Task SmtpServer_SessionCreated(TCP_Server_e_SessionCreated<SMTP_Session> e)
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

            // Raised when a new SMTP session is established, before the greeting banner is sent.
            e.Session.StartedAsync += async (eArgsStarted) => {
                // Reject session.
                //eArgsStarted.Response = new SMTP_ServerResponse(554,null,"Session rejected.");

                // Custom greeting text
                //eArgsStarted.Response = new SMTP_ServerResponse(220,null,"domain Custom text.");
            };

            // HELO command received.
            e.Session.HeloAsync += async (eArgsHelo) => {
                //eArgsHelo.Response = new SMTP_ServerResponse(502,null,"HELO Disabled.");
            };

            // EHLO command received.
            e.Session.EhloAsync += async (eArgsEhlo) => {
                // Reject EHLO.
                //eArgsEhlo.Response = new SMTP_ServerResponse(550,null,"EHLO rejected by policy.");
            };

            // MAIL FROM: command received.
            e.Session.MailFromAsync += async (eArgsMailFrom) => {
                IPAddress remoteIP = eArgsMailFrom.Session.RemoteEndPoint.Address;

                // Check non-authetnticated users for PTR(HELO name match) -> SPF -> MX -> domain.
                if(!eArgsMailFrom.Session.IsAuthenticated && !IsLanOrLocalIp(remoteIP)){
                    try{
                        // Run validation
                        await SMTP_SenderValidator.ValidateAsync(remoteIP,eArgsMailFrom.Session.EhloHost!,eArgsMailFrom.MailFrom.Mailbox);
            
                        // If no exception is thrown, validation succeeded (SMTP 250 OK)
                        eArgsMailFrom.Response = new (250,new SMTP_t_EnhancedStatusCode(2,1,0),"Sender validation PASSED successfully.");
                    }
                    catch (SMTP_ValidatorException ex){
                        // Map to professional SMTP response codes based on IsTemporary flag
                        if(ex.IsTemporary){
                            eArgsMailFrom.Response = new (451,new SMTP_t_EnhancedStatusCode(4,3,0),$"Temporary Error (Try Again Later): {ex.ErrorText}");
                        }
                        else{
                            eArgsMailFrom.Response = new (550,new SMTP_t_EnhancedStatusCode(5,7,1),$"Permanent Rejection (Policy Violation): {ex.ErrorText}");
                        }
                    }
                    catch (Exception ex){
                        eArgsMailFrom.Response = new (554,new SMTP_t_EnhancedStatusCode(5,3,0),$"Unexpected error: {ex.Message}");
                    }
                }
            };

            // RCPT TO: command received.
            e.Session.RcptToAsync += async (eArgsRcptTo) => {
                // Reject MAIL TO:.
                //eArgsRcptTo.Response = new SMTP_ServerResponse(550,null,"No such user here.");
            };


            // Raised when DATA or BDAT needs stream where to store message
            e.Session.MessageStoringBeginAsync += async (eArgsStoringBegin) => {
                // You should use non-memory based stream here.
                eArgsStoringBegin.StoreStream = new MemoryStream();
            };

            // Raised when: Client disconnects,message reading errors or session inactivity timeout.
            e.Session.MessageStoringCancelAsync += async (eArgsStoringCancel) => {
                // Release stream. For file based stream, you can delete file.
                eArgsStoringCancel.Stream.Dispose();
            };

            // Raised when message successfully stored by server.
            e.Session.MessageStoringCompleteAsync += async (eArgsStoringCompleted) => {
                // Reject DATA/BDAT
                //eArgsStoringCompleted.Response = new SMTP_ServerResponse(554,new SMTP_t_EnhancedStatusCode(5,7,1),"Transaction failed (policy rejection).");
                
                eArgsStoringCompleted.Stream.Close();
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

        #region static method IsLanOrLocalIp

        /// <summary>
        /// Determines whether the specified IP address belongs to a local loopback 
        /// or a private local area network (LAN) range (such as RFC 1918 private blocks or link-local addresses).
        /// </summary>
        /// <param name="ip">The <see cref="IPAddress"/> to evaluate.</param>
        /// <returns>
        /// <c>true</c> if the IP address is a loopback, private IPv4, or local IPv6 address; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="ip"/> is <c>null</c> (optional handling).</exception>
        private static bool IsLanOrLocalIp(IPAddress ip)
        {
            if (ip == null) return false;

            // Handle IPv4 mapped to IPv6 (e.g., ::ffff:127.0.0.1)
            if(ip.IsIPv4MappedToIPv6){
                ip = ip.MapToIPv4();
            }

            // Loopback (127.0.0.1 / ::1)
            if(IPAddress.IsLoopback(ip)){
                return true;
            }

            byte[] bytes = ip.GetAddressBytes();

            // IPv4 Private Ranges (RFC 1918 & Link-Local)
            if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork){
                // 10.0.0.0/8
                if (bytes[0] == 10) return true;
        
                // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
                if (bytes[0] == 172 && (bytes[1] >= 16 && bytes[1] <= 31)) return true;
        
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168) return true;
        
                // 169.254.0.0/16 (Link-Local / APIPA)
                if (bytes[0] == 169 && bytes[1] == 254) return true;
            }
            // IPv6 Private / Local Ranges
            else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6){
                // Unique Local Addresses (fc00::/7)
                if ((bytes[0] & 0xfe) == 0xfc) return true;
        
                // Link-Local Addresses (fe80::/10)
                if ((bytes[0] == 0xfe) && ((bytes[1] & 0xc0) == 0x80)) return true;
            }

            return false;
        }
    }

    #endregion
}
