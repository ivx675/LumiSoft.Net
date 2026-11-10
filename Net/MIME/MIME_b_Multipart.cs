using LumiSoft.Net.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME body with a media type whose top-level type is
    /// <c>multipart</c>.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 2046 section 5.1. A multipart body contains one or more
    /// child MIME entities separated by a boundary delimiter. A multipart
    /// body must contain at least one body part.
    /// </remarks>
    public class MIME_b_Multipart : MIME_b
    {
        private MIME_EntityCollection m_pBodyParts;
        private string                m_TextPreamble = "";
        private string                m_TextEpilogue = "";
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b_Multipart"/> class.
        /// </summary>
        /// <param name="contentType">
        /// Multipart Content-Type header value.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="contentType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="contentType"/> does not contain the
        /// required <c>boundary</c> parameter.
        /// </exception>
        public MIME_b_Multipart(MIME_h_ContentType contentType) : base(contentType)
        {
            if(contentType == null){
                throw new ArgumentNullException(nameof(contentType));
            }
            if(string.IsNullOrEmpty(contentType.Param_Boundary)){
                throw new ArgumentException("Argument 'contentType' doesn't contain required boundary parameter.",nameof(contentType));
            }

            m_pBodyParts = new MIME_EntityCollection();
        }

        /// <summary>
        /// Internal constructor. No Content-Type is created, user must do it manually.
        /// </summary>
        internal MIME_b_Multipart() 
        {
            m_pBodyParts = new MIME_EntityCollection();
        }

        #region override method Dispose

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        internal override void Dispose()
        {
            foreach(var bodyPart in m_pBodyParts) {
                bodyPart.Dispose();
            }
        }

        #endregion


        #region static method ParseAsync

        /// <summary>
        /// Parses a multipart MIME body from the specified stream.
        /// </summary>
        /// <param name="owner">Owner MIME entity.</param>
        /// <param name="stream">
        /// Stream containing the encoded body data.
        /// </param>
        /// <returns>
        /// Returns the parsed MIME body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="owner"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the multipart entity is missing the required
        /// <c>boundary</c> parameter.
        /// </exception>
        internal static async Task<MIME_b> ParseAsync(MIME_Entity owner,Stream stream)
        {
            if(owner == null){
                throw new ArgumentNullException("owner");
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }
            if(owner.ContentType == null || owner.ContentType.Param_Boundary == null){
                throw new ParseException("Multipart Content-Type is missing the required 'boundary' parameter.");
            }
          
            MIME_b_Multipart retVal = new MIME_b_Multipart(owner.ContentType);
            await retVal.ParseInternalAsync(owner,owner.ContentType.Param_Boundary,stream);

            return retVal;
        }

        #endregion
                     

        #region method ParseInternalAsync

        internal async Task ParseInternalAsync(MIME_Entity owner,string boundaryId,Stream stream)
        {
            // =========================================================================
            // RFC 2046 BOUNDARY SYNTAX RULES (Section 5.1.1)
            // -------------------------------------------------------------------------
            // 1. DASH-BOUNDARY: --boundaryId
            // 2. ENCAPSULATION BOUNDARY: \r\n--boundaryId
            // 3. CLOSING BOUNDARY: \r\n--boundaryId--
            // =========================================================================

            List<Stream> bodyParts = new List<Stream>();
            Stream? preambleStream = null;
            Stream? epilogueStream = null;

            byte[] boundaryBytes = Encoding.UTF8.GetBytes("\r\n--" + boundaryId);
            byte[] openingBoundaryBytes = Encoding.UTF8.GetBytes("--" + boundaryId);
    
            int boundaryLen = boundaryBytes.Length; 
            int standardDashBoundaryLen = 2 + boundaryId.Length; 
            int maxBoundaryMarkerLen = boundaryLen + 4;

            const int bufferSize = 64000;
            byte[] buffer = new byte[bufferSize];

            int bytesInBuffer = 0;
            long absoluteBufferStart = 0;
            long previousBoundaryEndPos = -1;
            long firstBoundaryAbsPos = -1;
            int searchOffset = 0;

            while(true){
                // Shift leftover unparsed bytes to the front of the buffer
                if(searchOffset < bytesInBuffer){
                    int unparsedCount = bytesInBuffer - searchOffset;
                    Array.Copy(buffer, searchOffset, buffer, 0, unparsedCount);
                    absoluteBufferStart += searchOffset;
                    bytesInBuffer = unparsedCount;
                }
                // Buffer was fully consumed; align absolute start and reset buffer count
                else{
                    absoluteBufferStart = stream.Position - bytesInBuffer;
                    if (absoluteBufferStart < 0) absoluteBufferStart = 0;
                    bytesInBuffer = 0;
                }

                int bytesRead = await stream.ReadAsync(buffer.AsMemory(bytesInBuffer, bufferSize - bytesInBuffer));
                if(bytesRead == 0 && bytesInBuffer == 0){
                    break;
                }

                bytesInBuffer += bytesRead;
                searchOffset = 0; 

                ReadOnlySpan<byte> span = buffer.AsSpan(0, bytesInBuffer);

                while(true){
                    int absoluteIndex = -1;
                    int dashBoundaryLen = standardDashBoundaryLen;
                    bool matched = false;

                    // 1. Check if we are at the absolute start of the stream looking for the opening boundary
                    if(firstBoundaryAbsPos == -1 && absoluteBufferStart == 0 && searchOffset == 0){
                        if(span.Length < openingBoundaryBytes.Length){
                            // Not enough bytes yet in the first chunk to verify the opening boundary;
                            // break out to read more data from the stream.
                            break;
                        }

                        if(span.Slice(0,openingBoundaryBytes.Length).SequenceEqual(openingBoundaryBytes)){
                            absoluteIndex = 0;
                            dashBoundaryLen = openingBoundaryBytes.Length;
                            matched = true;
                        }
                    }

                    // 2. If not matched at start, search for standard encapsulation boundary (\r\n--boundaryId)
                    if(!matched){
                        int relativeIndex = span.Slice(searchOffset).IndexOf(boundaryBytes);
                        if(relativeIndex == -1){
                            searchOffset = Math.Max(0, bytesInBuffer - maxBoundaryMarkerLen);
                            break; 
                        }

                        absoluteIndex = searchOffset + relativeIndex;
                    }
            
                    // Ensures we have at least 2 bytes remaining in the buffer after the boundary ID 
                    // to read its trailer (\r\n or --). If the buffer cuts off here, pause and read more.
                    int afterBoundaryIndex = absoluteIndex + (absoluteIndex == 0 ? 0 : 2) + dashBoundaryLen;
                    if(afterBoundaryIndex + 2 > bytesInBuffer){
                        searchOffset = absoluteIndex;
                        break;
                    }

                    ReadOnlySpan<byte> trailer = span.Slice(afterBoundaryIndex, 2);

                    bool isClosing = trailer[0] == (byte)'-' && trailer[1] == (byte)'-';
                    bool isCRLF = trailer[0] == (byte)'\r' && trailer[1] == (byte)'\n';

                    long absMatchPos = absoluteBufferStart + absoluteIndex + (absoluteIndex == 0 ? 0 : 2);

                    if(isCRLF || isClosing){
                        int trailerLen = 2;
                        searchOffset = (absoluteIndex + (absoluteIndex == 0 ? 0 : 2)) + dashBoundaryLen + trailerLen;

                        if(firstBoundaryAbsPos == -1){
                            firstBoundaryAbsPos = absMatchPos;

                            long preambleLength = firstBoundaryAbsPos > 2 ? firstBoundaryAbsPos - 2 : firstBoundaryAbsPos;
                            if(preambleLength > 0){
                                preambleStream = new BoundedStream(stream, 0, preambleLength);
                            }
                        }

                        if(previousBoundaryEndPos != -1){
                            long currentPartEndPos = absMatchPos - 2; 
                            long partLength = currentPartEndPos - previousBoundaryEndPos;
                    
                            bodyParts.Add(new BoundedStream(stream, previousBoundaryEndPos, partLength));
                        }

                        if(isClosing){
                            if(searchOffset + 1 < bytesInBuffer && span[searchOffset] == (byte)'\r' && span[searchOffset + 1] == (byte)'\n'){
                                searchOffset += 2;
                            }

                            long epilogueStart = absoluteBufferStart + searchOffset;
                            long epilogueLength = stream.Length - epilogueStart;

                            if(epilogueLength > 0){
                                epilogueStream = new BoundedStream(stream, epilogueStart, epilogueLength);
                            }

                            break;
                        }

                        previousBoundaryEndPos = absMatchPos + dashBoundaryLen + trailerLen;
                    }
                    else{
                        searchOffset = afterBoundaryIndex;
                    }
                }

                if(bytesRead == 0){
                    break;
                }
            }

            foreach(var bodyPart in bodyParts){
                bodyPart.Position = 0;
                MIME_Entity entity = new MIME_Entity();
                await entity.ParseInternalAsync(bodyPart);
                m_pBodyParts.Add(entity);
                entity.SetParent(owner);
            }

            if(preambleStream != null){
                preambleStream.Position = 0;
                if(preambleStream.Length < 1000){
                    using var reader = new StreamReader(preambleStream, Encoding.UTF8,leaveOpen: true);
                    m_TextPreamble = reader.ReadToEnd();
                }
            }

            if(epilogueStream != null){
                epilogueStream.Position = 0;
                if(epilogueStream.Length < 1000){
                    using var reader = new StreamReader(epilogueStream, Encoding.UTF8, leaveOpen: true);
                    m_TextEpilogue = reader.ReadToEnd();
                }
            }

            m_pBodyParts.SetModified(false);
        }

        #endregion


        #region override SetParent

        /// <summary>
        /// Sets body parent.
        /// </summary>
        /// <param name="entity">Owner entity.</param>
        /// <param name="setContentType">If true sets entity.ContentType header value.</param>
        internal override void SetParent(MIME_Entity entity,bool setContentType)
        {
            base.SetParent(entity,setContentType);

            // Owner entity has no content-type or has different content-type, just add/overwrite it.
            if(setContentType && (entity.ContentType == null || !string.Equals(entity.ContentType.TypeWithSubtype,this.MediaType,StringComparison.InvariantCultureIgnoreCase))){
                entity.ContentType = this.ContentType;
            }
        }

        #endregion

        #region method ToStreamAsync

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">
        /// Raised when the multipart body is not bound to a MIME entity or the
        /// owner MIME entity does not have a Content-Type header.
        /// </exception>
        internal protected override async Task ToStreamAsync(Stream stream,bool utf8Headers,bool reEncodeHeaders,CancellationToken cancellationToken = default)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            if(this.Entity == null){
                throw new InvalidOperationException("Multipart body is not bound to any MIME entity.");
            }
            if(this.Entity.ContentType == null){
                throw new InvalidOperationException("Multipart body owner entity ContentType is not specified.");
            }

            /* RFC 2046 5.1.1.
                NOTE:  The CRLF preceding the boundary delimiter line is conceptually
                attached to the boundary so that it is possible to have a part that
                does not end with a CRLF (line  break). 
            
                dash-boundary := "--" boundary
                 
                multipart-body := [preamble CRLF]
                                  dash-boundary transport-padding CRLF
                                  body-part *encapsulation
                                  close-delimiter transport-padding
                                  [CRLF epilogue]
                 
                encapsulation := delimiter transport-padding
                                 CRLF body-part

                delimiter := CRLF dash-boundary

                close-delimiter := delimiter "--"
            */

            // Set "preamble" text if any.
            if (!string.IsNullOrEmpty(m_TextPreamble)){
                string preamble = m_TextPreamble.EndsWith("\r\n") ? m_TextPreamble : m_TextPreamble + "\r\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(preamble),cancellationToken);
            }

            for(int i=0;i<m_pBodyParts.Count;i++){
                MIME_Entity bodyPart = m_pBodyParts[i];

                // First boundary has no preceeding CRLF.
                if(i == 0){
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("--" + this.Entity.ContentType.Param_Boundary + "\r\n"), cancellationToken);
                }
                // Boundaries > 1.
                else{
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("\r\n--" + this.Entity.ContentType.Param_Boundary + "\r\n"), cancellationToken);
                }
                                
                await bodyPart.ToStreamAsync(stream,utf8Headers,reEncodeHeaders,cancellationToken);

                // Last body part, close boundary.
                if(i == (m_pBodyParts.Count - 1)){
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("\r\n--" + this.Entity.ContentType.Param_Boundary + "--"), cancellationToken);
                }
            }

            // Set "epilogoue" text if any.
            if(!string.IsNullOrEmpty(m_TextEpilogue)){
                string epilogoue = m_TextEpilogue.StartsWith("\r\n") ? m_TextEpilogue : "\r\n" + m_TextEpilogue;
                await stream.WriteAsync(Encoding.UTF8.GetBytes(epilogoue), cancellationToken);
            }
        }

        #endregion
        

        #region Properties implementation
        
        /// <summary>
        /// Gets a value indicating whether any child MIME entity has been modified.
        /// </summary>
        public override bool IsModified
        {
            get{ return m_pBodyParts.IsModified; }
        }

        /// <summary>
        /// Gets the collection of child MIME entities contained in this multipart
        /// body.
        /// </summary>
        /// <remarks>
        /// Child MIME entities of a multipart body are referred to as
        /// <i>body parts</i> in RFC 2046.
        /// </remarks>
        public MIME_EntityCollection BodyParts
        {
            get{ return m_pBodyParts; }
        }

        /// <summary>
        /// Gets or sets the multipart preamble text.
        /// </summary>
        /// <remarks>
        /// Defined by RFC 2046 section 5.1.1. The preamble is the text that
        /// appears between the MIME entity headers and the first multipart
        /// boundary delimiter.
        /// </remarks>
        public string TextPreamble
        {
            get{ return m_TextPreamble; }

            set{ m_TextPreamble = value; }
        }

        /// <summary>
        /// Gets or sets the multipart epilogue text.
        /// </summary>
        /// <remarks>
        /// Defined by RFC 2046 section 5.1.1. The epilogue follows the closing
        /// multipart boundary delimiter and may be ignored by MIME-compliant
        /// implementations.
        /// </remarks>
        public string TextEpilogue
        {
            get{ return m_TextEpilogue; }

            set{ m_TextEpilogue = value; }
        }

        #endregion
    }
}
