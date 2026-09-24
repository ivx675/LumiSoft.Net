using LumiSoft.Net.MIME;
using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Defines the abstract base type for IMAP <c>BODYSTRUCTURE</c> elements
    /// returned by the IMAP <c>FETCH BODYSTRUCTURE</c> command.
    /// </summary>
    /// <remarks>
    /// A <c>BODYSTRUCTURE</c> element describes the MIME structure of a message
    /// part according to RFC 3501 section 7.4.2. Concrete implementations represent
    /// either single‑part entities (such as <c>text/plain</c>, <c>image/jpeg</c>,
    /// or <c>message/rfc822</c>) or multipart entities (such as
    /// <c>multipart/mixed</c>, <c>multipart/alternative</c>, or
    /// <c>multipart/related</c>).
    ///
    /// <para>
    /// The base class exposes common MIME metadata fields including
    /// <see cref="ContentType"/>, <see cref="ContentDisposition"/>,
    /// <see cref="Language"/>, and <see cref="Location"/>. Derived classes provide
    /// the structural details required by the IMAP grammar, such as multipart
    /// child parts or single‑part size and encoding information.
    /// </para>
    /// </remarks>
    public abstract class IMAP_t_BodyStructure_e
    {
        private IMAP_t_BodyStructure_e_MultiPart? m_pParent = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        protected IMAP_t_BodyStructure_e()
        {
        }        


        #region method SetParent

        /// <summary>
        /// Sets this entity parent entity.
        /// </summary>
        /// <param name="parent">Parent entity.</param>
        internal void SetParent(IMAP_t_BodyStructure_e_MultiPart? parent)
        {
            m_pParent = parent;
        }


        #endregion
        
        #region Properties implementation

        /// <summary>
        /// Gets the parent multipart <c>BODYSTRUCTURE</c> element, if this part is
        /// contained within another multipart entity.
        /// </summary>
        /// <remarks>
        /// This value is <c>null</c> when the element is the top‑level bodystructure
        /// or when it is not part of a multipart container. For nested multipart
        /// structures, the property provides a direct reference to the enclosing
        /// <see cref="IMAP_t_BodyStructure_e_MultiPart"/> instance, enabling traversal
        /// of the MIME hierarchy.
        /// </remarks>
        public IMAP_t_BodyStructure_e_MultiPart? Parent
        {
            get { return m_pParent; }
        }

        /// <summary>
        /// Gets the <c>Content-Type</c> header associated with this BODYSTRUCTURE element.
        /// </summary>
        /// <remarks>
        /// For single‑part entities, this value reflects the media type and parameters
        /// parsed from the IMAP <c>BODYSTRUCTURE</c> response. For multipart entities,
        /// the value represents the constructed <c>multipart/*</c> type derived from the
        /// multipart subtype (e.g., <c>multipart/mixed</c>, <c>multipart/alternative</c>).
        /// </remarks>
        public abstract MIME_h_ContentType? ContentType
        {
            get;
        }

        /// <summary>
        /// Gets the MIME <c>Content-Disposition</c> header value for this BODYSTRUCTURE element.
        /// </summary>
        /// <remarks>
        /// Represents the optional <c>disposition</c> field from the IMAP <c>BODYSTRUCTURE</c>
        /// response. The value may be <c>null</c> when the server reports <c>NIL</c> or omits
        /// the disposition entirely. When present, the object includes the disposition type
        /// (e.g., <c>inline</c>, <c>attachment</c>) and any associated parameters such as
        /// <c>filename</c>.
        /// </remarks>
        public abstract MIME_h_ContentDisposition? ContentDisposition
        {
            get;
        }

        /// <summary>
        /// Gets the language value associated with this BODYSTRUCTURE element.
        /// </summary>
        /// <remarks>
        /// Represents the optional <c>language</c> field from the IMAP <c>BODYSTRUCTURE</c>
        /// response. The value may be <c>null</c> when the server reports <c>NIL</c> or does
        /// not provide a language specification. When present, the value typically contains
        /// a language tag such as <c>en</c>, <c>et</c>, or other RFC 3066 / BCP 47 identifiers.
        /// </remarks>
        public abstract string? Language
        {
            get;
        }

        /// <summary>
        /// Gets the location value associated with this BODYSTRUCTURE element.
        /// </summary>
        /// <remarks>
        /// Represents the optional <c>location</c> field from the IMAP <c>BODYSTRUCTURE</c>
        /// response. The value may be <c>null</c> when the server reports <c>NIL</c> or omits
        /// the field entirely. When present, the value typically contains a server‑defined
        /// identifier indicating where the message part is stored or referenced.
        /// </remarks>
        public abstract string? Location
        {
            get;
        }

        
        /// <summary>
        /// Gets IMAP fetch BODY[part-specifier] value. This value can be used to fetch this entity from full message.
        /// </summary>
        public string PartSpecifier
        {
            get{
                string retVal = "";

                if(m_pParent == null){
                    retVal = "";
                }
                else{
                    // Multipart main entity part specifier is "", first child starts from "1".

                    IMAP_t_BodyStructure_e            currentItem   = this;
                    IMAP_t_BodyStructure_e_MultiPart? currentParent = m_pParent;
                    while(currentParent != null){
                        int index = currentParent.IndexOfBodyPart(currentItem) + 1;

                        if(string.IsNullOrEmpty(retVal)){
                            retVal = index.ToString();
                        }
                        else{
                            retVal = index.ToString() + "." + retVal;
                        }

                        // Move <--- left upper parent.
                        currentItem   = currentParent;
                        currentParent = currentParent.m_pParent;
                    }
                }

                return retVal;
            }
        }

        #endregion
    }
}
