using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.DNS 
{
    /// <summary>
    /// Represents the resource records contained in a single section of a DNS
    /// response (Answer, Authority, or Additional). This collection provides
    /// typed accessors for common RR types and exposes each group as an array
    /// to allow efficient counting and indexing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each typed property returns an array of records of the corresponding RR
    /// type.
    /// </para>
    /// </remarks>
    public class DNS_RecordCollection
    {
        private readonly DNS_rr[] _records;

        /// <summary>
        /// Initializes a new instance of the <see cref="DNS_RecordCollection"/> class
        /// using the specified set of DNS resource records.
        /// </summary>
        /// <param name="records">
        /// The resource records belonging to a DNS message section (Answer,
        /// Authority, or Additional).
        /// </param>
        public DNS_RecordCollection(IEnumerable<DNS_rr> records)
        {
            _records = records.ToArray();
        }

        /// <summary>
        /// Gets all IPv4 address (A) records in the collection.
        /// </summary>
        public DNS_rr_A[] A => _records.OfType<DNS_rr_A>().ToArray();

        /// <summary>
        /// Gets all IPv6 address (AAAA) records in the collection.
        /// </summary>
        public DNS_rr_AAAA[] AAAA => _records.OfType<DNS_rr_AAAA>().ToArray();

        /// <summary>
        /// Gets all canonical name (CNAME) records in the collection.
        /// </summary>
        public DNS_rr_CNAME[] CNAME => _records.OfType<DNS_rr_CNAME>().ToArray();

        /// <summary>
        /// Gets all mail exchange (MX) records in the collection.
        /// </summary>
        public DNS_rr_MX[] MX => _records.OfType<DNS_rr_MX>().ToArray();

        /// <summary>
        /// Gets all name server (NS) records in the collection.
        /// </summary>
        public DNS_rr_NS[] NS => _records.OfType<DNS_rr_NS>().ToArray();

        /// <summary>
        /// Gets all pointer (PTR) records in the collection.
        /// </summary>
        public DNS_rr_PTR[] PTR => _records.OfType<DNS_rr_PTR>().ToArray();

        /// <summary>
        /// Gets all start‑of‑authority (SOA) records in the collection.
        /// </summary>
        public DNS_rr_SOA[] SOA => _records.OfType<DNS_rr_SOA>().ToArray();

        /// <summary>
        /// Gets all service location (SRV) records in the collection.
        /// </summary>
        public DNS_rr_SRV[] SRV => _records.OfType<DNS_rr_SRV>().ToArray();

        /// <summary>
        /// Gets all text (TXT) records in the collection.
        /// </summary>
        public DNS_rr_TXT[] TXT => _records.OfType<DNS_rr_TXT>().ToArray();

        /// <summary>
        /// Gets all resource records in the collection, regardless of type.
        /// </summary>
        public IEnumerable<DNS_rr> All => _records;
    }
}
