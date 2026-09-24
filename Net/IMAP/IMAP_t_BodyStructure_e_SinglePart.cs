using LumiSoft.Net.MIME;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Xml.Linq;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents a single-part IMAP BODYSTRUCTURE element as defined in RFC 3501.
    /// </summary>
    /// <remarks>
    /// Models the structure of a non-multipart MIME body, including content type,
    /// identifiers, encoding, size, optional line count, and optional metadata such
    /// as MD5, disposition, language, and location. For <c>MESSAGE/RFC822</c> parts,
    /// this class also exposes the nested ENVELOPE and BODYSTRUCTURE.
    /// </remarks>
    public class IMAP_t_BodyStructure_e_SinglePart : IMAP_t_BodyStructure_e
    {
        private MIME_h_ContentType?        m_pContentType            = null;
        private string?                    m_ContentID               = null;
        private string?                    m_ContentDescription      = null;
        private string?                    m_ContentTransferEncoding = null;
        private long                       m_ContentSize             = 0;
        private int?                       m_LinesCount              = null;
        private IMAP_t_Envelope?           m_pEnvelope               = null;
        private IMAP_t_BodyStructure?      m_pBodyStructure          = null;
        private string?                    m_Md5                     = null;
        private MIME_h_ContentDisposition? m_pContentDisposition     = null;
        private string?                    m_Language                = null;
        private string?                    m_Location                = null;

        private IMAP_t_BodyStructure_e_SinglePart(
            MIME_h_ContentType?        contentType,
            string?                    contentId,
            string?                    contentDescription,
            string?                    contentTransferEncoding,
            long                       contentSize,
            int?                       linesCount,
            IMAP_t_Envelope?           envelope,
            IMAP_t_BodyStructure?      bodyStructure,
            string?                    md5,
            MIME_h_ContentDisposition? contentDisposition,
            string?                    language,
            string?                    location)
        {
            m_pContentType            = contentType;
            m_ContentID               = contentId;
            m_ContentDescription      = contentDescription;
            m_ContentTransferEncoding = contentTransferEncoding;
            m_ContentSize             = contentSize;
            m_LinesCount              = linesCount;
            m_pEnvelope               = envelope;
            m_pBodyStructure          = bodyStructure;
            m_Md5                     = md5;
            m_pContentDisposition     = contentDisposition;
            m_Language                = language;
            m_Location                = location;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses a single-part IMAP BODYSTRUCTURE element according to RFC 3501 section 7.4.2.
        /// Handles TEXT, MESSAGE/RFC822, and all other single-part MIME types, including
        /// mandatory positional fields and optional values that may be NIL.
        /// </summary>
        /// <param name="imapReader">
        /// IMAP token reader positioned at the beginning of a BODYSTRUCTURE list.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous parsing operation.
        /// </param>
        /// <returns>
        /// A populated <see cref="IMAP_t_BodyStructure_e_SinglePart"/> instance containing
        /// content type, identifiers, encoding, size, line count, optional MD5, disposition,
        /// language, location, and (for MESSAGE/RFC822) nested envelope and bodystructure.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the BODYSTRUCTURE syntax is invalid or required structural elements
        /// are missing.
        /// </exception>
        /// <remarks>
        /// This parser follows strict positional rules defined by the IMAP BODYSTRUCTURE
        /// grammar. TEXT parts include a line count; MESSAGE/RFC822 parts include a line
        /// count, envelope, and nested bodystructure. Optional fields are parsed only when
        /// present and may legally be NIL.
        /// </remarks>
        internal static async Task<IMAP_t_BodyStructure_e_SinglePart> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /*
                Single-Part BODYSTRUCTURE Variants
                RFC 3501, Section 7.4.2
                ------------------------

                A single-part BODYSTRUCTURE always begins with an atom representing the MIME
                type (e.g., "TEXT", "IMAGE", "APPLICATION", "AUDIO", "VIDEO", "MESSAGE").
                The syntax differs slightly depending on the type.

                1. Normal Single-Part
                ---------------------
                ("TYPE" "SUBTYPE" parameters id description encoding size
                    [md5]
                    [disposition]
                    [language]
                    [location]
                )

                Characteristics:
                - TYPE is anything except "TEXT" or "MESSAGE".
                - No line count field.
                - Optional fields may be NIL.

                Examples:
                  ("IMAGE" "JPEG" NIL NIL NIL "BASE64" 4096)
                  ("APPLICATION" "PDF" NIL NIL NIL "BASE64" 12000)

                2. TEXT Single-Part
                -------------------
                ("TEXT" "SUBTYPE" parameters id description encoding size lines
                    [md5]
                    [disposition]
                    [language]
                    [location]
                )

                Characteristics:
                - TYPE = "TEXT".
                - Includes a line count field immediately after size.
                - Optional fields may be NIL.

                Examples:
                  ("TEXT" "PLAIN" ("CHARSET" "UTF-8") NIL NIL "7BIT" 120 10)

                3. MESSAGE/RFC822 Single-Part
                -----------------------------
                ("MESSAGE" "RFC822" parameters id description encoding size lines
                    envelope
                    bodystructure
                    [md5]
                    [disposition]
                    [language]
                    [location]
                )

                Characteristics:
                - TYPE = "MESSAGE", SUBTYPE = "RFC822".
                - Includes line count.
                - Contains nested ENVELOPE and nested BODYSTRUCTURE.
                - Still treated as single-part.

                Example:
                  ("MESSAGE" "RFC822" NIL NIL NIL "7BIT" 2048 100
                      (ENVELOPE (...))
                      (BODYSTRUCTURE (...))
                  )

                Notes:
                ------
                - All three forms are single-part because the first item is an atom.
                - Only TEXT and MESSAGE/RFC822 include a line count.
                - Only MESSAGE/RFC822 includes nested ENVELOPE and nested BODYSTRUCTURE.
            */

            if(imapReader.ReadChar() != '('){
                throw new ParseException("Invalid BODYSTRUCTURE value.");
            }

            #region Common values

            string type = await imapReader.ReadStringAsync(cancellationToken) ?? "text";

            string subType = await imapReader.ReadStringAsync(cancellationToken) ?? "plain";

            var contentType = new MIME_h_ContentType(type + "/" + subType);
            // List of content-type parameters.
            if(imapReader.PeekIs('(')){
                imapReader.ReadChar();

                while(true){
                    if(imapReader.PeekIs(')')){
                        imapReader.ReadChar();

                        break;
                    }

                    string? paramName = await imapReader.ReadStringAsync(cancellationToken);

                    // Parameter value missing, not name value pair.
                    if(imapReader.PeekIs(')')){
                        imapReader.ReadChar();

                        if(!string.IsNullOrEmpty(paramName)){
                            contentType.Parameters[paramName] = null;
                        }

                        break;
                    }

                    string? paramValue = await imapReader.ReadStringAsync(cancellationToken);

                    if(!string.IsNullOrEmpty(paramName)){
                        if(paramValue != null){
                            contentType.Parameters[paramName] = MIME_Encoding_EncodedWord.DecodeTextS(paramValue);
                        }
                        else{
                            contentType.Parameters[paramName] = null;
                        }
                    }
                }
            }
            // We must have NIL here.
            else if(await imapReader.ReadStringAsync(cancellationToken) != null){
                throw new ParseException("Invalid BODYSTRUCTURE value.");
            }

            string? id = await imapReader.ReadStringAsync(cancellationToken);
            
            string? description = await imapReader.ReadStringAsync(cancellationToken);
            if(description != null){
                description = MIME_Encoding_EncodedWord.DecodeTextS(description);
            }

            string? encoding = await imapReader.ReadStringAsync(cancellationToken);

            string? sizeString = await imapReader.ReadStringAsync(cancellationToken);
            int size = 0;
            if(int.TryParse(sizeString,out int sizeO)){
                size = sizeO;
            }

            #endregion

            #region Text/xxx fields
            
            int? linesCount = null;
            if(string.Equals("TEXT",type,StringComparison.InvariantCultureIgnoreCase)){
                // lines
                string? linesCountString = await imapReader.ReadStringAsync(cancellationToken);
                if(int.TryParse(linesCountString,out int lCount)){
                    linesCount = lCount;
                }
            }

            #endregion
            
            #region MESSAGE/RFC822 fields

            IMAP_t_Envelope?      envelope      = null;
            IMAP_t_BodyStructure? bodyStructure = null;

            if(string.Equals("MESSAGE",type,StringComparison.OrdinalIgnoreCase) && string.Equals("RFC822",subType,StringComparison.OrdinalIgnoreCase)){
                // lines
                string? linesCountString = await imapReader.ReadStringAsync(cancellationToken);
                if(int.TryParse(linesCountString,out int lCount)){
                    linesCount = lCount;
                }

                // envelope
                if(imapReader.PeekIs("NIL")){
                    imapReader.ReadAtom();
                }
                else{
                    envelope = await IMAP_t_Envelope.ParseAsync(imapReader,cancellationToken);
                }

                // bodystructure
                if(imapReader.PeekIs("NIL")){
                    imapReader.ReadAtom();
                }
                else{
                    bodyStructure = await IMAP_t_BodyStructure.ParseAsync(imapReader,cancellationToken);
                }                
            }

            #endregion

            #region Optional value

            string?                    md5                = null;
            MIME_h_ContentDisposition? contentDisposition = null;
            string?                    language           = null;
            string?                    location           = null;

            // md5
            if(!imapReader.PeekIs(')')){
                md5 = await imapReader.ReadStringAsync(cancellationToken);
            }

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

            // Part end-tag.
            if(imapReader.ReadChar() != ')'){
                throw new ParseException("Invalid BODYSTRUCTURE value.");
            }

            return new IMAP_t_BodyStructure_e_SinglePart(
                contentType,
                id,
                description,
                encoding,
                size,
                linesCount,
                envelope,
                bodyStructure,
                md5,
                contentDisposition,
                language,
                location
                );
        }

        #endregion

        
        #region Properties implementation

        /// <summary>
        /// Gets the parsed Content-Type header for this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Returns the MIME media type and any associated parameters as provided
        /// by the IMAP BODYSTRUCTURE response. May be <c>null</c> if the server
        /// reported no Content-Type information.
        /// </remarks>
        public override MIME_h_ContentType? ContentType
        {
            get{ return m_pContentType; }
        }

        /// <summary>
        /// Gets the MIME <c>Content-ID</c> header value for this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the <c>id</c> field from the IMAP BODYSTRUCTURE response.
        /// May be <c>null</c> when the server reports <c>NIL</c>.
        /// </remarks>
        public string? ContentID
        {
            get{ return m_ContentID; }
        }

        /// <summary>
        /// Gets the MIME <c>Content-Description</c> header value for this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the <c>description</c> field from the IMAP BODYSTRUCTURE response.
        /// May be <c>null</c> when the server reports <c>NIL</c> or omits the header.
        /// </remarks>
        public string? ContentDescription
        {
            get{ return m_ContentDescription; }
        }

        /// <summary>
        /// Gets the MIME <c>Content-Transfer-Encoding</c> header value for this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the <c>encoding</c> field from the IMAP BODYSTRUCTURE response.
        /// May be <c>null</c> when the server reports <c>NIL</c> or omits the header.
        /// </remarks>
        public string? ContentTransferEncoding
        {
            get{ return m_ContentTransferEncoding; }
        }

        /// <summary>
        /// Gets the size of this BODYSTRUCTURE part in octets.
        /// </summary>
        /// <remarks>
        /// Represents the <c>size</c> field from the IMAP BODYSTRUCTURE response.
        /// This value reflects the number of bytes in the part's content and is
        /// always present in the server response. A value of <c>0</c> indicates
        /// that the server reported <c>NIL</c> or a non‑numeric size.
        /// </remarks>
        public long ContentSize
        {
            get{ return m_ContentSize; }
        }

        /// <summary>
        /// Gets the number of textual lines in this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the <c>lines</c> field from the IMAP BODYSTRUCTURE response.
        /// This value is present only for <c>TEXT</c> parts and <c>MESSAGE/RFC822</c>
        /// parts. It may be <c>null</c> when the server reports <c>NIL</c>.
        /// </remarks>
        public int? LinesCount
        {
            get{ return m_LinesCount; }
        }

        /// <summary>
        /// Gets the parsed ENVELOPE structure for this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Present only for <c>MESSAGE/RFC822</c> parts. Represents the nested
        /// IMAP ENVELOPE returned by the server, or <c>null</c> when the server
        /// reports <c>NIL</c>.
        /// </remarks>
        public IMAP_t_Envelope? Envelope
        {
            get{ return m_pEnvelope; }
        }

        /// <summary>
        /// Gets the nested BODYSTRUCTURE associated with this part.
        /// </summary>
        /// <remarks>
        /// Present only for <c>MESSAGE/RFC822</c> parts. Represents the embedded
        /// MIME structure of the message body, or <c>null</c> when the server
        /// reports <c>NIL</c>.
        /// </remarks>
        public IMAP_t_BodyStructure? BodyStructure
        {
            get{ return m_pBodyStructure; }
        }

        /// <summary>
        /// Gets the MD5 checksum value associated with this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the optional <c>md5</c> field from the IMAP BODYSTRUCTURE response.
        /// May be <c>null</c> when the server reports <c>NIL</c> or omits the value.
        /// </remarks>
        public string? Md5
        {
            get{ return m_Md5; }
        }

        /// <summary>
        /// Gets the MIME <c>Content-Disposition</c> header value for this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the optional <c>disposition</c> field from the IMAP BODYSTRUCTURE
        /// response, including any associated parameters. May be <c>null</c> when the
        /// server reports <c>NIL</c> or omits the header.
        /// </remarks>
        public override MIME_h_ContentDisposition? ContentDisposition
        {
            get{ return m_pContentDisposition; }
        }

        /// <summary>
        /// Gets the language value associated with this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the optional <c>language</c> field from the IMAP BODYSTRUCTURE
        /// response. May be <c>null</c> when the server reports <c>NIL</c> or does not
        /// provide a language specification.
        /// </remarks>
        public override string? Language
        {
            get{ return m_Language; }
        }

        /// <summary>
        /// Gets the location value associated with this BODYSTRUCTURE part.
        /// </summary>
        /// <remarks>
        /// Represents the optional <c>location</c> field from the IMAP BODYSTRUCTURE
        /// response. May be <c>null</c> when the server reports <c>NIL</c> or omits
        /// the value.
        /// </remarks>
        public override string? Location
        {
            get{ return m_Location; }
        }

        #endregion
    }
}
