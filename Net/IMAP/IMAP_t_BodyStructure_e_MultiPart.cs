using LumiSoft.Net.MIME;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Mime;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents an IMAP <c>BODYSTRUCTURE</c> multipart entity as defined in
    /// RFC 3501 section 7.4.2.
    /// </summary>
    /// <remarks>
    /// A multipart body structure contains one or more child <c>BODYSTRUCTURE</c>
    /// elements, each of which may be either a single‑part or another multipart
    /// structure. The multipart container also includes a MIME <c>Content-Type</c>
    /// with a multipart subtype (such as <c>mixed</c>, <c>alternative</c>,
    /// <c>related</c>, or <c>signed</c>) and may optionally include extension
    /// fields: <c>Content-Disposition</c>, <c>language</c>, and <c>location</c>.
    /// </remarks>
    public class IMAP_t_BodyStructure_e_MultiPart : IMAP_t_BodyStructure_e
    {
        private MIME_h_ContentType           m_pContentType;
        private MIME_h_ContentDisposition?   m_pContentDisposition = null;
        private string?                      m_Language            = null;
        private string?                      m_Location            = null;
        private IMAP_t_BodyStructure_e[]     m_pBodyParts;

        private IMAP_t_BodyStructure_e_MultiPart(
            MIME_h_ContentType contentType,
            MIME_h_ContentDisposition? contentDisposition,
            string? language,
            string? location,
            IMAP_t_BodyStructure_e[] bodyParts)
        {
            m_pContentType        = contentType;
            m_pContentDisposition = contentDisposition;
            m_Language            = language;
            m_Location            = location;
            m_pBodyParts          = bodyParts;

            foreach(IMAP_t_BodyStructure_e e in bodyParts){
                e.SetParent(this);
            }
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses an IMAP <c>BODYSTRUCTURE</c> multipart element according to RFC 3501 section 7.4.2.
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the opening parenthesis of the multipart container.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A populated <see cref="IMAP_t_BodyStructure_e_MultiPart"/> instance containing:
        /// <list type="bullet">
        /// <item><description>One or more child <c>BODYSTRUCTURE</c> elements.</description></item>
        /// <item><description>The multipart subtype (e.g., <c>mixed</c>, <c>alternative</c>, <c>related</c>).</description></item>
        /// <item><description>Optional extension fields: disposition, language, and location.</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the multipart syntax is invalid, the subtype is missing or NIL,
        /// or any structural element does not conform to RFC 3501.
        /// </exception>
        /// <remarks>
        /// The method distinguishes multipart from single‑part structures by examining the raw
        /// character stream: multipart containers always begin with <c>((</c> because the first
        /// child <c>BODYSTRUCTURE</c> immediately follows the multipart's opening parenthesis.
        /// Child elements are parsed recursively until a non‑parenthesis token indicates the
        /// multipart subtype.
        /// </remarks>
        internal static async Task<IMAP_t_BodyStructure_e_MultiPart> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /*
                 RFC 3501 7.4.2. multipart body structure.

                 multipart = "(" 1*body ")" " " body-ext-mpart

                 A multipart body contains:
                   - One or more child BODYSTRUCTURE elements.
                   - A Content-Type with a multipart subtype (mixed, alternative, related, etc).
                   - Optional extension fields:
                       * disposition
                       * language
                       * location

                 Example: ((part1)(part2) "mixed")
            */

            if(imapReader.ReadChar() != '('){
                throw new ParseException("Invalid BODYSTRUCTURE value.");
            }

            List<IMAP_t_BodyStructure_e> parts = new List<IMAP_t_BodyStructure_e>();
            while(true){
                if(imapReader.PeekIs("((")){
                    parts.Add(await IMAP_t_BodyStructure_e_MultiPart.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("(")){
                    parts.Add(await IMAP_t_BodyStructure_e_SinglePart.ParseAsync(imapReader,cancellationToken));
                }
                // No more parts
                else{
                    break;
                }
            }

            string? subType = await imapReader.ReadStringAsync(cancellationToken);
            if(subType == null){
                throw new ParseException("Invalid BODYSTRUCTURE value: Multipart subtype cannot be NIL.");
            }
            MIME_h_ContentType contentType = new MIME_h_ContentType("multipart/" + subType);

            #region Optional values

            MIME_h_ContentDisposition? contentDisposition = null;
            string?                    language           = null;
            string?                    location           = null;

            // disposition
            if(!imapReader.PeekIs(')')){
                // ("ATTACHMENT" ("FILENAME" "report.pdf")) or NIL
                if(imapReader.PeekIs("NIL")){
                    imapReader.ReadAtom();
                }
                else{
                    if(imapReader.ReadChar() != '('){
                        throw new ParseException("Invalid BODYSTRUCTURE value.");
                    }

                    string disposition = await imapReader.ReadStringAsync(cancellationToken) ?? "inline";

                    contentDisposition = new MIME_h_ContentDisposition(disposition);

                    // No parameters.
                    if(imapReader.PeekIs("NIL")){
                        imapReader.ReadAtom();
                    }
                    // Read parameters.
                    else{
                        // parameters start-tag
                        if(imapReader.ReadChar() != '('){
                            throw new ParseException("Invalid BODYSTRUCTURE value.");
                        }

                        while(true){
                            // parameters end-tag
                            if(imapReader.PeekIs(')')){
                                imapReader.ReadChar();

                                break;
                            }

                            string? paramName = await imapReader.ReadStringAsync(cancellationToken);

                            // Parameter value missing, not name value pair.
                            if(imapReader.PeekIs(')')){
                                imapReader.ReadChar();

                                if(!string.IsNullOrEmpty(paramName)){
                                    contentDisposition.Parameters[paramName] = null;
                                }

                                break;
                            }

                            string? paramValue = await imapReader.ReadStringAsync(cancellationToken);

                            if(!string.IsNullOrEmpty(paramName)){
                                if(paramValue != null){
                                    contentDisposition.Parameters[paramName] = MIME_Encoding_EncodedWord.DecodeTextS(paramValue);
                                }
                                else{
                                    contentDisposition.Parameters[paramName] = null;
                                }
                            }
                        }
                    }

                    // dispostion end-tag
                    if(imapReader.ReadChar() != ')'){
                        throw new ParseException("Invalid BODYSTRUCTURE value.");
                    }
                }                
            }

            // language
            if(!imapReader.PeekIs(')')){
                language = await imapReader.ReadStringAsync(cancellationToken);
            }

            // location
            if(!imapReader.PeekIs(')')){
                location = await imapReader.ReadStringAsync(cancellationToken);
            }

            #endregion

            if(imapReader.ReadChar() != ')'){
                throw new ParseException("Invalid BODYSTRUCTURE value.");
            }

            return new IMAP_t_BodyStructure_e_MultiPart(contentType,contentDisposition,language,location,parts.ToArray());
        }

        #endregion

        
        #region method IndexOfBodyPart

        /// <summary>
        /// Gets the specified body part zero-based index number.
        /// </summary>
        /// <param name="bodyPart">Body part.</param>
        /// <returns>Return specified body part zero-based index number.</returns>
        internal int IndexOfBodyPart(IMAP_t_BodyStructure_e bodyPart)
        {
            return m_pBodyParts.IndexOf(bodyPart);
        }

        #endregion

        
        #region Properties implementation

        /// <summary>
        /// Gets the <c>Content-Type</c> header associated with this BODYSTRUCTURE element.
        /// </summary>
        /// <remarks>
        /// For single‑part entities, this value reflects the media type and parameters
        /// parsed from the IMAP <c>BODYSTRUCTURE</c> response. For multipart entities,
        /// the value represents the constructed <c>multipart/*</c> type derived from the
        /// multipart subtype (e.g., <c>multipart/mixed</c>, <c>multipart/alternative</c>).
        /// </remarks>
        public override MIME_h_ContentType ContentType
        {
            get{ return m_pContentType; }
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
        public override MIME_h_ContentDisposition? ContentDisposition
        {
            get{ return m_pContentDisposition; }
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
        public override string? Language
        {
            get{ return m_Language; }
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
        public override string? Location
        {
            get{ return m_Location; }
        }

        /// <summary>
        /// Gets the collection of child <c>BODYSTRUCTURE</c> elements contained within
        /// this multipart entity.
        /// </summary>
        /// <remarks>
        /// Represents the mandatory <c>1*body</c> sequence defined in RFC 3501 for
        /// multipart body structures. Each entry corresponds to a single part within
        /// the multipart container, and may itself be either a single‑part or another
        /// multipart structure. The array is never <c>null</c>; multipart entities
        /// always contain at least one child part.
        /// </remarks>
        public IMAP_t_BodyStructure_e[] BodyParts
        {
            get{ return m_pBodyParts; }
        }

        #endregion
    }
}
