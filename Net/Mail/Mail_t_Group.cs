using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents a group address.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 5322, section 3.4. A group consists of a display name,
    /// a colon, zero or more mailbox members, and a terminating semicolon.
    /// </remarks>
    public class Mail_t_Group : Mail_t_Address
    {
        private string?          m_DisplayName = null;
        private Mail_t_Mailbox[] m_pMembers;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Group"/> class.
        /// </summary>
        /// <param name="displayName">The group display name. A <see langword="null"/> value means that it is not specified.</param>
        /// <param name="members">The mailbox members of the group.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is <see langword="null"/>.</exception>
        public Mail_t_Group(string? displayName,Mail_t_Mailbox[] members)
        {
            if(members == null) {
                throw new ArgumentNullException(nameof(members));
            }

            m_DisplayName = displayName;
            m_pMembers    = members;
        }


        #region method override ToString

        /// <summary>
        /// Returns the group address in its default serialized form.
        /// </summary>
        /// <returns>The serialized group address.</returns>
        public override string ToString()
        {
            return ToString(true);
        }

        /// <summary>
        /// Returns the group address in serialized mail-header format.
        /// </summary>
        /// <param name="utf8">Specifies whether non-ASCII display-name text may be written as UTF-8. If <see langword="false"/>, RFC 2047 encoded-word encoding is used when required.</param>
        /// <returns>The serialized group address.</returns>
        public override string ToString(bool utf8)
        {
            StringBuilder retVal = new StringBuilder();
            if(string.IsNullOrEmpty(m_DisplayName)){
                retVal.Append(":");
            }
            else{
                if(!utf8 && MIME_Encoding_EncodedWord.MustEncode(m_DisplayName)){
                    retVal.Append(MIME_Encoding_EncodedWord.EncodeS(MIME_EncodedWordEncoding.B,Encoding.UTF8,true,m_DisplayName) + ":");
                }
                else{
                    retVal.Append(TextUtils.QuoteString(m_DisplayName) + ":");
                }
            }
            for(int i=0;i<m_pMembers.Length;i++){
                retVal.Append(m_pMembers[i].ToString(utf8));
                if(i < (m_pMembers.Length - 1)){
                    retVal.Append(",");
                }
            }
            retVal.Append(";");            

            return retVal.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the optional group display name.
        /// </summary>
        public string? DisplayName
        {
            get{ return m_DisplayName; }
        }

        /// <summary>
        /// Gets the mailbox members of the group.
        /// </summary>
        public Mail_t_Mailbox[] Members
        {
            get{ return m_pMembers; }
        }

        #endregion
    }
}
