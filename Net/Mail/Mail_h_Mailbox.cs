using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents a header field whose value is a mailbox, such as the
    /// <c>Sender</c> header field.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 5322.
    ///
    /// <code>
    /// header = &quot;FieldName:&quot; mailbox CRLF
    /// </code>
    /// </remarks>
    public class Mail_h_Mailbox : MIME_h
    {
        private string?        m_ParseValue = null;
        private string         m_Name;
        private Mail_t_Mailbox m_pAddress;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_h_Mailbox"/> class.
        /// </summary>
        /// <param name="fieldName">The header field name, such as <c>Sender</c>.</param>
        /// <param name="mailbox">The mailbox value for the header field.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fieldName"/> or <paramref name="mailbox"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="fieldName"/> is empty.
        /// </exception>
        public Mail_h_Mailbox(string fieldName,Mail_t_Mailbox mailbox)
        {
            if(fieldName == null){
                throw new ArgumentNullException(nameof(fieldName));
            }
            if(fieldName == string.Empty){
                throw new ArgumentException("The header field name cannot be empty.",nameof(fieldName));
            }
            if(mailbox == null){
                throw new ArgumentNullException(nameof(mailbox));
            }

            m_Name     = fieldName;
            m_pAddress = mailbox;
        }


        #region static method Parse

        /// <summary>
        /// Parses a mailbox header field from its serialized representation.
        /// </summary>
        /// <param name="value">The serialized header field, including its name; for example, <c>Sender: john.doe@domain.com</c>.</param>
        /// <returns>A parsed mailbox header field.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> is not a valid mailbox header field.</exception>
        public static Mail_h_Mailbox Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            string[] name_value = value.Split(new char[]{':'},2);
            if(name_value.Length != 2){
                throw new ParseException("The header field must contain a field name followed by a colon.");
            }

            MIME_Reader r = new MIME_Reader(name_value[1].Trim());

            string? word = r.QuotedReadToDelimiter(new char[]{',','<',':'});
            // Invalid value.
            if(word == null){
                throw new ParseException("The header field contains an invalid mailbox value.");
            }
            // name-addr
            else if(r.Peek(true) == '<'){
                Mail_h_Mailbox h = new Mail_h_Mailbox(name_value[0],new Mail_t_Mailbox(word != null ? MIME_Encoding_EncodedWord.DecodeS(TextUtils.UnQuoteString(word)) : null,r.ReadParenthesized()));
                h.m_ParseValue = value;

                return h;
            }
            // addr-spec
            else{
                Mail_h_Mailbox h = new Mail_h_Mailbox(name_value[0],new Mail_t_Mailbox(null,word));
                h.m_ParseValue = value;

                return h;
            }
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the serialized header field.
        /// </summary>
        /// <param name="utf8">
        /// Specifies whether a regenerated header field may contain UTF-8
        /// characters as defined by RFC 6532.
        /// </param>
        /// <param name="reEncode">
        /// Specifies whether the header field is regenerated. If
        /// <see langword="false"/> and the field was parsed, its original
        /// representation is returned.
        /// </param>
        /// <returns>The serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {
            if(!reEncode && m_ParseValue != null){
                return m_ParseValue;
            }
            else{
                return m_Name + ": " + m_pAddress.ToString(utf8) + "\r\n";
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
            get{ return false; }
        }

        /// <summary>
        /// Gets the header field name, such as <c>Sender</c>.
        /// </summary>
        public override string Name
        {
            get{ return m_Name; }
        }

        /// <summary>
        /// Gets the mailbox represented by this header field.
        /// </summary>
        public Mail_t_Mailbox Address
        {
            get{ return m_pAddress; }
        }

        #endregion
    }
}
