using System.Net.Mail;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using LumiSoft.Net.MIME;
using LumiSoft.Net.Mail;

namespace Mail_Message_Console 
{
    internal class Program 
    {
        static void Main(string[] args)
        {
            try{               
                DateTime startTime = DateTime.Now;
                                
                //------- Simple way ------------------------------------------------------------------------------------                
                /*
                // Create simple message.
                Mail_Message mail = Mail_Message.Create(
                    new Mail_t_Mailbox(null,"from@test.xxx"),
                    new Mail_t_Address[]{new Mail_t_Mailbox(null,"to@test.xxx")},
                    null,
                    null,
                    "This is subject text.",
                    "This is message body text.",
                    "<html><b>This is html body text.</b></html>",
                    new Mail_t_Attachment[]{new Mail_t_Attachment("attachment.txt")}
                );
                
                */
                /*
                // Create signed message.
                // X509Certificate2 testCert = ... NOTE: Load your certificate here.
                Mail_Message mail = Mail_Message.Create_MultipartSigned(
                    testCert,
                    new Mail_t_Mailbox(null,"from@test.xxx"),
                    new Mail_t_Address[]{new Mail_t_Mailbox(null,"to@test.xxx")},
                    null,
                    null,
                    "This is subject text.",
                    "This is message body text.",
                    "<html><b>This is html body text.</b></html>",
                    new Mail_t_Attachment[]{new Mail_t_Attachment("attachment.txt")}
                );*/
                

                //------- Long way --------------------------------------------------------------------------------------

                // Just call some example method here.
                // For example:
                Mail_Message mail = Create_PlainText();
                //Mail_Message mail = Create_MultipartSigned(testCert);
                //bool isValid = VerifySignatures(Create_SignedText(testCert));
                //ConvertToMultipartSigned(testCert,mail);
                // ...

                // Use any of these methods to serialize mail message.
                // mail.ToByte();
                // mail.ToFile();
                // mail.ToStream();

                Console.WriteLine(mail.ToString());
            }
            catch(Exception x){
                Console.WriteLine(x.ToString());
            }

            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }


        #region static method Create_PlainText

