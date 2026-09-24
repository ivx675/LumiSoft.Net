using LumiSoft.Net.MIME;
using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents a BODYSTRUCTURE element as defined by IMAP RFC 3501.
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
