using System;
using System.Collections;
using System.Collections.Generic;

using LumiSoft.Net.DNS;

namespace LumiSoft.Net.DNS.Client
{
	/// <summary>
	/// This class represents dns server response.
	/// </summary>
	[Serializable]
	public class DNS_ServerResponse
	{
		private bool                 m_Truncated           = false;
        private int                  m_ID                  = 0;
		private DNS_ResponseCode     m_RCODE               = DNS_ResponseCode.NoError;
		private DNS_RecordCollection m_pAnswers;
		private DNS_RecordCollection m_pAuthoritiveAnswers;
		private DNS_RecordCollection m_pAdditionalAnswers;
		
		internal DNS_ServerResponse(bool truncated,int id,DNS_ResponseCode rcode,List<DNS_rr> answers,List<DNS_rr> authoritiveAnswers,List<DNS_rr> additionalAnswers)
		{
            m_Truncated           = truncated;
            m_ID                  = id;
			m_RCODE               = rcode;	
			m_pAnswers            = new DNS_RecordCollection(answers);
			m_pAuthoritiveAnswers = new DNS_RecordCollection(authoritiveAnswers);
			m_pAdditionalAnswers  = new DNS_RecordCollection(additionalAnswers);
		}
		

		#region method Parse
        		
		internal static DNS_ServerResponse Parse(byte[] reply)
		{	
			//--- Parse headers ------------------------------------//

			/* RFC 1035 4.1.1. Header section format
			 
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
			 
			QDCOUNT
				an unsigned 16 bit integer specifying the number of
				entries in the question section.

			ANCOUNT
				an unsigned 16 bit integer specifying the number of
				resource records in the answer section.
				
			NSCOUNT
			    an unsigned 16 bit integer specifying the number of name
                server resource records in the authority records section.

			ARCOUNT
			    an unsigned 16 bit integer specifying the number of
                resource records in the additional records section.
				
			*/		
			
			int              id                     = (reply[0]  << 8 | reply[1]);
			OPCODE           opcode                 = (OPCODE)((reply[2] >> 3) & 15);
            bool             truncated              = (reply[2] & 0x02) != 0; 
			DNS_ResponseCode replyCode              = (DNS_ResponseCode)(reply[3]  & 15);	
			int              queryCount             = (reply[4]  << 8 | reply[5]);
			int              answerCount            = (reply[6]  << 8 | reply[7]);
			int              authoritiveAnswerCount = (reply[8]  << 8 | reply[9]);
			int              additionalAnswerCount  = (reply[10] << 8 | reply[11]);
			//---- End of headers -------------------------------------------------------//
			int pos = 12;
	
			if(truncated){
                // If the response is truncated, we should not attempt to parse the rest of the message.
                return new DNS_ServerResponse(truncated, id, replyCode, new List<DNS_rr>(), new List<DNS_rr>(), new List<DNS_rr>());
            }			

			//----- Parse question part ------------//
			for(int q=0;q<queryCount;q++){
				DNS_Client.ReadQName(reply,ref pos);
				//qtype + qclass
				pos += 4;
			}
			//--------------------------------------//

			// 1) parse answers
			// 2) parse authoritive answers
			// 3) parse additional answers
			List<DNS_rr> answers = ParseAnswers(reply,answerCount,ref pos);
			List<DNS_rr> authoritiveAnswers = ParseAnswers(reply,authoritiveAnswerCount,ref pos);
			List<DNS_rr> additionalAnswers = ParseAnswers(reply,additionalAnswerCount,ref pos);

			return new DNS_ServerResponse(truncated,id,replyCode,answers,authoritiveAnswers,additionalAnswers);
		}

		#endregion		

		#region method ParseAnswers