        private static Mail_Message Create_PlainText()
        {
            /*
             * Create plain text mail message.
             * 
             * Message
             * text/plain
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
            msg.Body = text_plain;
            text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");

            return msg;
        }

        #endregion

        #region static method Create_Html

        private static Mail_Message Create_Html()
        {
            /*
             * Create html mail message.
             * 
             * Message
             * text/html
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
            msg.Body = text_html;
            text_html.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"<html><b>This is hatml body text.</b></html>");
                        
            return msg;
        }

        #endregion

        #region static method Create_PlainText_Html

        private static Mail_Message Create_PlainText_Html()
        {
            /*
             * Create plain text and html alternative mail message.
             * 
             * Message
             * multipart/alternative
             *   text/plain
             *   text/html
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            //--- multipart/alternative -----------------------------------------------------------------------------------------
            MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
            contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative(contentType_multipartAlternative);
            msg.Body = multipartAlternative;

                //--- text/plain ----------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_plain = new MIME_Entity();
                MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                entity_text_plain.Body = text_plain;
                text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");
                multipartAlternative.BodyParts.Add(entity_text_plain);

                //--- text/html ------------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_html = new MIME_Entity();
                MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                entity_text_html.Body = text_html;
                text_html.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"<html><b>This is html body text.</b></html>");
                multipartAlternative.BodyParts.Add(entity_text_html);
                                                
            return msg;
        }

        #endregion


        #region static method Create_AttachmentOnly

        private static Mail_Message Create_AttachmentOnly()
        {
            /*
             * Create single attachemnt only(no text at all) mail message.
             * 
             * Message
             * application/octet-stream
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";
            
            MIME_b_Application application_octetStream = new MIME_b_Application(MIME_MediaTypes.Application.octet_stream);
            application_octetStream.SetDataFromFile("attachment.txt",MIME_TransferEncodings.Base64);
            msg.Body = application_octetStream;
            msg.ContentDisposition = new MIME_h_ContentDisposition(MIME_DispositionTypes.Attachment);
                        
            return msg;
        }

        #endregion

        #region static method Create_PlainText_Attachment

        private static Mail_Message Create_PlainText_Attachment()
        {
            /*
             * Create plain text and single attachment mail message.
             * 
             * Message
             * multipart/mixed
             *   text/plain
             *   application/octet-stream
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            //--- multipart/mixed -------------------------------------------------------------------------------------------------
            MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
            contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed(contentType_multipartMixed);
            msg.Body = multipartMixed;

                //--- text/plain ---------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_plain = new MIME_Entity();
                MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                entity_text_plain.Body = text_plain;
                text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");
                multipartMixed.BodyParts.Add(entity_text_plain);

                //--- application/octet-stream --------------------------------------------------------------------------------------
                multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment.txt"));

            return msg;
        }

        #endregion

        #region static method Create_PlainText_Html_Attachment

        private static Mail_Message Create_PlainText_Html_Attachment()
        {
            /*
             * Create plain text,html alternative and single attachment mail message.
             * 
             * Message
             * multipart/mixed
             *   multipart/alternative
             *     text/plain
             *     text/html
             *   application/octet-stream
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            //--- multipart/mixed -------------------------------------------------------------------------------------------------
            MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
            contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed(contentType_multipartMixed);
            msg.Body = multipartMixed;

                //--- multipart/alternative -----------------------------------------------------------------------------------------
                MIME_Entity entity_multipartAlternative = new MIME_Entity();
                MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
                contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
                MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative(contentType_multipartAlternative);
                entity_multipartAlternative.Body = multipartAlternative;
                multipartMixed.BodyParts.Add(entity_multipartAlternative);

                    //--- text/plain ----------------------------------------------------------------------------------------------------
                    MIME_Entity entity_text_plain = new MIME_Entity();
                    MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                    entity_text_plain.Body = text_plain;
                    text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");
                    multipartAlternative.BodyParts.Add(entity_text_plain);

                    //--- text/html ------------------------------------------------------------------------------------------------------
                    MIME_Entity entity_text_html = new MIME_Entity();
                    MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                    entity_text_html.Body = text_html;
                    text_html.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"<html><b>This is hatml body text.</b></html>");
                    multipartAlternative.BodyParts.Add(entity_text_html);
            
            //--- application/octet-stream -----------------------------------------------------------------------------------------------
            multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment.txt"));
                                                
            return msg;
        }

        #endregion


        #region static method Create_2Attachments

        private static Mail_Message Create_2Attachments()
        {
            /*
             * Create 2 attachments only mail message.
             * 
             * Message
             * multipart/mixed
             *   application/octet-stream
             *   application/octet-stream
            */


            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            //--- multipart/mixed -------------------------------------------------------------------------------------------------
            MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
            contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed(contentType_multipartMixed);
            msg.Body = multipartMixed;

                //--- application/octet-stream --------------------------------------------------------------------------------------
                multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment.txt"));

                //--- application/octet-stream --------------------------------------------------------------------------------------
                multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment2.txt"));

            return msg;
        }

        #endregion

        #region static method Create_PlainText_2Attachments

