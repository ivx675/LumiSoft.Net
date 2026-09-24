using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;
using LumiSoft.Net.Mail;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// This class represents the IMAP FETCH response ENVELOPE data-item.
    /// </summary>
    /// <remarks>
    /// The ENVELOPE structure provides a summary of the message's header fields,
    /// including date, subject, address lists, and message identifiers. 
    /// </remarks>
    public class IMAP_t_Fetch_r_i_Envelope : IMAP_t_Fetch_r_i
    {
        private IMAP_t_Envelope m_pEnvelope;
		    
		/// <summary>
		/// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_Envelope"/>
		/// class using the specified ENVELOPE element.
		/// </summary>
		/// <param name="envelope">
		/// The ENVELOPE element describing the message’s header fields. This value
		/// must not be <c>null</c>.
		/// </param>
        public IMAP_t_Fetch_r_i_Envelope(IMAP_t_Envelope envelope)
        {
            if(envelope == null){
                throw new ArgumentNullException(nameof(envelope));
            }

            m_pEnvelope = envelope;
        }


        #region method ParseAsync

        /// <summary>
        /// Parses an IMAP <c>ENVELOPE</c> FETCH data item.
        /// </summary>
        /// <remarks>
        /// Expects the reader at the <c>ENVELOPE</c> atom and consumes the full
        /// parenthesized structure:
        /// <code>
        /// (date subject from sender reply-to to cc bcc in-reply-to message-id)
        /// </code>
        /// </remarks>
        /// <param name="imapReader">Reader positioned at an ENVELOPE data item.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Parsed ENVELOPE FETCH data-item structure.</returns>
        /// <exception cref="ParseException">
        /// Thrown if the ENVELOPE syntax is malformed.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Envelope> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* IMAP FETCH Data Item: ENVELOPE
               Reference: RFC 3501, Section 7.4.2

               ENVELOPE returns a structured summary of key message header fields without
               requiring the client to fetch the full header.

               Syntax (FETCH response):
                   ENVELOPE SP envelope-structure

               Envelope structure:
                   (date subject from sender reply-to to cc bcc in-reply-to message-id)

               Example:
                   S: * 12 FETCH (ENVELOPE
                        ("Mon, 18 Sep 2023 14:22:10 +0000"
                         "Meeting Tomorrow"
                         (("Alice" NIL "alice" "example.com"))
                         (("Alice" NIL "alice" "example.com"))
                         (("Alice" NIL "alice" "example.com"))
                         (("Bob" NIL "bob" "example.com"))
                         NIL
                         NIL
                         "<CAF12345@example.com>"
                         "<MSGID-98765@example.com>"))

               Field notes:
                 • date          – "Date:" header field, IMAP string or NIL.
                 • subject       – "Subject:" header field, IMAP string or NIL.
                 • from          – Address list from "From:" header, NIL or list.
                 • sender        – Address list from "Sender:" header, NIL or list.
                 • reply-to      – Address list from "Reply-To:" header, NIL or list.
                 • to            – Address list from "To:" header, NIL or list.
                 • cc            – Address list from "Cc:" header, NIL or list.
                 • bcc           – Address list from "Bcc:" header, NIL or list.
                 • in-reply-to   – "In-Reply-To:" header field, IMAP string or NIL.
                 • message-id    – "Message-ID:" header field, IMAP string or NIL.

               All string fields may be NIL. Address lists may be NIL or empty. The parser
               must read the parenthesized structure exactly as defined in RFC 3501.
            */

            if(!string.Equals("ENVELOPE", imapReader.ReadAtom(), StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: ENVELOPE data-item not found.");
            }
                        
            return new IMAP_t_Fetch_r_i_Envelope(await IMAP_t_Envelope.ParseAsync(imapReader,cancellationToken));
        }

        #endregion

        #region static method ConstructEnvelope

		/// <summary>
		/// Construct secified mime entity ENVELOPE string.
		/// </summary>
		/// <param name="entity">Mail message.</param>
		/// <returns></returns>
		public static string ConstructEnvelope(Mail_Message entity)
		{
			/* RFC 3501 7.4.2
				ENVELOPE
					A parenthesized list that describes the envelope structure of a
					message.  This is computed by the server by parsing the
					[RFC-2822] header into the component parts, defaulting various
					fields as necessary.

					The fields of the envelope structure are in the following
					order: date, subject, from, sender, reply-to, to, cc, bcc,
					in-reply-to, and message-id.  The date, subject, in-reply-to,
					and message-id fields are strings.  The from, sender, reply-to,
					to, cc, and bcc fields are parenthesized lists of address
					structures.

					An address structure is a parenthesized list that describes an
					electronic mail address.  The fields of an address structure
					are in the following order: personal name, [SMTP]
					at-domain-list (source route), mailbox name, and host name.

					[RFC-2822] group syntax is indicated by a special form of
					address structure in which the host name field is NIL.  If the
					mailbox name field is also NIL, this is an end of group marker
					(semi-colon in RFC 822 syntax).  If the mailbox name field is
					non-NIL, this is a start of group marker, and the mailbox name
					field holds the group name phrase.

					If the Date, Subject, In-Reply-To, and Message-ID header lines
					are absent in the [RFC-2822] header, the corresponding member
					of the envelope is NIL; if these header lines are present but
					empty the corresponding member of the envelope is the empty
					string.
					
						Note: some servers may return a NIL envelope member in the
						"present but empty" case.  Clients SHOULD treat NIL and
						empty string as identical.

						Note: [RFC-2822] requires that all messages have a valid
						Date header.  Therefore, the date member in the envelope can
						not be NIL or the empty string.

						Note: [RFC-2822] requires that the In-Reply-To and
						Message-ID headers, if present, have non-empty content.
						Therefore, the in-reply-to and message-id members in the
						envelope can not be the empty string.

					If the From, To, cc, and bcc header lines are absent in the
					[RFC-2822] header, or are present but empty, the corresponding
					member of the envelope is NIL.

					If the Sender or Reply-To lines are absent in the [RFC-2822]
					header, or are present but empty, the server sets the
					corresponding member of the envelope to be the same value as
					the from member (the client is not expected to know to do
					this).

						Note: [RFC-2822] requires that all messages have a valid
						From header.  Therefore, the from, sender, and reply-to
						members in the envelope can not be NIL.
		 
					ENVELOPE ("date" "subject" from sender reply-to to cc bcc "in-reply-to" "messageID")
			*/

			// NOTE: all header fields and parameters must in ENCODED form !!!

            MIME_Encoding_EncodedWord wordEncoder = new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.B,Encoding.UTF8);
            wordEncoder.Split = false;

			StringBuilder retVal = new StringBuilder();
			retVal.Append("ENVELOPE (");

			// date
            try{
			    if(entity.Date != DateTime.MinValue){
				    retVal.Append(TextUtils.QuoteString(MIME_Utils.DateTimeToRfc2822(entity.Date)));
		    	}
			    else{
				    retVal.Append("NIL");
			    }
            }
            catch{
                retVal.Append("NIL");
            }

			// subject
			if(entity.Subject != null){
				//retVal.Append(" " + TextUtils.QuoteString(wordEncoder.Encode(entity.Subject)));
                string val = wordEncoder.Encode(entity.Subject);
                retVal.Append(" {" + val.Length + "}\r\n" + val);
			}
			else{
				retVal.Append(" NIL");
			}

			// from
			if(entity.From != null && entity.From.Count > 0){
				retVal.Append(" " + ConstructAddresses(entity.From.ToArray(),wordEncoder));
			}
			else{
				retVal.Append(" NIL");
			}

			// sender	
			//	NOTE: There is confusing part, according rfc 2822 Sender: is MailboxAddress and not AddressList.
			if(entity.Sender != null){
				retVal.Append(" (");

				retVal.Append(ConstructAddress(entity.Sender,wordEncoder));

				retVal.Append(")");
			}
			else{
				retVal.Append(" NIL");
			}

			// reply-to
			if(entity.ReplyTo != null){
				retVal.Append(" " + ConstructAddresses(entity.ReplyTo.Mailboxes,wordEncoder));
			}
			else{
				retVal.Append(" NIL");
			}

			// to
			if(entity.To != null && entity.To.Count > 0){
				retVal.Append(" " + ConstructAddresses(entity.To.Mailboxes,wordEncoder));
			}
			else{
				retVal.Append(" NIL");
			}

			// cc
			if(entity.Cc != null && entity.Cc.Count > 0){
				retVal.Append(" " + ConstructAddresses(entity.Cc.Mailboxes,wordEncoder));
			}
			else{
				retVal.Append(" NIL");
			}

			// bcc
			if(entity.Bcc != null && entity.Bcc.Count > 0){
				retVal.Append(" " + ConstructAddresses(entity.Bcc.Mailboxes,wordEncoder));
			}
			else{
				retVal.Append(" NIL");
			}

			// in-reply-to			
			if(entity.InReplyTo != null){
				retVal.Append(" " + TextUtils.QuoteString(wordEncoder.Encode(entity.InReplyTo)));
			}
			else{
				retVal.Append(" NIL");
			}

			// message-id
			if(entity.MessageID != null){
				retVal.Append(" " + TextUtils.QuoteString(wordEncoder.Encode(entity.MessageID)));
			}
			else{
				retVal.Append(" NIL");
			}

			retVal.Append(")");

			return retVal.ToString();			
		}

		#endregion

        
        #region private static method ConstructAddresses

		/// <summary>
		/// Constructs ENVELOPE addresses structure.
		/// </summary>
		/// <param name="mailboxes">Mailboxes.</param>
        /// <param name="wordEncoder">Unicode words encoder.</param>
		/// <returns></returns>
		private static string ConstructAddresses(Mail_t_Mailbox[] mailboxes,MIME_Encoding_EncodedWord wordEncoder)
		{
			StringBuilder retVal = new StringBuilder();
			retVal.Append("(");

			foreach(Mail_t_Mailbox address in mailboxes){                
				retVal.Append(ConstructAddress(address,wordEncoder));
			}

			retVal.Append(")");

			return retVal.ToString();
		}

		#endregion

		#region private static method ConstructAddress

		/// <summary>
		/// Constructs ENVELOPE address structure.
		/// </summary>
		/// <param name="address">Mailbox address.</param>
        /// <param name="wordEncoder">Unicode words encoder.</param>
		/// <returns></returns>
		private static string ConstructAddress(Mail_t_Mailbox address,MIME_Encoding_EncodedWord wordEncoder)
		{
			/* An address structure is a parenthesized list that describes an
			   electronic mail address.  The fields of an address structure
			   are in the following order: personal name, [SMTP]
			   at-domain-list (source route), mailbox name, and host name.
			*/

			// NOTE: all header fields and parameters must in ENCODED form !!!

			StringBuilder retVal = new StringBuilder();
			retVal.Append("(");

			// personal name
            if(address.DisplayName != null){
			    retVal.Append(TextUtils.QuoteString(wordEncoder.Encode(RemoveCrlf(address.DisplayName))));
            }
            else{
                retVal.Append("NIL");
            }

			// source route, always NIL (not used nowdays)
			retVal.Append(" NIL");

			// mailbox name
			retVal.Append(" " + TextUtils.QuoteString(wordEncoder.Encode(RemoveCrlf(address.LocalPart))));

			// host name
            if(address.Domain != null){
			    retVal.Append(" " + TextUtils.QuoteString(wordEncoder.Encode(RemoveCrlf(address.Domain))));
            }
            else{
                retVal.Append(" NIL");
            }

			retVal.Append(")");

			return retVal.ToString();
		}

		#endregion

        #region static method RemoveCrlf

        /// <summary>
        /// Removes CR and LF chars from the specified string.
        /// </summary>
        /// <param name="value">String value.</param>
        /// <returns>Reurns string.</returns>
        private static string RemoveCrlf(string value)
        {
            if(value == null){
                throw new ArgumentNullException("value");
            }

            return value.Replace("\r","").Replace("\n","");
        }

        #endregion


        #region Properties implementation
                
        /// <summary>
		/// Gets the ENVELOPE element associated with this FETCH response item.
		/// </summary>
		/// <remarks>
		/// The ENVELOPE element provides the message’s header information such as
		/// date, subject, address fields, and message identifiers. This property
		/// exposes the <see cref="IMAP_t_Envelope"/> instance supplied when the
		/// FETCH ENVELOPE data item was constructed.
		/// </remarks>
        public IMAP_t_Envelope Envelope
        {
            get{ return m_pEnvelope; }
        }

        #endregion
    }
}
