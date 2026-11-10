using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents an RFC 5322 address list.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 5322, section 3.4. An address list contains mailboxes
    /// and groups separated by commas.
    ///
    /// <code>
    /// address-list = address *(&quot;,&quot; address)
    /// address      = mailbox / group
    /// </code>
    /// </remarks>
    public class Mail_t_AddressList : IEnumerable<Mail_t_Address>
    {
        private bool                 m_IsModified = false;
        private List<Mail_t_Address> m_pList;

        /// <summary>
        /// Initializes an empty address list.
        /// </summary>
        public Mail_t_AddressList()
        {
            m_pList = new List<Mail_t_Address>();
        }


        #region static method Parse

        /// <summary>
        /// Parses an address list from the specified string.
        /// </summary>
        /// <param name="value">The serialized address list.</param>
        /// <returns>The parsed address list.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> is not a valid address list.</exception>
        public static Mail_t_AddressList Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            /* RFC 5322 3.4.
                address         =   mailbox / group
                mailbox         =   name-addr / addr-spec
                name-addr       =   [display-name] angle-addr
                angle-addr      =   [CFWS] "<" addr-spec ">" [CFWS] / obs-angle-addr
                group           =   display-name ":" [group-list] ";" [CFWS]
                display-name    =   phrase
                mailbox-list    =   (mailbox *("," mailbox)) / obs-mbox-list
                address-list    =   (address *("," address)) / obs-addr-list
                group-list      =   mailbox-list / CFWS / obs-group-list
            */

            MIME_Reader        r      = new MIME_Reader(value);
            Mail_t_AddressList retVal = new Mail_t_AddressList();
            while(true){
                string? word = r.QuotedReadToDelimiter(new char[]{',','<',':'});
                // We processed all data.
                if(word == null){
                    break;
                }
                // group
                else if(r.Peek(true) == ':'){
                    string? displayName = word != null ? MIME_Encoding_EncodedWord.DecodeS(TextUtils.UnQuoteString(word)) : null;
                    
                    // Consume ':'
                    r.Char(true);
           
                    List<Mail_t_Mailbox> members = new List<Mail_t_Mailbox>();
                    while (true){
                        word = r.QuotedReadToDelimiter(new char[]{',','<',':',';'});
                        // We processed all data.
                        if((word == null && r.Available == 0) || r.Peek(false) == ';'){
                            break;
                        }
                        // Invalid address list value.
                        else if(string.IsNullOrEmpty(word)){
                            throw new ParseException("The address list contains an invalid group member.");
                        }
                        // name-addr
                        else if(r.Peek(true) == '<'){  
                            members.Add(new Mail_t_Mailbox(word != null ? MIME_Encoding_EncodedWord.DecodeS(TextUtils.UnQuoteString(word)) : null,r.ReadParenthesized()));                    
                        }
                        // addr-spec
                        else{
                            members.Add(new Mail_t_Mailbox(null,word));
                        }
                       
                        // We reached at the end of group.
                        if(r.Peek(true) == ';'){
                            r.Char(true);
                            break;
                        }
                        // We have more addresses.
                        if(r.Peek(true) == ','){
                            r.Char(false);
                        }
                    }

                    Mail_t_Group group = new Mail_t_Group(displayName,members.ToArray());
                    retVal.Add(group);
                }
                // name-addr
                else if(r.Peek(true) == '<'){
                    retVal.Add(new Mail_t_Mailbox(word != null ? MIME_Encoding_EncodedWord.DecodeS(TextUtils.UnQuoteString(word.Trim())) : null,r.ReadParenthesized()));                    
                }
                // addr-spec
                else{
                    retVal.Add(new Mail_t_Mailbox(null,word));
                }

                // We have more addresses.
                if(r.Peek(true) == ','){
                    r.Char(false);
                }
            }

            return retVal;
        }

        #endregion


        #region method Insert

        /// <summary>
        /// Inserts an address into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which to insert the address.</param>
        /// <param name="value">The address to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside the valid insertion range.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        public void Insert(int index,Mail_t_Address value)
        {
            if(index < 0 || index > m_pList.Count){
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            m_pList.Insert(index,value);
            m_IsModified = true;
        }

        #endregion

        #region method Add

        /// <summary>
        /// Adds an address to the end of the collection.
        /// </summary>
        /// <param name="value">The address to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        public void Add(Mail_t_Address value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            m_pList.Add(value);
            m_IsModified = true;
        }

        #endregion

        #region method Remove

        /// <summary>
        /// Removes the specified address from the collection.
        /// </summary>
        /// <param name="value">The address to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        public void Remove(Mail_t_Address value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            m_pList.Remove(value);            
            m_IsModified = true;
        }

        #endregion

        #region method Clear

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        public void Clear()
        {
            m_pList.Clear();
            m_IsModified = true;
        }

        #endregion

        #region method ToArray

        /// <summary>
        /// Copies the addresses to a new array.
        /// </summary>
        /// <returns>An array containing the addresses in the list.</returns>
        public Mail_t_Address[] ToArray()
        {
            return m_pList.ToArray();
        }

        #endregion

        #region override method ToString

        /// <summary>
        /// Returns the address list in serialized mail-header format.
        /// </summary>
        /// <returns>The serialized address list.</returns>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            for(int i=0;i<m_pList.Count;i++){
                if(i == (m_pList.Count - 1)){
                    retVal.Append(m_pList[i].ToString());
                }
                else{
                    retVal.Append(m_pList[i].ToString() + ",");
                }
            }

            return retVal.ToString();
        }

        #endregion


        #region method AcceptChanges

        /// <summary>
        /// Marks the address list as unchanged.
        /// </summary>
        internal void AcceptChanges()
        {
            m_IsModified = false;
        }

        #endregion


        #region interface IEnumerator

        /// <summary>
        /// Returns an enumerator that iterates through the addresses.
		/// </summary>
        /// <returns>An enumerator for the addresses.</returns>
        IEnumerator IEnumerable.GetEnumerator()
		{
			return m_pList.GetEnumerator();
		}

        /// <summary>
        /// Returns a generic enumerator that iterates through the addresses.
        /// </summary>
        /// <returns>An enumerator for the addresses.</returns>
        public IEnumerator<Mail_t_Address> GetEnumerator()
        {
            return m_pList.GetEnumerator();
        }

        #endregion

        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether the list has been modified
        /// since it was parsed.
        /// </summary>
        public bool IsModified
        {            
            get{ return m_IsModified; }
        }

        /// <summary>
        /// Gets the number of addresses in the list.
        /// </summary>
        public int Count
        {
            get{ return m_pList.Count; }
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get.</param>
        /// <returns>The element at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside the valid range.</exception>
        public Mail_t_Address this[int index]
        {
            get{ 
                if(index < 0 || index >= m_pList.Count){
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return m_pList[index]; 
            }
        }

        /// <summary>
        /// Gets all mailboxes in the address list, including mailboxes contained
        /// in group addresses.
        /// </summary>
        public Mail_t_Mailbox[] Mailboxes
        {
            get{
                List<Mail_t_Mailbox> retVal = new List<Mail_t_Mailbox>();
                foreach(Mail_t_Address address in this){
                    if(address is Mail_t_Mailbox){
                        retVal.Add((Mail_t_Mailbox)address);
                    }
                    else{
                        retVal.AddRange(((Mail_t_Group)address).Members);
                    }
                }

                return retVal.ToArray(); 
            }
        }

        #endregion
    }
}