        private static Mail_Message Create_PlainText_2Attachments()
        {
            /*
             * Create plain text and 2 attachments mail message.
             * 
             * Message
             * multipart/mixed
             *   text/plain
             *   application/octet-stream
             *   application/octet-stream
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            //--- multipart/mixed -------------------------------------------------------------------------------------------------
            MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
            contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed(contentType_multipartMixed);
            msg.Body = multipartMixed;

                //--- text/plain ---------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_plain = new MIME_Entity();
                MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                entity_text_plain.Body = text_plain;
                text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");
                multipartMixed.BodyParts.Add(entity_text_plain);

                //--- application/octet-stream --------------------------------------------------------------------------------------
                multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment.txt"));

                //--- application/octet-stream --------------------------------------------------------------------------------------
                multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment2.txt"));

            return msg;
        }

        #endregion

        #region static method Create_PlainText_Html_2Attachment

        private static Mail_Message Create_PlainText_Html_2Attachment()
        {
            /*
             * Create plain text,html alternative and single attachment mail message.
             * 
             * Message
             * multipart/mixed
             *   multipart/alternative
             *     text/plain
             *     text/html
             *   application/octet-stream
             *   application/octet-stream
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            //--- multipart/mixed -------------------------------------------------------------------------------------------------
            MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
            contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed(contentType_multipartMixed);
            msg.Body = multipartMixed;

                //--- multipart/alternative -----------------------------------------------------------------------------------------
                MIME_Entity entity_multipartAlternative = new MIME_Entity();
                MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
                contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
                MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative(contentType_multipartAlternative);
                entity_multipartAlternative.Body = multipartAlternative;
                multipartMixed.BodyParts.Add(entity_multipartAlternative);

                    //--- text/plain ----------------------------------------------------------------------------------------------------
                    MIME_Entity entity_text_plain = new MIME_Entity();
                    MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                    entity_text_plain.Body = text_plain;
                    text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");
                    multipartAlternative.BodyParts.Add(entity_text_plain);

                    //--- text/html ------------------------------------------------------------------------------------------------------
                    MIME_Entity entity_text_html = new MIME_Entity();
                    MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                    entity_text_html.Body = text_html;
                    text_html.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"<html><b>This is hatml body text.</b></html>");
                    multipartAlternative.BodyParts.Add(entity_text_html);
            
            //--- application/octet-stream -----------------------------------------------------------------------------------------------
            multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment.txt"));

            //--- application/octet-stream -----------------------------------------------------------------------------------------------
            multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment2.txt"));
                                                
            return msg;
        }

        #endregion


        #region static method Create_DSN

        /// <summary>
        /// Creates delivery status notification(DSN) message. For more info see RFC 3464.
        /// </summary>
        /// <returns>Returns created message.</returns>
        private static Mail_Message Create_DSN()
        {
            Mail_Message message = Create_PlainText();

            Mail_Message dsnMsg = new Mail_Message();
            dsnMsg.MimeVersion = "1.0";
            dsnMsg.Date = DateTime.Now;
            dsnMsg.From = new Mail_t_MailboxList();
            dsnMsg.From.Add(new Mail_t_Mailbox("Mail Delivery Subsystem","postmaster@local"));
            dsnMsg.To = new Mail_t_AddressList();
            dsnMsg.To.Add(new Mail_t_Mailbox(null,"to@domain.com"));
            dsnMsg.Subject = "DSN message";

            //--- multipart/report -------------------------------------------------------------------------------------------------
            MIME_h_ContentType contentType_multipartReport = new MIME_h_ContentType(MIME_MediaTypes.Multipart.report);            
            contentType_multipartReport.Parameters["report-type"] = "delivery-status";
            contentType_multipartReport.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
            MIME_b_MultipartReport multipartReport = new MIME_b_MultipartReport(contentType_multipartReport);
            dsnMsg.Body = multipartReport;

                //--- multipart/alternative -----------------------------------------------------------------------------------------
                MIME_Entity entity_multipart_alternative = new MIME_Entity();
                MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
                contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
                MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative(contentType_multipartAlternative);
                entity_multipart_alternative.Body = multipartAlternative;
                multipartReport.BodyParts.Add(entity_multipart_alternative);

                    //--- text/plain ---------------------------------------------------------------------------------------------------
                    MIME_Entity entity_text_plain = new MIME_Entity();
                    MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                    entity_text_plain.Body = text_plain;
                    text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is DSN message.");
                    multipartAlternative.BodyParts.Add(entity_text_plain);

                    //--- text/html -----------------------------------------------------------------------------------------------------
                    MIME_Entity entity_text_html = new MIME_Entity();
                    MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                    entity_text_html.Body = text_html;
                    text_html.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"<html>This is DSN message.</html>");
                    multipartAlternative.BodyParts.Add(entity_text_html);

                //--- message/delivery-status
                MIME_Entity entity_message_deliveryStatus = new MIME_Entity();
                MIME_b_MessageDeliveryStatus body_message_deliveryStatus = new MIME_b_MessageDeliveryStatus();
                entity_message_deliveryStatus.Body = body_message_deliveryStatus;
            
                //--- per-message-fields ----------------------------------------------------------------------------
                MIME_h_Collection messageFields = body_message_deliveryStatus.MessageFields;
                messageFields.Add(new MIME_h_Unstructured("Original-Envelope-Id","envelopid"));                
                messageFields.Add(new MIME_h_Unstructured("Arrival-Date",MIME_Utils.DateTimeToRfc2822(DateTime.Now)));
                messageFields.Add(new MIME_h_Unstructured("Received-From-MTA","dns;fromserver.com"));                
                messageFields.Add(new MIME_h_Unstructured("Reporting-MTA","dns;reportingserver.com"));
                //---------------------------------------------------------------------------------------------------

                //--- per-recipient-fields --------------------------------------------------------------------------
                MIME_h_Collection recipientFields = new MIME_h_Collection(new MIME_h_Provider());
                recipientFields.Add(new MIME_h_Unstructured("Original-Recipient","rfc822;originalRecipient@domain.com"));
                recipientFields.Add(new MIME_h_Unstructured("Final-Recipient","rfc822;finalRecipient@domain.com"));
                recipientFields.Add(new MIME_h_Unstructured("Action","failed"));
                recipientFields.Add(new MIME_h_Unstructured("Status","5.0.0"));
                recipientFields.Add(new MIME_h_Unstructured("Diagnostic-Code","550 user doesn't exist.")); 
                recipientFields.Add(new MIME_h_Unstructured("Remote-MTA","dns;errorsendermta.com"));
                recipientFields.Add(new MIME_h_Unstructured("Last-Attempt-Date",MIME_Utils.DateTimeToRfc2822(DateTime.Now)));
                recipientFields.Add(new MIME_h_Unstructured("Will-Retry-Until",MIME_Utils.DateTimeToRfc2822(DateTime.Now)));
                body_message_deliveryStatus.RecipientBlocks.Add(recipientFields);

                // ... There may be more report blocks.

                //---------------------------------------------------------------------------------------------------

                multipartReport.BodyParts.Add(entity_message_deliveryStatus);

                //--- message/rfc822
                if(message != null){
                    MIME_Entity entity_message_rfc822 = new MIME_Entity();
                    MIME_b_MessageRfc822 body_message_rfc822 = new MIME_b_MessageRfc822();
                    entity_message_rfc822.Body = body_message_rfc822;
                    body_message_rfc822.Message = message;                        
                    multipartReport.BodyParts.Add(entity_message_rfc822);
                }

            return dsnMsg;
        }

        #endregion


        #region static method Create_HtmlWithImages

        /// <summary>
        /// Creates simple html message with 1 inline image.
        /// </summary>
        /// <returns>Returns created message.</returns>
        private static Mail_Message Create_HtmlWithImages()
        {
            /* The general idea of html embed items are that all content is in multipart/related and
             * each inline item in html can be referenced by cid:.
             * 
             * multipart/related
             *   text/html
             *   image1
             *     Content-ID(cid: in html)
            */

            string imageCID = "dnfjfbfbfafnf";

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            MIME_h_ContentType contentType = new MIME_h_ContentType(MIME_MediaTypes.Multipart.related);
            contentType.Param_Boundary = Guid.NewGuid().ToString().Replace("-","_");
            MIME_b_MultipartRelated entity_related = new MIME_b_MultipartRelated(contentType);
            msg.Body = entity_related;

                string htmlText = "<html>This is html text body and below is embed image.<p/><img src=\"cid:" + imageCID + "\" /></html>";

                MIME_Entity  entity_text_html = new MIME_Entity();
                MIME_b_Text body_text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                entity_text_html.Body = body_text_html;
                body_text_html.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,htmlText);
                entity_related.BodyParts.Add(entity_text_html);

