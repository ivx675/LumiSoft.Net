using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>RFC822.SIZE</c> FETCH response data item.
    /// <para>
    /// The <c>RFC822.SIZE</c> attribute is defined in RFC 3501 section 7.4.2 as
    /// the size of the message in octets as stored on the server. It appears in
    /// FETCH responses in the form:
    /// <code>
    /// RFC822.SIZE SP number
    /// </code>
    /// where the value is a non‑negative decimal integer representing the full
    /// raw message size, including all headers and body content, exactly as it
    /// exists in the mailbox. The size may differ from the transmitted SMTP size
    /// due to transfer encodings or line ending normalization.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_r_i_Rfc822Size : IMAP_t_Fetch_r_i
    {
        private long m_Size = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_Rfc822Size"/> class
        /// using the parsed <c>RFC822.SIZE</c> value from a FETCH response.
        /// <para>
        /// The <c>RFC822.SIZE</c> attribute represents the size of the message in octets
        /// as stored on the server, as defined in RFC 3501 section 7.4.2. The value is a
        /// non‑negative decimal integer that includes the complete raw message content,
        /// including all headers and body data.
        /// </para>
        /// </summary>
        /// <param name="size">
        /// The message size in octets. Must be a non‑negative value.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="size"/> is negative.
        /// </exception>
        public IMAP_t_Fetch_r_i_Rfc822Size(long size)
        {
            if(size < 0){
                throw new ArgumentException("Argument 'size' value must be >= 0.","size");
            }

            m_Size = size;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the IMAP <c>RFC822.SIZE</c> FETCH response data item.
        /// <para>
        /// The <c>RFC822.SIZE</c> attribute is defined in RFC 3501 section 7.4.2 as
        /// the size of the message in octets as stored on the server. It appears in
        /// FETCH responses in the form:
        /// <code>
        /// RFC822.SIZE SP number
        /// </code>
        /// where the value is a non‑negative decimal integer representing the full
        /// message size, including all headers and body content. The size may differ
        /// from the transmitted SMTP size due to transfer encodings or line ending
        /// normalization.
        /// </para>
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the start of the RFC822.SIZE data item.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token.
        /// </param>
        /// <returns>
        /// A <see cref="IMAP_t_Fetch_r_i_Rfc822Size"/> instance containing the parsed
        /// message size in octets.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the RFC822.SIZE data item is missing or the numeric value is
        /// malformed.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_Rfc822Size> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* RFC 3501 7.4.2. FETCH Response – RFC822.SIZE data item.
               Syntax: RFC822.SIZE SP number
               Meaning: The size of the message in octets as stored on the server.
               Notes:
                 • The value represents the full message size, including all headers
                   and body content, exactly as it exists in the mailbox.
                 • Returned as a non‑negative decimal number.
                 • The size may differ from the size of the message as transmitted
                   over SMTP due to transfer encodings or line ending normalization.
               Example:
                 S: * 23 FETCH (RFC822.SIZE 42891)
            */

            if(!string.Equals("RFC822.SIZE", imapReader.ReadAtom(), StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: RFC822.SIZE data-item not found.");
            }

            string sizeString = imapReader.ReadAtom();
            if(!long.TryParse(sizeString, out long size)){
                throw new ParseException("Invalid FETCH RFC822.SIZE data-item: malformed RFC822.SIZE value.");
            }

            return new IMAP_t_Fetch_r_i_Rfc822Size(size);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP <c>RFC822.SIZE</c> value parsed from the FETCH response.
        /// <para>
        /// The <c>RFC822.SIZE</c> attribute represents the size of the message in
        /// octets as stored on the server, as defined in RFC 3501 section 7.4.2.
        /// The value includes the complete raw message content, including all
        /// headers and body data, exactly as it exists in the mailbox.
        /// </para>
        /// </summary>
        public long Size
        {
            get{ return m_Size; }
        }

        #endregion
    }
}
