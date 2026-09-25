
using LumiSoft.Net.Log;
using LumiSoft.Net.Mail;
using LumiSoft.Net.POP3;
using LumiSoft.Net.POP3.Client;
using System.Net.Sockets;

namespace POP3_Client_Console 
{
    internal class Program 
    {
        static async Task Main(string[] args)
        {
            string host     = "";
            int    port     = 995;
            bool   ssl      = true;
            bool   tls      = false;
            string userName = "";
            string password = "";

            try{
                using(var pop3Client = new POP3_Client()){
                    pop3Client.Logger = new Logger();
                    pop3Client.Logger.WriteLog += (s,e) => { 
                        //Console.WriteLine(e.LogEntry.RemoteEndPoint?.ToString() + " " + e.LogEntry.EntryType + " " + e.LogEntry.Text);
                    };
                    
                    await pop3Client.ConnectAsync(
                        null, // Null = auto allocated.
                        host,
                        port,
                        AddressFamily.Unspecified, // AddressFamily.Unspecified = .NET dual-stack socket (IPv4 + IPv6)
                        ssl,
                        null // Ssl options: Remote certificate validation,client certificate, ...
                     );

                    // Get and store server supported capbailities.
                    await pop3Client.CapaAsync();

                    if(!ssl && tls){
                        await pop3Client.StlsAsync(
                            null // Ssl options: Remote certificate validation,client certificate, ...
                        );
                    }

                    // Supported auth:
                    // AUTH_SASL_Client_CramMd5
                    // AUTH_SASL_Client_DigestMd5
                    // AUTH_SASL_Client_Plain
                    // AUTH_SASL_Client_Login
                    // AUTH_SASL_Client_Ntlm
                    // AUTH_SASL_Client_XOAuth
                    // AUTH_SASL_Client_XOAuth2
                    await pop3Client.AuthAsync(
                        pop3Client.AuthGetStrongestMethod(userName,password) // Selects the strongest auth method server supports
                    );
                    
                    //---- Easy access through message object ------------------------------------------------------------------------

                    // Load messages info and expose it through .Messages property.
                    await pop3Client.LoadMessagesAsync();

                    Console.WriteLine();
                    Console.WriteLine("------ Eassy access messages ------------------------------");
                    foreach(POP3_ClientMessage msg in pop3Client.Messages){
                        Mail_Message header = await msg.HeaderToMailMessageAsync();
                        Console.WriteLine($"Message UID: {msg.UniqueId} InternalDate: {header.Date} Subject: {header.Subject}");

                        // Possible message operations
                        // msg.MarkForDeletionAsync
                        // msg.HeaderToMailMessageAsync
                        // msg.HeaderToStringAsync
                        // msg.HeaderToByteAsync
                        // msg.HeaderToStreamAsync
                        // msg.ToMailMessageAsync
                        // msg.ToByteAsync
                        // msg.ToStreamAsync
                        // msg.TopLinesToByteAsync
                        // msg.TopLinesToStreamAsync
                    }
                    
                    Console.WriteLine("------------------------------------------------------------");
                    Console.WriteLine();
                    
                    //----------------------------------------------------------------------------------------------------------------


                    //---- POP3 LIST + TOP without easy access -----------------------------------------------------------------------
                    Console.WriteLine();
                    Console.WriteLine("------ POP3 LIST + TOP messages ----------------------------");

                    var msgList = await pop3Client.ListAsync();
                    foreach(var msgInfo in msgList){
                        MemoryStream ms = new MemoryStream();
                        await pop3Client.TopAsync(msgInfo.MessageNumber,0,ms,int.MaxValue);
                        ms.Position = 0;

                        Mail_Message header = Mail_Message.ParseFromStream(ms);
                        Console.WriteLine($"Message SeqNo: {msgInfo.MessageNumber} InternalDate: {header.Date} Subject: {header.Subject}");
                    }
                    
                    Console.WriteLine("------------------------------------------------------------");
                    Console.WriteLine();
                    //----------------------------------------------------------------------------------------------------------------
                }
            }
            catch(Exception x){ 
                Console.WriteLine("Error:" + x.ToString());
            }

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