                MIME_Entity  entity_image = new MIME_Entity();
                entity_image.ContentDisposition = new MIME_h_ContentDisposition(MIME_DispositionTypes.Inline);
                entity_image.ContentID = imageCID;
                MIME_b_Image body_image = new MIME_b_Image(MIME_MediaTypes.Image.jpeg);
                entity_image.Body = body_image;
                body_image.SetDataFromFile("image.jpg",MIME_TransferEncodings.Base64);
                entity_related.BodyParts.Add(entity_image);
                        
            return msg;
        }

        #endregion


        #region static method Create_MultipartSigned

        /// <summary>
        /// Creates multipart signed message.
        /// </summary>
        /// <param name="signerCert">Signer certificate.</param>
        /// <returns>Returns created message.</returns>
        private static Mail_Message Create_MultipartSigned(X509Certificate2 signerCert)
        {
            /*
             * Create multipart/signed mail message.
             * 
             * NOTE: mulipart/signed has always 2 child entities. 1 For content(multipart or singlepart), 2 Signature entity.
             * 
             * Message
             * multipart/signed
             *  multipart/mixed - If you have only 1 entity, for example text/plain, you can use it here instead of multipart/mixed.
             *   text/plain
             *   application/octet-stream
             *  application/pkcs7-signature      MIME library adds this.
            */

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            //--- multipart/signed -------------------------------------------------------------------------------------------------
            MIME_b_MultipartSigned multipartSigned = new MIME_b_MultipartSigned();
            msg.Body = multipartSigned;
            // Set signing certificate
            multipartSigned.SetCertificate(signerCert);

                //--- multipart/mixed -------------------------------------------------------------------------------------------------
                MIME_Entity entity_multipart_mixed = new MIME_Entity();
                MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
                contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-','.');
                MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed(contentType_multipartMixed);
                entity_multipart_mixed.Body = multipartMixed;
                multipartSigned.BodyParts.Add(entity_multipart_mixed); 

                    //--- text/plain ---------------------------------------------------------------------------------------------------
                    MIME_Entity entity_text_plain = new MIME_Entity();
                    MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                    entity_text_plain.Body = text_plain;
                    text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");
                    multipartMixed.BodyParts.Add(entity_text_plain);

                    //--- application/octet-stream --------------------------------------------------------------------------------------
                    multipartMixed.BodyParts.Add(MIME_Entity.CreateEntity_Attachment("attachment.txt"));

            //--- application/pkcs7-signature      MIME library adds this.----------------------------------------------------------------
            
            return msg;
        }