		private static List<DNS_rr> ParseAnswers(byte[] reply,int answerCount,ref int offset)
		{
			/* RFC 1035 4.1.3. Resource record format
			 
										   1  1  1  1  1  1
			 0  1  2  3  4  5  6  7  8  9  0  1  2  3  4  5
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			|                                               |
			/                                               /
			/                      NAME                     /
			|                                               |
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			|                      TYPE                     |
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			|                     CLASS                     |
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			|                      TTL                      |
			|                                               |
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			|                   RDLENGTH                    |
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--|
			/                     RDATA                     /
			/                                               /
			+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
			*/

			List<DNS_rr> answers = new List<DNS_rr>();
			//---- Start parsing answers ------------------------------------------------------------------//
			for(int i=0;i<answerCount;i++){        
				string? name = DNS_Client.ReadQName(reply,ref offset);
				if(name == null){
					break;
				}
                                
				int type     = reply[offset++] << 8  | reply[offset++];
				int rdClass  = reply[offset++] << 8  | reply[offset++];
				int ttl      = reply[offset++] << 24 | reply[offset++] << 16 | reply[offset++] << 8  | reply[offset++];
				int rdLength = reply[offset++] << 8  | reply[offset++];
                				
                if((DNS_RecordType)type == DNS_RecordType.A){
                    answers.Add(DNS_rr_A.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.NS){
                    answers.Add(DNS_rr_NS.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.CNAME){
                    answers.Add(DNS_rr_CNAME.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.SOA){
                    answers.Add(DNS_rr_SOA.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.PTR){
                    answers.Add(DNS_rr_PTR.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.HINFO){
                    answers.Add(DNS_rr_HINFO.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.MX){
                    answers.Add(DNS_rr_MX.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.TXT){
                    answers.Add(DNS_rr_TXT.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.AAAA){
                    answers.Add(DNS_rr_AAAA.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.SRV){
                    answers.Add(DNS_rr_SRV.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else if((DNS_RecordType)type == DNS_RecordType.NAPTR){
                    answers.Add(DNS_rr_NAPTR.Parse(name,reply,ref offset,rdLength,ttl));
                }
                else{
                    // Unknown record, skip it.
                    offset += rdLength;
                }
			}

			return answers;
		}

		#endregion


		#region Properties Implementation

		/// <summary>
		/// Indicates whether the DNS server marked the UDP response as truncated (TC = 1).
		/// A truncated response means the message did not fit into a single UDP packet,
		/// and the client must retry the same query over TCP to obtain the full answer.
		/// </summary>
		internal bool IsTruncated
		{
			get{ return m_Truncated; }
		}

        /// <summary>
		/// Gets the DNS transaction identifier (ID) assigned to the query.
		/// </summary>
        public int ID
        {
            get{ return m_ID; }
        }

		/// <summary>
		/// Gets the DNS response code (RCODE) returned by the server.  
		/// The response code indicates the overall status of the DNS query,
		/// such as success, format error, server failure, name error (NXDOMAIN),
		/// not implemented, or refused.  
		/// </summary>
		public DNS_ResponseCode ResponseCode
		{
			get{ return m_RCODE; }
		}

		/// <summary>
		/// Gets the resource records returned in the Answer section of the DNS response.
		/// These records contain the actual data associated with the queried name,
		/// such as A, AAAA, CNAME, MX, TXT, or other RR types. 
		/// </summary>
		public DNS_RecordCollection Answers
		{
			get{ return m_pAnswers; }
		}

		/// <summary>
		/// Gets the resource records contained in the Authority section of the DNS response.
		/// These records typically provide information about which name servers are
		/// authoritative for the queried domain (NS records), or may include SOA records
		/// in negative responses such as NXDOMAIN. 
		/// </summary>
		public DNS_RecordCollection AuthoritiveAnswers
		{
			get{ return m_pAuthoritiveAnswers; }
		}

		/// <summary>
		/// Gets the resource records contained in the Additional section of the DNS response.
		/// These records supply extra information that may assist in resolving the query,
		/// such as address records (A/AAAA) for name servers listed in the Authority section,
		/// or other supplemental data required to complete resolution.
		/// </summary>
		public DNS_RecordCollection AdditionalAnswers
		{
			get{ return m_pAdditionalAnswers; }
		}

		#endregion
	}
}
