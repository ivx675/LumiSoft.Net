using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace LumiSoft.Net.DNS.Client
{
	/// <summary>
	/// DNS client.
	/// </summary>
	public class DNS_Client : IDisposable
    {        
        private static DNS_Client  m_pDnsClient;
        // 
        private bool                                  m_IsDisposed    = false;
        private IPAddress[]                           m_DnsServers    = [];
		private bool                                  m_UseDnsCache   = true;
        private Dictionary<int,DNS_ClientTransaction> m_pTransactions;
        private Socket                                m_pIPv4Socket;
        private Socket?                               m_pIPv6Socket;
        private Random                                m_pRandom;
        private DNS_ClientCache                       m_pCache;

		/// <summary>
		/// Static constructor.
		/// </summary>
		static DNS_Client()
		{
			// Try to get default NIC dns servers.
            List<IPAddress> dnsServers = new List<IPAddress>();
			try{
                foreach(NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()){
                    if(nic.OperationalStatus == OperationalStatus.Up){
                        foreach(IPAddress ip in nic.GetIPProperties().DnsAddresses){
                            if(ip.AddressFamily == AddressFamily.InterNetwork){
                                if(!dnsServers.Contains(ip)){
                                    dnsServers.Add(ip);
                                }
                            }
                        }
                    }
                }
			}
			catch{
            }

            m_pDnsClient = new DNS_Client();
            m_pDnsClient.DnsServers = dnsServers.ToArray();
        }

		/// <summary>
		/// Default constructor.
		/// </summary>
		public DNS_Client()
		{
            m_pTransactions = new Dictionary<int,DNS_ClientTransaction>();
            m_pRandom = new Random();
            m_pCache = new DNS_ClientCache();

            m_pIPv4Socket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
            m_pIPv4Socket.Bind(new IPEndPoint(IPAddress.Any,0));
            _= ReadResponsesLoopAsync(m_pIPv4Socket);

            if(Socket.OSSupportsIPv6){
                m_pIPv6Socket = new Socket(AddressFamily.InterNetworkV6,SocketType.Dgram,ProtocolType.Udp);
                m_pIPv6Socket.Bind(new IPEndPoint(IPAddress.IPv6Any,0));
                _= ReadResponsesLoopAsync(m_pIPv6Socket);
            }
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public void Dispose()
        {
            if(m_IsDisposed){
                return;
            }
            m_IsDisposed = true;

            m_pIPv4Socket.Dispose();

            if(m_pIPv6Socket != null){
                m_pIPv6Socket.Dispose();
            }

            m_pCache.Dispose();
        }

        #endregion


        #region method CreateTransaction

        /// <summary>
        /// Creates new DNS client transaction.
        /// </summary>
        /// <param name="queryType">Query type.</param>
        /// <param name="queryText">Query text. It depends on queryType.</param>
        /// <param name="timeout">Transaction timeout in milliseconds. DNS default value is 2000, value 0 means no timeout - this is not suggested.</param>
        /// <returns>Returns DNS client transaction.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>queryText</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        /// <remarks>Creates asynchronous(non-blocking) DNS transaction. Call <see cref="DNS_ClientTransaction.Start"/> to start transaction.
        /// It is allowd to create multiple conccurent transactions.</remarks>
        public DNS_ClientTransaction CreateTransaction(DNS_RecordType queryType,string queryText,int timeout)
        {
            return CreateTransaction(this.DnsServers,queryType,queryText,timeout);
        }

        /// <summary>
        /// Creates new DNS client transaction.
        /// </summary>
        /// <param name="dnsServers">DNS servers.</param>
        /// <param name="queryType">Query type.</param>
        /// <param name="queryText">Query text. It depends on queryType.</param>
        /// <param name="timeout">Transaction timeout in milliseconds. DNS default value is 2000, value 0 means no timeout - this is not suggested.</param>
        /// <returns>Returns DNS client transaction.</returns>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and and this method is accessed.</exception>
        /// <exception cref="ArgumentNullException">Is raised when <b>dnsServers</b> or <b>queryText</b> is null reference.</exception>
        /// <exception cref="ArgumentException">Is raised when any of the arguments has invalid value.</exception>
        /// <remarks>Creates asynchronous(non-blocking) DNS transaction. Call <see cref="DNS_ClientTransaction.Start"/> to start transaction.
        /// It is allowd to create multiple conccurent transactions.</remarks>
        public DNS_ClientTransaction CreateTransaction(IPAddress[] dnsServers,DNS_RecordType queryType,string queryText,int timeout)
        {   
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(dnsServers == null){
                throw new ArgumentNullException(nameof(dnsServers));
            }
            if(queryText == null){
                throw new ArgumentNullException(nameof(queryText));
            }
            if(queryText == string.Empty){
                throw new ArgumentException("Argument 'queryText' must be a non-empty string.",nameof(queryText));
            }
            if(queryType == DNS_RecordType.PTR){
                if(!IPAddress.TryParse(queryText,out _)){
                    throw new ArgumentException("Argument 'queryText' value must be IP address if queryType == DNS_RecordType.PTR.",nameof(queryText));
                }
            }

            if(queryType == DNS_RecordType.PTR){
				string ip = queryText;

				// See if IP is ok.
				IPAddress ipA = IPAddress.Parse(ip);		
				queryText = "";

				// IPv6
				if(ipA.AddressFamily == AddressFamily.InterNetworkV6){
					// 4321:0:1:2:3:4:567:89ab
					// would be
					// b.a.9.8.7.6.5.0.4.0.0.0.3.0.0.0.2.0.0.0.1.0.0.0.0.0.0.0.1.2.3.4.IP6.ARPA
					
					char[] ipChars = ip.Replace(":","").ToCharArray();
					for(int i=ipChars.Length - 1;i>-1;i--){
						queryText += ipChars[i] + ".";
					}
					queryText += "IP6.ARPA";
				}
				// IPv4
				else{
					// 213.35.221.186
					// would be
					// 186.221.35.213.in-addr.arpa

					string[] ipParts = ip.Split('.');
					//--- Reverse IP ----------
					for(int i=3;i>-1;i--){
						queryText += ipParts[i] + ".";
					}
					queryText += "in-addr.arpa";
				}
			}

            // Create transaction ID.
            int transactionID = 0;
            lock(m_pTransactions){
                while(true){
                    transactionID = m_pRandom.Next(0xFFFF);

                    // We got not used transaction ID.
                    if(!m_pTransactions.ContainsKey(transactionID)){
                        break;
                    }
                }
            }

            DNS_ClientTransaction retVal = new DNS_ClientTransaction(this,dnsServers,transactionID,queryType,queryText,timeout);
            retVal.StateChanged += delegate(object? s1,EventArgs<DNS_ClientTransaction> e1){
                if(retVal.State == DNS_ClientTransactionState.Disposed){
                    lock(m_pTransactions){
                        m_pTransactions.Remove(e1.Value.ID);
                    }
                }
            };
            lock(m_pTransactions){
                m_pTransactions.Add(retVal.ID,retVal);
            }

            return retVal;
        }

        #endregion

        #region method Query

        /// <summary>
        /// Sends a DNS query synchronously using the client's configured DNS servers
        /// and returns the first server response. This method blocks the calling
        /// thread until the query operation completes.
        /// </summary>
        /// <param name="queryText">
        /// The domain name or record owner name to query.
        /// </param>
        /// <param name="queryType">
        /// The DNS record type to request (A, AAAA, MX, TXT, etc.).
        /// </param>
        /// <param name="timeout">
        /// The timeout for the DNS transaction, in milliseconds.
        /// </param>
        /// <returns>
        /// The DNS server response if one was received. If no server responds before
        /// the transaction completes or times out, the result is <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the DNS client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="queryText"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="queryText"/> is null or empty.
        /// </exception>
		public DNS_ServerResponse? Query(string queryText,DNS_RecordType queryType,int timeout)
		{
            return Query(this.DnsServers,queryText,queryType,timeout);
        }

		/// <summary>
        /// Sends a DNS query synchronously and returns the first server response. This
        /// method blocks the calling thread until the asynchronous query operation
        /// completes.
        /// </summary>
        /// <param name="dnsServers">
        /// The list of DNS servers to query. All servers are queried in parallel.
        /// </param>
        /// <param name="queryText">
        /// The domain name or record owner name to query.
        /// </param>
        /// <param name="queryType">
        /// The DNS record type to request (A, AAAA, MX, TXT, etc.).
        /// </param>
        /// <param name="timeout">
        /// The timeout for the DNS transaction, in milliseconds.
        /// </param>
        /// <returns>
        /// The DNS server response if one was received. If no server responds before
        /// the transaction completes or times out, the result is <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the DNS client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dnsServers"/> or <paramref name="queryText"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="dnsServers"/> is empty or
        /// <paramref name="queryText"/> is null or empty.
        /// </exception>
		public DNS_ServerResponse? Query(IPAddress[] dnsServers,string queryText,DNS_RecordType queryType,int timeout)
		{
            return QueryAsync(dnsServers,queryText,queryType,timeout).GetAwaiter().GetResult();
		}

        #endregion

        #region method QueryAsync

        /// <summary>
        /// Sends a DNS query asynchronously and returns the first server response, or
        /// <c>null</c> if no response is received before the transaction completes.
        /// </summary>
        /// <param name="queryText">
        /// The domain name or record owner name to query.
        /// </param>
        /// <param name="queryType">
        /// The DNS record type to request (A, AAAA, MX, TXT, etc.).
        /// </param>
        /// <param name="timeout">
        /// The timeout for the DNS transaction, in milliseconds. If the timeout
        /// expires, the transaction is disposed.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the DNS server response if one was received. If no server responds before
        /// the transaction completes or times out, the result is <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the DNS client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="queryText"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if 
        /// <paramref name="queryText"/> is null or empty.
        /// </exception>
        public Task<DNS_ServerResponse?> QueryAsync(string queryText,DNS_RecordType queryType,int timeout)
        {
            return QueryAsync(this.DnsServers,queryText,queryType,timeout);
        }

        /// <summary>
        /// Sends a DNS query asynchronously and returns the first server response, or
        /// <c>null</c> if no response is received before the transaction completes.
        /// </summary>
        /// <param name="dnsServers">
        /// The list of DNS servers to query. All servers are queried in parallel.
        /// </param>
        /// <param name="queryText">
        /// The domain name or record owner name to query.
        /// </param>
        /// <param name="queryType">
        /// The DNS record type to request (A, AAAA, MX, TXT, etc.).
        /// </param>
        /// <param name="timeout">
        /// The timeout for the DNS transaction, in milliseconds. If the timeout
        /// expires, the transaction is disposed.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the DNS server response if one was received. If no server responds before
        /// the transaction completes or times out, the result is <c>null</c>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the DNS client instance has already been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dnsServers"/> or <paramref name="queryText"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="dnsServers"/> is empty or
        /// <paramref name="queryText"/> is null or empty.
        /// </exception>
        public Task<DNS_ServerResponse?> QueryAsync(IPAddress[] dnsServers,string queryText,DNS_RecordType queryType,int timeout)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(GetType().Name);
            }
            if(dnsServers == null){
                throw new ArgumentNullException(nameof(dnsServers));
            }
            if(dnsServers.Length < 1){
                throw new ArgumentException("The dnsServers array cannot be empty.",nameof(dnsServers));
            }
            if(string.IsNullOrEmpty(queryText)){
                throw new ArgumentException("The queryText cannot be null or empty.",nameof(queryText));
            }

            var tcs = new TaskCompletionSource<DNS_ServerResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);

            DNS_ClientTransaction transaction = CreateTransaction(dnsServers,queryType,queryText,timeout);

            transaction.StateChanged += (s, e) => {
                if(transaction.State == DNS_ClientTransactionState.Completed ||
                    transaction.State == DNS_ClientTransactionState.Disposed)
                {
                    tcs.TrySetResult(transaction.Response);
                }
            };

            transaction.Start();

            return tcs.Task;
        }

        #endregion

        
        #region method GetHostAddresses

        /// <summary>
        /// Synchronously resolves the specified host name or IP address to an array of IP addresses 
        /// using dual-stack (IPv4 and IPv6) DNS queries.
        /// </summary>
        /// <param name="hostNameOrIP">The host name or IP address to resolve.</param>
        /// <returns>
        /// An array of <see cref="IPAddress"/> instances associated with the specified host name, or an empty array if no records are found.
        /// </returns>
        /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="hostNameOrIP"/> is <c>null</c>.</exception>
        /// <exception cref="IOException">Thrown if a network timeout occurs and no response is received from the DNS server.</exception>
        /// <exception cref="DNS_ClientException">Thrown if the DNS server returns a fatal error code (such as Server Failure or Refused) or 
        /// if the domain does not exist (NameError) on both queries.</exception>
        public IPAddress[] GetHostAddresses(string hostNameOrIP)
        {
            return GetHostAddressesAsync(hostNameOrIP).GetAwaiter().GetResult();
        }

        #endregion

        #region method GetHostAddressesAsync

        /// <summary>
        /// Asynchronously resolves the specified host name or IP address to an array of IP addresses 
        /// using dual-stack (IPv4 and IPv6) DNS queries.
        /// </summary>
        /// <param name="hostNameOrIP">The host name or IP address to resolve.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains an array of <see cref="IPAddress"/> 
        /// instances associated with the specified host name, or an empty array if no records are found.
        /// </returns>
        /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="hostNameOrIP"/> is <c>null</c>.</exception>
        /// <exception cref="IOException">Thrown if a network timeout occurs and no response is received from the DNS server.</exception>
        /// <exception cref="DNS_ClientException">Thrown if the DNS server returns a fatal error code (such as Server Failure or Refused) or 
        /// if the domain does not exist (NameError) on both queries.</exception>
        public async Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrIP)
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(hostNameOrIP == null){
                throw new ArgumentNullException(nameof(hostNameOrIP));
            }

            // hostNameOrIP is a IP address, return it.
            if(IPAddress.TryParse(hostNameOrIP,out IPAddress? ip)){
                return [ip];
            }

            // This is probably NetBios name.
			if(hostNameOrIP.IndexOf(".") == -1){
                return await System.Net.Dns.GetHostAddressesAsync(hostNameOrIP);
            }

            Task<DNS_ServerResponse?> taskA    = QueryAsync(hostNameOrIP,DNS_RecordType.A,1000);
            Task<DNS_ServerResponse?> taskAAAA = QueryAsync(hostNameOrIP,DNS_RecordType.AAAA,1000);

            await Task.WhenAll(taskA,taskAAAA);

            DNS_ServerResponse? responseA    = taskA.Result;
            DNS_ServerResponse? responseAAAA = taskAAAA.Result;

            if(responseA == null || responseAAAA == null){
                throw new IOException("No response received from DNS server (timeout).");
            }

            var retVal = new List<IPAddress>();
                        
            if(responseA.ResponseCode == DNS_ResponseCode.NoError){
                retVal.AddRange(responseA.Answers.A.Select(r => r.IP).ToList());
            }
            else if(responseA.ResponseCode != DNS_ResponseCode.NameError){
                throw new DNS_ClientException(responseA.ResponseCode);
            }
                        
            if(responseAAAA.ResponseCode == DNS_ResponseCode.NoError){
                retVal.AddRange(responseAAAA.Answers.AAAA.Select(r => r.IP).ToList());
            }
            else if(responseAAAA.ResponseCode != DNS_ResponseCode.NameError){
                throw new DNS_ClientException(responseAAAA.ResponseCode);
            }
            
            // Some buggy servers may return NameError for empty A or AAAA records, so fail only if both queries has NameError.
            if(responseA.ResponseCode == DNS_ResponseCode.NameError && responseAAAA.ResponseCode == DNS_ResponseCode.NameError){
                throw new DNS_ClientException(DNS_ResponseCode.NameError);
            }            

            return retVal.ToArray();
        }

        #endregion


        #region method ReadResponsesLoopAsync

        private async Task ReadResponsesLoopAsync(Socket socket)
        {            
            var buffer = new byte[1500];

            EndPoint remoteEP = socket.AddressFamily == AddressFamily.InterNetwork
                ? new IPEndPoint(IPAddress.Any,0)
                : new IPEndPoint(IPAddress.IPv6Any,0);

            while(!m_IsDisposed){
                try{
                    var result = await socket.ReceiveFromAsync(new ArraySegment<byte>(buffer),SocketFlags.None,remoteEP);

                    // Copy only the received bytes
                    var packet = new byte[result.ReceivedBytes];
                    Buffer.BlockCopy(buffer, 0, packet, 0, result.ReceivedBytes);

                    _ = ProcessResponseAsync(((IPEndPoint)result.RemoteEndPoint).Address, packet);
                }
                catch(ObjectDisposedException){
                    // Socket closed → exit loop
                    return;
                }
                catch(Exception){
                    continue;
                }
            }
        }

        #endregion

        #region method ProcessResponseAsync

        private async Task ProcessResponseAsync(IPAddress serverIP,byte[] packet)
        {
            try{
                DNS_ServerResponse serverResponse = DNS_ServerResponse.Parse(packet);
                DNS_ClientTransaction? transaction = null;
                // Pass response to transaction.
                if(m_pTransactions.TryGetValue(serverResponse.ID, out transaction)) {
                    if(transaction.State == DNS_ClientTransactionState.Active){
                        // Cache query.
                        if(m_UseDnsCache && serverResponse.ResponseCode == DNS_ResponseCode.NoError) {
                            m_pCache.AddToCache(transaction.QName, (int)transaction.QType,serverResponse);
                        }

                        transaction.ProcessResponse(serverIP,serverResponse);
                    }
                }
            }
            catch{
                // We don't care about errors here, skip them.
            }
        }

        #endregion

        #region method SendAsync

        /// <summary>
        /// Sends specified packet to the specified target IP end point.
        /// </summary>
        /// <param name="target">Target end point.</param>
        /// <param name="packet">Packet to send.</param>
        internal async Task SendAsync(IPAddress target,byte[] packet)
        {
            try{
                if(target.AddressFamily == AddressFamily.InterNetwork){
                    await m_pIPv4Socket.SendToAsync(packet,SocketFlags.None,new IPEndPoint(target,53));
                }
                else if (target.AddressFamily == AddressFamily.InterNetworkV6){
                    ArgumentNullException.ThrowIfNull(m_pIPv6Socket);

                    await m_pIPv6Socket.SendToAsync(packet,SocketFlags.None,new IPEndPoint(target,53));
                }
            }
            catch{
            }
        }

        #endregion


        #region static method ReadQName

        /// <summary>
        /// Decodes a domain name (QNAME) from a DNS message starting at the specified
        /// offset. Supports DNS name compression as defined in RFC 1035. Returns the
        /// decoded Unicode domain name, or <c>null</c> if the message is malformed.
        /// </summary>
        /// <param name="reply">
        /// The DNS message buffer containing the encoded domain name.
        /// </param>
        /// <param name="offset">
        /// The current position within the message buffer. When the method returns,
        /// the offset is advanced past the encoded name unless a compression pointer
        /// was used, in which case it is advanced only past the pointer bytes.
        /// </param>
        /// <returns>
        /// The decoded domain name in Unicode form, or <c>null</c> if the name cannot
        /// be parsed due to malformed data or invalid compression pointers.
        /// </returns>
        /// <remarks>
        /// This method implements DNS label parsing and compression handling according
        /// to RFC 1035 section 4.1.4. Labels are read as ASCII bytes and converted to
        /// Unicode using <see cref="IdnMapping"/> to provide a user‑friendly domain
        /// name representation.
        /// </remarks>
        internal static string? ReadQName(byte[] reply, ref int offset)
        {
            try{
                string name = string.Empty;
                int pointerCount = 0;
                int originalOffset = -1;

                while(offset < reply.Length){
                    byte b = reply[offset];

                    // We have label terminator "0"
                    if(b == 0){
                        offset++;

                        break;
                    }

                    // Check if it's a pointer (first two bits are always 1: 0xC0)
                    bool isPointer = (b & 0xC0) == 0xC0;

                    if(isPointer){
                        if(offset + 1 >= reply.Length){
                            return null;
                        }

                        int pointerTarget = ((b & 0x3F) << 8) | reply[offset + 1];

                        // Save the position right after the pointer so the caller's 
                        // offset advances correctly past the pointer bytes.
                        if(originalOffset == -1){
                            originalOffset = offset + 2;
                        }

                        // Guard against infinite pointer loops in malformed packets
                        pointerCount++;
                        if(pointerCount > 10){
                            return null;
                        }

                        // Jump to the pointer target
                        offset = pointerTarget;
                    }
                    else{
                        // Label length (8-bit, first 2 bits always 0)
                        int labelLength = b & 0x3F;
                        offset++;

                        if(offset + labelLength > reply.Length){
                            return null;
                        }

                        if(name.Length > 0){
                            name += ".";
                        }

                        // Copy label into name
                        name += System.Text.Encoding.ASCII.GetString(reply,offset,labelLength);
                        offset += labelLength;
                    }
                }

                // Restore the stream offset to right after the pointer location
                if(originalOffset != -1){
                    offset = originalOffset;
                }

                // Convert domain name to Unicode. For more info see RFC 5890.
                if(name.Length > 0){
                    try{
                        System.Globalization.IdnMapping idn = new System.Globalization.IdnMapping();
                        name = idn.GetUnicode(name);
                    }
                    catch{
                        // Fallback to original name if IDN conversion fails
                    }
                }

                return name;
            }
            catch{
                return null;
            }
        }

        #endregion

        #region static method ReadCharacterString

        /// <summary>
        /// Reads character-string from spefcified data and offset.
        /// </summary>
        /// <param name="data">Data from where to read.</param>
        /// <param name="offset">Offset from where to start reading.</param>
        /// <returns>Returns readed string.</returns>
        internal static string ReadCharacterString(byte[] data,ref int offset)
        {
            /* RFC 1035 3.3.
                <character-string> is a single length octet followed by that number of characters. 
                <character-string> is treated as binary information, and can be up to 256 characters 
                in length (including the length octet).
            */

            int dataLength = (int)data[offset++];
            string retVal = Encoding.Default.GetString(data,offset,dataLength);
            offset += dataLength;

            return retVal;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets static DNS client.
        /// </summary>
        public static DNS_Client Static
        {
            get{ 
                return m_pDnsClient; 
            }
        }

        /// <summary>
		/// Gets or sets dns servers.
		/// </summary>
        /// <exception cref="ArgumentNullException">Is raised when null value is passed.</exception>
		public IPAddress[] DnsServers
		{
			get{
                return m_DnsServers; 
            }

			set{
                if(value == null){
                    throw new ArgumentNullException();
                }

                m_DnsServers = value; 
            }
		}

		/// <summary>
		/// Gets or sets if to use dns caching.
		/// </summary>
		public bool UseDnsCache
		{
			get{ return m_UseDnsCache; }

			set{ m_UseDnsCache = value; }
		}

        /// <summary>
        /// Gets DNS cache.
        /// </summary>
        public DNS_ClientCache Cache
        {
            get{ return m_pCache; }
        }

		#endregion

	}
}
