using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace LumiSoft.Net.DNS.Client
{
    /// <summary>
    /// This class represents DNS client transaction.
    /// </summary>
    public class DNS_ClientTransaction
    {
        private readonly object            m_pLock              = new object();
        private DNS_ClientTransactionState m_State              = DNS_ClientTransactionState.WaitingForStart;
        private DateTime                   m_CreateTime;
        private DNS_Client                 m_pOwner;
        private IPAddress[]                m_pDnsServers;
        private int                        m_ID                 = 1;
        private string                     m_QName              = "";
        private DNS_RecordType             m_QType              = 0;
        private int                        m_Timeout            = 0;
        private TimerEx                    m_pTimeoutTimer;
        private DNS_ServerResponse?        m_pResponse          = null;
        private int                        m_ResponseCount      = 0;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="owner">Owner DNS client.</param>
        /// <param name="dnsServers">DNS servers.</param>
        /// <param name="id">Transaction ID.</param>
        /// <param name="qtype">QTYPE value.</param>
        /// <param name="qname">QNAME value.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>owner</b> or <b>qname</b> is null reference.</exception>
        internal DNS_ClientTransaction(DNS_Client owner,IPAddress[] dnsServers,int id,DNS_RecordType qtype,string qname,int timeout)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(dnsServers);
            ArgumentNullException.ThrowIfNull(qname);

            m_pOwner      = owner;
            m_pDnsServers = dnsServers;
            m_ID          = id;
            m_QName       = qname;
            m_QType       = qtype;
            m_Timeout     = timeout;

            m_CreateTime    = DateTime.Now;
            m_pTimeoutTimer = new TimerEx(timeout);
            m_pTimeoutTimer.Elapsed += new System.Timers.ElapsedEventHandler(m_pTimeoutTimer_Elapsed);
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resource being used.
        /// </summary>
        public void Dispose()
        {
            bool raiseEvent = false;

            lock(m_pLock){
                if(m_State == DNS_ClientTransactionState.Disposed){
                    return;
                }

                if(m_State != DNS_ClientTransactionState.Disposed){
                    m_State = DNS_ClientTransactionState.Disposed;
                    raiseEvent = true;
                }
            }

            m_pTimeoutTimer?.Dispose();

            StateChanged = null;
            Timeout = null;

            if(raiseEvent){
                OnStateChanged();
            }
}

        #endregion


        #region Events handling

        #region method m_pTimeoutTimer_Elapsed

        /// <summary>
        /// Is called when DNS transaction timeout timer triggers.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event data.</param>
        private void m_pTimeoutTimer_Elapsed(object? sender,System.Timers.ElapsedEventArgs e)
        {
            try{
                OnTimeout();
            }
            catch{
                // We don't care about errors here.
            }
            finally{
                Dispose();
            }
        }

        #endregion

        #endregion


        #region method Start

        /// <summary>
        /// Starts DNS transaction processing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Is raised when this method is called in invalid transaction state.</exception>
        public void Start()
        {
            if(this.State != DNS_ClientTransactionState.WaitingForStart){
                throw new InvalidOperationException("DNS_ClientTransaction.Start may be called only in 'WaitingForStart' transaction state.");
            }

            SetState(DNS_ClientTransactionState.Active);

            // Move processing to thread pool.
            ThreadPool.QueueUserWorkItem(delegate(object? state){
                try{
                    // Use DNS cache if allowed.
                    if(m_pOwner.UseDnsCache){
                        DNS_ServerResponse? response = m_pOwner.Cache.GetFromCache(m_QName,(int)m_QType);
                        if(response != null){
                            m_pResponse = response;

                            SetState(DNS_ClientTransactionState.Completed);
                            Dispose();

                            return;
                        }
                    }

                    byte[] queryPacket = CreateQuery(m_ID,m_QName,m_QType,1);

                    m_pTimeoutTimer.Start();

                    // Send parallel query to DNS server(s).
                    foreach(IPAddress server in m_pDnsServers){
                        _= m_pOwner.SendAsync(server,queryPacket);
                    }
                }
                catch{
                    SetState(DNS_ClientTransactionState.Completed);
                    Dispose();
                }
            });
        }

        #endregion


        #region method ProcessResponse

        internal void ProcessResponse(IPAddress serverIP,DNS_ServerResponse response)
        {
            if(response == null){
                return;
            }

            try{
                bool shouldComplete = false;

                lock(m_pLock){
                    if(this.State != DNS_ClientTransactionState.Active){
                        return;
                    }

                    // UDP DNS response is truncated, we need to re-query using TCP.
                    if(response.IsTruncated){
                        byte[] queryPacket = CreateQuery(m_ID,m_QName,m_QType,1);
                        _= QueryTcpAsync(queryPacket,serverIP,53);
                        return;
                    }

                    m_ResponseCount++;

                    // If server refused to complete query and we have more active queries to other servers, skip that response.
                    if(response.ResponseCode == DNS_ResponseCode.Refused && m_ResponseCount < m_pDnsServers.Length){
                        return;
                    }

                    m_pResponse = response;
                    SetState(DNS_ClientTransactionState.Completed);
                    shouldComplete = true;
                }

                if(shouldComplete){
                    Dispose();
                }
            }
            catch{
                // Ignore malformed/unexpected response handling errors while transaction is active.
            }
        }

        #endregion

        #region method QueryTcpAsync

        private async Task QueryTcpAsync(byte[] dnsQuery,IPAddress server,int port)
        {
            using CancellationTokenSource cts =  new CancellationTokenSource(m_Timeout);
            CancellationToken token = cts.Token;
            
            try{
                lock(m_pLock){
                    if(this.State != DNS_ClientTransactionState.Active){
                        return;
                    }                    
                }
                using TcpClient tcp = new TcpClient();

                await tcp.ConnectAsync(server,port,token).ConfigureAwait(false);

                lock(m_pLock){
                    if(this.State != DNS_ClientTransactionState.Active){
                        return;
                    }
                }

                using(NetworkStream stream = tcp.GetStream()){
                    // TCP DNS uses a 2-byte big-endian length prefix (RFC 1035 §4.2.2).
                    ushort length = (ushort)dnsQuery.Length;
                    byte[] lenPrefix = { (byte)(length >> 8),(byte)(length & 0xFF) };

                    await stream.WriteAsync(lenPrefix,0,2,token).ConfigureAwait(false);
                    await stream.WriteAsync(dnsQuery,0,dnsQuery.Length,token).ConfigureAwait(false);

                    byte[] respLenBuf = new byte[2];
                    await stream.ReadAtLeastAsync(respLenBuf,2,throwOnEndOfStream:true,token).ConfigureAwait(false);
                    int respLen = (respLenBuf[0] << 8) | respLenBuf[1];
                    if(respLen <= 0 || respLen > 65535){
                        return;
                    }

                    byte[] responseBytes = new byte[respLen];
                    await stream.ReadAtLeastAsync(responseBytes,respLen,throwOnEndOfStream:true,token).ConfigureAwait(false);

                    DNS_ServerResponse parsedResponse = DNS_ServerResponse.Parse(responseBytes);
                    ProcessResponse(server,parsedResponse);
                }
            }
            catch{
                // If TCP query fails, allow remaining UDP/TCP responses or timeout timer to conclude transaction.
            }
        }

        #endregion


        #region method SetState

        /// <summary>
        /// Sets transaction state.
        /// </summary>
        /// <param name="state">New transaction state.</param>
        private void SetState(DNS_ClientTransactionState state)
        {
            bool raiseEvent = false;

            lock(m_pLock){
                if(m_State == DNS_ClientTransactionState.Disposed){
                    return;
                }

                if(m_State != state){
                    m_State = state;
                    raiseEvent = true;
                }
            }

            if(raiseEvent){
                OnStateChanged();
            }
        }

        #endregion

        #region method CreateQuery

        private byte[] CreateQuery(int ID,string qname,DNS_RecordType qtype,int qclass)
        {
            //---- Create header --------------------------------------------//
            // Header is first 12 bytes of query

            /* 4.1.1. Header section format
                                          1  1  1  1  1  1
            0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                      ID                       |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |QR|   Opcode  |AA|TC|RD|RA|   Z    |   RCODE   |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    QDCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    ANCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    NSCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                    ARCOUNT                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            QR  A one bit field that specifies whether this message is a
                query (0), or a response (1).

            OPCODE          A four bit field that specifies kind of query in this
                message.  This value is set by the originator of a query
                and copied into the response.  The values are:

                0               a standard query (QUERY)

                1               an inverse query (IQUERY)

                2               a server status request (STATUS)

            */

            byte[] buffer = new byte[1400];
            //--------- Header part -----------------------------------//
            buffer[0]  = (byte) (ID >> 8); buffer[1]  = (byte) (ID & 0xFF);
            buffer[2]  = (byte) 1;         buffer[3]  = (byte) 0;
            buffer[4]  = (byte) 0;         buffer[5]  = (byte) 1;
            buffer[6]  = (byte) 0;         buffer[7]  = (byte) 0;
            buffer[8]  = (byte) 0;         buffer[9]  = (byte) 0;
            buffer[10] = (byte) 0;         buffer[11] = (byte) 1;
            //---------------------------------------------------------//

            //---- End of header --------------------------------------------//


            //----Create query ------------------------------------//

            /*  Rfc 1035 4.1.2. Question section format
                                              1  1  1  1  1  1
            0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                                               |
            /                     QNAME                     /
            /                                               /
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                     QTYPE                     |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
            |                     QCLASS                    |
            +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+

            QNAME
                a domain name represented as a sequence of labels, where
                each label consists of a length octet followed by that
                number of octets.  The domain name terminates with the
                zero length octet for the null label of the root.  Note
                that this field may be an odd number of octets; no
                padding is used.
            */

            // Convert unicode domain name. For more info see RFC 5890.
            System.Globalization.IdnMapping ldn = new System.Globalization.IdnMapping();
            qname = ldn.GetAscii(qname);

            string[] labels = qname.Split(new char[] {'.'},StringSplitOptions.RemoveEmptyEntries);
            int position = 12;

            // Copy all domain parts(labels) to query
            // eg. lumisoft.ee = 2 labels, lumisoft and ee.
            // format = label.length + label(bytes)
            foreach(string label in labels){
                // convert label string to byte array
                byte[] b = Encoding.ASCII.GetBytes(label);
                if(b.Length == 0 || b.Length > 63){
                    continue;
                }

                // add label lenght to query
                buffer[position++] = (byte)(b.Length);
                b.CopyTo(buffer,position);

                // Move position by label length
                position += b.Length;
            }

            // Terminate domain (see note above)
            buffer[position++] = (byte) 0;

            // Set QTYPE 
            buffer[position++] = (byte) 0;
            buffer[position++] = (byte)qtype;

            // Set QCLASS
            buffer[position++] = (byte) 0;
            buffer[position++] = (byte)qclass;

            // ---- EDNS0 OPT RR ---------------------------
            // EDNS0 needed for increasing default 512 packet size to 1232.

            // Name = root (0)
            buffer[position++] = 0;

            // Type = OPT (41)
            buffer[position++] = 0;
            buffer[position++] = 41;

            // UDP payload size (recommended 1232)
            buffer[position++] = (byte)(1232 >> 8);
            buffer[position++] = (byte)(1232 & 0xFF);

            // Extended RCODE = 0
            buffer[position++] = 0;

            // EDNS Version = 0
            buffer[position++] = 0;

            // Z flags = 0 (unless you want DO bit)
            buffer[position++] = 0;
            buffer[position++] = 0;

            // RDATA length = 0 (no options)
            buffer[position++] = 0;
            buffer[position++] = 0;
            //--------------------------------------------------------

            //-------------------------------------------------------//

            return buffer.AsMemory(0,position).ToArray();
        }

        #endregion


        #region Properties implementaion

        /// <summary>
        /// Get DNS transaction state.
        /// </summary>
        public DNS_ClientTransactionState State
        {
            get{ return m_State; }
        }

        /// <summary>
        /// Gets transaction create time.
        /// </summary>
        public DateTime CreateTime
        {
            get{ return m_CreateTime; }
        }

        /// <summary>
        /// Gets DNS transaction ID.
        /// </summary>
        public int ID
        {
            get{ return m_ID; }
        }

        /// <summary>
        /// Gets QNAME value.
        /// </summary>
        public string QName
        {
            get{ return m_QName; }
        }

        /// <summary>
        /// Gets QTYPE value.
        /// </summary>
        public DNS_RecordType QType
        {
            get{ return m_QType; }
        }

        /// <summary>
        /// Gets DNS server response. Value null means no response received yet.
        /// </summary>
        public DNS_ServerResponse? Response
        {
            get{ return m_pResponse; }
        }

        #endregion

        #region Events implementation

        /// <summary>
        /// This event is raised when DNS transaction state has changed.
        /// </summary>
        public event EventHandler<EventArgs<DNS_ClientTransaction>>? StateChanged;

        #region method OnStateChanged

        /// <summary>
        /// Raises <b>StateChanged</b> event.
        /// </summary>
        private void OnStateChanged()
        {
            EventHandler<EventArgs<DNS_ClientTransaction>>? handler;
            lock(m_pLock){
                handler = this.StateChanged;
            }
            handler?.Invoke(this,new EventArgs<DNS_ClientTransaction>(this));
        }

        #endregion

        /// <summary>
        /// This event is raised when DNS transaction times out.
        /// </summary>
        public event EventHandler? Timeout;

        #region method OnTimeout

        /// <summary>
        /// Raises <b>Timeout</b> event.
        /// </summary>
        private void OnTimeout()
        {
           EventHandler? handler;
           lock(m_pLock){
               handler = this.Timeout;
           }
           handler?.Invoke(this,new EventArgs());
        }

        #endregion

        #endregion
    }
}