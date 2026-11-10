using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{    
    /// <summary>
    /// Represents a mailbox address.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 5322, section 3.4. A mailbox may contain an optional
    /// display name and an angle-address, or an addr-spec by itself.
    ///
    /// <code>
    /// mailbox    = name-addr / addr-spec
    /// name-addr  = [display-name] angle-addr
    /// angle-addr = [CFWS] &quot;&lt;&quot; addr-spec &quot;&gt;&quot; [CFWS]
    /// addr-spec  = local-part &quot;@&quot; domain
    /// </code>
    /// </remarks>
    public class Mail_t_Mailbox : Mail_t_Address
    {
        private string? m_DisplayName = null;
        private string  m_Address;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Mailbox"/> class.
        /// </summary>
        /// <param name="displayName">The optional display name.</param>
        /// <param name="address">The mailbox address.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> is <see langword="null"/>.</exception>
        public Mail_t_Mailbox(string? displayName,string address)
        {
            if(address == null){
                throw new ArgumentNullException(nameof(address));
            }

            m_DisplayName = displayName;
            m_Address     = address;
        }

        #region static method Parse

        /// <summary>
        /// Parses a mailbox from the specified string.
        /// </summary>
        /// <param name="value">The serialized mailbox value, such as <c>user@example.com</c> or <c>John Doe &lt;user@example.com&gt;</c>.</param>
        /// <returns>The parsed mailbox.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> is not a valid mailbox.</exception>
        public static Mail_t_Mailbox Parse(string value)
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
                    throw new ParseException("The specified value is not a valid mailbox.");
                }
                // name-addr
                else if(r.Peek(true) == '<'){
                    return new Mail_t_Mailbox(word != null ? MIME_Encoding_EncodedWord.DecodeS(TextUtils.UnQuoteString(word.Trim())) : null,r.ReadParenthesized());
                }
                // addr-spec
                else{
                    return new Mail_t_Mailbox(null,word);
                }
            }

            throw new ParseException("The specified value is not a valid mailbox.");
        }

        #endregion

        
        #region method override ToString

        /// <summary>
        /// Returns the mailbox in its default serialized form.
        /// </summary>
        /// <returns>The serialized mailbox.</returns>
        public override string ToString()
        {
            return ToString(true);
        }

        /// <summary>
        /// Returns the mailbox in serialized mail-header format.
        /// </summary>
        /// <param name="utf8">Specifies whether non-ASCII display-name text may be written as UTF-8. If <see langword="false"/>, RFC 2047 encoded-word encoding is used when required.</param>
        /// <returns>The serialized mailbox.</returns>
        public override string ToString(bool utf8)
        {
            if(string.IsNullOrEmpty(m_DisplayName)){
                return m_Address;
            }
            else{
                if(!utf8 && MIME_Encoding_EncodedWord.MustEncode(m_DisplayName)){
                    return MIME_Encoding_EncodedWord.EncodeS(MIME_EncodedWordEncoding.B,Encoding.UTF8,true,m_DisplayName) + " " + "<" + m_Address + ">";
                }
                else if(System.Text.RegularExpressions.Regex.IsMatch(m_DisplayName,@"[""(),:;<>@\[\\\]]")){
                    return TextUtils.QuoteString(m_DisplayName) + " " + "<" + m_Address + ">";
                }
                else{
                    return m_DisplayName + " " + "<" + m_Address + ">";
                }
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the optional display name.
        /// </summary>
        public string? DisplayName
        {
            get{ return m_DisplayName; }
        }

        /// <summary>
        /// Gets the mailbox address.
        /// </summary>
        public string Address
        {
            get{ return m_Address; }
        }

        /// <summary>
        /// Gets the local part of the mailbox address.
        /// </summary>
        public string LocalPart
        {
            get{ 
                string[] localpart_domain = m_Address.Split('@');

                return localpart_domain[0]; 
            }
        }

        /// <summary>
        /// Gets the domain part of the mailbox address.
        /// </summary>
        /// <remarks>
        /// Returns an empty string when the address does not contain exactly
        /// one separator between a local part and a domain.
        /// </remarks>
        public string Domain
        {
            get{ 
                string[] localpart_domain = m_Address.Split('@');

                if(localpart_domain.Length == 2){
                    return localpart_domain[1]; 
                }
                else{
                    return "";
                }
            }
        }

        #endregion
    }
}
