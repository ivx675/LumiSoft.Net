using LumiSoft.Net.Mail;
using LumiSoft.Net.MIME;
using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents a BODYSTRUCTURE element as defined by IMAP RFC 9051.
    /// </summary>
    /// <remarks>
    /// A BODYSTRUCTURE element describes the MIME structure of a message. This
    /// class encapsulates the top‑level BODYSTRUCTURE entity, which may be either
    /// a single‑part element or a multipart element containing nested parts.
    /// The contained element defines the complete MIME tree for the message.
    /// </remarks>
    public class IMAP_t_BodyStructure
    {
        private IMAP_t_BodyStructure_e m_pMessage;

        /// <summary>
        /// Initializes a new <see cref="IMAP_t_BodyStructure"/> instance using the
        /// specified root BODYSTRUCTURE element.
        /// </summary>
        /// <param name="message">
        /// The root BODYSTRUCTURE element representing the top‑level MIME structure
        /// of the message. This may be a single‑part element or a multipart element,
        /// depending on the message’s MIME layout. The value must not be <c>null</c>.
        /// </param>
        public IMAP_t_BodyStructure(IMAP_t_BodyStructure_e message)
        {
            if(message == null){
                throw new ArgumentNullException(nameof(message));
            }

            m_pMessage = message;
        }


        #region static method ParseAsync

        /// <summary>
        /// Creates an <see cref="IMAP_t_BodyStructure"/> instance by interpreting the
        /// structure of a <c>BODYSTRUCTURE</c> element according to RFC 3501 section 7.4.2.
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the beginning of a <c>BODYSTRUCTURE</c> element.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_t_BodyStructure"/> containing either a single‑part or
        /// multipart bodystructure element, depending on the detected syntax.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the structure does not conform to the <c>BODYSTRUCTURE</c>
        /// grammar or when an unexpected token is encountered.
        /// </exception>
        internal static async Task<IMAP_t_BodyStructure> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* BODYSTRUCTURE Part Type Detection RFC 3501, Section 7.4.2

                After removing the outer parentheses of a BODYSTRUCTURE element, inspect the
                first item inside the list. This determines whether the part is single-part
                or multipart.

                Single-Part Syntax:
                -------------------
                ("TYPE" "SUBTYPE" ...)

                The first item is an atom (a quoted string or bare word). This indicates a
                single-part body.

                Examples:
                  ("TEXT" "PLAIN" ...)
                  ("IMAGE" "JPEG" ...)
                  ("APPLICATION" "PDF" ...)
                  ("MESSAGE" "RFC822" ...)

                Multipart Syntax:
                -----------------
                ((part1) (part2) ... "SUBTYPE" ...)

                The first item is itself a parenthesized list. This indicates a multipart
                body containing one or more subparts.

                Examples:
                  (("TEXT" "PLAIN" ...) "MIXED" ...)
                  (("TEXT" "PLAIN" ...) ("IMAGE" "JPEG" ...) "ALTERNATIVE" ...)
                  (("TEXT" "PLAIN" ...) ("TEXT" "HTML" ...) "RELATED" ...)

                Detection Rule:
                ---------------
                If the first item is an atom:
                    Parse as SinglePart.

                If the first item is a list:
                    Parse as MultiPart.
            */

            if(imapReader.PeekIs("((")){
                return new IMAP_t_BodyStructure(await IMAP_t_BodyStructure_e_MultiPart.ParseAsync(imapReader,cancellationToken));
            }
            else if(imapReader.PeekIs("(")){
                return new IMAP_t_BodyStructure(await IMAP_t_BodyStructure_e_SinglePart.ParseAsync(imapReader,cancellationToken));
            }

            throw new Exception();
        }

        #endregion

        #region static method FromMessage

        /// <summary>
        /// Creates an <see cref="IMAP_t_BodyStructure"/> from a Mail_Message.
        /// </summary>
        /// <param name="message">The source <see cref="Mail_Message"/>.</param>
        /// <returns>Returns a populated <see cref="IMAP_t_BodyStructure"/> instance.</returns>
        public static IMAP_t_BodyStructure FromMessage(Mail_Message message)
        {
            if(message == null){
                throw new ArgumentNullException(nameof(message));
            }

            IMAP_t_BodyStructure_e rootEntity = ConvertMimeEntity(message);

            return new IMAP_t_BodyStructure(rootEntity);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a valid IMAP BODYSTRUCTURE string representation of the message tree 
        /// (excluding the 'BODYSTRUCTURE' keyword).
        /// </summary>
        /// <returns>A valid IMAP BODYSTRUCTURE parenthesized string.</returns>
        public override string ToString()
        {
            if(m_pMessage == null){
                return "NIL";
            }

            // Delegate serialization depending on whether it's a single-part or multi-part entity
            if(m_pMessage is IMAP_t_BodyStructure_e_SinglePart singlePart){
                return SinglePartToString(singlePart);
            }
            else if (m_pMessage is IMAP_t_BodyStructure_e_MultiPart multiPart){
                return MultiPartToString(multiPart);
            }

            return "NIL";
        }

        #endregion

        #region method GetAttachments

        /// <summary>
        /// Gets message attachments.
        /// </summary>
        /// <param name="includeInline">Specifies if 'inline' entities are included.</param>
        /// <returns>Returns message attachments.</returns>
        public IMAP_t_BodyStructure_e_SinglePart[] GetAttachments(bool includeInline)
        {
            List<IMAP_t_BodyStructure_e_SinglePart> retVal = new List<IMAP_t_BodyStructure_e_SinglePart>();
            foreach(IMAP_t_BodyStructure_e entity in this.AllEntities){
                MIME_h_ContentType?        contentType = entity.ContentType;
                MIME_h_ContentDisposition? disposition = entity.ContentDisposition;

                if(entity is IMAP_t_BodyStructure_e_SinglePart){
                    if(disposition != null && string.Equals(disposition.DispositionType,"attachment",StringComparison.InvariantCultureIgnoreCase)){
                        retVal.Add((IMAP_t_BodyStructure_e_SinglePart)entity);
                    }
                    else if(disposition != null && string.Equals(disposition.DispositionType,"inline",StringComparison.InvariantCultureIgnoreCase)){
                        if(includeInline){
                            retVal.Add((IMAP_t_BodyStructure_e_SinglePart)entity);
                        }
                    }
                    else if(contentType != null && string.Equals(contentType.Type,"application",StringComparison.InvariantCultureIgnoreCase)){
                        retVal.Add((IMAP_t_BodyStructure_e_SinglePart)entity);
                    }
                    else if(contentType != null && string.Equals(contentType.Type,"image",StringComparison.InvariantCultureIgnoreCase)){
                        retVal.Add((IMAP_t_BodyStructure_e_SinglePart)entity);
                    }
                    else if(contentType != null && string.Equals(contentType.Type,"video",StringComparison.InvariantCultureIgnoreCase)){
                        retVal.Add((IMAP_t_BodyStructure_e_SinglePart)entity);
                    }
                    else if(contentType != null && string.Equals(contentType.Type,"audio",StringComparison.InvariantCultureIgnoreCase)){
                        retVal.Add((IMAP_t_BodyStructure_e_SinglePart)entity);
                    }
                    else if(contentType != null && string.Equals(contentType.Type,"message",StringComparison.InvariantCultureIgnoreCase)){
                        retVal.Add((IMAP_t_BodyStructure_e_SinglePart)entity);
                    }
                }
            }

            return retVal.ToArray();
        }

        #endregion

        
        #region static method ConvertMimeEntity

        /// <summary>
        /// Recursively converts a MIME entity into its corresponding IMAP body structure representation (<see cref="IMAP_t_BodyStructure_e"/>).
        /// </summary>
        /// <param name="entity">The <see cref="MIME_Entity"/> to convert.</param>
        /// <returns>An <see cref="IMAP_t_BodyStructure_e"/> instance representing either a multi-part container or a single-part body structure.</returns>
        private static IMAP_t_BodyStructure_e ConvertMimeEntity(MIME_Entity entity)
        {
            // Check if it's a multipart entity
            if (entity.Body is MIME_b_Multipart multipartBody){
                var childEntities = new List<IMAP_t_BodyStructure_e>();
                foreach(MIME_Entity subEntity in multipartBody.BodyParts){
                    childEntities.Add(ConvertMimeEntity(subEntity));
                }

                return new IMAP_t_BodyStructure_e_MultiPart(
                    contentType: entity.ContentType ?? new MIME_h_ContentType("multipart/mixed"),
                    contentDisposition: entity.ContentDisposition,
                    language: null,
                    location: null,
                    bodyParts: childEntities.ToArray()
                );
            }

            // Check if it's a nested MESSAGE/RFC822 embedded message part
            IMAP_t_Envelope? innerEnvelope = null;
            IMAP_t_BodyStructure? innerBodyStructure = null;

            if(entity.Body is MIME_b_MessageRfc822 rfc822Body && rfc822Body.Message != null){
                innerEnvelope = IMAP_t_Envelope.FromMessage(rfc822Body.Message);
                innerBodyStructure = FromMessage(rfc822Body.Message);
            }

            int contentSize = 0;
            if(entity.Body is MIME_b_SinglepartBase b1){
                contentSize =  b1.EncodedDataSize;
            }

            int linesCount = 0;
            if(entity.Body is MIME_b_Text b2){
                linesCount = b2.Text.Split('\n').Length;
            }

            // Single-part entity
            return new IMAP_t_BodyStructure_e_SinglePart(
                contentType: entity.ContentType ?? new MIME_h_ContentType("text/plain"),
                contentId: entity.ContentID,
                contentDescription: entity.ContentDescription,
                contentTransferEncoding: entity.ContentTransferEncoding ?? "7BIT",
                contentSize: contentSize,
                linesCount: linesCount,
                envelope: innerEnvelope,
                bodyStructure: innerBodyStructure,
                md5: null,
                contentDisposition: entity.ContentDisposition,
                language: null,
                location: null
            );
        }

        #endregion

        #region static method SinglePartToString

        /// <summary>
        /// Serializes an IMAP single-part body structure entity into its corresponding RFC-compliant IMAP string representation.
        /// </summary>
        /// <param name="sp">The <see cref="IMAP_t_BodyStructure_e_SinglePart"/> instance to serialize.</param>
        /// <returns>A parenthesized string representing the single-part body structure and all its required fields.</returns>
        private static string SinglePartToString(IMAP_t_BodyStructure_e_SinglePart sp)
        {
            /*
                IMAP4rev2 (RFC 9051) Single-Part BODYSTRUCTURE Syntax:
                ("TYPE" "SUBTYPE" (parameters) id desc encoding size [lines] [envelope bodystructure] md5 (disposition) language location)
         
                Field Sequence:
                  1. type        - Content-Type major type (e.g., "TEXT", "APPLICATION")
                  2. subtype     - Content-Type subtype (e.g., "PLAIN", "OCTET-STREAM")
                  3. parameters  - Content-Type parameters list (attribute-value pairs) or NIL
                  4. id          - Content-ID string or NIL
                  5. description - Content-Description string or NIL
                  6. encoding    - Content-Transfer-Encoding (e.g., "7BIT", "BASE64")
                  7. size        - Body size in octets (number)
                  8. lines       - Line count (mandatory for TEXT/* and MESSAGE/RFC822 types)
                  9. env/body    - Nested Envelope and BodyStructure (only for MESSAGE/RFC822)
                 10. md5         - MD5 checksum string or NIL
                 11. disposition - Content-Disposition structure (type and parameters) or NIL
                 12. language    - Content-Language string/list or NIL
                 13. location    - Content-Location string or NIL
            */

            var sb = new System.Text.StringBuilder();
            sb.Append('(');

            // 1 & 2. Type & Subtype
            string type = "TEXT";
            string subType = "PLAIN";
            if(sp.ContentType != null){
                if (!string.IsNullOrEmpty(sp.ContentType.Type)) type = sp.ContentType.Type.ToUpperInvariant();
                if (!string.IsNullOrEmpty(sp.ContentType.SubType)) subType = sp.ContentType.SubType.ToUpperInvariant();
            }

            sb.Append($"\"{type}\" \"{subType}\" ");

            // 3. Parameters (body-fld-param)
            if(sp.ContentType != null && sp.ContentType.Parameters.Count > 0){
                sb.Append('(');
                bool first = true;
                foreach (var param in sp.ContentType.Parameters){
                    if(!first){
                        sb.Append(' ');
                    }

                    string paramName = EscapeString(param.Name);
                    string? val = param.Value;
                    if(val == null){
                        sb.Append($"\"{paramName}\" NIL");
                    }
                    else{
                        sb.Append($"\"{paramName}\" \"{EscapeString(val)}\"");
                    }
                    first = false;
                }
                sb.Append(") ");
            }
            else{
                sb.Append("NIL ");
            }

            // 4. ID (body-fld-id)
            if (string.IsNullOrEmpty(sp.ContentID)) sb.Append("NIL ");
            else sb.Append($"\"{EscapeString(sp.ContentID)}\" ");

            // 5. Description (body-fld-desc)
            if (string.IsNullOrEmpty(sp.ContentDescription)) sb.Append("NIL ");
            else sb.Append($"\"{EscapeString(sp.ContentDescription)}\" ");

            // 6. Encoding (body-fld-enc)
            string encoding = !string.IsNullOrEmpty(sp.ContentTransferEncoding) ? sp.ContentTransferEncoding.ToUpperInvariant() : "7BIT";
            sb.Append($"\"{encoding}\" ");

            // 7. Size (body-fld-octets)
            sb.Append($"{sp.ContentSize} ");

            // 8. Lines (for TEXT or MESSAGE/RFC822)
            bool isText = string.Equals("TEXT", type, StringComparison.OrdinalIgnoreCase);
            bool isMessageRfc822 = string.Equals("MESSAGE", type, StringComparison.OrdinalIgnoreCase) && string.Equals("RFC822", subType, StringComparison.OrdinalIgnoreCase);

            if (isText || isMessageRfc822){
                int lines = sp.LinesCount.HasValue ? sp.LinesCount.Value : 0;
                sb.Append($"{lines} ");
            }

            // 9. MESSAGE/RFC822 specific fields (Envelope & BodyStructure)
            if(isMessageRfc822){
                if (sp.Envelope != null) sb.Append($"{sp.Envelope} ");
                else sb.Append("NIL ");

                if (sp.BodyStructure != null) sb.Append($"{sp.BodyStructure} ");
                else sb.Append("NIL ");
            }

            // 10. MD5 (body-fld-md5)
            if (string.IsNullOrEmpty(sp.Md5)) sb.Append("NIL ");
            else sb.Append($"\"{EscapeString(sp.Md5)}\" ");

            // 11. Content-Disposition (body-fld-dsp)
            if(sp.ContentDisposition == null){
                sb.Append("NIL");
            }
            else{
                string dispType = EscapeString(sp.ContentDisposition.DispositionType.ToUpperInvariant());
                sb.Append($"(\"{dispType}\" ");
                
                if(sp.ContentDisposition.Parameters.Count > 0){
                    sb.Append('(');
                    bool first = true;
                    foreach (var param in sp.ContentDisposition.Parameters){
                        if(!first){
                            sb.Append(' ');
                        }

                        string paramName = EscapeString(param.Name);
                        string? val = param.Value;
                        if(val == null){
                            sb.Append($"\"{paramName}\" NIL");
                        }
                        else{
                            sb.Append($"\"{paramName}\" \"{EscapeString(val)}\"");
                        }
                        first = false;
                    }
                    sb.Append(')');
                }
                else{
                    sb.Append("NIL");
                }
                sb.Append(')');
            }

            // 12. Language (body-fld-lang)
            if (string.IsNullOrEmpty(sp.Language)) sb.Append(" NIL");
            else sb.Append($" \"{EscapeString(sp.Language)}\"");

            // 13. Location (body-fld-loc)
            if (string.IsNullOrEmpty(sp.Location)) sb.Append(" NIL");
            else sb.Append($" \"{EscapeString(sp.Location)}\"");

            sb.Append(')');

            return sb.ToString();
        }

        #endregion

        #region static method MultiPartToString

        /// <summary>
        /// Serializes an IMAP multi-part body structure entity into its corresponding RFC-compliant IMAP string representation.
        /// </summary>
        /// <param name="mp">The <see cref="IMAP_t_BodyStructure_e_MultiPart"/> instance to serialize.</param>
        /// <returns>A parenthesized string representing the multi-part body structure, including its child parts, subtype, and optional extension fields.</returns>
        private static string MultiPartToString(IMAP_t_BodyStructure_e_MultiPart mp)
        {
            /*
                IMAP4rev2 (RFC 9051) Multi-Part BODYSTRUCTURE Syntax:
                (body-part-1 [body-part-2 ...] "SUBTYPE" [disposition] [language] [location])
         
                Field Sequence:
                  1. body-parts  - One or more nested body structures (single-part or multi-part)
                  2. subtype     - Content-Type subtype (e.g., "MIXED", "ALTERNATIVE", "RELATED")
                  3. disposition - Content-Disposition structure (type and parameters) or NIL (extension)
                  4. language    - Content-Language string/list or NIL (extension)
                  5. location    - Content-Location string or NIL (extension)
            */

            var sb = new StringBuilder();
            sb.Append('(');

            // Child parts
            foreach (var part in mp.BodyParts){
                if(part is IMAP_t_BodyStructure_e_SinglePart sp){
                    sb.Append(SinglePartToString(sp));
                }
                else if(part is IMAP_t_BodyStructure_e_MultiPart subMp){
                    sb.Append(MultiPartToString(subMp));
                }
            }

            // Subtype
            string subType = "MIXED";
            if(mp.ContentType != null && !string.IsNullOrEmpty(mp.ContentType.SubType)){
                subType = mp.ContentType.SubType.ToUpperInvariant();
            }
            sb.Append($" \"{subType}\"");

            // Optional extension fields (Disposition, Language, Location)
            if(mp.ContentDisposition != null || !string.IsNullOrEmpty(mp.Language) || !string.IsNullOrEmpty(mp.Location)){
                if(mp.ContentDisposition == null){
                    sb.Append(" NIL");
                }
                else{
                    string dispType = EscapeString(mp.ContentDisposition.DispositionType.ToUpperInvariant());
                    sb.Append($" (\"{dispType}\" NIL)");
                }

                if(string.IsNullOrEmpty(mp.Language)){
                    sb.Append(" NIL");
                }
                else{
                    sb.Append($" \"{EscapeString(mp.Language)}\"");
                }

                if(!string.IsNullOrEmpty(mp.Location)){
                    sb.Append($" \"{EscapeString(mp.Location)}\"");
                }
            }

            sb.Append(')');

            return sb.ToString();
        }

        #endregion

        #region static method EscapeString

        private static string EscapeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the root BODYSTRUCTURE element of the message.
        /// </summary>
        /// <remarks>
        /// The root element represents the top‑level MIME structure of the message.
        /// For single‑part messages, this is the single‑part BODYSTRUCTURE element.
        /// For multipart messages, this is the top‑level multipart BODYSTRUCTURE
        /// element containing all nested parts. This property provides a unified
        /// entry point to the MIME tree, matching the convention used by MailKit
        /// where the root structure is referred to as the “message” element.
        /// </remarks>
        public IMAP_t_BodyStructure_e Message
        {
            get{ return m_pMessage; }
        }

        /// <summary>
        /// Gets if message contains signed data.
        /// </summary>
        public bool IsSigned
        {
            get{
                foreach(IMAP_t_BodyStructure_e entity in this.AllEntities){
                    if(string.Equals(entity.ContentType?.TypeWithSubtype,MIME_MediaTypes.Application.pkcs7_mime,StringComparison.InvariantCultureIgnoreCase)){
                        return true;
                    }
                    else if(string.Equals(entity.ContentType?.TypeWithSubtype,MIME_MediaTypes.Multipart.signed,StringComparison.InvariantCultureIgnoreCase)){
                        return true;
                    }
                }

                return false;
            }
        }        
        
        /// <summary>
        /// Gets all MIME entities as list.
        /// </summary>
        public IMAP_t_BodyStructure_e[] AllEntities
        {
            get{
                List<IMAP_t_BodyStructure_e> retVal        = new List<IMAP_t_BodyStructure_e>();
                List<IMAP_t_BodyStructure_e> entitiesQueue = new List<IMAP_t_BodyStructure_e>();
                entitiesQueue.Add(m_pMessage);
            
                while(entitiesQueue.Count > 0){
                    IMAP_t_BodyStructure_e currentEntity = entitiesQueue[0];
                    entitiesQueue.RemoveAt(0);
        
                    retVal.Add(currentEntity);

                    // Current entity is multipart entity, add it's body-parts for processing.
                    if(currentEntity is IMAP_t_BodyStructure_e_MultiPart){
                        IMAP_t_BodyStructure_e[] bodyParts = ((IMAP_t_BodyStructure_e_MultiPart)currentEntity).BodyParts;
                        for(int i=0;i<bodyParts.Length;i++){
                            entitiesQueue.Insert(i,bodyParts[i]);
                        }
                    }
                }

                return retVal.ToArray();
            }
        }

        /// <summary>
        /// Gets attachment entities. Content-Disposition "inline" not included, use GetAttachments method which allows to include "inline".
        /// </summary>
        public IMAP_t_BodyStructure_e_SinglePart[] Attachments
        {
            get{ return GetAttachments(false); }
        }

        /// <summary>
        /// Gets first text/plain body entity, returns null if no such entity.
        /// </summary>
        public IMAP_t_BodyStructure_e_SinglePart? BodyTextEntity
        {
            get{ 
                foreach(IMAP_t_BodyStructure_e e in this.AllEntities){
                    if(string.Equals(e.ContentType?.TypeWithSubtype,MIME_MediaTypes.Text.plain,StringComparison.InvariantCultureIgnoreCase)){
                        return (IMAP_t_BodyStructure_e_SinglePart)e;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets first text/html body entity, returns null if no such entity.
        /// </summary>
        public IMAP_t_BodyStructure_e_SinglePart? BodyTextHtmlEntity
        {
            get{ 
                foreach(IMAP_t_BodyStructure_e e in this.AllEntities){
                    if(string.Equals(e.ContentType?.TypeWithSubtype,MIME_MediaTypes.Text.html,StringComparison.InvariantCultureIgnoreCase)){
                        return (IMAP_t_BodyStructure_e_SinglePart)e;
                    }
                }

                return null;
            }
        }

        #endregion
    }
}
