using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.TCP 
{
    /// <summary>
    /// Specifies how TLS is applied to a TCP server connection.
    /// </summary>
    public enum TCP_ServerTlsMode
    {
        /// <summary>
        /// No TLS is used; connection stays unencrypted.
        /// </summary>
        None,

        /// <summary>
        /// TLS is used immediately after connection (implicit TLS).
        /// </summary>
        Implicit,

        /// <summary>
        /// Connection starts unencrypted and is upgraded to TLS using a protocol-specific command.
        /// </summary>
        Explicit
    }

}
