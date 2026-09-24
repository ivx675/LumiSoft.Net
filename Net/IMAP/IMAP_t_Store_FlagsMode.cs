using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Specifies the flag update mode used by the IMAP <c>STORE</c> command.
    /// </summary>
    /// <remarks>
    /// The mode determines whether message flags are replaced, added, or
    /// removed, and whether the server should send untagged <c>FETCH</c>
    /// responses showing updated flags, as defined in RFC 3501.
    /// </remarks>
    public enum IMAP_t_Store_FlagsMode
    {
        /// <summary>
        /// Replaces the entire flag set with the specified flags.
        /// Corresponds to the IMAP <c>FLAGS</c> operation.
        /// </summary>
        Replace,

        /// <summary>
        /// Replaces the entire flag set silently, suppressing untagged
        /// <c>FETCH</c> responses. Corresponds to <c>FLAGS.SILENT</c>.
        /// </summary>
        ReplaceSilent,

        /// <summary>
        /// Adds the specified flags to the existing flag set.
        /// Corresponds to <c>+FLAGS</c>.
        /// </summary>
        Add,

        /// <summary>
        /// Adds the specified flags silently, suppressing untagged
        /// <c>FETCH</c> responses. Corresponds to <c>+FLAGS.SILENT</c>.
        /// </summary>
        AddSilent,

        /// <summary>
        /// Removes the specified flags from the existing flag set.
        /// Corresponds to <c>-FLAGS</c>.
        /// </summary>
        Remove,

        /// <summary>
        /// Removes the specified flags silently, suppressing untagged
        /// <c>FETCH</c> responses. Corresponds to <c>-FLAGS.SILENT</c>.
        /// </summary>
        RemoveSilent
    }
}