        #endregion

        #region static method Create_SignedText

        /// <summary>
        /// Creates simple signed message with 1 signed text entity.
        /// </summary>
        /// <returns>Returns created message.</returns>
        /// <param name="cert">Signer certificate.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>cert</b> is null reference.</exception>
        private static Mail_Message Create_SignedText(X509Certificate2 cert)
        {
            if(cert == null){
                throw new ArgumentNullException("cert");
            }

            Mail_Message msg = new Mail_Message();
            msg.MimeVersion = "1.0";
            msg.MessageID = MIME_Utils.CreateMessageID();
            msg.Date = DateTime.Now;
            msg.From = new Mail_t_MailboxList();
            msg.From.Add(new Mail_t_Mailbox("john doe","from@domain.com"));
            msg.To = new Mail_t_AddressList();
            msg.To.Add(new Mail_t_Mailbox("john doe","to@domain.com"));
            msg.Subject = "This is subject text.";

            // Create text enty for signing.
            MIME_Entity entity_text_plain = new MIME_Entity();
            MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
            entity_text_plain.Body = text_plain;
            text_plain.SetText(MIME_TransferEncodings.QuotedPrintable,Encoding.UTF8,"This is mail message plain body text.");
            
            // Sign text entity and store to msg.
            Create_ApplicationPkcs7Mime_SignedData(msg,entity_text_plain,cert);

            return msg;
        }

        #endregion




        #region static method SaveAttachement

        /// <summary>
        /// Saves attachment out of the message.
        /// </summary>
        /// <param name="fileName">File name with optiona path.</param>
        private static void SaveAttachement(string fileName)
        {
            // Create simple example message with attachment.
            Mail_Message msg = Create_PlainText_Attachment();

            msg.Attachments[0].DataToFile(fileName);
        }

        #endregion

        #region static method AccessAttachmentData

        /// <summary>
        /// Shows how to access attchment data.
        /// </summary>
        private static void AccessAttachmentData()
        {
            // Create simple example message with attachment.
            Mail_Message msg = Create_PlainText_Attachment();

            // NOTE: All attachments are based on MIME_b_SinglepartBase !
            
            // Get body data as byte[]
            // For smaller data, you may call ".Data" - but for very big attachments you should use Stream methods.
            byte[] decodedData = ((MIME_b_SinglepartBase)msg.Attachments[0].Body).Data;

            // Store body data to Stream
            MemoryStream stream = new MemoryStream();
            msg.Attachments[0].DataToStream(stream);
        }

