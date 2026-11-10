using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents a <c>Return-Path</c> header field.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 5322, section 3.6.7.
    /// A Return-Path field contains a path enclosed in angle brackets,
    /// or an empty path represented by <c>&lt;&gt;</c>.
    /// </remarks>
    public class Mail_h_ReturnPath : MIME_h
    {
        private bool    m_IsModified = false;
        private string? m_Address    = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_h_ReturnPath"/> class.
        /// </summary>
        /// <param name="address">The mailbox address. A <see langword="null"/> value represents the null path.</param>
        public Mail_h_ReturnPath(string? address)
        {
            m_Address = address;
        }


        #region static method Parse

        /// <summary>
        /// Parses a <c>Return-Path</c> header field from its serialized representation.
        /// </summary>
        /// <param name="value">The serialized header field, including its name; for example, <c>Return-Path: &lt;john.doe@domain.com&gt;</c>.</param>
        /// <returns>A parsed <see cref="Mail_h_ReturnPath"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when <paramref name="value"/> does not contain a header field name and value.</exception>
        public static Mail_h_ReturnPath Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            string[] name_value = value.Split(new char[]{':'},2);
            if(name_value.Length != 2){
                throw new ParseException("The Return-Path header field must contain a field name followed by a colon.");
            }

            Mail_h_ReturnPath retVal = new Mail_h_ReturnPath(null);

            MIME_Reader r = new MIME_Reader(name_value[1].Trim());
            r.ToFirstChar();
            // Return-Path missing <>, some server won't be honor RFC.
            if(!r.StartsWith("<")){
                retVal.m_Address = r.ToEnd();
            }
            else{
                retVal.m_Address = r.ReadParenthesized();
            }

            return retVal;
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the serialized <c>Return-Path</c> header field, including the terminating CRLF.
        /// </summary>
        /// <param name="utf8">This header field does not contain encoded words; the parameter is retained for compatibility with the base class.</param>
        /// <param name="reEncode">This header field does not preserve an original representation; the parameter is retained for compatibility with the base class.</param>
        /// <returns>The serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {
            if(string.IsNullOrEmpty(m_Address)){
                return "Return-Path: <>\r\n";
            }
            else{
                return "Return-Path: <" + m_Address + ">\r\n";
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
            get{ return m_IsModified; }
        }

        /// <summary>
        /// Gets the header field name, which is always <c>Return-Path</c>.
        /// </summary>
        public override string Name
        {
            get{ return "Return-Path"; }
        }

        /// <summary>
        /// Gets the mailbox address. A <see langword="null"/> value represents the null path.
        /// </summary>
        public string? Address
        {
            get{ return m_Address; }
        }

        #endregion
    }
}
