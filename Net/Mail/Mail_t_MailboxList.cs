using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents a <c>mailbox-list</c> as defined in RFC 5322 section 3.4.
    /// </summary>
    /// <remarks>
    /// A <c>mailbox-list</c> contains one or more mailbox addresses.
    ///
    /// <code>
    /// mailbox-list = (mailbox *("," mailbox)) / obs-mbox-list
    /// </code>
    /// </remarks>
    public class Mail_t_MailboxList : IEnumerable<Mail_t_Mailbox>
    {
        private bool                 m_IsModified = false;
        private List<Mail_t_Mailbox> m_pList;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="Mail_t_MailboxList"/> class.
        /// </summary>
        public Mail_t_MailboxList()
        {
            m_pList = new List<Mail_t_Mailbox>();
        }


        #region static method Parse

        /// <summary>
        /// Parses a <c>mailbox-list</c> from the specified string.
        /// </summary>
        /// <param name="value">
        /// String containing the <c>mailbox-list</c> value.
        /// </param>
        /// <returns>
        /// Returns the parsed <see cref="Mail_t_MailboxList"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="value"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when <paramref name="value"/> is not a valid
        /// <c>mailbox-list</c>.
        /// </exception>
        public static Mail_t_MailboxList Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            MIME_Reader        r      = new MIME_Reader(value);
            Mail_t_MailboxList retVal = new Mail_t_MailboxList();
            while(true){
                string? word = r.QuotedReadToDelimiter(new char[]{',','<'});
                // We processed all data.
                if(word == null){
                    break;
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
        /// Inserts a mailbox into the collection at the specified index.
        /// </summary>
        /// <param name="index">
        /// Zero-based index at which to insert the mailbox.
        /// </param>
        /// <param name="value">
        /// Mailbox to insert.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Raised when <paramref name="index"/> is out of range.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="value"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Insert(int index,Mail_t_Mailbox value)
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
        /// Adds the specified mailbox to the end of the collection.
        /// </summary>
        /// <param name="value">
        /// Mailbox to add.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="value"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Add(Mail_t_Mailbox value)
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
        /// Removes the specified mailbox from the collection.
        /// </summary>
        /// <param name="value">
        /// Mailbox to remove.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="value"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Remove(Mail_t_Mailbox value)
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
        /// Removes all mailboxes from the collection.
        /// </summary>
        public void Clear()
        {
            m_pList.Clear();
            m_IsModified = true;
        }

        #endregion

        #region method ToArray

        /// <summary>
        /// Creates an array containing the mailboxes in this collection.
        /// </summary>
        /// <returns>
        /// Returns an array containing the mailboxes in this collection.
        /// </returns>
        public Mail_t_Mailbox[] ToArray()
        {
            return m_pList.ToArray();
        }

        #endregion

        #region override method ToString

        /// <summary>
        /// Returns a string representation of the mailbox list.
        /// </summary>
        /// <returns>
        /// Returns the mailbox list as a string.
        /// </returns>
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
        /// Resets IsModified property to false.
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
        public IEnumerator<Mail_t_Mailbox> GetEnumerator()
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
        /// Gets the number of mailboxes in the collection.
        /// </summary>
        public int Count
        {
            get{ return m_pList.Count; }
        }

        /// <summary>
        /// Gets the mailbox at the specified index.
        /// </summary>
        /// <param name="index">
        /// Zero-based index of the mailbox to get.
        /// </param>
        /// <returns>
        /// Returns the mailbox at the specified index.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Raised when <paramref name="index"/> is out of range.
        /// </exception>
        public Mail_t_Mailbox this[int index]
        {
            get{ 
                if(index < 0 || index >= m_pList.Count){
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return m_pList[index]; 
            }
        }

        #endregion
    }
}
