using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.DNS
{
    /// <summary>
    /// DNS response codes (RCODE) used in message headers, as defined in
    /// RFC 1035, RFC 2136, RFC 2845, and RFC 6891.
    /// </summary>
    public enum DNS_ResponseCode
    {
        /// <summary>
        /// No error condition.
        /// </summary>
        NoError = 0,

        /// <summary>
        /// Format error — The server could not interpret the query.
        /// </summary>
        FormatError = 1,

        /// <summary>
        /// Server failure — The server encountered an internal error.
        /// </summary>
        ServerFailure = 2,

        /// <summary>
        /// Name error (NXDOMAIN) — The domain name does not exist.
        /// </summary>
        NameError = 3,

        /// <summary>
        /// Not implemented — The server does not support the requested operation.
        /// </summary>
        NotImplemented = 4,

        /// <summary>
        /// Refused — The server refuses to perform the operation for policy reasons.
        /// </summary>
        Refused = 5,

        /// <summary>
        /// Name exists when it should not (RFC 2136).
        /// </summary>
        YxDomain = 6,

        /// <summary>
        /// RRset exists when it should not (RFC 2136).
        /// </summary>
        YxRrSet = 7,

        /// <summary>
        /// RRset does not exist when it should (RFC 2136).
        /// </summary>
        NxRrSet = 8,

        /// <summary>
        /// Server is not authoritative for the zone (RFC 2136).
        /// </summary>
        NotAuthoritative = 9,

        /// <summary>
        /// Name not in zone (RFC 2136).
        /// </summary>
        NotZone = 10,

        /// <summary>
        /// TSIG signature failure (RFC 2845).
        /// </summary>
        BadSignature = 16,

        /// <summary>
        /// TSIG key failure (RFC 2845).
        /// </summary>
        BadKey = 17,

        /// <summary>
        /// TSIG timestamp failure (RFC 2845).
        /// </summary>
        BadTime = 18
    }
}
