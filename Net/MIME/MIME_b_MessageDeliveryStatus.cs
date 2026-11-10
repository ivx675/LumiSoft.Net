using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME <c>message/delivery-status</c> body as defined in
    /// RFC 3464.
    /// </summary>
    /// <remarks>
    /// A <c>message/delivery-status</c> body contains delivery status
    /// notification (DSN) fields describing the delivery status of a
    /// message. It consists of a per-message field block followed by one or
    /// more per-recipient field blocks.
    ///
    /// <code>
    /// delivery-status-content = per-message-fields
    ///                           1*( CRLF per-recipient-fields )
    ///
    /// per-message-fields =
    ///            [ original-envelope-id-field CRLF ]
    ///            reporting-mta-field CRLF
    ///            [ dsn-gateway-field CRLF ]
    ///            [ received-from-mta-field CRLF ]
    ///            [ arrival-date-field CRLF ]
    ///            *( extension-field CRLF )
    ///
    /// per-recipient-fields =
    ///            [ original-recipient-field CRLF ]
    ///            final-recipient-field CRLF
    ///            action-field CRLF
    ///            status-field CRLF
    ///            [ remote-mta-field CRLF ]
    ///            [ diagnostic-code-field CRLF ]
    ///            [ last-attempt-date-field CRLF ]
    ///            [ final-log-id-field CRLF ]
    ///            [ will-retry-until-field CRLF ]
    ///            *( extension-field CRLF )
    /// </code>
    /// </remarks>
    public class MIME_b_MessageDeliveryStatus : MIME_b
    {
        private MIME_h_Collection       m_pMessageFields;
        private List<MIME_h_Collection> m_pRecipientBlocks;
        
        /// <summary>
        /// Default constructor.
        /// </summary>
        public MIME_b_MessageDeliveryStatus() : base(new MIME_h_ContentType("message/delivery-status"))
        {
            m_pMessageFields   = new MIME_h_Collection(new MIME_h_Provider());
            m_pRecipientBlocks = new List<MIME_h_Collection>();
        }


        #region static method Parse

        /// <summary>
        /// Parses a <c>message/delivery-status</c> body from the specified
        /// stream.
        /// </summary>
        /// <param name="owner">
        /// Owner MIME entity.
        /// </param>
        /// <param name="stream">
        /// Stream from which to read the body data.
        /// </param>
        /// <returns>
        /// Returns the parsed body.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="owner"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the body contains invalid data that cannot be parsed.
        /// </exception>
        internal static MIME_b Parse(MIME_Entity owner,Stream stream)
        {
            if(owner == null){
                throw new ArgumentNullException(nameof(owner));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            string data;
            using(var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true))
            {
                data = reader.ReadToEnd();
            }
            string[] blocks = data.Split(["\r\n\r\n"],StringSplitOptions.RemoveEmptyEntries);

            MIME_b_MessageDeliveryStatus retVal = new MIME_b_MessageDeliveryStatus();
            for(int i = 0; i < blocks.Length; i++) {
                if(i == 0) {
                    retVal.m_pMessageFields.ParseFromString(blocks[i]);
                }
                else{
                    MIME_h_Collection recipientFields = new MIME_h_Collection(new MIME_h_Provider());
                    recipientFields.ParseFromString(blocks[i]);
                    retVal.m_pRecipientBlocks.Add(recipientFields);
                }
            }

            return retVal;
        }

        #endregion


        #region method ToStreamAsync

        /// <inheritdoc/>
        internal protected override async Task ToStreamAsync(Stream stream,bool utf8Headers,bool reEncodeHeaders,CancellationToken cancellationToken = default)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            
            await m_pMessageFields.ToStreamAsync(stream, utf8Headers, reEncodeHeaders, cancellationToken);
            stream.Write(new byte[]{(byte)'\r',(byte)'\n'},0,2);
            foreach(MIME_h_Collection recipientBlock in m_pRecipientBlocks){
                await recipientBlock.ToStreamAsync(stream,utf8Headers,reEncodeHeaders,cancellationToken);
                stream.Write(new byte[]{(byte)'\r',(byte)'\n'},0,2);
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether any delivery status fields have been
        /// modified.
        /// </summary>
        public override bool IsModified
        {
            get{ 
                if(m_pMessageFields.IsModified){
                    return true;
                }
                foreach(MIME_h_Collection recipientBlock in m_pRecipientBlocks){
                    if(recipientBlock.IsModified){
                        return true;
                    }
                }

                return false; 
            }
        }

        /// <summary>
        /// Gets the collection of per-recipient field blocks.
        /// </summary>
        /// <remarks>
        /// Each item represents the fields associated with a single recipient.
        /// </remarks>
        public MIME_h_Collection MessageFields
        {
            get{ return m_pMessageFields; }
        }
        
        /// <summary>
        /// Gets reciepent report blocks collection.
        /// </summary>
        /// <remarks>Each block contains per-recipient-fields.</remarks>
        public List<MIME_h_Collection> RecipientBlocks
        {
            get{ return m_pRecipientBlocks; }
        }

        #endregion
    }
}
