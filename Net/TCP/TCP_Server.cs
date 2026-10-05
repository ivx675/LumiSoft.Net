using LumiSoft.Net.Log;
using LumiSoft.Net.SMTP.Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LumiSoft.Net.TCP
{    
    /// <summary>
    /// This class implements generic TCP session based server.
    /// </summary>
    public class TCP_Server<T> : IDisposable where T : TCP_ServerSession,new()
    {
        private bool                     m_IsDisposed           = false;
        private bool                     m_IsRunning            = false;
        private TCP_ServerEndpoint[]     m_pListenEndpoints     = [];
        private long                     m_MaxConnections       = 0;
        private long                     m_MaxConnectionsPerIP  = 0; 
        private int                      m_SessionIdleTimeout   = 100;
        private Logger?                  m_pLogger              = null;
        private DateTime                 m_StartTime;
        private long                     m_ConnectionsProcessed = 0;
        private List<Socket>             m_pListeningSockets;
        private TCP_SessionCollection<T> m_pSessions;
        private Timer                    m_pTimer_IdleTimeout;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TCP_Server()
        {
            m_pListeningSockets = new List<Socket>();
            m_pSessions = new TCP_SessionCollection<T>();

            m_pTimer_IdleTimeout = new Timer(_ => TimeoutScan(),null,1000,60000);
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
            if(m_IsRunning){
                try{
                    Stop();
                }
                catch{
                }
            }
            m_IsDisposed = true;

            m_pTimer_IdleTimeout.Dispose();

            // We must call disposed event before we release events.
            try {
                OnDisposed();
            }
            catch{
                // We never should get exception here, user should handle it, just skip it.
            }

            // Release all events.
            this.Started  = null;
            this.Stopped  = null;
            this.Disposed = null;
            this.Error    = null;
        }

        #endregion


        #region method Start

        /// <summary>
        /// Starts the TCP server if it is not already running. Initializes runtime
        /// state, records the start time, and begins accepting incoming connections
        /// on all configured IPv4/IPv6 bindings. Each binding launches its own
        /// asynchronous accept loop. If startup succeeds, the <see cref="OnStarted"/>
        /// callback is raised.
        /// </summary>
        public void Start()
        {
            if(m_IsDisposed){
                throw new ObjectDisposedException("TCP_Server");
            }
            if(m_IsRunning){
                return;
            }
            m_IsRunning = true;

            m_StartTime = DateTime.Now;
            m_ConnectionsProcessed = 0;

            try{
                foreach(TCP_ServerEndpoint ep in m_pListenEndpoints){
                    if(ep.IPEndPoint.AddressFamily == AddressFamily.InterNetwork || ep.IPEndPoint.AddressFamily == AddressFamily.InterNetworkV6){
                        _= AcceptConnectionLoopAsync(ep);
                    }
                }
            }
            catch(Exception x){
                OnError(x);
            }

            OnStarted();
        }
                
        #endregion

        #region method Stop

        /// <summary>
        /// Stops the TCP server if it is currently running. Marks the server as
        /// inactive, disposes all active listening sockets to stop accepting new
        /// connections and raises the <see cref="OnStopped"/>
        /// callback. Existing client sessions continue running until they exit
        /// naturally or are closed by timeout logic.
        /// </summary>
        public void Stop()
        {
            if(!m_IsRunning){
                return;
            }
            m_IsRunning = false;

            // Dispose all listeining sockets.
            foreach(Socket socket in m_pListeningSockets){
                try{
                    socket.Dispose();
                }
                catch(Exception x){
                    OnError(x);
                }
            }
            m_pListeningSockets.Clear();

            OnStopped();
        }

        #endregion

        #region method Restart

        /// <summary>
        /// Restarts the TCP server by stopping the current instance and starting
        /// it again. If the server is not running, this method simply performs a
        /// fresh start. Any existing listening sockets are closed and new accept
        /// loops are initialized.
        /// </summary>
        public void Restart()
        {
            Stop();
            Start();
        }

        #endregion


        #region virtual method OnMaxConnectionsExceeded

        /// <summary>
        /// Is called when new incoming session and server maximum allowed connections exceeded.
        /// </summary>
        /// <param name="session">Incoming session.</param>
        /// <remarks>This method allows inhereted classes to report error message to connected client.
        /// Session will be disconnected after this method completes.
        /// </remarks>
        protected virtual void OnMaxConnectionsExceeded(T session)
        {
        }

        #endregion

        #region virtual method OnMaxConnectionsPerIPExceeded

        /// <summary>
        /// Is called when new incoming session and server maximum allowed connections per connected IP exceeded.
        /// </summary>
        /// <param name="session">Incoming session.</param>
        /// <remarks>This method allows inhereted classes to report error message to connected client.
        /// Session will be disconnected after this method completes.
        /// </remarks>
        protected virtual void OnMaxConnectionsPerIPExceeded(T session)
        {
        }

        #endregion


        #region method AcceptConnectionLoopAsync

        private async Task AcceptConnectionLoopAsync(TCP_ServerEndpoint ep)
        {
            Socket? listener = null;
            try{            
                listener = new(ep.IPEndPoint.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
                if(ep.IPEndPoint.AddressFamily == AddressFamily.InterNetworkV6){
                    listener.DualMode = false;
                }
                listener.Bind(ep.IPEndPoint);
                listener.Listen(backlog:500);
    
                while(m_IsRunning){
                    try{
                        Socket clientSocket = await listener.AcceptAsync();
                        clientSocket.NoDelay = true;

                        _ = ProcessConnectionAsync(clientSocket,ep);
                    }
                    catch(Exception x){
                        if(m_IsRunning){
                            OnError(x);
                        }
                    }
                }
            }
            catch(Exception x){
                if(m_IsRunning){
                    OnError(x);
                }
            }
            finally{
                listener?.Dispose();
            }
        }

        #endregion

        #region method ProcessConnection

        private async Task ProcessConnectionAsync(Socket socket,TCP_ServerEndpoint ep)
        {
            if(socket == null){
                throw new ArgumentNullException(nameof(socket));
            }
            if(ep == null){
                throw new ArgumentNullException(nameof(ep));
            }

            m_ConnectionsProcessed++;
                                
            try{
                T session = new T();
                session.Init(this,socket,ep.HostName,ep.TlsMode == TCP_ServerTlsMode.Implicit,ep.Certificate);

                // Maximum allowed connections exceeded, reject connection.
                if(m_MaxConnections != 0 && m_pSessions.Count > m_MaxConnections){
                    OnMaxConnectionsExceeded(session);
                    session.Dispose();
                }
                // Maximum allowed connections per IP exceeded, reject connection.
                else if(m_MaxConnectionsPerIP != 0 && m_pSessions.GetConnectionsPerIP(session.RemoteEndPoint.Address) > m_MaxConnectionsPerIP){
                    OnMaxConnectionsPerIPExceeded(session);
                    session.Dispose();
                }
                // Start processing new session.
                else{
                    session.Disposing += new EventHandler(delegate(object? sender,EventArgs e){                        
                        m_pSessions.Remove(session);
                    });
                    m_pSessions.Add(session);

                    // Raise session created event.
                    if (this.SessionCreatedAsync != null) {
                        await this.SessionCreatedAsync(new TCP_Server_e_SessionCreated<T>(session));
                    }

                    await session.StartIAsync();
                }
            }
            catch(Exception x){
                OnError(x);
            }
        }

        #endregion

        #region method TimeoutScan

        /// <summary>
        /// Scans all active sessions and disconnects those whose last activity
        /// exceeds the configured idle timeout. Ensures timed‑out sessions invoke
        /// their timeout handler and are disposed if not already closed.
        /// </summary>
        private void TimeoutScan()
        {
            try{
                foreach(T session in this.Sessions.ToArray()){
                    try{
                        if(DateTime.Now > session.TcpStream.LastActivity.AddSeconds(m_SessionIdleTimeout)){                            
                            session.OnTimeoutI();
                        }
                    }
                    catch{
                    }
                }
            }
            catch (Exception x){
                OnError(x);
            }
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets a value indicating whether this server instance has been disposed.
        /// When <c>true</c>, accessing members that require an active instance will
        /// raise an <see cref="ObjectDisposedException"/>.
        /// </summary>
        public bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }

        /// <summary>
        /// Gets a value indicating whether the server is currently running. This becomes
        /// <c>true</c> after the server has been started and remains <c>true</c> until
        /// the server is stopped or disposed.
        /// </summary>
        public bool IsRunning
        {
            get{ return m_IsRunning; }
        }
                
        /// <summary>
        /// Gets or sets the TCP server listening endpoints. Each endpoint defines an
        /// advertised hostname, bind IP address, port, and TLS configuration used by
        /// the server when accepting incoming connections.
        /// </summary>
        /// <remarks>
        /// The collection specifies all network endpoints on which the server listens.
        /// Assigning <c>null</c> resets the list to an empty array. Accessing this
        /// property after the server has been disposed will raise an
        /// <see cref="ObjectDisposedException"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server instance has been
        /// disposed.
        /// </exception>
        public TCP_ServerEndpoint[] ListenEndpoints
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pListenEndpoints; 
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                if(value == null){
                    value = [];
                }

                m_pListenEndpoints = value;
            }
        }        

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections the server allows.
        /// A value of <c>0</c> means no limit is enforced.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a negative value is assigned.
        /// </exception>
        public long MaxConnections
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                
                return m_MaxConnections; 
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                if(value < 0){
                    throw new ArgumentException("Property 'MaxConnections' value must be >= 0.");
                }

                m_MaxConnections = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections allowed from a
        /// single IP address. A value of <c>0</c> means no per‑IP limit is enforced.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a negative value is assigned.
        /// </exception>
        public long MaxConnectionsPerIP
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                
                return m_MaxConnectionsPerIP; 
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                if(m_MaxConnectionsPerIP < 0){
                    throw new ArgumentException("Property 'MaxConnectionsPerIP' value must be >= 0.");
                }

                m_MaxConnectionsPerIP = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum allowed session idle time, in seconds. When a session
        /// remains idle longer than this value, it will be terminated. A value of <c>0</c>
        /// means no idle timeout is enforced, although this is not recommended.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server has been disposed.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a negative value is assigned.
        /// </exception>
        public int SessionIdleTimeout
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                
                return m_SessionIdleTimeout; 
            }

            set{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                if(value < 0){
                    throw new ArgumentException("Property 'SessionIdleTimeout' value must be >= 0.");
                }

                m_SessionIdleTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the logger used by the server for diagnostic and operational
        /// messages. A value of <c>null</c> disables logging.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server has been disposed.
        /// </exception>
        public Logger? Logger
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return m_pLogger; 
            }

            set{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                m_pLogger = value; 
            }
        }

        /// <summary>
        /// Gets the time when the server was started.
        /// </summary>
        /// <remarks>
        /// This property is only valid while the server is running. Accessing it before
        /// the server has been started or after it has been stopped will result in an
        /// exception.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server instance has been
        /// disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the server is not running.
        /// </exception>
        public DateTime StartTime
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                if(!m_IsRunning){
                    throw new InvalidOperationException("TCP server is not running.");
                }

                return m_StartTime; 
            }
        }
                
        /// <summary>
        /// Gets the total number of connections the server has processed since it was
        /// started.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server has been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the server is not running.
        /// </exception>
        public long ConnectionsProcessed
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }
                if(!m_IsRunning){
                    throw new InvalidOperationException("TCP server is not running.");
                }

                return m_ConnectionsProcessed; 
            }
        }

        /// <summary>
        /// Gets the collection of active TCP sessions currently handled by the server.
        /// </summary>
        /// <remarks>
        /// The collection reflects all sessions that are presently connected and not yet
        /// terminated. Accessing this property after the server has been disposed will
        /// raise an <see cref="ObjectDisposedException"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when this property is accessed after the server instance has been
        /// disposed.
        /// </exception>
        public TCP_SessionCollection<T> Sessions
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_Server");
                }

                return m_pSessions; 
            }
        }


        /// <summary>
        /// Gets local listening IP end points.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        internal IPEndPoint[] LocalEndPoints
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                List<IPEndPoint> retVal = new List<IPEndPoint>();
                foreach(TCP_ServerEndpoint bind in this.ListenEndpoints){
                    if(bind.IP.Equals(IPAddress.Any)){
                        foreach(IPAddress ip in System.Net.Dns.GetHostAddresses("")){
                            if(ip.AddressFamily == AddressFamily.InterNetwork && !retVal.Contains(new IPEndPoint(ip,bind.Port))){
                                retVal.Add(new IPEndPoint(ip,bind.Port));
                            }
                        }
                    }
                    else if(bind.IP.Equals(IPAddress.IPv6Any)){
                        foreach(IPAddress ip in System.Net.Dns.GetHostAddresses("")){
                            if(ip.AddressFamily == AddressFamily.InterNetworkV6 && !retVal.Contains(new IPEndPoint(ip,bind.Port))){
                                retVal.Add(new IPEndPoint(ip,bind.Port));
                            }
                        }
                    }
                    else{
                        if(!retVal.Contains(bind.IPEndPoint)){
                            retVal.Add(bind.IPEndPoint);
                        }
                    }
                }
            
                return retVal.ToArray();
            }
        }

        #endregion

        #region Events Implementation

        /// <summary>
        /// This event is raised when TCP server has started.
        /// </summary>
        public event EventHandler? Started = null;

        #region method OnStarted

        /// <summary>
        /// Raises <b>Started</b> event.
        /// </summary>
        protected void OnStarted()
        {
            if(this.Started != null){
                this.Started(this,new EventArgs());
            }
        }

        #endregion

        /// <summary>
        /// This event is raised when TCP server has stopped.
        /// </summary>
        public event EventHandler? Stopped = null;

        #region method OnStopped

        /// <summary>
        /// Raises <b>Stopped</b> event.
        /// </summary>
        protected void OnStopped()
        {
            if(this.Stopped != null){
                this.Stopped(this,new EventArgs());
            }
        }

        #endregion

        /// <summary>
        /// This event is raised when TCP server has disposed.
        /// </summary>
        public event EventHandler? Disposed = null;

        #region method OnDisposed

        /// <summary>
        /// Raises <b>Disposed</b> event.
        /// </summary>
        protected void OnDisposed()
        {
            if(this.Disposed != null){
                this.Disposed(this,new EventArgs());
            }
        }

        #endregion

        /// <summary>
        /// Occurs when a new TCP session has been created. This asynchronous event is
        /// raised immediately after the session object is constructed but before any
        /// protocol‑specific processing begins.
        /// </summary>
        /// <remarks>
        /// Handlers may perform initialization, logging, or session‑level configuration.
        /// The event argument provides access to the newly created session instance.
        /// </remarks>
        public event Func<TCP_Server_e_SessionCreated<T>,Task>? SessionCreatedAsync = null;
                        
        /// <summary>
        /// This event is raised when TCP server has unknown unhandled error.
        /// </summary>
        public event ErrorEventHandler? Error = null;

        #region method OnError

        /// <summary>
        /// Raises <b>Error</b> event.
        /// </summary>
        /// <param name="x">Exception happened.</param>
        private void OnError(Exception x)
        {
            if(this.Error != null){
                this.Error(this,new Error_EventArgs(x,new System.Diagnostics.StackTrace()));
            }
        }

        #endregion

        #endregion

    }
}
