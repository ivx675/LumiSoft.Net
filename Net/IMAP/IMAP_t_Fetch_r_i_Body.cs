using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a <c>BODY</c> data-item used in IMAP FETCH results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This object models the content associated with a <c>BODY</c> FETCH
    /// element as defined in RFC 3501. It provides access to the section
    /// identifier, optional starting offset, and the stream containing the
    /// raw octets of the referenced message portion.
    /// </para>
    /// <para>
    /// The section identifier corresponds to the text inside the square
    /// brackets of a <c>BODY[section]</c> specification. An empty string
    /// indicates that no section was specified, meaning the data-item refers
    /// to the complete message content associated with <c>BODY[]</c>.
    /// </para>
    /// <para>
    /// The offset value indicates the byte position at which the returned
    /// data begins. A value of <c>-1</c> signifies that no explicit offset
    /// was provided. When no offset is specified, the section is interpreted
    /// as beginning at position zero.
    /// </para>
    /// <para>
    /// The stream contains the exact literal bytes associated with the
    /// section and offset. No decoding, transformation, or interpretation is
    /// applied to the content stored in the stream.
    /// </para>
    /// </remarks>
    public class IMAP_t_Fetch_r_i_Body : IMAP_t_Fetch_r_i
    {
        private string? m_Section = "";
        private long?   m_Offset  = null;
        private Stream  m_pStream;

        /// <summary>
        /// Initializes a new instance of the <c>IMAP_t_Fetch_r_i_Body</c> class
        /// using the specified section identifier, optional offset value, and
        /// destination content stream.
        /// </summary>
        /// <param name="section">
        /// The section identifier associated with the <c>BODY</c> data‑item.
        /// A value of <c>null</c> indicates that no section was specified.
        /// </param>
        /// <param name="offset">
        /// The starting byte offset for the returned data. When this value is
        /// <c>null</c>, no explicit offset was specified and the section is
        /// interpreted as beginning at position zero.
        /// </param>
        /// <param name="stream">
        /// The stream into which the raw content associated with the
        /// <c>BODY</c> data‑item is written.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="offset"/> is non‑null and less than zero.
        /// </exception>
        public IMAP_t_Fetch_r_i_Body(string? section,long? offset,Stream stream)
        {
            if(offset != null && offset < 0){
                throw new ArgumentException("Arument 'offset' value must be >=0.",nameof(offset));
            }
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            m_Section = section;
            m_Offset  = offset;
            m_pStream = stream;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses a <c>BODY[]</c> or <c>BODY[section]</c> FETCH data item from an IMAP
        /// server response. This method handles full‑message fetches, section‑specific
        /// fetches, and partial fetches using the IMAP4rev1 literal mechanism.
        /// </summary>
        /// <param name="msgSeqNo">
        /// The message sequence number associated with this FETCH response.
        /// </param>
        /// <param name="imapReader">
        /// The IMAP lexical reader positioned at the <c>BODY</c> atom. The reader is
        /// responsible for parsing the section specifier, optional partial‑fetch
        /// offset, and the literal header that follows.
        /// </param>
        /// <param name="getStoreStreamCallback">
        /// Optional callback that allows the caller to supply a destination stream
        /// for storing the literal payload. The callback receives the literal size
        /// before any data is read, enabling the caller to allocate an appropriate
        /// stream or reject oversized literals.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that may be used to cancel the literal read operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_t_Fetch_r_i_Body"/> instance containing the parsed
        /// section specifier, optional partial‑fetch offset, and the stream into which
        /// the literal payload was written. If the server returns <c>NIL</c>, the
        /// returned stream remains empty.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the <c>BODY</c> token is missing, if the section specifier is
        /// malformed, if the partial‑fetch offset is invalid, or if the literal header
        /// is syntactically incorrect.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying stream is closed unexpectedly while reading the
        /// literal payload.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Body> ParseAsync(int msgSeqNo,_IMAP_Reader imapReader,EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /*
                BODY[]
                IMAP4rev1 – RFC 3501, Section 7.4.2 and Section 6.4.5

                Meaning:
                    Returns the entire message, including both the header and the
                    body. This is equivalent to the RFC822 data-item.

                Syntax (client request):
                    BODY[]
                    BODY[<section>]
                    BODY[<section>]<offset.length>

                Returned Data (server response):
                    The server returns a literal containing the requested data.
                    When a partial fetch is used, the server echoes ONLY the offset:

                        BODY[]<offset> {N}

                    The requested length is NOT included in the response. The literal
                    size {N} indicates how many bytes are actually returned, which may
                    be fewer than requested.

                    NIL is returned if the message or section has no content.

                Section Specifiers:
                    BODY[HEADER]
                        Returns only the message header.

                    BODY[TEXT]
                        Returns only the message body.

                    BODY[1]
                        Returns the first MIME part of a multipart message.

                    BODY[2.1]
                        Returns the first subpart of the second MIME part.

                    BODY[<section>.MIME]
                        Returns the MIME header of the specified part.

                    BODY[<section>.HEADER]
                        Returns the header of the specified part.

                    BODY[<section>.TEXT]
                        Returns the body of the specified part.

                Partial Fetch (client request):
                    BODY[]<offset.length>
                    BODY[<section>]<offset.length>

                    Returns a substring beginning at byte 'offset' and containing up
                    to 'length' bytes.

                Partial Fetch (server response):
                    BODY[]<offset> {N}
                    BODY[<section>]<offset> {N}

                    - Only the offset is echoed.
                    - The literal size {N} is the actual number of bytes returned.
                    - The server may return fewer bytes than requested.
            */

            if(!string.Equals("BODY",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: BODY[] data-item not found.");
            }

            string? section = imapReader.ReadBracketed();
            if(section == string.Empty){
                section = null;
            }

            long? offset = null;
            if(imapReader.PeekIs('<')){
                string partial = imapReader.ReadAtom();
                if(partial.Length < 3){
                    throw new ParseException("Invalid BODY[] offset value.");
                }
                partial = partial.Substring(1,partial.Length - 2);

                if(long.TryParse(partial,out long offsetParsed)){
                    offset = offsetParsed;
                }
                else{
                    throw new ParseException("Invalid BODY[] offset value.");
                }
            }

            var retValue = new IMAP_t_Fetch_r_i_Body(section,offset,new MemoryStreamEx());

            if(imapReader.PeekIs("NIL")){
                imapReader.ReadAtom();
            }
            else{
                // Allow user to set store stream.
                if(getStoreStreamCallback != null){
                    long literalSize = imapReader.PeekLiteralSize();
                    IMAP_e_Fetch_GetStoreStream e = new IMAP_e_Fetch_GetStoreStream(msgSeqNo,retValue,literalSize);
                    getStoreStreamCallback(null,e);
                    if(e.Stream != null){
                        retValue.SetStream(e.Stream);
                    }
                }

                await imapReader.ReadLiteralAsync(retValue.Stream,cancellationToken);
            }

            return retValue;
        }

        #endregion


        #region method SetStream

        /// <summary>
        /// Sets Stream property value.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null reference.</exception>
        internal void SetStream(Stream stream)
        {
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            m_pStream = stream;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the section identifier associated with a <c>BODY</c> data‑item.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value corresponds to the text inside the square brackets of a
        /// <c>BODY[section]</c> specification as defined in RFC 3501. Examples
        /// include <c>HEADER</c>, <c>TEXT</c>, <c>1</c>, <c>2.1</c>, and other
        /// MIME part identifiers.
        /// </para>
        /// <para>
        /// When this value is <c>null</c>, no section was specified (for example,
        /// <c>BODY[]</c>). In that case, the data‑item refers to the complete
        /// message content associated with <c>BODY[]</c>.
        /// </para>
        /// <para>
        /// The section identifier does not include any partial‑fetch offset
        /// (the <c>&lt;offset&gt;</c> or <c>&lt;offset.length&gt;</c> suffix).
        /// Offsets and lengths are represented separately.
        /// </para>
        /// </remarks>
        public string? BodySection
        {
            get{ return m_Section; }
        }

        /// <summary>
        /// Gets the starting byte offset associated with a <c>BODY</c> data‑item.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value represents the byte position from which the returned
        /// section begins. When this value is <c>null</c>, no explicit offset
        /// was specified and the section is interpreted as beginning at
        /// position zero.
        /// </para>
        /// <para>
        /// When the offset is non‑null, the FETCH request includes an explicit
        /// partial‑fetch specification using the syntax
        /// <c>BODY[section]&lt;offset[.length]&gt;</c>. The server returns data
        /// beginning at this byte position within the requested body section.
        /// </para>
        /// </remarks>
        public long? Offset
        {
            get{ return m_Offset; }
        }

        /// <summary>
        /// Gets the stream associated with the <c>BODY</c> data-item content.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The stream contains the exact octets of the message portion being
        /// processed, without any decoding, transformation, or interpretation.
        /// </para>
        /// </remarks>
        public Stream Stream
        {
            get{ return m_pStream; }
        }

        #endregion
    }
}
