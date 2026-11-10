using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents a header field whose value is an address list, such as
    /// <c>To</c>, <c>Cc</c>, or <c>Bcc</c>.
    /// </summary>
    /// <remarks>
    /// The header field name is retained as supplied to the constructor. The
    /// address list can contain mailboxes and groups as defined by RFC 5322.
    ///
    /// <code>
    /// header       = &quot;FieldName:&quot; address-list CRLF
    /// address-list = address *(&quot;,&quot; address)
    /// address      = mailbox / group
    /// </code>
    /// </remarks>
    public class Mail_h_AddressList : MIME_h
    {
        private string?            m_ParseValue = null;
        private string             m_Name;
        private Mail_t_AddressList m_pAddresses;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_h_AddressList"/> class.
        /// </summary>
        /// <param name="fieldName">The header field name, such as <c>To</c>.</param>
        /// <param name="values">The address collection for the header field.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fieldName"/> or <paramref name="values"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="fieldName"/> is empty.
        /// </exception>
        public Mail_h_AddressList(string fieldName,Mail_t_AddressList values)
        {
            if(fieldName == null){
                throw new ArgumentNullException(nameof(fieldName));
            }
            if(fieldName == string.Empty){
                throw new ArgumentException("The header field name cannot be empty.",nameof(fieldName));
            }
            if(values == null){
                throw new ArgumentNullException(nameof(values));
            }

            m_Name       = fieldName;
            m_pAddresses = values;
        }


        #region static method Parse

        /// <summary>
        /// Parses an address-list header field from its serialized representation.
        /// </summary>
        /// <param name="value">The serialized header field, including its name; for example, <c>To: user@example.com</c>.</param>
        /// <returns>A parsed address-list header field.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> is not a valid address-list header field.</exception>
        public static Mail_h_AddressList Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            string[] name_value = value.Split(new char[]{':'},2);
            if(name_value.Length != 2){
                throw new ParseException("The header field must contain a field name followed by a colon.");
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

            Mail_h_AddressList retVal = new Mail_h_AddressList(name_value[0],Mail_t_AddressList.Parse(name_value[1].Trim()));
            retVal.m_ParseValue = value;
            retVal.m_pAddresses.AcceptChanges();

            return retVal;
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the serialized header field, including the terminating CRLF.
        /// </summary>
        /// <param name="utf8">
        /// Specifies whether rewritten header fields may contain UTF-8 characters
        /// as defined by RFC 6532. If <see langword="false"/>, non-ASCII values
        /// are encoded using the applicable MIME encoding rules.
        /// </param>
        /// <param name="reEncode">
        /// Specifies whether the header field is regenerated. If
        /// <see langword="false"/> and the parsed value is unchanged, its original
        /// representation may be returned.
        /// </param>
        /// <returns>The serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {            
            if(reEncode || this.IsModified || m_ParseValue == null){
                StringBuilder retVal = new StringBuilder();
                retVal.Append(this.Name + ": ");
                for(int i=0;i<m_pAddresses.Count;i++){
                    if(i > 0){
                        retVal.Append("\t");
                    }
 
                    // Don't add ',' for last item.
                    if(i == (m_pAddresses.Count - 1)){
                        retVal.Append(m_pAddresses[i].ToString(utf8) + "\r\n");
                    }
                    else{
                        retVal.Append(m_pAddresses[i].ToString(utf8) + ",\r\n");
                    }
                }
                // No items, we need to add ending CRLF.
                if(m_pAddresses.Count == 0){
                    retVal.Append("\r\n");
                }

                return retVal.ToString();
            }
            else{
                return m_ParseValue;
            }            
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this header field has been modified
        /// since it was parsed.
        /// </summary>
        public override bool IsModified
        {
            get{ return m_pAddresses.IsModified; }
        }

        /// <summary>
        /// Gets the header field name, such as <c>To</c>.
        /// </summary>
        public override string Name
        {
            get{ return m_Name; }
        }

        /// <summary>
        /// Gets the address collection represented by this header field.
        /// </summary>
        /// <remarks>
        /// The returned collection is the live collection used by the header
        /// field. Changes to it affect serialization and modification state.
        /// </remarks>
        public Mail_t_AddressList Addresses
        {
            get{ return m_pAddresses; }
        }

        #endregion
    }
}
