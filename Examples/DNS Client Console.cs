using System.Net;
using LumiSoft.Net.DNS;
using LumiSoft.Net.DNS.Client;
using LumiSoft.Net.SMTP.Server;

namespace DNS_Client_Console 
{
    internal class Program 
    {
        static void Main(string[] args)
        { 
            try{
                DNS_Client dsnClient = DNS_Client.Static;
                dsnClient.DnsServers = [IPAddress.Parse("8.8.8.8"),IPAddress.Parse("8.8.4.4")];

                Console.WriteLine("dsnClient.GetHostAddresses(\"www.google.com\")");
                foreach(IPAddress ip in dsnClient.GetHostAddresses("www.google.com")){
                    Console.WriteLine($"IP: {ip}");
                }

                Console.WriteLine();
                Console.WriteLine("dsnClient.Query(\"www.google.com\",DNS_RecordType.A,2000)");
                var response = dsnClient.Query("www.google.com",DNS_RecordType.A,2000); 
                if(response != null && response.ResponseCode == DNS_ResponseCode.NoError) {
                    foreach(var record in response.Answers.A) {
                        Console.WriteLine($"Answer: {record}");
                    }
                }
                else {
                    Console.WriteLine("No answers received.");
                }
                
                Console.WriteLine();
                Console.WriteLine("dsnClient.Query(\"google.com\",DNS_RecordType.MX,2000)");
                response = dsnClient.Query("google.com",DNS_RecordType.MX,2000); 
                if(response != null && response.ResponseCode == DNS_ResponseCode.NoError) {
                    foreach(var record in response.Answers.MX) {
                        Console.WriteLine($"Answer: {record}");
                    }
                    foreach(var record in response.AdditionalAnswers.All) {
                        Console.WriteLine($"Additional Answer: {record}");
                    }
                }
                else {
                    Console.WriteLine("No answers received.");
                }

                Console.WriteLine();
                Console.WriteLine("dsnClient.Query(\"google.com\",DNS_RecordType.TXT,2000)");
                response = dsnClient.Query("google.com",DNS_RecordType.TXT,2000); 
                if(response != null && response.ResponseCode == DNS_ResponseCode.NoError) {
                    foreach(var record in response.Answers.TXT) {
                        Console.WriteLine($"Answer: {record}");
                    }
                }
                else {
                    Console.WriteLine("No answers received.");
                }
            }
            catch(Exception x){
                Console.WriteLine($"Error: {x.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }        
    }
}
