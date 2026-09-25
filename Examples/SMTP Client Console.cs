using LumiSoft.Net;
using LumiSoft.Net.Log;
using LumiSoft.Net.Mail;
using LumiSoft.Net.POP3.Client;
using LumiSoft.Net.SMTP.Client;
using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace SMTP_Client_Console 
{
    internal class Program 
    {
        static async Task Main(string[] args)
        {
            string host     = "";
            int    port     = 465;
            bool   ssl      = true;
            bool   tls      = false;
            string userName = "";
            string password = "";
            string from     = "from@domain.com";
            string to       = "to@domain.com";

            try{
                Mail_Message message = Mail_Message.Create(
                    new Mail_t_Mailbox(null,from),
                    new []{new Mail_t_Mailbox(null,to)},
                    null,
                    null,
                    "Test email",
                    "This is test message.",
                    null,
                    null
                );

                //------ Quick send -----------------------------------------------------------------------------------
                Logger logger = new Logger();
                logger.WriteLog += (s,e) => { 
                    Console.WriteLine(e.LogEntry.EntryType + " " + e.LogEntry.Text);
                };

                await SMTP_Client.QuickSendAsync(
                    null,
                    null,
                    host,
                    25,
                    AddressFamily.Unspecified, // AddressFamily.Unspecified = .NET dual-stack socket (IPv4 + IPv6)
                    TcpClientSecurity.UseTlsIfSupported,
                    null,
                    userName,
                    password,
                    from,
                    new[] {to},
                    message,
                    logger,
                    CancellationToken.None
                );                
                //-----------------------------------------------------------------------------------------------------
                
                 Console.WriteLine();
                 Console.WriteLine();

                //---- Normal sending ---------------------------------------------------------------------------------
                using(var smtpClient = new SMTP_Client()){
                    smtpClient.Logger = new Logger();
                    smtpClient.Logger.WriteLog += (s,e) => { 
                        Console.WriteLine(e.LogEntry.RemoteEndPoint?.ToString() + " " + e.LogEntry.EntryType + " " + e.LogEntry.Text);
                    };
                    
                    await smtpClient.ConnectAsync(
                        null, // Null = auto allocated.
                        host,
                        port,
                        AddressFamily.Unspecified, // AddressFamily.Unspecified = .NET dual-stack socket (IPv4 + IPv6)
                        ssl,
                        null // Ssl options: Remote certificate validation,client certificate, ...
                    );

                    // Response to server greeting and store server supported capbailities.
                    await smtpClient.EhloHeloAsync(Dns.GetHostName());

                    if(!ssl && tls){
                        await smtpClient.StartTlsAsync(
                            null // Ssl options: Remote certificate validation,client certificate, ...
                        );

                        // Per SMTP specification, we need to send EHLO after STARTTLS.
                        await smtpClient.EhloHeloAsync(Dns.GetHostName());
                    }

                    // Supported auth:
                    // AUTH_SASL_Client_CramMd5
                    // AUTH_SASL_Client_DigestMd5
                    // AUTH_SASL_Client_Plain
                    // AUTH_SASL_Client_Login
                    // AUTH_SASL_Client_Ntlm
                    // AUTH_SASL_Client_XOAuth
                    // AUTH_SASL_Client_XOAuth2
                    await smtpClient.AuthAsync(
                        smtpClient.AuthGetStrongestMethod(userName,password) // Selects the strongest auth method server supports
                    );

                    await smtpClient.MailFromAsync(from,-1);

                    await smtpClient.RcptToAsync(to);

                    await smtpClient.SendMessageAsync(message,true);
                }                
                //-----------------------------------------------------------------------------------------------------
            }
            catch(Exception x){ 
                Console.WriteLine("Error:" + x.ToString());
            }

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
