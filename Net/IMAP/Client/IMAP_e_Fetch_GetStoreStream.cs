using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Client 
{
    /// <summary>
    /// Provides data for the FETCH getStoreStreamCallback callback, allowing the caller
    /// to supply a stream for storing the payload of a FETCH data item.
    /// </summary>
    /// <remarks>
    /// During a FETCH operation, certain data items (such as <c>RFC822</c>,
    /// <c>RFC822.HEADER</c>, <c>RFC822.TEXT</c>, <c>BODY[]</c>, or
    /// <c>BODY[section]</c>) return their content as IMAP literals. This event
    /// argument is used to let the caller specify the destination stream into
    /// which the literal data will be written.
    /// <para/>
    /// If no stream is assigned, the client will use its default storage
    /// mechanism for the data item.
    /// </remarks>
    public class IMAP_e_Fetch_GetStoreStream : EventArgs
    {
        private int              m_MsgSeqNo  = 0;
        private IMAP_t_Fetch_r_i m_pDataItem;
        private long             m_DataLength;
        private Stream?          m_pStream   = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_e_Fetch_GetStoreStream"/> class,
        /// providing information about the FETCH data item and the size of the incoming data
        /// that will be written to the storage stream.
        /// </summary>
        /// <param name="msgSeqNo">
        /// The message sequence number associated with this FETCH data item.
        /// </param>
        /// <param name="dataItem">
        /// The FETCH data item descriptor. Must not be <c>null</c>.
        /// </param>
        /// <param name="dataLength">
        /// The size of the incoming data (in bytes) that will be written to the provided
        /// <see cref="Stream"/>. This value allows the caller to allocate an appropriately
        /// sized stream or decide whether to store the data at all.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dataItem"/> is <c>null</c>.
        /// </exception>
        internal IMAP_e_Fetch_GetStoreStream(int msgSeqNo,IMAP_t_Fetch_r_i dataItem,long dataLength)
        {
            if(dataItem == null){
                throw new ArgumentNullException(nameof(dataItem));
            }

            m_MsgSeqNo   = msgSeqNo;
            m_pDataItem  = dataItem;
            m_DataLength = dataLength;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the message sequence number associated with the FETCH response.
        /// </summary>
        public int MessageSeqNo
        { 
            get { return m_MsgSeqNo; } 
        }

        /// <summary>
        /// Gets the FETCH data item for which a storage stream is being requested.
        /// </summary>
        public IMAP_t_Fetch_r_i DataItem
        { 
            get { return m_pDataItem; } 
        }

        /// <summary>
        /// Gets the size (in bytes) of the data that will be written to the
        /// provided <see cref="Stream"/>. 
        /// </summary>
        public long DataLength
        { 
            get { return m_DataLength; }
        }

        /// <summary>
        /// Gets or sets the stream into which the data item’s literal payload will
        /// be written. If not set, the client will store the data using its default
        /// behavior.
        /// </summary>
        public Stream? Stream
        { 
            get { return m_pStream; } 

            set { m_pStream = value; }
        }        

        #endregion
    }
}
