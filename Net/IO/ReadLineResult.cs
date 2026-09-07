using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IO 
{
    /// <summary>
    /// This class represents result of ReadLine and ReadLineAsync methods.
    /// </summary>
    public class ReadLineResult 
    {
        private Memory<byte> m_LineBuffer;
        private int          m_BytesInBuffer;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="lineBuffer">Line buffer.</param>
        /// <param name="bytesInBuffer">Number of bytes in the buffer.</param>
        internal ReadLineResult(Memory<byte> lineBuffer,int bytesInBuffer) 
        {
            m_LineBuffer    = lineBuffer;
            m_BytesInBuffer = bytesInBuffer;
        }

        #region Properties Implementation

        /// <summary>
        /// Gets number of bytes stored in the buffer. Ending line-feed characters are included.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
        public int BytesInBuffer
        {
            get{
                return m_BytesInBuffer; 
            }
        }

        /// <summary>
        /// Gets number of line data bytes stored in the buffer. Ending line-feed characters are not included.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
        public int LineBytesInBuffer
        {
            get{
                int retVal = m_BytesInBuffer;

                if(m_BytesInBuffer > 1){
                    if(m_LineBuffer.Span[m_BytesInBuffer - 1] == '\n'){
                        retVal--;
                        if(m_LineBuffer.Span[m_BytesInBuffer - 2] == '\r'){
                            retVal--;
                        }
                    }
                }
                else if(m_BytesInBuffer > 0){
                    if(m_LineBuffer.Span[m_BytesInBuffer - 1] == '\n'){
                        retVal--;
                    }
                }

                return retVal; 
            }
        }

        /// <summary>
        /// Gets line as ASCII string. Returns null if EOS(end of stream) reached. Ending line-feed characters are not included.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
        public string? LineAscii
        {
            get{
                if(this.BytesInBuffer == 0){
                    return null;
                }
                else{
                    return Encoding.ASCII.GetString(m_LineBuffer.Span[..LineBytesInBuffer]); 
                }
            }
        }

        /// <summary>
        /// Gets line as UTF-8 string. Returns null if EOS(end of stream) reached. Ending line-feed characters not included.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
        public string? LineUtf8
        {
            get{
                if(this.BytesInBuffer == 0){
                    return null;
                }
                else{

                    return Encoding.UTF8.GetString(m_LineBuffer.Span[..LineBytesInBuffer]);
                }
            }
        }

        /// <summary>
        /// Gets line as UTF-32 string. Returns null if EOS(end of stream) reached. Ending line-feed characters are not included.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Is raised when this object is disposed and this property is accessed.</exception>
        /// <exception cref="InvalidOperationException">Is raised when this property is accessed in ivalid state.</exception>
        public string? LineUtf32
        {
            get{
                if(this.BytesInBuffer == 0){
                    return null;
                }
                else{
                    return Encoding.UTF32.GetString(m_LineBuffer.Span[..LineBytesInBuffer]);
                }
            }
        }

        #endregion
    }
}
