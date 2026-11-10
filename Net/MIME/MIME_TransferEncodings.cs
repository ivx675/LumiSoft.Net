using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Defines MIME content transfer encoding names as specified in
    /// RFC 2045 section 6.
    /// </summary>
    public static class MIME_TransferEncodings
    {        
        /// <summary>
        /// The 7bit transfer encoding.
        /// </summary>
        /// <remarks>
        /// Content contains only US-ASCII characters.
        /// </remarks>
        public const string SevenBit = "7bit";

        /// <summary>
        /// The 8bit transfer encoding.
        /// </summary>
        /// <remarks>
        /// Content may contain 8-bit characters but remains line-oriented.
        /// </remarks>
        public const string EightBit = "8bit";

        /// <summary>
        /// The quoted-printable transfer encoding.
        /// </summary>
        public const string QuotedPrintable = "quoted-printable";

        /// <summary>
        /// The Base64 transfer encoding.
        /// </summary>
        public const string Base64 = "base64";

        /// <summary>
        /// The binary transfer encoding.
        /// </summary>
        /// <remarks>
        /// Content may contain arbitrary binary data.
        /// </remarks>
        public const string Binary = "binary";
    }
}
