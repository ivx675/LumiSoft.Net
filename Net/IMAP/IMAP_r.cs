using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.IMAP.Server;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// This is base class for any IMAP server response.
    /// </summary>
    public abstract class IMAP_r
    {        
        #region virtual method ToString
                
        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <param name="utf8Allowed">
        /// Specifies whether UTF-8 strings are permitted in IMAP
        /// responses.
        /// </param>
        /// <returns>
        /// A string representation of this instance.
        /// </returns>
        public virtual string ToString(bool utf8Allowed)
        {
            return ToString() ?? "";
        }

        #endregion
    }
}
