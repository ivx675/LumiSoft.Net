using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.DNS
{
    /// <summary>
	/// Defines the DNS type codes used in both query (QTYPE) and resource record
	/// (TYPE) fields, as specified in RFC 1035 and related extensions.
	/// </summary>
	public enum DNS_RecordType
	{
		/// <summary>
		/// IPv4 host address (A record).
		/// </summary>
		A = 1,

		/// <summary>
		/// Authoritative name server.
		/// </summary>
		NS    = 2,  

	//	MD    = 3,  Obsolete
	//	MF    = 4,  Obsolete

		/// <summary>
		/// Canonical name for an alias.
		/// </summary>
		CNAME = 5,  

		/// <summary>
		/// Start of a zone of authority.
		/// </summary>
		SOA   = 6,  

	//	MB    = 7,  EXPERIMENTAL
	//	MG    = 8,  EXPERIMENTAL
	//  MR    = 9,  EXPERIMENTAL
	//	NULL  = 10, EXPERIMENTAL

	/*	/// <summary>
		/// A well known service description.
		/// </summary>
		WKS   = 11, */

		/// <summary>
		/// Domain name pointer.
		/// </summary>
		PTR   = 12, 

		/// <summary>
		/// Host information (CPU and OS).
		/// </summary>
		HINFO = 13, 
/*
		/// <summary>
		/// Mailbox or mail list information.
		/// </summary>
		MINFO = 14, */

		/// <summary>
		/// Mail exchange.
		/// </summary>
		MX    = 15, 

		/// <summary>
		/// Arbitrary text strings.
		/// </summary>
		TXT   = 16, 

		/// <summary>
		/// IPv6 host address.
		/// </summary>
		AAAA = 28,

        /// <summary>
		/// Service location record.
		/// </summary>
        SRV = 33,

        /// <summary>
		/// Naming Authority Pointer (NAPTR) record.
		/// </summary>
        NAPTR = 35,

        /// <summary>
		/// Special query type requesting all records the server is willing to return.
		/// </summary>
        ANY = 255,
	}
}
