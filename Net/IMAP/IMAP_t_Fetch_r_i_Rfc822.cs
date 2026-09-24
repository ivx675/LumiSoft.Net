using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RFC822</c> FETCH response data item.
    /// </summary>
    /// <remarks>
    /// The <c>RFC822</c> data item is defined in RFC 3501 §7.4.2 and corresponds
    /// to the full raw message content, including both headers and body, returned
    /// as a single literal. It is functionally equivalent to requesting
    /// <c>BODY[]</c>.
    /// </remarks>
    public class IMAP_t_Fetch_r_i_Rfc822 : IMAP_t_Fetch_r_i
    {
        private Stream m_pStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_Rfc822"/>
        /// class using the specified stream.
        /// </summary>
        /// <param name="stream">
        /// The stream containing the RFC822 message data. This stream holds the full
        /// raw message content (headers and body) associated with the RFC822 FETCH
        /// data item as defined in RFC 3501 §7.4.2. The stream must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Fetch_r_i_Rfc822(Stream stream)
        {
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            m_pStream = stream;
        }


        #region method ParseAsync

        /// <summary>
        /// Parses an <c>RFC822</c> FETCH data item from an IMAP server response.
        /// This data item returns the full raw message content, including both the
        /// header and the body, exactly as stored on the server. No MIME parsing,
        /// decoding, or transformation is performed.
        /// </summary>
        /// <param name="msgSeqNo">
        /// The message sequence number associated with this FETCH response.
        /// </param>
        /// <param name="imapReader">
        /// The IMAP lexical reader positioned at the <c>RFC822</c> atom. The reader
        /// is responsible for parsing the literal header and reading the literal
        /// payload containing the complete RFC822 message.
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
        /// An <see cref="IMAP_t_Fetch_r_i_Rfc822"/> instance containing the full
        /// RFC822 message exactly as transmitted by the server. If the server returns
        /// <c>NIL</c>, the returned stream remains empty.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the <c>RFC822</c> token is missing or malformed, or if the
        /// literal header is syntactically invalid.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying stream is closed unexpectedly while reading the
        /// literal payload.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Rfc822> ParseAsync(int msgSeqNo,_IMAP_Reader imapReader,EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /*  RFC 3501 7.4.2 RFC822
             
                Syntax: RFC822 {literal} / NIL
                
                Returns the full message content (headers + body) as a literal.
                NIL → no data.
            */

            if(!string.Equals("RFC822",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: RFC822 data-item not found.");
            }

            var retValue = new IMAP_t_Fetch_r_i_Rfc822(new MemoryStreamEx());

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
        /// Gets the stream containing the RFC822 message data.
        /// </summary>
        /// <remarks>
        /// For the <c>RFC822</c> FETCH data item (RFC 3501 §7.4.2), this stream
        /// holds the full raw message content returned as a literal, including both
        /// headers and body.
        /// </remarks>
        public Stream Stream
        {
            get{ return m_pStream; }
        }

        #endregion
    }
}
