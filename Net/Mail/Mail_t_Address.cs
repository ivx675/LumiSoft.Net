using System;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents an RFC 5322 address.
    /// </summary>
    /// <remarks>
    /// This is the base class for <see cref="Mail_t_Mailbox"/> and
    /// <see cref="Mail_t_Group"/> address types.
    /// </remarks>
    public abstract class Mail_t_Address
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Address"/> class.
        /// </summary>
        protected Mail_t_Address()
        {
        }


        #region abstract method ToString

        /// <summary>
        /// Returns the address in its serialized mail-header format.
        /// </summary>
        /// <param name="utf8">Specifies whether non-ASCII text may be written as UTF-8. If <see langword="false"/>, applicable MIME encoded-word encoding is used.</param>
        /// <returns>The serialized address.</returns>
        public abstract string ToString(bool utf8);

        #endregion

    }
}