        #endregion



        #region static method GetTextFromTextEntity

        /// <summary>
        /// Gets decoded text from text/xxx mime entity.
        /// </summary>
        /// <returns>Returnn decoded text.</returns>
        private static string  GetTextFromTextEntity()
        {
            // Create simple example message with attachment.
            Mail_Message msg = Create_PlainText_Attachment();

            // In this exmple we search first text entity.
            MIME_Entity textEntity = null;
            foreach(MIME_Entity entity in msg.AllEntities){
                if(string.Equals(entity.ContentType.Type,"text",StringComparison.InvariantCultureIgnoreCase)){
                    textEntity = entity;

                    break;
                }
            }

            // Get text out of entity.
            return ((MIME_b_Text)textEntity.Body).Text;
        }

        #endregion


        #region static method VerifySignatures

        /// <summary>
        /// Verifies signed message signatures.
        /// </summary>
        /// <param name="signedMsg">Signed message.</param>
        /// <returns>Returns true is all signatures are valid.</returns>
        private static bool VerifySignatures(Mail_Message signedMsg)
        {
            if(signedMsg == null){
                throw new ArgumentNullException("signedMsg");
            }

            return signedMsg.VerifySignatures();
        }

        #endregion

        #region static method ConvertToMultipartSigned

        /// <summary>
        /// Converts message to multipart/signed.
        /// </summary>
        /// <param name="cert">Signer certificate.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>cert</b> or <b>message</b> is null reference.</exception>
        private static void ConvertToMultipartSigned(X509Certificate2 cert,Mail_Message message)
        {
            if(cert == null){
                throw new ArgumentNullException("cert");
            }
            if(message == null){
                throw new ArgumentNullException("message");
            }

            message.ConvertToMultipartSigned(cert);
        }

        #endregion

        #region static method Create_ApplicationPkcs7Mime_SignedData

        /// <summary>
        /// Creates <b>application/pkcs7-mime; smime-type=signed-data</b> entity.
        /// </summary>
        /// <param name="signedEntity">Entity where to create signed entity.</param>
        /// <param name="entityToSign">MIME entity to sign.</param>
        /// <param name="cert">Signer certificate.</param>
        /// <exception cref="ArgumentNullException">Is riased when <b>signedEntity</b>,<b>entityToSign</b> or <b>cert</b> is null reference.</exception>
        private static void Create_ApplicationPkcs7Mime_SignedData(MIME_Entity signedEntity,MIME_Entity entityToSign,X509Certificate2 cert)
        {
            if(signedEntity == null){
                throw new ArgumentNullException("signedEntity");
            }
            if(entityToSign == null){
                throw new ArgumentNullException("entityToSign");
            }
            if(cert == null){
                throw new ArgumentNullException("cert");
            }

            /* RFC 5751 3.4.2. Signing Using application/pkcs7-mime with SignedData.
             
                Content-Type: application/pkcs7-mime; smime-type=signed-data;
                    name=smime.p7m
                Content-Transfer-Encoding: base64
                Content-Disposition: attachment; filename=smime.p7m

                base64-pkcs7 data.
            */

            //---- Create pkcs7 ---------------------------------------------
            ContentInfo contentInfo = new ContentInfo(entityToSign.ToByte(null,null));
            SignedCms signedCms = new SignedCms(contentInfo);
            CmsSigner cmsSigner = new CmsSigner(cert);
            signedCms.ComputeSignature(cmsSigner);
            byte[] pkcs7Data = signedCms.Encode();

            MIME_b_ApplicationPkcs7Mime pkcs7Mime = new MIME_b_ApplicationPkcs7Mime();            
            signedEntity.Body = pkcs7Mime;
            signedEntity.ContentType.Parameters["smime-type"] = "signed-data";
            signedEntity.ContentType.Param_Name = "smime.p7m";
            signedEntity.ContentDisposition = new MIME_h_ContentDisposition(MIME_DispositionTypes.Attachment);
            signedEntity.ContentDisposition.Param_FileName = "smime.p7m";
            pkcs7Mime.SetData(new MemoryStream(pkcs7Data),MIME_TransferEncodings.Base64);
        }

        #endregion
    }
}
