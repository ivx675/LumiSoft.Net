using LumiSoft.Net.IO;
using LumiSoft.Net.Log;
using LumiSoft.Net.SMTP.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace LumiSoft.Net.TCP
{
    /// <summary>
    /// This class implements generic TCP server session.
    /// </summary>
    public class TCP_ServerSession : TCP_Session
    {
        private bool                      m_IsDisposed    = false;
        private bool                      m_IsTerminated  = false;
        private object                    m_pServer       = new object();
        private string                    m_ID            = "";
        private DateTime                  m_ConnectTime;
        private string                    m_LocalHostName = "";
        private IPEndPoint?               m_pLocalEP      = null;
        private IPEndPoint?               m_pRemoteEP     = null;
        private bool                      m_IsSsl         = false;
        private bool                      m_IsSecure      = false;
        private X509Certificate?          m_pCertificate  = null;
        private NetworkStream?            m_pRawTcpStream = null;
        private SmartStream?              m_pTcpStream    = null;
        private object?                   m_pTag          = null;
        private Dictionary<string,object> m_pTags;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TCP_ServerSession()
        {
            m_pTags = new Dictionary<string,object>();
        }

        #region method Dispose

        /// <summary>
        /// Cleans up any resources being used.
        /// </summary>
        public override void Dispose()
        {
            if(m_IsDisposed){
                return;
            }
            if(!m_IsTerminated){
                try{
                    Disconnect();
                }
                catch{
                    // Skip disconnect errors.
                }
            }
            
            if(Disposing != null){
                Disposing(this,EventArgs.Empty);
            }

            m_IsDisposed = true;

            // We must call disposed event before we release events.
            try{
                OnDisposed();
            }
            catch{
                // We never should get exception here, user should handle it, just skip it.
            }

            m_pLocalEP = null;
            m_pRemoteEP = null;
            m_pCertificate = null;
            if(m_pTcpStream != null){
                m_pTcpStream.Dispose();
            }
            m_pTcpStream = null;
            if(m_pRawTcpStream != null){
                m_pRawTcpStream.Close();
            }
            m_pRawTcpStream = null;

            // Release events.
            this.IdleTimeout       = null;
            this.DisconnectedAsync = null;
            this.Disposed          = null;
            this.Error             = null;
            this.ErrorAsync        = null;
            this.Disposing         = null;
        }

        #endregion


        #region method Init

        /// <summary>
        /// Initializes session. This method is called from TCP_Server when new session created.
        /// </summary>
        /// <param name="server">Owner TCP server.</param>
        /// <param name="socket">Connected socket.</param>
        /// <param name="hostName">Local host name.</param>
        /// <param name="ssl">Specifies if session should switch to SSL.</param>
        /// <param name="certificate">SSL certificate.</param>
        internal void Init(object server,Socket socket,string hostName,bool ssl,X509Certificate? certificate)
        {   
            // NOTE: We may not raise any event here !
            
            m_pServer       = server;
            m_LocalHostName = hostName;
            m_IsSsl         = ssl;
            m_ID            = Guid.NewGuid().ToString();
            m_ConnectTime   = DateTime.Now;
            m_pLocalEP      = socket.LocalEndPoint as IPEndPoint;
            m_pRemoteEP     = socket.RemoteEndPoint as IPEndPoint;
            m_pCertificate  = certificate;

            socket.ReceiveBufferSize = 32000;
            socket.SendBufferSize = 32000;

            m_pRawTcpStream = new NetworkStream(socket,true);
            m_pTcpStream    = new SmartStream(m_pRawTcpStream,true);
        }

        #endregion

        #region method StartIAsync

        /// <summary>
        /// This method is called from TCP server when session should start processing incoming connection.
        /// </summary>
        internal async Task StartIAsync()
        {
            if(m_IsSsl){
                try{
                    // Log
                    LogAddText("Starting SSL negotiation now.");

                    await SwitchToSecureAsync();

                    LogAddText("SSL negotiation completed successfully.");
                }
                catch(Exception x){
                    LogAddException(x);
                    if(!this.IsDisposed){
                        Disconnect();
                    }

                    return;
                }
            }

            Start();
        }

        #endregion

        #region method Start

        /// <summary>
        /// This method is called from TCP server when session should start processing incoming connection.
        /// </summary>
        protected virtual void Start()
        {
        }

        #endregion


        #region method SwitchToSecureAsync

        /// <summary>
        /// Upgrades the current plaintext TCP connection to a secure TLS connection
        /// using the server certificate configured for this session. This method
        /// performs a synchronous TLS handshake over the existing network stream
        /// and replaces the underlying <see cref="SmartStream"/> with a secure
        /// <see cref="SslStream"/> instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the session has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the connection is already secure or when no server
        /// certificate has been configured.
        /// </exception>
        /// <exception cref="System.Security.Authentication.AuthenticationException">
        /// Thrown when the TLS handshake fails.
        /// </exception>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous TLS upgrade
        /// operation.
        /// </returns>
        public async Task SwitchToSecureAsync()
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if(this.IsSecureConnection){
                throw new InvalidOperationException("Connection is already secure.");
            }            
            if(m_pCertificate == null){
                throw new InvalidOperationException("There is no certificate specified.");
            }

            var sslStream = new SslStream(m_pTcpStream!.SourceStream,true);
            await sslStream.AuthenticateAsServerAsync(m_pCertificate);

            // Close old stream, but leave source stream open.
            m_pTcpStream.IsOwner = false;
            m_pTcpStream.Dispose();

            m_IsSecure = true;
            m_pTcpStream = new SmartStream(sslStream,true);
        }

        #endregion
// Remove ME:
        #region method Disconnect

        /// <summary>
        /// Disconnects session.
        /// </summary>
        public override void Disconnect()
        {
            if(m_IsDisposed){
                return;
            }
            if(m_IsTerminated){
                return;
            }
            m_IsTerminated = true;

            try{
                if(this.DisconnectedAsync != null) {
                   this.DisconnectedAsync(new EventArgs()).GetAwaiter().GetResult();
                }
            }
            catch(Exception x){
                // We never should get exception here, user should handle it.
                OnError(x);
            }

            Dispose();
        }

        #endregion

        #region method DisconnectAsync

        /// <summary>
        /// Terminates the active session and raises the <see cref="DisconnectedAsync"/>
        /// event if it has subscribers. If the session is already disposed or has been
        /// previously terminated, the method returns immediately without performing
        /// further actions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method marks the session as terminated, invokes the asynchronous
        /// <see cref="DisconnectedAsync"/> event handler (if present), and then disposes
        /// the session. The event is invoked with <see cref="EventArgs.Empty"/> because
        /// no protocol‑specific disconnect metadata is provided at the base class level.
        /// </para>
        /// <para>
        /// Derived server implementations may override or extend this behavior to
        /// perform protocol‑specific cleanup, logging, or final message exchange before
        /// the session is closed.
        /// </para>
        /// <para>
        /// Calling this method multiple times is safe; subsequent calls exit early based
        /// on the <c>m_IsDisposed</c> and <c>m_IsTerminated</c> flags.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous disconnect operation.
        /// </returns>
        public virtual async Task DisconnectAsync()
        {
            if(m_IsDisposed){
                return;
            }
            if(m_IsTerminated){
                return;
            }
            m_IsTerminated = true;

            if(this.DisconnectedAsync != null){
                await this.DisconnectedAsync(new EventArgs());
            }

            Dispose();
        }

        #endregion


        #region method OnTimeoutAsync

        /// <summary>
        /// Is called when session idle timeout reached.
        /// </summary>
        protected virtual async Task OnTimeoutAsync()
        {
            try{
                OnIdleTimeout();
            }
            finally{
                await DisconnectAsync();
            }
        }

        #endregion

        #region method OnTimeoutI

        /// <summary>
        /// Just calls <b>OnTimeoutAsync</b> method.
        /// </summary>
        internal virtual void OnTimeoutI()
        {            
            _= OnTimeoutAsync();
        }

        #endregion


        #region method LogAddText

        /// <summary>
        /// Logs specified text.
        /// </summary>
        /// <param name="text">text to log.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>text</b> is null reference.</exception>
        private void LogAddText(string text)
        {
            if(text == null){
                throw new ArgumentNullException("text");
            }
            
            try{
                object? logger = this.Server?.GetType()?.GetProperty("Logger")?.GetValue(this.Server,null);
                if(logger != null){
                    ((Logger)logger).AddText(
                        this.ID,
                        this.AuthenticatedUserIdentity,
                        text,                        
                        this.LocalEndPoint,
                        this.RemoteEndPoint
                    );
                }
            }
            catch{
            }
        }

        #endregion

        #region method LogAddException

        /// <summary>
        /// Logs specified exception.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>exception</b> is null reference.</exception>
        private void LogAddException(Exception exception)
        {
            if(exception == null){
                throw new ArgumentNullException("exception");
            }
            
            try{
                object? logger = this.Server?.GetType()?.GetProperty("Logger")?.GetValue(this.Server,null);
                if(logger != null){
                    ((Logger)logger).AddException(
                        this.ID,
                        this.AuthenticatedUserIdentity,
                        exception.Message,                        
                        this.LocalEndPoint,
                        this.RemoteEndPoint,
                        exception
                    );
                }
            }
            catch{
            }
        }

        #endregion


        #region Properties Implementation

        /// <summary>
        /// Gets if TCP server session is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get{ return m_IsDisposed; }
        }

        /// <summary>
        /// Gets owner TCP server.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public object Server
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }

                return m_pServer; 
            }
        }

        /// <summary>
        /// Gets local host name.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public string LocalHostName
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }

                return m_LocalHostName;
            }
        }

        /// <summary>
        /// Gets session certificate.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public X509Certificate? Certificate
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }
                
                return m_pCertificate; 
            }
        }

        /// <summary>
        /// Gets or sets user data.
        /// </summary>
        public object? Tag
        {
            get{ return m_pTag; }

            set{ m_pTag = value; }
        }

        /// <summary>
        /// Gets user data items collection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public Dictionary<string,object> Tags
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }

                return m_pTags; 
            }
        }


        /// <summary>
        /// Gets if session is connected.
        /// </summary>
        public override bool IsConnected
        {
            get{ return true; }
        }

        /// <summary>
        /// Gets session ID.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override string ID
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }

                return m_ID; 
            }
        }

        /// <summary>
        /// Gets the time when session was connected.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override DateTime ConnectTime
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }

                return m_ConnectTime; 
            }
        }

        /// <summary>
        /// Gets the last time when data was sent or received.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override DateTime LastActivity
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }
 
                return m_pTcpStream?.LastActivity ?? DateTime.MinValue; 
            }
        }

        /// <summary>
        /// Gets session local IP end point.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override IPEndPoint LocalEndPoint
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }
                ArgumentNullException.ThrowIfNull(m_pLocalEP);

                return m_pLocalEP; 
            }
        }

        /// <summary>
        /// Gets session remote IP end point.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override IPEndPoint RemoteEndPoint
        {
            get{
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }
                ArgumentNullException.ThrowIfNull(m_pRemoteEP);

                return m_pRemoteEP; 
            }
        }
        
        /// <summary>
        /// Gets if this session TCP connection is secure connection.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override bool IsSecureConnection
        {
            get{ 
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }

                return m_IsSecure; 
            }
        }
                
        /// <summary>
        /// Gets TCP stream which must be used to send/receive data through this session.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        public override SmartStream TcpStream
        {
            get{  
                if(m_IsDisposed){
                    throw new ObjectDisposedException("TCP_ServerSession");
                }
                ArgumentNullException.ThrowIfNull(m_pTcpStream);

                return m_pTcpStream; 
            }
        }
                                
        #endregion

        #region Events Implementation

        /// <summary>
        /// Occurs when the session becomes idle for longer than the configured timeout period.
        /// </summary>
        public event EventHandler? IdleTimeout = null;

        #region method OnIdleTimeout

        /// <summary>
        /// Raises <b>IdleTimeout</b> event.
        /// </summary>
        private void OnIdleTimeout()
        {
            if(this.IdleTimeout != null){
                this.IdleTimeout(this,new EventArgs());
            }
        }

        #endregion

        /// <summary>
        /// Occurs when the session has been disconnected. The event is raised after the
        /// session transitions into a terminated state and before the session is disposed.
        /// </summary>
        public event Func<EventArgs,Task>? DisconnectedAsync = null;

        /// <summary>
        /// Occurs when the session has been disposed.
        /// </summary>
        public event EventHandler? Disposed = null;

        #region method OnDisposed

        /// <summary>
        /// Raises <b>Disposed</b> event.
        /// </summary>
        private void OnDisposed()
        {
            if(this.Disposed != null){
                this.Disposed(this,new EventArgs());
            }
        }

        #endregion
        
        /// <summary>
        /// This event is raised when TCP server session has unknown unhandled error.
        /// </summary>
        public event ErrorEventHandler? Error = null;

        #region method OnError

        /// <summary>
        /// Raises <b>Error</b> event.
        /// </summary>
        /// <param name="x">Exception happened.</param>
        protected virtual void OnError(Exception x)
        {
            if(this.Error != null){
                this.Error(this,new Error_EventArgs(x,new System.Diagnostics.StackTrace()));
            }
        }

        #endregion

        /// <summary>
        /// Asynchronous error notification event for the session.
        /// 
        /// <para>
        /// The server raises this event when an exception occurs inside the
        /// session processing pipeline. The event provides the exception details
        /// through an <see cref="ExceptionEventArgs"/> instance and awaits the
        /// subscriber's returned <see cref="Task"/>, allowing the handler to
        /// perform asynchronous logging, diagnostics, or cleanup.
        /// </para>
        /// 
        /// <para>
        /// This event supports only a single subscriber. If more than one handler
        /// is attached, the server will reject the subscription to ensure
        /// predictable async behavior and to prevent multicast delegate issues
        /// where only the last handler's <see cref="Task"/> would be awaited.
        /// </para>
        /// 
        /// <para>
        /// If no handler is attached, the event is skipped and the exception is
        /// not processed further by the session.
        /// </para>
        /// </summary>
        public event Func<ExceptionEventArgs,Task>? ErrorAsync = null;

        #region method OnErrorAsync

        /// <summary>
        /// Raises the <see cref="ErrorAsync"/> event when an exception occurs
        /// inside the TCP session processing pipeline.
        /// 
        /// <para>
        /// This method wraps the exception into an <see cref="ExceptionEventArgs"/>
        /// instance and invokes the asynchronous <see cref="ErrorAsync"/> handler
        /// if one is attached. The server awaits the handler's returned
        /// <see cref="Task"/>, allowing the subscriber to perform asynchronous
        /// logging, cleanup, or diagnostics.
        /// </para>
        /// </summary>
        /// <param name="x">The exception that occurred.</param>
        protected virtual async Task OnErrorAsync(Exception x)
        {
            if(this.ErrorAsync != null){
                await this.ErrorAsync(new ExceptionEventArgs(x));
            }
        }

        #endregion


        /// <summary>
        /// Internal notification that the session is being disposed.
        /// </summary>
        internal event EventHandler? Disposing = null;

        #endregion

    }
}
