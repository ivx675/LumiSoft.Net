using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;

using LumiSoft.Net.IO;
using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents an electronic mail message as defined in RFC 5322.
    /// </summary>
    public class Mail_Message : MIME_Message
    {
        private Stream? m_pStream = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_Message"/> class.
        /// </summary>
        public Mail_Message()
        {
            this.Header.FieldsProvider.HeaderFields.Add("From",typeof(Mail_h_MailboxList));
            this.Header.FieldsProvider.HeaderFields.Add("Sender",typeof(Mail_h_Mailbox));
            this.Header.FieldsProvider.HeaderFields.Add("Reply-To",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("To",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("Cc",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("Bcc",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("Resent-From",typeof(Mail_h_MailboxList));
            this.Header.FieldsProvider.HeaderFields.Add("Resent-Sender",typeof(Mail_h_Mailbox));
            this.Header.FieldsProvider.HeaderFields.Add("Resent-To",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("Resent-Cc",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("Resent-Bcc",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("Resent-Reply-To",typeof(Mail_h_AddressList));
            this.Header.FieldsProvider.HeaderFields.Add("Return-Path",typeof(Mail_h_ReturnPath));
            this.Header.FieldsProvider.HeaderFields.Add("Received",typeof(Mail_h_Received));
            this.Header.FieldsProvider.HeaderFields.Add("Disposition-Notification-To",typeof(Mail_h_MailboxList));
        }

        #region override method Dispose

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        /// <remarks>
        /// If this instance owns a backing stream, the stream is disposed.
        /// </remarks>
        public override void Dispose()
        {
            if(m_pStream != null){
                m_pStream.Dispose();
                m_pStream = null;
            }

            base.Dispose();
        }

        #endregion


        #region static method Create

        /// <summary>
        /// Creates a mail message with the specified content and attachments.
        /// </summary>
        /// <param name="from">
        /// Sender address.
        /// </param>
        /// <param name="to">
        /// Recipient addresses.
        /// </param>
        /// <param name="cc">
        /// Carbon copy recipient addresses, or <see langword="null"/> if not used.
        /// </param>
        /// <param name="bcc">
        /// Blind carbon copy recipient addresses, or <see langword="null"/> if
        /// not used.
        /// </param>
        /// <param name="subject">
        /// Message subject.
        /// </param>
        /// <param name="text">
        /// Plain-text message body.
        /// </param>
        /// <param name="html">
        /// HTML message body, or <see langword="null"/> if not used.
        /// </param>
        /// <param name="attachments">
        /// Message attachments, or <see langword="null"/> if not used.
        /// </param>
        /// <returns>
        /// Returns the created mail message.
        /// </returns>
        /// <remarks>
        /// Depending on the supplied arguments, the message body is created as
        /// one of the following MIME structures:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <c>text/plain</c>
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>multipart/alternative</c> containing plain-text and HTML body parts.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>multipart/mixed</c> containing the message body and attachments.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>multipart/mixed</c> containing a
        /// <c>multipart/alternative</c> body and attachments.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static Mail_Message Create(Mail_t_Mailbox from,Mail_t_Address[] to,Mail_t_Address[]? cc,Mail_t_Address[]? bcc,string subject,string text,string? html,Mail_t_Attachment[]? attachments)
        {
            // Create header.
            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            if(from != null){
                msg.From = new Mail_t_MailboxList();
                msg.From.Add(from);
            }
            if(to != null){
                msg.To = new Mail_t_AddressList();
                foreach(Mail_t_Address address in to){
                    msg.To.Add(address);
                }
            }
            msg.Subject = subject;

            // Create message without attachments.
            if(attachments == null || attachments.Length == 0){
                // text/plain
                if(string.IsNullOrEmpty(html)){
                    MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                    msg.Body = text_plain;
                    text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,text);
                }
                // multipart/alternative
                //  text/plain
                //  text/html
                else{
                    MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative();
                    msg.Body = multipartAlternative;
                        multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Plain(MIME_TransferEncodings.QuotedPrintable,text));
                        multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Html(MIME_TransferEncodings.QuotedPrintable,html));
                }
            }
            // Create message with attachments.
            else{                
                // multipart/mixed
                //  text/plain
                //  application/octet-stream
                if(string.IsNullOrEmpty(html)){
                    MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed();
                    msg.Body = multipartMixed;
                        multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Text_Plain(MIME_TransferEncodings.QuotedPrintable,text));

                        // Add attachments.
                        foreach(Mail_t_Attachment attachment in attachments){
                            try{
                                multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment(attachment.Name,attachment.GetStream()));
                            }
                            finally{
                                attachment.CloseStream();
                            }
                        }
                }
                // multipart/mixed
                //  multipart/alternative
                //   text/plain
                //   text/html
                //  application/octet-stream
                else{
                    MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed();
                    msg.Body = multipartMixed;
                        MIME_Entity entity_multipart_alternative = new MIME_Entity();
                        MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative();
                        entity_multipart_alternative.Body = multipartAlternative;
                        multipartMixed.BodyParts.Add(entity_multipart_alternative);
                            multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Plain(MIME_TransferEncodings.QuotedPrintable,text));
                            multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Html(MIME_TransferEncodings.QuotedPrintable,html));

                        // Add attachments.
                        foreach(Mail_t_Attachment attachment in attachments){
                            try{
                                multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment(attachment.Name,attachment.GetStream()));
                            }
                            finally{
                                attachment.CloseStream();
                            }
                        }
                }
            }

            return msg;
        }

        #endregion

        #region static method Create_MultipartSigned

        /// <summary>
        /// Creates an S/MIME signed mail message.
        /// </summary>
        /// <param name="signerCert">
        /// Certificate used to sign the message.
        /// </param>
        /// <param name="from">
        /// Sender address.
        /// </param>
        /// <param name="to">
        /// Recipient addresses.
        /// </param>
        /// <param name="cc">
        /// Carbon copy recipient addresses, or <see langword="null"/> if not
        /// used.
        /// </param>
        /// <param name="bcc">
        /// Blind carbon copy recipient addresses, or <see langword="null"/> if
        /// not used.
        /// </param>
        /// <param name="subject">
        /// Message subject.
        /// </param>
        /// <param name="text">
        /// Plain-text message body.
        /// </param>
        /// <param name="html">
        /// HTML message body, or <see langword="null"/> if not used.
        /// </param>
        /// <param name="attachments">
        /// Message attachments, or <see langword="null"/> if not used.
        /// </param>
        /// <returns>
        /// Returns the created signed mail message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="signerCert"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The message is created as a <c>multipart/signed</c> S/MIME message.
        /// Depending on the supplied content and attachments, the signed content
        /// is created as one of the following MIME structures:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <c>text/plain</c>
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>multipart/alternative</c> containing plain-text and HTML body
        /// parts.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>multipart/mixed</c> containing the message body and attachments.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>multipart/mixed</c> containing a
        /// <c>multipart/alternative</c> body and attachments.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        public static Mail_Message Create_MultipartSigned(X509Certificate2 signerCert,Mail_t_Mailbox from,Mail_t_Address[] to,Mail_t_Address[] cc,Mail_t_Address[] bcc,string subject,string text,string html,Mail_t_Attachment[] attachments)
        {
            if(signerCert == null){
                throw new ArgumentNullException("signerCert");
            }

            // Create header.
            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            if(from != null){
                msg.From = new Mail_t_MailboxList();
                msg.From.Add(from);
            }
            if(to != null){
                msg.To = new Mail_t_AddressList();
                foreach(Mail_t_Address address in to){
                    msg.To.Add(address);
                }
            }
            msg.Subject = subject;

            // Create message without attachments.
            if(attachments == null || attachments.Length == 0){
                // multipart/signed
                //  text/plain
                //  application/pkcs7-signature  MIME library adds this when message saved out.
                if(string.IsNullOrEmpty(html)){
                    MIME_b_MultipartSigned multipartSigned = new MIME_b_MultipartSigned();
                    msg.Body = multipartSigned;
                    multipartSigned.SetCertificate(signerCert);
                        multipartSigned.BodyParts.Add(MIME_Entity.CreateEntity_Text_Plain(MIME_TransferEncodings.QuotedPrintable,text));
                }
                // multipart/signed
                //  multipart/alternative
                //   text/plain
                //   text/html
                //  application/pkcs7-signature  MIME library adds this when message saved out.
                else{
                    MIME_b_MultipartSigned multipartSigned = new MIME_b_MultipartSigned();
                    msg.Body = multipartSigned;
                    multipartSigned.SetCertificate(signerCert);
                        MIME_Entity entity_multipart_alternative = new MIME_Entity();
                        MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative();
                        entity_multipart_alternative.Body = multipartAlternative;
                        multipartSigned.BodyParts.Add(entity_multipart_alternative);
                            multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Plain(MIME_TransferEncodings.QuotedPrintable,text));
                            multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Html(MIME_TransferEncodings.QuotedPrintable,html));
                }                
            }
            // Create message with attachments.
            else{
                // multipart/signed
                //  multipart/mixed
                //   text/plain
                //   application/octet-stream
                //  application/pkcs7-signature  MIME library adds this when message saved out.
                if(string.IsNullOrEmpty(html)){
                    MIME_b_MultipartSigned multipartSigned = new MIME_b_MultipartSigned();
                    msg.Body = multipartSigned;
                    multipartSigned.SetCertificate(signerCert);
                        MIME_Entity entity_multipart_mixed = new MIME_Entity();
                        MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed();
                        entity_multipart_mixed.Body = multipartMixed;
                        multipartSigned.BodyParts.Add(entity_multipart_mixed);
                            multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Text_Plain(MIME_TransferEncodings.QuotedPrintable,text));

                            // Add attachments.
                            foreach(Mail_t_Attachment attachment in attachments){
                                try{
                                    multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment(attachment.Name,attachment.GetStream()));
                                }
                                finally{
                                    attachment.CloseStream();
                                }
                            }
                }
                // multipart/signed
                //  multipart/mixed
                //   multipart/alternative
                //    text/plain
                //    text/html
                //   application/octet-stream
                //  application/pkcs7-signature  MIME library adds this when message saved out.
                else{
                    MIME_b_MultipartSigned multipartSigned = new MIME_b_MultipartSigned();
                    msg.Body = multipartSigned;
                    multipartSigned.SetCertificate(signerCert);
                        MIME_Entity entity_multipart_mixed = new MIME_Entity();
                        MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed();
                        entity_multipart_mixed.Body = multipartMixed;
                        multipartSigned.BodyParts.Add(entity_multipart_mixed);
                            MIME_Entity entity_multipart_alternative = new MIME_Entity();
                            MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative();
                            entity_multipart_alternative.Body = multipartAlternative;
                            multipartMixed.BodyParts.Add(entity_multipart_alternative);
                                multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Plain(MIME_TransferEncodings.QuotedPrintable,text));
                                multipartAlternative.BodyParts.Add(MIME_Entity.CreateEntity_Text_Html(MIME_TransferEncodings.QuotedPrintable,html));

                            // Add attachments.
                            foreach(Mail_t_Attachment attachment in attachments){
                                try{
                                    multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment(attachment.Name,attachment.GetStream()));
                                }
                                finally{
                                    attachment.CloseStream();
                                }
                            }
                }                
            }

            return msg;
        }

        #endregion

        #region static method ParseFromStream

        /// <summary>
        /// Parses a mail message from the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream from which to parse the mail message. Parsing begins at the
        /// current stream position.
        /// </param>
        /// <param name="persistentStream">
        /// Specifies whether the parsed mail message retains access to the
        /// source stream after parsing. If <see langword="true"/>, the source
        /// stream must remain available for the lifetime of the parsed mail
        /// message. If <see langword="false"/>, the source stream is no longer
        /// required after parsing completes.
        /// </param>
        /// <param name="ownsStream">
        /// Specifies whether the returned mail message takes ownership of the
        /// backing stream and disposes it when the mail message is disposed.
        /// </param>
        /// <returns>
        /// Returns the parsed mail message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the mail message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// This method blocks until the mail message has been completely parsed.
        /// For asynchronous operation, use
        /// <see cref="ParseFromStreamAsync(Stream,bool,bool,CancellationToken)"/>.
        /// </remarks>
        public static new Mail_Message ParseFromStream(Stream stream,bool persistentStream,bool ownsStream)
        {
            return ParseFromStreamAsync(stream,persistentStream,ownsStream,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region static method ParseFromStreamAsync

        /// <summary>
        /// Parses a mail message from the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream from which to parse the mail message. Parsing begins at the
        /// current stream position.
        /// </param>
        /// <param name="persistentStream">
        /// Specifies whether the parsed mail message retains access to the
        /// source stream after parsing. If <see langword="true"/>, the source
        /// stream must remain available for the lifetime of the parsed mail
        /// message. If <see langword="false"/>, the source stream is no longer
        /// required after parsing completes.
        /// </param>
        /// <param name="ownsStream">
        /// Specifies whether the returned mail message takes ownership of the
        /// backing stream and disposes it when the mail message is disposed.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the parsed mail message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the mail message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// When <paramref name="persistentStream"/> is
        /// <see langword="true"/>, the parser uses the supplied stream directly
        /// if it supports seeking. Otherwise, the stream content is copied to an
        /// internal backing store which remains available for the lifetime of
        /// the parsed mail message.
        ///
        /// When <paramref name="ownsStream"/> is
        /// <see langword="true"/>, the returned mail message takes ownership of
        /// the backing stream and disposes it when the mail message is disposed.
        /// </remarks>
        public static new async Task<Mail_Message> ParseFromStreamAsync(Stream stream,bool persistentStream,bool ownsStream,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }
            
            // Stream not persistent or is not seekable, create copy of stream.
            if(!persistentStream ||!stream.CanSeek){
                var ms = new MemoryStreamEx(64000);
                await stream.CopyToAsync(ms,cancellationToken);
                if(ownsStream) {
                    await stream.DisposeAsync();
                }
                stream = ms;
                stream.Position = 0;
                ownsStream = true;
            }

            Mail_Message retVal = new Mail_Message();
            if(ownsStream){
                retVal.m_pStream = stream;
            }
            await retVal.ParseInternalAsync(stream,cancellationToken);

            return retVal;
        }

        #endregion

        #region static method ParseFromFile

        /// <summary>
        /// Parses a mail message from the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File from which to parse the mail message.
        /// </param>
        /// <returns>
        /// Returns the parsed mail message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the mail message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// This method blocks until the mail message has been completely parsed.
        /// For asynchronous operation, use
        /// <see cref="ParseFromFileAsync(string,CancellationToken)"/>.
        /// </remarks>
        public static new Mail_Message ParseFromFile(string fileName)
        {
            return ParseFromFileAsync(fileName,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region static method ParseFromFileAsync

        /// <summary>
        /// Parses a mail message from the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File from which to parse the mail message.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the parsed mail message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the mail message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        public static new async Task<Mail_Message> ParseFromFileAsync(string fileName,CancellationToken cancellationToken = default)
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }
            if(fileName.Length == 0){
                throw new ArgumentException("Argument 'fileName' cannot be empty.",nameof(fileName));
            }

            var stream = File.OpenRead(fileName);

            return await ParseFromStreamAsync(stream,true,true,cancellationToken);
        }

        #endregion

        #region static method ParseFromByteArray

        /// <summary>
        /// Parses a mail message from the specified byte array.
        /// </summary>
        /// <param name="data">
        /// Mail message data.
        /// </param>
        /// <returns>
        /// Returns the parsed mail message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the mail message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// This method blocks until the mail message has been completely parsed.
        /// For asynchronous operation, use
        /// <see cref="ParseFromByteArrayAsync(byte[],CancellationToken)"/>.
        /// </remarks>
        public static new Mail_Message ParseFromByteArray(byte[] data)
        {
            return ParseFromByteArrayAsync(data,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region static method ParseFromByteArrayAsync

        /// <summary>
        /// Parses a mail message from the specified byte array.
        /// </summary>
        /// <param name="data">
        /// Mail message data.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <returns>
        /// Returns the parsed mail message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the mail message contains invalid data that cannot be
        /// parsed.
        /// </exception>
        /// <remarks>
        /// The returned mail message owns the internal stream created for the
        /// specified byte array and releases it when disposed.
        /// </remarks>
        public static new async Task<Mail_Message> ParseFromByteArrayAsync(byte[] data,CancellationToken cancellationToken = default)
        {
            if(data == null){
                throw new ArgumentNullException(nameof(data));
            }

            return await ParseFromStreamAsync(new MemoryStream(data,false),true,true,cancellationToken);
        }

        #endregion


        #region method Clone

        /// <summary>
        /// Creates a copy of this mail message.
        /// </summary>
        /// <returns>
        /// Returns the cloned mail message.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this method is accessed.
        /// </exception>
        public Mail_Message Clone()
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            MemoryStreamEx ms = new MemoryStreamEx(64000);
            this.ToStream(ms);
            ms.Position = 0;
            
            return Mail_Message.ParseFromStream(ms,true,true);
        }

        #endregion

        #region method GetAttachments

        /// <summary>
        /// Gets the attachments contained in this message.
        /// </summary>
        /// <param name="includeInline">
        /// Specifies whether entities with a Content-Disposition value of
        /// <c>inline</c> are included.
        /// </param>
        /// <returns>
        /// Returns the attachments contained in this message.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this method is accessed.
        /// </exception>
        public MIME_Entity[] GetAttachments(bool includeInline)
        {
            return GetAttachments(includeInline,true);
        }

        /// <summary>
        /// Gets the attachments contained in this message.
        /// </summary>
        /// <param name="includeInline">
        /// Specifies whether entities with a Content-Disposition value of
        /// <c>inline</c> are included.
        /// </param>
        /// <param name="includeEmbbedMessage">
        /// Specifies whether attachments contained in embedded
        /// <c>message/rfc822</c> entities are included.
        /// </param>
        /// <returns>
        /// Returns the attachments contained in this message.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this method is accessed.
        /// </exception>
        /// <remarks>
        /// Attachments are identified by the Content-Disposition header field
        /// and, when absent, by the MIME media type.
        /// </remarks>
        public MIME_Entity[] GetAttachments(bool includeInline,bool includeEmbbedMessage)
        {
            if(this.IsDisposed){
                throw new ObjectDisposedException(this.GetType().Name);
            }

            List<MIME_Entity> retVal = new List<MIME_Entity>();
            foreach(MIME_Entity entity in GetAllEntities(includeEmbbedMessage)){
                MIME_h_ContentType? contentType= null;
                try{
                    contentType = entity.ContentType;
                }
                catch{
                    // ContentType parsing failed.
                }
                MIME_h_ContentDisposition? disposition = null;
                try{
                    disposition = entity.ContentDisposition;
                }
                catch{
                    // ContentDisposition parsing failed.
                }

                if(disposition != null && string.Equals(disposition.DispositionType,"attachment",StringComparison.InvariantCultureIgnoreCase)){
                    retVal.Add(entity);
                }
                else if(disposition != null && string.Equals(disposition.DispositionType,"inline",StringComparison.InvariantCultureIgnoreCase)){
                    if(includeInline){
                        retVal.Add(entity);
                    }
                }
                else if(contentType != null && contentType.Type.ToLower() == "application"){
                    retVal.Add(entity);
                }
                else if(contentType != null && contentType.Type.ToLower() == "image"){
                    retVal.Add(entity);
                }
                else if(contentType != null && contentType.Type.ToLower() == "video"){
                    retVal.Add(entity);
                }
                else if(contentType != null && contentType.Type.ToLower() == "audio"){
                    retVal.Add(entity);
                }
                else if(contentType != null && contentType.Type.ToLower() == "message"){
                    retVal.Add(entity);
                }
            }

            return retVal.ToArray();
        }

        #endregion


        #region Properties Implementation

        // Permanent headerds list: http://www.rfc-editor.org/rfc/rfc4021.txt

        /// <summary>
        /// Gets or sets the Date header field.
        /// </summary>
        /// <remarks>
        /// Specifies the date and time when the message was composed and considered
        /// ready for submission to a mail transport system.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.1.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Date header field cannot be parsed.
        /// </exception>
        public DateTime? Date
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Date");
                if(h != null){
                    try{
                        return MIME_Utils.ParseRfc2822DateTime(((MIME_h_Unstructured)h).Value);
                    }
                    catch{
                        throw new ParseException("Header field 'Date' parsing failed.");
                    }
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Date");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Date");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Date",MIME_Utils.DateTimeToRfc2822(value.Value)));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Date",MIME_Utils.DateTimeToRfc2822(value.Value)));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the From header field.
        /// </summary>
        /// <remarks>
        /// Specifies the author or authors of the message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.2.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the From header field cannot be parsed.
        /// </exception>
        public Mail_t_MailboxList? From
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("From");
                if(h != null){
                    if(!(h is Mail_h_MailboxList)){
                        throw new ParseException("Header field 'From' parsing failed.");
                    }

                    return ((Mail_h_MailboxList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("From");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("From");
                    if(h == null){
                        this.Header.Add(new Mail_h_MailboxList("From",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_MailboxList("From",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Sender header field.
        /// </summary>
        /// <remarks>
        /// Specifies the mailbox responsible for transmitting the message.
        /// This header is typically used when the author specified by the
        /// <see cref="From"/> header differs from the actual sender.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.2.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Sender header field cannot be parsed.
        /// </exception>
        public Mail_t_Mailbox? Sender
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Sender");
                if(h != null){
                    if(!(h is Mail_h_Mailbox)){
                        throw new ParseException("Header field 'Sender' parsing failed.");
                    }

                    return ((Mail_h_Mailbox)h).Address;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Sender");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Sender");
                    if(h == null){
                        this.Header.Add(new Mail_h_Mailbox("Sender",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_Mailbox("Sender",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Reply-To header field.
        /// </summary>
        /// <remarks>
        /// Specifies the address or addresses to which replies to the message
        /// should be sent.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.2.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Reply-To header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? ReplyTo
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Reply-To");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'Reply-To' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Reply-To");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Reply-To");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("Reply-To",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("Reply-To",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the To header field.
        /// </summary>
        /// <remarks>
        /// Specifies the primary recipient or recipients of the message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.3.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the To header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? To
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("To");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'To' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("To");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("To");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("To",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("To",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Cc header field.
        /// </summary>
        /// <remarks>
        /// Specifies recipient or recipients who receive a copy of the message,
        /// but are not primary recipients.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.3.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Cc header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? Cc
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Cc");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'Cc' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Cc");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Cc");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("Cc",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("Cc",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Bcc header field.
        /// </summary>
        /// <remarks>
        /// Specifies recipient or recipients whose addresses are not disclosed
        /// to other recipients of the message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.3.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Bcc header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? Bcc
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Bcc");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'Bcc' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Bcc");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Bcc");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("Bcc",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("Bcc",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Message-ID header field.
        /// </summary>
        /// <remarks>
        /// Specifies a globally unique identifier for the message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.4.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? MessageID 
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Message-ID");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Message-ID");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Message-ID");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Message-ID",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Message-ID",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the In-Reply-To header field.
        /// </summary>
        /// <remarks>
        /// Specifies the message identifier or identifiers of the message being replied to.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.4.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? InReplyTo
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("In-Reply-To");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("In-Reply-To");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("In-Reply-To");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("In-Reply-To",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("In-Reply-To",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the References header field.
        /// </summary>
        /// <remarks>
        /// Specifies the message identifier or identifiers of related messages.
        /// This header is typically used to maintain message threads by containing
        /// the Message-ID values of preceding messages in the conversation.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.4.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? References
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("References");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("References");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("References");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("References",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("References",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Subject header field.
        /// </summary>
        /// <remarks>
        /// Specifies the topic of the message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.5.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? Subject
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Subject");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Subject");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Subject");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Subject",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Subject",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Comments header field.
        /// </summary>
        /// <remarks>
        /// Contains additional comments about the message.
        /// Returns <c>null</c> if the header field is not present.
        /// Note that some mail clients may not display this header to recipients.
        /// Defined in RFC 5322 section 3.6.5.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? Comments
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Comments");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Comments");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Comments");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Comments",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Comments",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Keywords header field.
        /// </summary>
        /// <remarks>
        /// Contains keywords or phrases associated with the message.
        /// Returns <c>null</c> if the header field is not present.
        /// The value typically contains a comma-separated list of keywords.
        /// Defined in RFC 5322 section 3.6.5.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? Keywords
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Keywords");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Keywords");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Keywords");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Keywords",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Keywords",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-Date header field.
        /// </summary>
        /// <remarks>
        /// Specifies the date and time when the message was reintroduced into the
        /// mail transport system.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.6.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Resent-Date header field cannot be parsed.
        /// </exception>
        public DateTime? ResentDate
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-Date");
                if(h != null){
                    try{
                        return MIME_Utils.ParseRfc2822DateTime(((MIME_h_Unstructured)h).Value);
                    }
                    catch{
                        throw new ParseException("Header field 'Resent-Date' parsing failed.");
                    }
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-Date");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-Date");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Resent-Date",MIME_Utils.DateTimeToRfc2822(value.Value)));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Resent-Date",MIME_Utils.DateTimeToRfc2822(value.Value)));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-From header field.
        /// </summary>
        /// <remarks>
        /// Specifies the mailbox or mailboxes of the person or system responsible
        /// for resending the message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.6.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Resent-From header field cannot be parsed.
        /// </exception>
        public Mail_t_MailboxList? ResentFrom
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-From");
                if(h != null){
                    if(!(h is Mail_h_MailboxList)){
                        throw new ParseException("Header field 'Resent-From' parsing failed.");
                    }

                    return ((Mail_h_MailboxList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-From");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-From");
                    if(h == null){
                        this.Header.Add(new Mail_h_MailboxList("Resent-From",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_MailboxList("Resent-From",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-Sender header field.
        /// </summary>
        /// <remarks>
        /// Specifies the mailbox responsible for resending the message.
        /// This header is typically used when the sender specified by the
        /// <see cref="ResentFrom"/> header differs from the actual sender.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.6.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Resent-Sender header field cannot be parsed.
        /// </exception>
        public Mail_t_Mailbox? ResentSender
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-Sender");
                if(h != null){
                    if(!(h is Mail_h_Mailbox)){
                        throw new ParseException("Header field 'Resent-Sender' parsing failed.");
                    }

                    return ((Mail_h_Mailbox)h).Address;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-Sender");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-Sender");
                    if(h == null){
                        this.Header.Add(new Mail_h_Mailbox("Resent-Sender",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_Mailbox("Resent-Sender",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-To header field.
        /// </summary>
        /// <remarks>
        /// Specifies the primary recipient or recipients of the resent message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.6.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Resent-To header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? ResentTo
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-To");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'Resent-To' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-To");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-To");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("Resent-To",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("Resent-To",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-Cc header field.
        /// </summary>
        /// <remarks>
        /// Specifies recipient or recipients who receive a copy of the resent message,
        /// but are not primary recipients.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.6.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Resent-Cc header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? ResentCc
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-Cc");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'Resent-Cc' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-Cc");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-Cc");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("Resent-Cc",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("Resent-Cc",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-Bcc header field.
        /// </summary>
        /// <remarks>
        /// Specifies recipient or recipients of the resent message whose addresses
        /// are not disclosed to other recipients.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.6.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Resent-Bcc header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? ResentBcc
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-Bcc");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'Resent-Bcc' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-Bcc");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-Bcc");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("Resent-Bcc",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("Resent-Bcc",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-Reply-To header field.
        /// </summary>
        /// <remarks>
        /// Specifies the address or addresses to which replies to the resent
        /// message should be sent.
        /// Returns <c>null</c> if the header field is not present.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Resent-Reply-To header field cannot be parsed.
        /// </exception>
        public Mail_t_AddressList? ResentReplyTo
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-Reply-To");
                if(h != null){
                    if(!(h is Mail_h_AddressList)){
                        throw new ParseException("Header field 'Resent-Reply-To' parsing failed.");
                    }

                    return ((Mail_h_AddressList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-Reply-To");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-Reply-To");
                    if(h == null){
                        this.Header.Add(new Mail_h_AddressList("Resent-Reply-To",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_AddressList("Resent-Reply-To",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Resent-Message-ID header field.
        /// </summary>
        /// <remarks>
        /// Specifies a globally unique identifier for the resent message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 5322 section 3.6.6.
        /// </remarks>
        public string? ResentMessageID
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Resent-Message-ID");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Resent-Message-ID");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Resent-Message-ID");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Resent-Message-ID",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Resent-Message-ID",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Return-Path header field.
        /// </summary>
        /// <remarks>
        /// Specifies the address to which non-delivery reports and other delivery
        /// status notifications should be sent.
        /// Returns <c>null</c> if the header field is not present.
        /// This header is normally added by the final delivery system rather than
        /// by the message author.
        /// Defined in RFC 5321 and RFC 5322.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Return-Path header field cannot be parsed.
        /// </exception>
        public Mail_h_ReturnPath? ReturnPath
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Return-Path");
                if(h != null){
                    if(!(h is Mail_h_ReturnPath)){
                        throw new ParseException("Header field 'Return-Path' parsing failed.");
                    }

                    return (Mail_h_ReturnPath)h;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Return-Path");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Return-Path");
                    if(h == null){
                        this.Header.Add(value);
                    }
                    else{
                        this.Header.ReplaceFirst(value);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the Received header fields.
        /// </summary>
        /// <remarks>
        /// Contains mail transfer trace information recorded by mail transfer
        /// agents that handled the message.
        /// Returns an empty array if no Received header fields are present.
        /// Defined in RFC 5322 section 3.6.7.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when a Received header field cannot be parsed.
        /// </exception>
        public Mail_h_Received[]? Received
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h[] h = this.Header["Received"];
                if(h != null){
                    List<Mail_h_Received> retVal = new List<Mail_h_Received>();
                    for(int i=0;i<h.Length;i++){
                        if(!(h[i] is Mail_h_Received)){
                            throw new ParseException("Header field 'Received' parsing failed.");
                        }

                        retVal.Add((Mail_h_Received)h[i]);
                    }

                    return retVal.ToArray();
                }
                else{
                    return null;
                }
            }
        }

        // Obsoleted by RFC 2822.
        // public string Encypted

        /// <summary>
        /// Gets or sets the Disposition-Notification-To header field.
        /// </summary>
        /// <remarks>
        /// Specifies the mailbox or mailboxes to which message disposition
        /// notifications (MDNs) should be sent.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 3798 section 2.1.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        /// <exception cref="ParseException">
        /// Is raised when the Disposition-Notification-To header field cannot be parsed.
        /// </exception>
        public Mail_t_MailboxList? DispositionNotificationTo
        {
            // Defined RFC 3798 2.1.

            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Disposition-Notification-To");
                if(h != null){
                    if(!(h is Mail_h_MailboxList)){
                        throw new ParseException("Header field 'From' parsing failed.");
                    }

                    return ((Mail_h_MailboxList)h).Addresses;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Disposition-Notification-To");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Disposition-Notification-To");
                    if(h == null){
                        this.Header.Add(new Mail_h_MailboxList("Disposition-Notification-To",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new Mail_h_MailboxList("Disposition-Notification-To",value));
                    }
                }
            }
        }

        // DispositionNotificationOptions

        /// <summary>
        /// Gets or sets the Accept-Language header field.
        /// </summary>
        /// <remarks>
        /// Specifies the preferred language or languages for replies to the message.
        /// Returns <c>null</c> if the header field is not present.
        /// The value typically contains one or more language tags, such as
        /// <c>en</c>, <c>en-US</c>, or <c>et</c>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? AcceptLanguage
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Accept-Language");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Accept-Language");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Accept-Language");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Accept-Language",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Accept-Language",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Original-Message-ID header field.
        /// </summary>
        /// <remarks>
        /// Specifies the Message-ID of the original message associated with this
        /// message.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 3798.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? OriginalMessageID
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Original-Message-ID");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Original-Message-ID");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Original-Message-ID");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Original-Message-ID",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Original-Message-ID",value));
                    }
                }
            }
        }

        // Bot used in modern email.
        // PICSLabel
        
        // Not widely used
        // public string Encoding

        /// <summary>
        /// Gets or sets the List-Archive header field.
        /// </summary>
        /// <remarks>
        /// Specifies a URL for accessing the mailing list archives.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2369.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ListArchive
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("List-Archive");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("List-Archive");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("List-Archive");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("List-Archive",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("List-Archive",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the List-Help header field.
        /// </summary>
        /// <remarks>
        /// Specifies a URL or contact information for obtaining help with the
        /// mailing list.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2369.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ListHelp
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("List-Help");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("List-Help");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("List-Help");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("List-Help",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("List-Help",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the List-ID header field.
        /// </summary>
        /// <remarks>
        /// Specifies a unique identifier for the mailing list through which the
        /// message was distributed.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2919.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ListID
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("List-ID");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("List-ID");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("List-ID");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("List-ID",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("List-ID",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the List-Owner header field.
        /// </summary>
        /// <remarks>
        /// Specifies contact information for the owner of the mailing list.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2369.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ListOwner
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("List-Owner");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("List-Owner");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("List-Owner");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("List-Owner",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("List-Owner",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the List-Post header field.
        /// </summary>
        /// <remarks>
        /// Specifies how messages may be posted to the mailing list.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2369.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ListPost
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("List-Post");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("List-Post");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("List-Post");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("List-Post",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("List-Post",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the List-Subscribe header field.
        /// </summary>
        /// <remarks>
        /// Specifies how to subscribe to the mailing list.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2369.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ListSubscribe
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("List-Subscribe");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("List-Subscribe");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("List-Subscribe");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("List-Subscribe",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("List-Subscribe",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the List-Unsubscribe header field.
        /// </summary>
        /// <remarks>
        /// Specifies how to unsubscribe from the mailing list.
        /// Returns <c>null</c> if the header field is not present.
        /// Defined in RFC 2369.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? ListUnsubscribe
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("List-Unsubscribe");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("List-Unsubscribe");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("List-Unsubscribe");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("List-Unsubscribe",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("List-Unsubscribe",value));
                    }
                }
            }
        }

        // Obsolete
        // MessageContext

        // Not for general use
        //public string AlternateRecipient

        // Obsolete
        //public string ContentReturn

        // Not for general use
        // public string GenerateDeliveryReport

        // Not for general use
        // public string PreventNonDeliveryReport

        // Obsolete
        // public string ContentIdentifier

        // Not for general use
        // public string DeliveryDate

        // Obsolete
        // public string ExpiryDate

        // Not for general use
        // public string Expires

        // Not for general use
        // public string ReplyBy

        /// <summary>
        /// Gets or sets the Importance header field.
        /// </summary>
        /// <remarks>
        /// Specifies the importance of the message.
        /// Common values are <c>high</c>, <c>normal</c>, and <c>low</c>.
        /// Returns <c>null</c> if the header field is not present.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? Importance
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Importance");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Importance");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Importance");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Importance",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Importance",value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the Priority header field.
        /// </summary>
        /// <remarks>
        /// Specifies the delivery priority of the message.
        /// Common values are <c>urgent</c>, <c>normal</c>, and <c>non-urgent</c>.
        /// Returns <c>null</c> if the header field is not present.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? Priority
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                MIME_h? h = this.Header.GetFirst("Priority");
                if(h != null){
                    return ((MIME_h_Unstructured)h).Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }
                
                if(value == null){
                    this.Header.RemoveAll("Priority");
                }
                else{
                    MIME_h? h = this.Header.GetFirst("Priority");
                    if(h == null){
                        this.Header.Add(new MIME_h_Unstructured("Priority",value));
                    }
                    else{
                        this.Header.ReplaceFirst(new MIME_h_Unstructured("Priority",value));
                    }
                }
            }
        }

        // Not for general use
        // public string Sensitivity

        // Not for general use
        // public string Language

        // Not for general use
        // public string MessageType

        // Not for general use
        // public string Autosubmitted

        // Not for general use
        // public string Autoforwarded

        // Not for general use
        // public string DiscloseRecipients

        // Not for general use
        // public string DeferredDelivery

        // Not for general use
        // public string LatestDeliveryTime

        // Not for general use
        // public string OriginatorReturnAddress


        /// <summary>
        /// Gets the attachments contained in this message.
        /// </summary>
        /// <remarks>
        /// Returns an array containing all message attachments.
        /// Returns an empty array if the message contains no attachments.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public MIME_Entity[] Attachments
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                return GetAttachments(false);
            }
        }

        /// <summary>
        /// Gets the character encoding of the message body text.
        /// </summary>
        /// <remarks>
        /// Returns the character encoding specified for the first
        /// <c>text/plain</c> body part.
        /// Returns <c>null</c> if the message does not contain a
        /// <c>text/plain</c> body part.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public Encoding? BodyTextEncoding
        {
            get
            {
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                foreach(MIME_Entity e in GetAllEntities(false)){
                    if(e.Body != null && string.Equals(e.Body.MediaType,MIME_MediaTypes.Text.plain,StringComparison.OrdinalIgnoreCase)){
                        return ((MIME_b_Text)e.Body).GetCharset();
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the message body text.
        /// </summary>
        /// <remarks>
        /// Returns the text of the first <c>text/plain</c> body part.
        /// Returns <c>null</c> if the message does not contain a
        /// <c>text/plain</c> body part.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? BodyText
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                foreach(MIME_Entity e in GetAllEntities(false)){
                    if(e.Body != null && string.Equals(e.Body.MediaType,MIME_MediaTypes.Text.plain,StringComparison.OrdinalIgnoreCase)){
                        return ((MIME_b_Text)e.Body).Text;
                    }
                }

                return null; 
            }
        }

        /// <summary>
        /// Gets the character encoding of the message HTML body.
        /// </summary>
        /// <remarks>
        /// Returns the character encoding specified for the first
        /// <c>text/html</c> body part.
        /// Returns <c>null</c> if the message does not contain a
        /// <c>text/html</c> body part.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public Encoding? BodyHtmlTextEncoding
        {
            get{
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                foreach(MIME_Entity e in GetAllEntities(false)){
                    if(e.Body != null && string.Equals(e.Body.MediaType,MIME_MediaTypes.Text.html,StringComparison.OrdinalIgnoreCase)){
                        return ((MIME_b_Text)e.Body).GetCharset();
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the HTML body text of the message.
        /// </summary>
        /// <remarks>
        /// Returns the text of the first <c>text/html</c> body part.
        /// Returns <c>null</c> if the message does not contain a
        /// <c>text/html</c> body part.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Is raised when this object is disposed and this property is accessed.
        /// </exception>
        public string? BodyHtmlText
        {
            get{ 
                if(this.IsDisposed){
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                foreach(MIME_Entity e in GetAllEntities(false)){
                    if(e.Body != null && string.Equals(e.Body.MediaType,MIME_MediaTypes.Text.html,StringComparison.OrdinalIgnoreCase)){
                        return ((MIME_b_Text)e.Body).Text;
                    }
                }

                return null; 
            }
        }

        #endregion

    }
}
