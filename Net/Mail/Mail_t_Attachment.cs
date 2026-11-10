using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;

using LumiSoft.Net.IO;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents a mail message attachment.
    /// </summary>
    public class Mail_t_Attachment
    {
        private string  m_Name;
        private string? m_FileName    = null;
        private bool    m_ZipCompress = false;
        private bool    m_CloseStream = true;
        private Stream? m_pStream     = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File name with path.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        public Mail_t_Attachment(string fileName) : this(null,fileName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File name with path.
        /// </param>
        /// <param name="zipCompress">
        /// Specifies whether the attachment content is ZIP-compressed.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        public Mail_t_Attachment(string fileName,bool zipCompress) : this(null,fileName,zipCompress)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified file.
        /// </summary>
        /// <param name="attachmentName">
        /// Attachment file name displayed in the mail message. If
        /// <see langword="null"/>, the file name portion of
        /// <paramref name="fileName"/> is used.
        /// </param>
        /// <param name="fileName">
        /// File name with path.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        public Mail_t_Attachment(string? attachmentName,string fileName) : this(null,fileName,false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified file.
        /// </summary>
        /// <param name="attachmentName">
        /// Attachment file name displayed in the mail message. If
        /// <see langword="null"/> or empty, the file name portion of
        /// <paramref name="fileName"/> is used.
        /// </param>
        /// <param name="fileName">
        /// File name with path.
        /// </param>
        /// <param name="zipCompress">
        /// Specifies whether the attachment content is ZIP-compressed.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        public Mail_t_Attachment(string? attachmentName,string fileName,bool zipCompress)
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }
            if(string.IsNullOrEmpty(fileName)){
                throw new ArgumentException("Argument 'fileName' value must be specified.",nameof(fileName));
            }

            if(string.IsNullOrEmpty(attachmentName)){
                m_Name = Path.GetFileName(fileName);
            }
            else{
                m_Name = attachmentName;
            }

            m_FileName    = fileName;
            m_ZipCompress = zipCompress;
            m_CloseStream = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified attachment data.
        /// </summary>
        /// <param name="attachmentName">
        /// Attachment file name displayed in the mail message.
        /// </param>
        /// <param name="data">
        /// Attachment data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="attachmentName"/> or
        /// <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
        public Mail_t_Attachment(string attachmentName,byte[] data) : this(attachmentName,data,false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified attachment data.
        /// </summary>
        /// <param name="attachmentName">
        /// Attachment file name displayed in the mail message.
        /// </param>
        /// <param name="data">
        /// Attachment data.
        /// </param>
        /// <param name="zipCompress">
        /// Specifies whether the attachment content is ZIP-compressed.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="attachmentName"/> or
        /// <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
        public Mail_t_Attachment(string attachmentName,byte[] data,bool zipCompress)
        {
            if(attachmentName == null){
                throw new ArgumentNullException(nameof(attachmentName));
            }
            if(data == null){
                throw new ArgumentNullException(nameof(data));
            }

            m_Name    = attachmentName;
            m_pStream = new MemoryStream(data);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified stream.
        /// </summary>
        /// <param name="attachmentName">
        /// Attachment file name displayed in the mail message.
        /// </param>
        /// <param name="stream">
        /// Stream containing the attachment data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="attachmentName"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        public Mail_t_Attachment(string attachmentName,Stream stream) : this(attachmentName,stream,false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_t_Attachment"/>
        /// class from the specified stream.
        /// </summary>
        /// <param name="attachmentName">
        /// Attachment file name displayed in the mail message.
        /// </param>
        /// <param name="stream">
        /// Stream containing the attachment data.
        /// </param>
        /// <param name="zipCompress">
        /// Specifies whether the attachment content is ZIP-compressed.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="attachmentName"/> or
        /// <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        public Mail_t_Attachment(string attachmentName,Stream stream,bool zipCompress)
        {
            if(attachmentName == null){
                throw new ArgumentNullException(nameof(attachmentName));
            }
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            m_Name    = attachmentName;
            m_pStream = stream;
        }


        #region method GetStream

        /// <summary>
        /// Gets attachment stream.
        /// </summary>
        /// <returns>Returns attachment stream.</returns>
        internal Stream GetStream()
        {
            if(m_pStream == null){
                ArgumentNullException.ThrowIfNull(m_FileName);

                m_pStream = File.OpenRead(m_FileName);
            }

            if(m_ZipCompress){
                MemoryStreamEx retVal = new MemoryStreamEx();

                using(ZipArchive archive = new ZipArchive(retVal,ZipArchiveMode.Create)){
                    ZipArchiveEntry entry = archive.CreateEntry(m_Name,CompressionLevel.Optimal);
                    using(Stream zipStream = entry.Open()){
                        m_pStream.CopyTo(zipStream,64000);
                    }
                }
                retVal.Position = 0;
                CloseStream();

                return retVal;
            }

            return m_pStream;
        }

        #endregion

        #region method CloseStream

        /// <summary>
        /// Closes stream.
        /// </summary>
        internal void CloseStream()
        {
            if(m_CloseStream){
                m_pStream?.Dispose();
            }
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the attachment file name.
        /// </summary>
        /// <remarks>
        /// If the attachment is configured for ZIP compression, the returned
        /// file name has a <c>.zip</c> extension.
        /// </remarks>
        public string Name
        {
            get{
                if(m_ZipCompress){
                    return Path.GetFileNameWithoutExtension(m_Name) + ".zip";
                }
                else{
                    return m_Name;
                }
            }
        }

        #endregion
    }
}
