using LumiSoft.Net.Mail;
using LumiSoft.Net.MIME;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents the IMAP ENVELOPE data item defined in RFC 3501.
    /// </summary>
    /// <remarks>
    /// The properties of this class correspond to the fields of the IMAP
    /// ENVELOPE structure.
    /// </remarks>
    public class IMAP_t_Envelope
    {
        private DateTime?        m_Date      = null;
        private string?          m_Subject   = null;
        private Mail_t_Mailbox[] m_pFrom;
        private Mail_t_Mailbox[] m_pSender;
        private Mail_t_Mailbox[] m_pReplyTo;
        private Mail_t_Mailbox[] m_pTo;
        private Mail_t_Mailbox[] m_pCc;
        private Mail_t_Mailbox[] m_pBcc;
        private string?          m_InReplyTo = null;
        private string?          m_MessageID = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Envelope"/> class.
        /// </summary>
        /// <param name="date">
        /// Message date.
        /// </param>
        /// <param name="subject">
        /// Message subject, or <see langword="null"/> if not specified.
        /// </param>
        /// <param name="from">
        /// From addresses. If <see langword="null"/>, an empty array is used.
        /// </param>
        /// <param name="sender">
        /// Sender addresses. If <see langword="null"/>, an empty array is used.
        /// </param>
        /// <param name="replyTo">
        /// Reply-To addresses. If <see langword="null"/>, an empty array is used.
        /// </param>
        /// <param name="to">
        /// Recipient addresses. If <see langword="null"/>, an empty array is used.
        /// </param>
        /// <param name="cc">
        /// Cc addresses. If <see langword="null"/>, an empty array is used.
        /// </param>
        /// <param name="bcc">
        /// Bcc addresses. If <see langword="null"/>, an empty array is used.
        /// </param>
        /// <param name="inReplyTo">
        /// In-Reply-To value, or <see langword="null"/> if not specified.
        /// </param>
        /// <param name="messageID">
        /// Message-ID value, or <see langword="null"/> if not specified.
        /// </param>
        /// <remarks>
        /// Any address-list parameter specified as <see langword="null"/> is
        /// normalized to an empty array.
        /// </remarks>
        public IMAP_t_Envelope(DateTime? date,string? subject,Mail_t_Mailbox[]? from,Mail_t_Mailbox[]? sender,Mail_t_Mailbox[]? replyTo,Mail_t_Mailbox[]? to,Mail_t_Mailbox[]? cc,Mail_t_Mailbox[]? bcc,string? inReplyTo,string? messageID)
        {
            m_Date      = date;
            m_Subject   = subject;
            m_pFrom     = from ?? [];
            m_pSender   = sender ?? [];
            m_pReplyTo  = replyTo ?? [];
            m_pTo       = to ?? [];
            m_pCc       = cc ?? [];
            m_pBcc      = bcc ?? [];
            m_InReplyTo = inReplyTo;
            m_MessageID = messageID;
        }


        #region static method ParseAsync

        /// <summary>
        /// Reads an IMAP ENVELOPE structure from the current reader position and
        /// constructs an <see cref="IMAP_t_Envelope"/> instance.
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the beginning of an ENVELOPE structure.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Envelope"/> containing the header fields defined
        /// by the ENVELOPE element.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the ENVELOPE structure is malformed or required tokens are
        /// missing.
        /// </exception>
        internal static async Task<IMAP_t_Envelope> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
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

            if(imapReader.ReadChar() != '('){
                throw new ParseException("Invalid ENVELOPE structure not found.");
            }

            // date
            string? dateString = await imapReader.ReadStringAsync(cancellationToken);
            DateTime date = DateTime.MinValue;
            if(dateString != null){
                try{
                    date = MIME_Utils.ParseRfc2822DateTime(dateString);
                }
                catch{
                    throw new ParseException($"Invalid Invalid ENVELOPE date '{dateString}'.");
                }
            }

            // subject
            string? subject = await imapReader.ReadStringAsync(cancellationToken);
            if(subject != null){
                subject = MIME_Encoding_EncodedWord.DecodeTextS(subject);
            }

            // from
            var from = await ParseAddressList(imapReader,cancellationToken);

            // sender
            var sender = await ParseAddressList(imapReader,cancellationToken);

            // reply-to
            var replyTo = await ParseAddressList(imapReader,cancellationToken);

            // to
            var to = await ParseAddressList(imapReader,cancellationToken);

            // cc
            var cc = await ParseAddressList(imapReader,cancellationToken);

            // bcc
            var bcc = await ParseAddressList(imapReader,cancellationToken);

            // in-reply-to
            string? inReplyTo = await imapReader.ReadStringAsync(cancellationToken);

            // message-id
            string? messageId = await imapReader.ReadStringAsync(cancellationToken);

            if(imapReader.ReadChar() != ')'){
                throw new ParseException("Invalid ENVELOPE structure: Final closing ')' not found.");
            }

            return new IMAP_t_Envelope(date,subject,from,sender,replyTo,to,cc,bcc,inReplyTo,messageId);
        }

        #endregion

        #region static method ParseFromString

        /// <summary>
        /// Parses an IMAP ENVELOPE structure from its textual representation.
        /// </summary>
        /// <param name="envelope">
        /// The ENVELOPE value to parse. The string must contain only the
        /// parenthesized ENVELOPE structure and must not reference IMAP literals,
        /// because this method operates on an in-memory string and performs no
        /// network or stream I/O.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Envelope"/> instance populated from the specified
        /// ENVELOPE data.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="envelope"/> is <see langword="null"/> or empty.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if the supplied ENVELOPE structure is malformed.
        /// </exception>
        /// <remarks>
        /// This method is intended for parsing ENVELOPE values that are already
        /// available as a complete string.
        /// </remarks>
        public static IMAP_t_Envelope ParseFromString(string envelope)
        {
            if(string.IsNullOrEmpty(envelope)){
                throw new ArgumentException("Envelope value may not be null or empty.",nameof(envelope));
            }

            return ParseAsync(new _IMAP_Reader(envelope)).GetAwaiter().GetResult();
        }

        #endregion

        #region static method FromMessage

        /// <summary>
        /// Creates an <see cref="IMAP_t_Envelope"/> instance from the specified mail message.
        /// </summary>
        /// <param name="message">The source <see cref="Mail_Message"/> to convert.</param>
        /// <returns>Returns a populated <see cref="IMAP_t_Envelope"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <paramref name="message"/> is a null reference.</exception>
        public static IMAP_t_Envelope FromMessage(Mail_Message message)
        {
            if(message == null){
                throw new ArgumentNullException(nameof(message));
            }            

            return new IMAP_t_Envelope(
                message.Date,
                message.Subject,
                message.From?.ToArray(),
                message.Sender == null ? null : [message.Sender],
                message.ReplyTo?.Mailboxes,
                message.To?.Mailboxes,
                message.Cc?.Mailboxes,
                message.Bcc?.Mailboxes,
                message.InReplyTo,
                message.MessageID
            );
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP ENVELOPE representation of this instance as a parenthesized 10-field tuple string 
        /// adhering to RFC 9051 (excluding the 'ENVELOPE' keyword).
        /// </summary>
        /// <returns>A valid IMAP ENVELOPE string.</returns>
        public override string ToString()
        {
            string dateStr = (Date == null) ? "NIL" : $"\"{Date:r}\"";
            string subjectStr = string.IsNullOrEmpty(Subject) ? "NIL" : $"\"{EscapeString(Subject)}\"";
            string inReplyToStr = string.IsNullOrEmpty(InReplyTo) ? "NIL" : $"\"{EscapeString(InReplyTo)}\"";
            string messageIdStr = string.IsNullOrEmpty(MessageID) ? "NIL" : $"\"{EscapeString(MessageID)}\"";

            return $"({dateStr} {subjectStr} " +
                   $"{FormatAddressListString(From)} " +
                   $"{FormatAddressListString(Sender)} " +
                   $"{FormatAddressListString(ReplyTo)} " +
                   $"{FormatAddressListString(To)} " +
                   $"{FormatAddressListString(Cc)} " +
                   $"{FormatAddressListString(Bcc)} " +
                   $"{inReplyToStr} {messageIdStr})";
        }

        #endregion


        #region static method EscapeString

        private static string EscapeString(string value)
        {
            if(string.IsNullOrEmpty(value)){
                return string.Empty;
            }

            return value
                .Replace("\r", "") // Strip carriage returns
                .Replace("\n", "") // Strip newlines
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        #endregion

        #region static method FormatAddressListString

        private static string FormatAddressListString(Mail_t_Mailbox[] mailboxes)
        {
            if(mailboxes == null || mailboxes.Length == 0){
                return "NIL";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append('(');
            foreach (var mb in mailboxes){
                // IMAP address structure: (name adl mailbox host)
                sb.Append('(');
        
                // 1. Name (display name or NIL)
                if(string.IsNullOrEmpty(mb.DisplayName)){
                    sb.Append("NIL");
                }
                else{
                    sb.Append('"').Append(EscapeString(mb.DisplayName)).Append('"');
                }
                sb.Append(' ');

                // 2. ADL (Source Route) - obsolete, always NIL in modern mail
                sb.Append("NIL");
                sb.Append(' ');

                // 3. Mailbox (local part) using built-in property
                sb.Append('"').Append(EscapeString(mb.LocalPart)).Append('"');
                sb.Append(' ');

                // 4. Host (domain) using built-in property
                sb.Append('"').Append(EscapeString(mb.Domain)).Append('"');

                sb.Append(')');
            }
            sb.Append(')');

            return sb.ToString();
        }

        #endregion        
        
        #region static method ParseAddressList

        /// <summary>
        /// Parses an IMAP ENVELOPE address-list.
        /// </summary>
        /// <param name="imapReader">IMAP reader positioned at the start of an ENVELOPE address-list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Array of <see cref="Mail_t_Mailbox"/> instances for all valid
        /// mailbox addresses in the list.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the address-list syntax is malformed.
        /// </exception>
        private async static Task<Mail_t_Mailbox[]> ParseAddressList(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            /* IMAP ENVELOPE address-list item
               RFC 3501 7.4.2

               address-list = "(" 1*address ")" / NIL
               address      = "(" name adl mailbox host ")"

               name    = display name or NIL
               adl     = at-domain-list (obsolete), always NIL in modern mail
               mailbox = local-part
               host    = domain

               Notes:
                 • IMAP ENVELOPE never returns RFC 5322 group syntax.
                 • Group addresses are flattened into individual mailbox addresses.
                 • NIL means the header field was absent.
            */

            // NIL
            if(imapReader.PeekIs("NIL")){
                imapReader.ReadAtom(); 

                return [];
            }
            // Empty list
            if (imapReader.PeekChar() == ')'){
                imapReader.ReadChar();

                return [];
            }
            if(imapReader.ReadChar() != '('){
                throw new ParseException("Invalid ENVELOPE address-list.");
            }

            List<Mail_t_Mailbox> retVal = new List<Mail_t_Mailbox>();
            while(true){
                if(imapReader.ReadChar() != '('){
                    throw new ParseException("Invalid ENVELOPE address-list.");
                }

                string? name = await imapReader.ReadStringAsync(cancellationToken);
                if(name != null){
                    name = MIME_Encoding_EncodedWord.DecodeTextS(name);
                }
                string? adl     = await imapReader.ReadStringAsync(cancellationToken);
                string? mailbox = await imapReader.ReadStringAsync(cancellationToken);
                string? host    = await imapReader.ReadStringAsync(cancellationToken);

                // Contruct address only if mailbox and host is present.
                if(mailbox != null && host != null){
                    retVal.Add(new Mail_t_Mailbox(name,mailbox + "@" + host));
                }

                if(imapReader.ReadChar() != ')'){
                    throw new ParseException("Invalid ENVELOPE address-list.");
                }

                // Address-list closing ')'
                if(imapReader.PeekIs(')')){
                    imapReader.ReadChar();

                    break;
                }
            }

            return retVal.ToArray();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the message date value from the ENVELOPE element.
        /// </summary>
        /// <remarks>
        /// The date corresponds to the <c>Date:</c> header field as represented in
        /// the IMAP ENVELOPE structure.
        /// Returns <c>null</c> if the ENVELOPE contained <c>NIL</c> for the Date field.
        /// </remarks>
        public DateTime? Date
        {
            get{ return m_Date; }
        }

        /// <summary>
        /// Gets the message subject value from the ENVELOPE element.
        /// </summary>
        /// <remarks>
        /// The subject corresponds to the <c>Subject:</c> header field as represented
        /// in the IMAP ENVELOPE structure. The value may be <c>null</c> if the
        /// ENVELOPE element contained <c>NIL</c> for the subject field.
        /// </remarks>
        public string? Subject
        {
            get{ return m_Subject; }
        }

        /// <summary>
        /// Gets the list of addresses from the ENVELOPE <c>From</c> field.
        /// </summary>
        /// <remarks>
        /// The <c>From</c> field corresponds to the <c>From:</c> header as represented
        /// in the IMAP ENVELOPE structure. The returned array may be empty if the
        /// ENVELOPE element contained <c>NIL</c> or an empty address list.
        /// </remarks>
        public Mail_t_Mailbox[] From
        {
            get{ return m_pFrom; }
        }

        /// <summary>
        /// Gets the list of addresses from the ENVELOPE <c>Sender</c> field.
        /// </summary>
        /// <remarks>
        /// The <c>Sender</c> field corresponds to the <c>Sender:</c> header as represented
        /// in the IMAP ENVELOPE structure. The returned array may be empty if the
        /// ENVELOPE element contained <c>NIL</c> or an empty address list.
        /// </remarks>
        public Mail_t_Mailbox[] Sender
        {
            get{ return m_pSender; }
        }

        /// <summary>
        /// Gets the list of addresses from the ENVELOPE <c>Reply-To</c> field.
        /// </summary>
        /// <remarks>
        /// The <c>Reply-To</c> field corresponds to the <c>Reply-To:</c> header as
        /// represented in the IMAP ENVELOPE structure. The returned array may be
        /// empty if the ENVELOPE element contained <c>NIL</c> or an empty address
        /// list.
        /// </remarks>
        public Mail_t_Mailbox[] ReplyTo
        {
            get{ return m_pReplyTo; }
        }

        /// <summary>
        /// Gets the list of addresses from the ENVELOPE <c>To</c> field.
        /// </summary>
        /// <remarks>
        /// The <c>To</c> field corresponds to the <c>To:</c> header as represented in
        /// the IMAP ENVELOPE structure. The returned array may be empty if the
        /// ENVELOPE element contained <c>NIL</c> or an empty address list.
        /// </remarks>
        public Mail_t_Mailbox[] To
        {
            get{ return m_pTo; }
        }

        /// <summary>
        /// Gets the list of addresses from the ENVELOPE <c>Cc</c> field.
        /// </summary>
        /// <remarks>
        /// The <c>Cc</c> field corresponds to the <c>Cc:</c> header as represented in
        /// the IMAP ENVELOPE structure. The returned array may be empty if the
        /// ENVELOPE element contained <c>NIL</c> or an empty address list.
        /// </remarks>
        public Mail_t_Mailbox[] Cc
        {
            get{ return m_pCc; }
        }

        /// <summary>
        /// Gets the list of addresses from the ENVELOPE <c>Bcc</c> field.
        /// </summary>
        /// <remarks>
        /// The <c>Bcc</c> field corresponds to the <c>Bcc:</c> header as represented in
        /// the IMAP ENVELOPE structure. The returned array may be empty if the
        /// ENVELOPE element contained <c>NIL</c> or an empty address list.
        /// </remarks>
        public Mail_t_Mailbox[] Bcc
        {
            get{ return m_pBcc; }
        }
        
        /// <summary>
        /// Gets the <c>In-Reply-To</c> value from the ENVELOPE element.
        /// </summary>
        /// <remarks>
        /// The <c>In-Reply-To</c> field corresponds to the <c>In-Reply-To:</c> header
        /// as represented in the IMAP ENVELOPE structure. The value may be
        /// <c>null</c> if the ENVELOPE element contained <c>NIL</c> for this field.
        /// </remarks>
        public string? InReplyTo
        {
            get{ return m_InReplyTo; }
        }
        
        /// <summary>
        /// Gets the <c>Message-ID</c> value from the ENVELOPE element.
        /// </summary>
        /// <remarks>
        /// The <c>Message-ID</c> field corresponds to the <c>Message-ID:</c> header as
        /// represented in the IMAP ENVELOPE structure. The value may be <c>null</c>
        /// if the ENVELOPE element contained <c>NIL</c> for this field.
        /// </remarks>
        public string? MessageID
        {
            get{ return m_MessageID; }
        }

        #endregion
    }
}
