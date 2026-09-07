using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LumiSoft.Net
{
    /// <summary>
    /// Specifies the security mode used for a TCP client connection.
    /// </summary>
    public enum TcpClientSecurity
    {
        /// <summary>
        /// No security; the connection is not encrypted.
        /// </summary>
        None = 0,

        /// <summary>
        /// Use SSL to secure the connection.
        /// </summary>
        SSL = 1,

        /// <summary>
        /// Use TLS to secure the connection.
        /// </summary>
        TLS = 2,

        /// <summary>
        /// Use TLS if the remote server supports it.
        /// </summary>
        UseTlsIfSupported = 3,
    }
}
