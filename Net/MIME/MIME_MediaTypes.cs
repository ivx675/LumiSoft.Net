using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Defines well-known MIME media types.
    /// </summary>
    /// <remarks>
    /// Additional media types are registered with IANA:
    /// https://www.iana.org/assignments/media-types/
    /// </remarks>
    public static class MIME_MediaTypes
    {
        #region application

        /// <summary>
        /// Defines well-known <c>application/*</c> media types.
        /// </summary>
        public static class Application
        {
            /// <summary>
            /// The <c>application/octet-stream</c> media type.
            /// </summary>
            public const string octet_stream = "application/octet-stream";

            /// <summary>
            /// The <c>application/pdf</c> media type.
            /// </summary>
            public const string pdf = "application/pdf";

            /// <summary>
            /// The <c>application/sdp</c> media type.
            /// </summary>
            public const string sdp = "application/sdp";

            /// <summary>
            /// The <c>application/xml</c> media type.
            /// </summary>
            public const string xml = "application/xml";

            /// <summary>
            /// The <c>application/zip</c> media type.
            /// </summary>
            public const string zip = "application/zip";

            /// <summary>
            /// The <c>application/x-pkcs7-signature</c> media type.
            /// </summary>
            public const string x_pkcs7_signature = "application/x-pkcs7-signature";

            /// <summary>
            /// The <c>application/pkcs7-mime</c> media type.
            /// </summary>
            public const string pkcs7_mime = "application/pkcs7-mime";
        }

        #endregion

        #region image

        /// <summary>
        /// Defines well-known <c>image/*</c> media types.
        /// </summary>
        public static class Image
        {
            /// <summary>
            /// The <c>image/gif</c> media type.
            /// </summary>
            public const string gif = "image/gif";

            /// <summary>
            /// The <c>image/jpeg</c> media type.
            /// </summary>
            public const string jpeg = "image/jpeg";

            /// <summary>
            /// The <c>image/tiff</c> media type.
            /// </summary>
            public const string tiff = "image/tiff";
        }
                
        #endregion

        #region text

        /// <summary>
        /// Defines well-known <c>text/*</c> media types.
        /// </summary>
        public static class Text
        {
            /// <summary>
            /// The <c>text/calendar</c> media type.
            /// </summary>
            public const string calendar = "text/calendar";
        
            /// <summary>
            /// The <c>text/css</c> media type.
            /// </summary>
            public const string css = "text/css";

            /// <summary>
            /// The <c>text/html</c> media type.
            /// </summary>
            public const string html = "text/html";

            /// <summary>
            /// The <c>text/plain</c> media type.
            /// </summary>
            public const string plain = "text/plain";

            /// <summary>
            /// The <c>text/rfc822-headers</c> media type.
            /// </summary>
            public const string rfc822_headers = "text/rfc822-headers";

            /// <summary>
            /// The <c>text/richtext</c> media type.
            /// </summary>
            public const string richtext = "text/richtext";

            /// <summary>
            /// The <c>text/xml</c> media type.
            /// </summary>
            public const string xml = "text/xml";
        }

        #endregion

        #region multipart

        /// <summary>
        /// Defines well-known <c>multipart/*</c> media types.
        /// </summary>
        public static class Multipart
        {
            /// <summary>
            /// The <c>multipart/alternative</c> media type.
            /// </summary>
            public const string alternative = "multipart/alternative";

            /// <summary>
            /// The <c>multipart/digest</c> media type.
            /// </summary>
            public const string digest = "multipart/digest";

            /// <summary>
            /// The <c>multipart/encrypted</c> media type.
            /// </summary>
            public const string encrypted = "multipart/encrypted";

            /// <summary>
            /// The <c>multipart/form-data</c> media type.
            /// </summary>
            public const string form_data = "multipart/form-data";

            /// <summary>
            /// The <c>multipart/mixed</c> media type.
            /// </summary>
            public const string mixed = "multipart/mixed";

            /// <summary>
            /// The <c>multipart/parallel</c> media type.
            /// </summary>
            public const string parallel = "multipart/parallel";

            /// <summary>
            /// The <c>multipart/related</c> media type.
            /// </summary>
            public const string related  = "multipart/related";

            /// <summary>
            /// The <c>multipart/report</c> media type.
            /// </summary>
            public const string report = "multipart/report";

            /// <summary>
            /// The <c>multipart/signed</c> media type.
            /// </summary>
            public const string signed = "multipart/signed";

            /// <summary>
            /// The <c>multipart/voice-message</c> media type.
            /// </summary>
            public const string voice_message = "multipart/voice-message";
        }

        #endregion

        #region message

        /// <summary>
        /// Defines well-known <c>message/*</c> media types.
        /// </summary>
        public static class Message
        {
            /// <summary>
            /// The <c>message/rfc822</c> media type.
            /// </summary>
            public const string rfc822 = "message/rfc822";

            /// <summary>
            /// The <c>message/disposition-notification</c> media type.
            /// </summary>
            public const string disposition_notification = "message/disposition-notification";

            /// <summary>
            /// The <c>message/delivery-status</c> media type.
            /// </summary>
            public const string delivery_status = "message/delivery-status";
        }

        #endregion        
    }
}
