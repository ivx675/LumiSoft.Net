using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RFC822.TEXT</c> FETCH response data-item as
    /// defined in RFC 3501 section 7.4.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>RFC822.TEXT</c> data-item corresponds to the message body
    /// only, excluding all header fields. It may be used in contexts where
    /// the full message is not required.
    /// </para>
    /// <para>
    /// The class itself does not interpret or decode message content; it
    /// simply provides a structured representation of the FETCH item within
    /// the IMAP client model.
    /// </para>
    /// </remarks>
    public class IMAP_t_Fetch_r_i_Rfc822Text : IMAP_t_Fetch_r_i
    {
        private Stream m_pStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_Rfc822Text"/>
        /// class using the provided stream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The supplied stream serves as the storage destination for any data
        /// associated with this FETCH data-item. Depending on how the instance
        /// is used, the stream may later receive message body content returned
        /// by an IMAP server, or it may remain unused if no data is written.
        /// </para>
        /// <para>
        /// The constructor does not perform any parsing or interpretation of
        /// IMAP data. It simply associates the instance with a writable stream
        /// provided by the caller.
        /// </para>
        /// </remarks>
        /// <param name="stream">
        /// A writable stream intended to receive the data for this FETCH
        /// data-item. Must not be <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Fetch_r_i_Rfc822Text(Stream stream)
        {
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            m_pStream = stream;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses an <c>RFC822.TEXT</c> FETCH data item from an IMAP server response.
        /// This data item returns only the message body, excluding all header fields.
        /// The returned body is provided exactly as stored on the server, without any
        /// MIME decoding, charset conversion, or transformation.
        /// </summary>
        /// <param name="msgSeqNo">
        /// The message sequence number associated with this FETCH response.
        /// </param>
        /// <param name="imapReader">
        /// The IMAP lexical reader positioned at the <c>RFC822.TEXT</c> atom. The
        /// reader is responsible for parsing the literal header and reading the
        /// literal payload containing the raw message body.
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
        /// An <see cref="IMAP_t_Fetch_r_i_Rfc822Text"/> instance containing the raw
        /// RFC822 message body exactly as transmitted by the server. If the server
        /// returns <c>NIL</c>, the returned stream remains empty.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the <c>RFC822.TEXT</c> token is missing or malformed, or if the
        /// literal header is syntactically invalid.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying stream is closed unexpectedly while reading the
        /// literal payload.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Rfc822Text> ParseAsync(int msgSeqNo,_IMAP_Reader imapReader,EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /*
                RFC822.TEXT IMAP4rev1 – RFC 3501, Section 7.4.2

                Meaning:
                    Returns the message body only, excluding all header fields.
                    This is equivalent to the BODY[TEXT] fetch section.

                Syntax:
                    RFC822.TEXT

                Returned Data:
                    A literal containing the raw message body as transmitted.
                    No MIME parsing or decoding is performed by the server.
                    NIL is returned if the message has no body.
            */

            if(!string.Equals("RFC822.TEXT",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: RFC822.TEXT data-item not found.");
            }

            var retValue = new IMAP_t_Fetch_r_i_Rfc822Text(new MemoryStreamEx());

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
        /// Gets the stream containing the RFC822.TEXT message data.
        /// </summary>
        public Stream Stream
        {
            get{ return m_pStream; }
        }

        #endregion
    }
}
