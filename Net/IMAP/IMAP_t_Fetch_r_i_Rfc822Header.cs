using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static System.Collections.Specialized.BitVector32;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RFC822.HEADER</c> FETCH response data-item as
    /// defined in RFC 3501 section 7.4.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>RFC822.HEADER</c> data-item corresponds to the message header
    /// section only, excluding the message body. It provides access to the
    /// raw RFC822 header fields exactly as stored on the server.
    /// </para>
    /// <para>
    /// The class itself does not interpret, decode, or process header
    /// content; it simply represents the FETCH item within the IMAP client
    /// model and exposes the storage stream supplied by the caller.
    /// </para>
    /// </remarks>
    public class IMAP_t_Fetch_r_i_Rfc822Header : IMAP_t_Fetch_r_i
    {
        private Stream m_pStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_Rfc822Header"/>
        /// class using the provided stream.
        /// </summary>
        /// <param name="stream">
        /// The stream containing the RFC822.HEADER message data. This stream holds the full
        /// raw message content (headers) associated with the RFC822.HEADER FETCH
        /// data item as defined in RFC 3501 §7.4.2. The stream must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Fetch_r_i_Rfc822Header(Stream stream)
        {
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            m_pStream = stream;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses an <c>RFC822.HEADER</c> FETCH data item from an IMAP server response.
        /// This data item returns only the message header section, excluding the body.
        /// The returned header is provided exactly as stored on the server, without any
        /// decoding, transformation, or normalization.
        /// </summary>
        /// <param name="msgSeqNo">
        /// The message sequence number associated with this FETCH response.
        /// </param>
        /// <param name="imapReader">
        /// The IMAP lexical reader positioned at the <c>RFC822.HEADER</c> atom. The
        /// reader is responsible for parsing the literal header and reading the literal
        /// payload containing the raw RFC822 header fields.
        /// </param>
        /// <param name="getStoreStreamCallback">
        /// Optional callback that allows the caller to supply a destination stream for
        /// storing the literal payload. The callback receives the literal size before
        /// any data is read, enabling the caller to allocate an appropriate stream or
        /// reject oversized literals.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that may be used to cancel the literal read operation.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_t_Fetch_r_i_Rfc822Header"/> instance containing the raw
        /// RFC822 header data exactly as transmitted by the server. If the server
        /// returns <c>NIL</c>, the returned stream remains empty.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the <c>RFC822.HEADER</c> token is missing or malformed, or if the
        /// literal header is syntactically invalid.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying stream is closed unexpectedly while reading the
        /// literal payload.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Rfc822Header> ParseAsync(int msgSeqNo,_IMAP_Reader imapReader,EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /*
                RFC822.HEADER
                IMAP4rev1 – RFC 3501, Section 7.4.2

                Meaning:
                    Returns the message header only, excluding the message body.
                    This is equivalent to the BODY[HEADER] fetch section.

                Syntax:
                    RFC822.HEADER

                Returned Data:
                    A literal containing the raw RFC822 header fields exactly as
                    stored. No parsing, decoding, or transformation is performed
                    by the server. The header includes all fields up to the blank
                    line that separates the header from the body.

                    NIL is returned if the message contains no header.
            */


            if(!string.Equals("RFC822.HEADER",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: RFC822.HEADER data-item not found.");
            }

            var retValue = new IMAP_t_Fetch_r_i_Rfc822Header(new MemoryStreamEx());

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
        /// Gets the stream containing the RFC822.HEADER message data.
        /// </summary>
        /// <remarks>
        /// For the <c>RFC822.HEADER</c> FETCH data item (RFC 3501 §7.4.2), this stream
        /// holds the full raw message header content.
        /// </remarks>
        public Stream Stream
        {
            get{ return m_pStream; }
        }

        #endregion
    }
}
