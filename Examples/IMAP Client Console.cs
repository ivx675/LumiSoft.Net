using LumiSoft.Net.IMAP;
using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.Log;
using System.Net.Sockets;

namespace IMAP_Console 
{
    internal class Program 
    {
        static async Task Main(string[] args)
        {
            string host     = "";
            int    port     = 993;
            bool   ssl      = true;
            bool   tls      = false;
            string userName = "";
            string password = "";

            try{
                using(var imapClient = new IMAP_Client()){
                    imapClient.Logger = new Logger();
                    imapClient.Logger.WriteLog += (s,e) => { 
                        //Console.WriteLine(e.LogEntry.RemoteEndPoint?.ToString() + " " + e.LogEntry.EntryType + " " + e.LogEntry.Text);
                    };
                    
                    await imapClient.ConnectAsync(
                        null, // Null = auto allocated.
                        host,
                        port,
                        AddressFamily.Unspecified, // AddressFamily.Unspecified = .NET dual-stack socket (IPv4 + IPv6)
                        ssl,
                        null // Ssl options: Remote certificate validation,client certificate, ...
                     );
                    Console.WriteLine("Connected:");
                    // Get and store server supported capbailities.
                    await imapClient.CapabilityAsync();

                    if(!ssl && tls){
                        await imapClient.StartTlsAsync(
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
                    await imapClient.AuthenticateAsync(
                        imapClient.AuthGetStrongestMethod(userName,password) // Selects the strongest auth method server supports
                    );                                                       
                    
                    Console.WriteLine();
                    Console.WriteLine("------Subscribed folders--------------------");
                    foreach(var folder in await imapClient.FoldersSubscribedAsync()){
                        Console.WriteLine(folder.FolderName);
                    }
                    Console.WriteLine("--------------------------------------------");
                    Console.WriteLine();

                    await imapClient.FolderSelectAsync("Inbox");

                    
                    //---- Easy access through message object ------------------------------------------------------------------------  
                    var msgSet = await imapClient.SelectedFolder!.CreateMessageSetAsync(
                        false,
                        IMAP_t_SeqSet.Parse("1:*"),
                        true,   // Specifies if to prefetch Envelope. Can be later lazy fetched in message object.
                        false,  // Specifies if to prefetch BodyStructure. Can be later lazy fetched in message object.
                        true    // Specifies if to prefetch Gmail. Can't be later lazy fetched in message object.
                    );
                    
                    Console.WriteLine();
                    Console.WriteLine("------ Eassy access messages ------------------------------");
                    foreach(var msg in msgSet) {
                        Console.WriteLine($"Message UID: {msg.Uid} InternalDate: {msg.InternalDate} Subject: {msg.Envelope?.Subject}");

                        // Possible message operations
                        //msg.CopyAsync
                        //msg.MoveAsync
                        //msg.MarkForDeletionAsync
                        //msg.StoreFlagsAsync
                        //msg.HeaderToMailMessageAsync
                        //msg.HeaderToStringAsync
                        //msg.HeaderToByteAsync
                        //msg.HeaderToStreamAsync
                        //msg.ToMailMessageAsync
                        //msg.ToByteAsync
                        //msg.ToStreamAsync
                        //msg.BodyToStreamAsync
                        //msg.GetEnvelopeAsync
                        //msg.GetBodyStructureAsync
                    }

                    Console.WriteLine("------------------------------------------------------------");
                    Console.WriteLine();
                    //----------------------------------------------------------------------------------------------------------------

                    //---- IMAP Fetch without easy access ----------------------------------------------------------------------------                    

                    // Create callback. It is called for each FETCH response.
                    EventHandler<IMAP_r_u_Fetch> responseCallback = delegate(object? sender,IMAP_r_u_Fetch e){
                        Console.WriteLine();
                        Console.WriteLine("------ Fetched message------------------------------------------------------------------------------");

                        Console.WriteLine($"Message UID: {e.Uid?.Uid} InternalDate: {e.InternalDate?.Date} Subject: {e.Envelope?.Envelope?.Subject}");

                        Console.WriteLine("----------------------------------------------------------------------------------------------------");
                        Console.WriteLine();
                    };

                    // Create callback. It is called for each FETCH data-item in response, which need stream where to store data.
                    // This callbackis optional. If not used, data is stored to mem-tmp file hybrid stream.
                    // This method is called for data-itmes like: BODY[],RFC822,RFC822.HEADER,RFC822.TEXT
                    EventHandler<IMAP_e_Fetch_GetStoreStream> getStoreStreamCallback = delegate(object? sender,IMAP_e_Fetch_GetStoreStream e){
                        //e.DataItem   // Fetch reponse data-item requesting to store data
                        //e.DataLength // Amount of data to store
                        //e.Stream;    // Set stream where to store data
                    };

                    await imapClient.MessagesFetchAsync(
                        IMAP_t_SeqSet.Parse("1:*"),
                        new IMAP_t_Fetch_i[]{ 
                            new IMAP_t_Fetch_i_Uid(),
                            new IMAP_t_Fetch_i_Rfc822Size(),
                            new IMAP_t_Fetch_i_InternalDate(),
                            new IMAP_t_Fetch_i_Flags(),
                            new IMAP_t_Fetch_i_Envelope(),
                            //new IMAP_t_Fetch_i_BodyStructure(),
                            //new IMAP_t_Fetch_i_Rfc822()
                            //new IMAP_t_Fetch_i_Rfc822Text(),
                            //new IMAP_t_Fetch_i_Rfc822Header(),
                            //new IMAP_t_Fetch_i_Body(null,null,null)
                        },
                        responseCallback,
                        getStoreStreamCallback 
                    );                    
                    //----------------------------------------------------------------------------------------------------------------

                    // You can combine OR AND(IMAP_t_Search_Key_Group) NOT with oher search key or search key group as needed.
                    var criteria = new IMAP_t_Search_Key_Group(new IMAP_t_Search_Key[]{
                        new IMAP_t_Search_Key_Unseen(),
                        //new IMAP_t_Search_Key_Subject("test"),
                    });                    
                    long[] matchedUids = await imapClient.MessagesSearchUidAsync(criteria);

                    if(matchedUids.Length > 0){
                        var msgSetSearchResult = await imapClient.SelectedFolder!.CreateMessageSetAsync(
                            true,
                            new IMAP_t_SeqSet(matchedUids),
                            true,   // Specifies if to prefetch Envelope. Can be later lazy fetched in message object.
                            false,  // Specifies if to prefetch BodyStructure. Can be later lazy fetched in message object.
                            true    // Specifies if to prefetch Gmail. Can't be later lazy fetched in message object.
                        );

                        Console.WriteLine();
                        Console.WriteLine("------ Eassy access search matched messages ------------------------------");
                        foreach(var msg in msgSetSearchResult) {
                            Console.WriteLine($"Message UID: {msg.Uid} InternalDate: {msg.InternalDate} Subject: {msg.Envelope?.Subject}");
                        }
                        Console.WriteLine("--------------------------------------------------------------------------");
                        Console.WriteLine();
                    }
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
