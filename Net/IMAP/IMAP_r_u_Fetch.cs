using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.IO;
using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.IMAP.Server;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a single <c>FETCH</c> result containing a message sequence
    /// number and its associated data-items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>FETCH</c> result consists of a 1‑based message sequence number and
    /// a collection of structured data-items describing various properties or
    /// contents of the message. These may include flags, envelope information,
    /// message size, body sections, internal date, and other standard or
    /// vendor‑specific fields.
    /// </para>
    /// <para>
    /// The class provides access to the sequence number and the complete set
    /// of data-items exactly as they appear within the <c>FETCH</c> result.
    /// No reordering or modification is applied.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Fetch : IMAP_r_u
    {
        private int                    m_MsgSeqNo   = 0;
        private List<IMAP_t_Fetch_r_i> m_pDataItems;

        /// <summary>
        /// Initializes a new <c>IMAP_r_u_Fetch</c> instance using the specified
        /// message sequence number and associated data-items.
        /// </summary>
        /// <param name="msgSeqNo">
        /// The 1‑based message sequence number. The value must be greater than or
        /// equal to one.
        /// </param>
        /// <param name="dataItems">
        /// The collection of data-items that form the contents of this
        /// <c>FETCH</c> result. The array must not be <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="msgSeqNo"/> is less than one.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="dataItems"/> is <c>null</c>.
        /// </exception>
        public IMAP_r_u_Fetch(int msgSeqNo,IMAP_t_Fetch_r_i[] dataItems)
        {
            if(msgSeqNo < 1){
                throw new ArgumentException("Argument 'msgSeqNo' value must be >= 1.","msgSeqNo");
            }
            if(dataItems == null){
                throw new ArgumentNullException("dataItems");
            }

            m_MsgSeqNo = msgSeqNo;

            m_pDataItems = new List<IMAP_t_Fetch_r_i>();
            m_pDataItems.AddRange(dataItems);
        }


        #region method ParseAsync

        /// <summary>
        /// Parses a single IMAP <c>FETCH</c> response and constructs an
        /// <see cref="IMAP_r_u_Fetch"/> object containing all returned data-items.
        /// </summary>
        /// <param name="imapReader">
        /// Reader used to obtain atoms, characters, literals, and structured
        /// tokens from the input stream.
        /// </param>
        /// <param name="getStoreStreamCallback">
        /// Optional callback that allows assignment of a custom storage stream
        /// for data-items that contain literal content.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to cancel the parsing operation.
        /// </param>
        /// <returns>
        /// A populated <see cref="IMAP_r_u_Fetch"/> instance containing all
        /// parsed data-items for the response.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the response does not conform to the expected
        /// <c>FETCH</c> structure or when an unsupported data-item is encountered.
        /// </exception>
        internal static async Task<IMAP_r_u_Fetch> ParseAsync(_IMAP_Reader imapReader,EventHandler<IMAP_e_Fetch_GetStoreStream>? getStoreStreamCallback,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* RFC 3501 7.4.2. FETCH Response.
                Example:    S: * 23 FETCH (FLAGS (\Seen) RFC822.SIZE 44827)
            */

            // Read response tag.
            if(imapReader.ReadAtom() != "*"){
                throw new ParseException("Invalid FETCH response: '*' is excpected.");
            }

            // Message sequnce number.
            string msgSeqNoString = imapReader.ReadAtom();
            if(!int.TryParse(msgSeqNoString,out int msgSeqNo)){
                throw new ParseException("Invalid FETCH response: Invalid message sequence number.");
            }

            // Read FETCH.
            if(!string.Equals("FETCH",imapReader.ReadAtom(),StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: 'FETCH' is excpected.");
            }

            // Read (.
            if(imapReader.ReadChar() != '('){
                throw new ParseException("Invalid FETCH response: '(' is excpected.");
            }

            List<IMAP_t_Fetch_r_i> dataItems = new List<IMAP_t_Fetch_r_i>();
            while(true){
                if(imapReader.PeekIs("BODY[")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Body.ParseAsync(msgSeqNo,imapReader,getStoreStreamCallback,cancellationToken));
                }
                else if(imapReader.PeekIs("BODYSTRUCTURE")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_BodyStructure.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("ENVELOPE")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Envelope.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("FLAGS")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Flags.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("INTERNALDATE")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_InternalDate.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("RFC822.HEADER")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Rfc822Header.ParseAsync(msgSeqNo,imapReader,getStoreStreamCallback,cancellationToken));
                }
                else if(imapReader.PeekIs("RFC822.SIZE")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Rfc822Size.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("RFC822.TEXT")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Rfc822Text.ParseAsync(msgSeqNo,imapReader,getStoreStreamCallback,cancellationToken));
                }
                else if(imapReader.PeekIs("RFC822")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Rfc822.ParseAsync(msgSeqNo,imapReader,getStoreStreamCallback,cancellationToken));                    
                }
                else if(imapReader.PeekIs("UID")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_Uid.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("X-GM-MSGID")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_xGmailMsgId.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("X-GM-THRID")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_xGmailThrId.ParseAsync(imapReader,cancellationToken));
                }
                else if(imapReader.PeekIs("X-GM-LABELS")){
                    dataItems.Add(await IMAP_t_Fetch_r_i_xGmailLabels.ParseAsync(imapReader,cancellationToken));
                }
                // ) - fetch closing.
                else if(imapReader.PeekIs(')')){
                    imapReader.ReadChar();

                    return new IMAP_r_u_Fetch(msgSeqNo,dataItems.ToArray());
                }
                else{
                    throw new ParseException("Not supported FETCH response data-item '" + imapReader.ReadAtom() + "'.");
                }
            }
        }

        #endregion


        #region override method ToStreamAsync

        /// <summary>
        /// Starts writing response to the specified stream.
        /// </summary>
        /// <param name="session">Owner IMAP session.</param>
        /// <param name="stream">Stream where to store response.</param>
        /// <param name="mailboxEncoding">Specifies how mailbox name is encoded.</param>
        /// <param name="completedAsyncCallback">Callback to be called when this method completes asynchronously.</param>
        /// <returns>Returns true is method completed asynchronously(the completedAsyncCallback is raised upon completion of the operation).
        /// Returns false if operation completed synchronously.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>stream</b> is null reference.</exception>
        protected override bool ToStreamAsync(IMAP_Session? session,Stream stream,IMAP_Mailbox_Encoding mailboxEncoding,EventHandler<EventArgs<Exception?>>? completedAsyncCallback)
        {
            if(stream == null){
                throw new ArgumentNullException("stream");
            }

            StringBuilder buffer = new StringBuilder();
            buffer.Append("* " + m_MsgSeqNo + " FETCH (");

            for(int i=0;i<m_pDataItems.Count;i++){
                IMAP_t_Fetch_r_i dataItem = m_pDataItems[i];

                if(i > 0){
                    buffer.Append(" ");
                }

                if(dataItem is IMAP_t_Fetch_r_i_Flags){
                    buffer.Append("FLAGS (" + ((IMAP_t_Fetch_r_i_Flags)dataItem).Flags.ToString() + ")");
                }
                else if(dataItem is IMAP_t_Fetch_r_i_Uid){
                    buffer.Append("UID " + ((IMAP_t_Fetch_r_i_Uid)dataItem).Uid.ToString());
                }
                else{
                    throw new NotImplementedException("Fetch response data-item '" + dataItem.ToString() + "' not implemented.");
                }
            }

            buffer.Append(")\r\n");
            
            string responseS = buffer.ToString();
            byte[] response  = Encoding.UTF8.GetBytes(responseS);

            // Log.
            if(session != null){
                session.LogAddWrite(response.Length,responseS.TrimEnd());
            }

            // Starts writing response to stream.
            IAsyncResult ar = stream.BeginWrite(
                response,
                0,
                response.Length,
                delegate(IAsyncResult r){                    
                    if(r.CompletedSynchronously){
                        return;
                    }

                    try{
                        stream.EndWrite(r);

                        if(completedAsyncCallback != null){
                            completedAsyncCallback(this,new EventArgs<Exception?>(null));
                        }
                    }
                    catch(Exception x){
                        if(completedAsyncCallback != null){
                            completedAsyncCallback(this,new EventArgs<Exception?>(x));
                        }
                    }
                },
                null
            );
            // Completed synchronously, process result.
            if(ar.CompletedSynchronously){
                stream.EndWrite(ar);

                return false;
            }
            // Completed asynchronously, stream.BeginWrite AsyncCallback will continue processing.
            else{
                return true;
            }
        }

        #endregion


        #region method FilterDataItem

        /// <summary>
        /// Returns specified data-item or null if no such item.
        /// </summary>
        /// <param name="dataItem">Data-item to filter.</param>
        /// <returns>Returns specified data-item or null if no such item.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>dataItem</b> is null reference.</exception>
        private IMAP_t_Fetch_r_i? FilterDataItem(Type dataItem)
        {
            if(dataItem == null){
                throw new ArgumentNullException("dataItem");
            }

            foreach(IMAP_t_Fetch_r_i item in m_pDataItems){
                if(item.GetType() == dataItem){
                    return item;
                }
            }

            return null;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the 1‑based message sequence number associated with this
        /// <c>FETCH</c> data-item.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The sequence number identifies the position of the message within the
        /// current mailbox view. Sequence numbers are assigned consecutively,
        /// starting at one, and may change when messages are added or removed.
        /// </para>
        /// <para>
        /// This value reflects the sequence number present in the parsed
        /// <c>FETCH</c> response and is not recalculated or adjusted.
        /// </para>
        /// </remarks>
        public int MessageSeqNo
        {
            get{ return m_MsgSeqNo; }
        }

        /// <summary>
        /// Gets the collection of data-items associated with this <c>FETCH</c>
        /// result.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each element represents a structured component of the <c>FETCH</c>
        /// result, such as flags, envelope information, message size, body
        /// sections, or other extensions defined by RFC 3501 or additional
        /// vendor-specific fields.
        /// </para>
        /// <para>
        /// The returned array reflects the items exactly as they appear within
        /// the <c>FETCH</c> result. No reordering or modification is applied.
        /// </para>
        /// </remarks>
        public IMAP_t_Fetch_r_i[] DataItems
        {
            get{ return m_pDataItems.ToArray(); }
        }

        /// <summary>
        /// Gets all <c>BODY[]</c> data-items associated with this <c>FETCH</c>
        /// result.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each element represents a <c>BODY</c> data-item without a section
        /// identifier. The returned array reflects the items exactly as they
        /// appear in the <c>FETCH</c> result.
        /// </para>
        /// </remarks>
        public IMAP_t_Fetch_r_i_Body[] BodyItems
        {
            get{
                List<IMAP_t_Fetch_r_i_Body> retVal = new List<IMAP_t_Fetch_r_i_Body>();
                foreach(IMAP_t_Fetch_r_i item in m_pDataItems){
                    if(item is IMAP_t_Fetch_r_i_Body){
                        retVal.Add((IMAP_t_Fetch_r_i_Body)item);
                    }
                }
                return retVal.ToArray(); 
            }
        }

        /// <summary>
        /// Gets the <c>BODY[]</c> data‑item associated with this <c>FETCH</c>
        /// result, or <c>null</c> if no such item is present.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <c>BODY[]</c> data‑item represents the message content without a
        /// section identifier. In typical <c>FETCH</c> results, only one such
        /// item is present.
        /// </para>
        /// <para>
        /// This property returns the first matching <c>BODY[]</c> item when
        /// available.
        /// </para>
        /// </remarks>
        public IMAP_t_Fetch_r_i_Body? Body
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Body)) as IMAP_t_Fetch_r_i_Body; }
        }
        
        /// <summary>
        /// Gets the <c>BODYSTRUCTURE</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_BodyStructure? BodyStructure
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_BodyStructure)) as IMAP_t_Fetch_r_i_BodyStructure; }
        }

        /// <summary>
        /// Gets the <c>ENVELOPE</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_Envelope? Envelope
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Envelope)) as IMAP_t_Fetch_r_i_Envelope; }
        }

        /// <summary>
        /// Gets the <c>FLAGS</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_Flags? Flags
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Flags)) as IMAP_t_Fetch_r_i_Flags; }
        }

        /// <summary>
        /// Gets the <c>INTERNALDATE</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_InternalDate? InternalDate
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_InternalDate)) as IMAP_t_Fetch_r_i_InternalDate; }
        }

        /// <summary>
        /// Gets the <c>RFC822</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_Rfc822? Rfc822
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Rfc822)) as IMAP_t_Fetch_r_i_Rfc822; }
        }

        /// <summary>
        /// Gets the <c>RFC822.HEADER</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_Rfc822Header? Rfc822Header
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Rfc822Header)) as IMAP_t_Fetch_r_i_Rfc822Header; }
        }

        /// <summary>
        /// Gets the <c>RFC822.SIZE</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_Rfc822Size? Rfc822Size
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Rfc822Size)) as IMAP_t_Fetch_r_i_Rfc822Size; }
        }

        /// <summary>
        /// Gets the <c>RFC822.TEXT</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_Rfc822Text? Rfc822Text
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Rfc822Text)) as IMAP_t_Fetch_r_i_Rfc822Text; }
        }

        /// <summary>
        /// Gets the <c>UID</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_Uid? Uid
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_Uid)) as IMAP_t_Fetch_r_i_Uid; }
        }

        /// <summary>
        /// Gets the <c>X-GM-MSGID</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_xGmailMsgId? X_GM_MSGID
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_xGmailMsgId)) as IMAP_t_Fetch_r_i_xGmailMsgId; }
        }

        /// <summary>
        /// Gets the <c>X-GM-THRID</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_xGmailThrId? X_GM_THRID
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_xGmailThrId)) as IMAP_t_Fetch_r_i_xGmailThrId; }
        }

        /// <summary>
        /// Gets the <c>X-GM-LABELS</c> data-item, or <c>null</c> if not present.
        /// </summary>
        public IMAP_t_Fetch_r_i_xGmailLabels? X_GM_LABELS
        {
            get{ return FilterDataItem(typeof(IMAP_t_Fetch_r_i_xGmailLabels)) as IMAP_t_Fetch_r_i_xGmailLabels; }
        }

        #endregion
    }
}
