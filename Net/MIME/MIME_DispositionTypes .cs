using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Defines common MIME content disposition types as specified in
    /// RFC 2183.
    /// </summary>
    public static class MIME_DispositionTypes
    {
        /// <summary>
        /// Indicates that the body part is intended to be displayed
        /// automatically as part of the message content.
        /// </summary>
        public const string Inline = "inline";

        /// <summary>
        /// Indicates that the body part is intended to be presented as an
        /// attachment rather than displayed automatically as part of the
        /// message content.
        /// </summary>
        public const string Attachment = "attachment";
    }
}
