using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Specifies the RFC 2047 encoded-word encoding method.
    /// </summary>
    public enum MIME_EncodedWordEncoding
    {
        /// <summary>
        /// The Q encoding as defined in RFC 2047 section 4.1.
        /// </summary>
        Q,

        /// <summary>
        /// The B encoding as defined in RFC 2047 section 4.2.
        /// </summary>
        B
    }
}
