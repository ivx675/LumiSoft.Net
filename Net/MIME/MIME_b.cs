using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents the body of a MIME entity.
    /// </summary>
    /// <remarks>
    /// Derived classes implement specific MIME body types such as text,
    /// multipart, message, image, audio, video, and application bodies.
    /// </remarks>
    public abstract class MIME_b
    {
        private MIME_Entity?       m_pEntity      = null;
        private MIME_h_ContentType m_pContentType;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_b"/> class.
        /// </summary>
        /// <param name="contentType">
        /// Content-Type associated with this MIME body.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="contentType"/> is
        /// <see langword="null"/>.
        /// </exception>
        public MIME_b(MIME_h_ContentType contentType)
        {
            if(contentType == null){
                throw new ArgumentNullException(nameof(contentType));
            }

            m_pContentType = contentType;
        }

        /// <summary>
        /// Internal constructor. No Content-Type is created, user must do it manually.
        /// </summary>
        internal MIME_b()
        {
            m_pContentType = new MIME_h_ContentType("text/plain");
        }

        #region method Dispose

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        /// <remarks>
        /// If this instance owns a backing stream, the stream is disposed.
        /// </remarks>
        internal virtual void Dispose()
        {
        }

        #endregion


        #region method SetParent

        /// <summary>
        /// Sets body parent.
        /// </summary>
        /// <param name="entity">Owner entity.</param>
        /// <param name="setContentType">If true sets entity.ContentType header value.</param>
        internal virtual void SetParent(MIME_Entity entity,bool setContentType)
        {
            m_pEntity = entity;

            // Owner entity has no content-type or has different content-type, just add/overwrite it.
            if(setContentType &&(entity.ContentType == null || !string.Equals(entity.ContentType.TypeWithSubtype,this.MediaType,StringComparison.InvariantCultureIgnoreCase))){
                entity.ContentType = m_pContentType;
            }
        }

        #endregion

        #region method ToStreamAsync

        /// <summary>
        /// Stores the MIME body to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream to which to write the body data.
        /// </param>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers. If <see langword="false"/>, rewritten non-ASCII
        /// header values are encoded using UTF-8 with RFC 2047 encoded-words
        /// and RFC 2231 parameter encoding.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether all header fields are regenerated and re-encoded.
        /// If <see langword="false"/>, unchanged parsed header fields retain
        /// their original wire representation.
        /// </param>
        /// <param name="cancellationToken">
        /// Token used to monitor for cancellation requests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        internal protected abstract Task ToStreamAsync(Stream stream,bool utf8Headers,bool reEncodeHeaders,CancellationToken cancellationToken = default);

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether the body has been modified.
        /// </summary>
        public abstract bool IsModified
        {
            get;
        }

        /// <summary>
        /// Gets the MIME entity that owns this body.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null"/> if the body is not bound to an entity.
        /// </remarks>
        public MIME_Entity? Entity
        {
            get{ return m_pEntity; }
        }

        /// <summary>
        /// Gets the MIME media type of this body, for example
        /// <c>text/plain</c>.
        /// </summary>
        public string MediaType
        {
            get{ return m_pContentType.TypeWithSubtype; }
        }


        /// <summary>
        /// Gets or sets the Content-Type header.
        /// </summary>
        internal MIME_h_ContentType ContentType
        {
            get{ return m_pContentType; }

            set{ m_pContentType = value; }
        }

        #endregion
    }
}
