using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Provides RFC 2047 encoded-word encoding and decoding functionality.
    /// </summary>
    public class MIME_Encoding_EncodedWord
    {
        private MIME_EncodedWordEncoding m_Encoding;
        private Encoding                 m_pCharset;
        private bool                     m_Split    = true;

        //private static readonly Regex encodedword_regex = new Regex(@"=\?(((?<charset>.*?)\*.*?)|(?<charset>.*?))\?(?<encoding>[qQbB])\?(?<value>.*?)\?=(?<whitespaces>\s*)",RegexOptions.IgnoreCase);
        private static readonly Regex encodedword_regex = new Regex(@"\=\?(?<charset>\S+?)\?(?<encoding>[qQbB])\?(?<value>.+?)\?\=(?<whitespaces>\s*)",RegexOptions.IgnoreCase);

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MIME_Encoding_EncodedWord"/> class.
        /// </summary>
        /// <param name="encoding">
        /// Encoded-word encoding method.
        /// </param>
        /// <param name="charset">
        /// Character set used to encode and decode text. UTF-8 is recommended
        /// unless another character set is required.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="charset"/> is
        /// <see langword="null"/>.
        /// </exception>
        public MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding encoding,Encoding charset)
        {
            if(charset == null){
                throw new ArgumentNullException(nameof(charset));
            }

            m_Encoding = encoding;
            m_pCharset = charset;
        }

                
        #region method Encode

        /// <summary>
        /// Encodes the specified text if encoding is required.
        /// </summary>
        /// <param name="text">
        /// Text to encode.
        /// </param>
        /// <returns>
        /// Returns the encoded text if encoding is required; otherwise returns
        /// the original text unchanged.
        /// </returns>
        public string Encode(string text)
        {            
            if(MustEncode(text)){
                return EncodeS(m_Encoding,m_pCharset,m_Split,text);
            }
            else{
                return text;
            }
        }

        #endregion

        #region method Decode

        /// <summary>
        /// Decodes the specified encoded-word.
        /// </summary>
        /// <param name="text">
        /// Encoded-word value to decode.
        /// </param>
        /// <returns>
        /// Returns the decoded text.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="text"/> is
        /// <see langword="null"/>.
        /// </exception>
        public string Decode(string text)
        {
            if(text == null){
                throw new ArgumentNullException(nameof(text));
            }

            return DecodeS(text);
        }

        #endregion


        #region static method MustEncode

        /// <summary>
        /// Determines whether the specified text requires RFC 2047
        /// encoded-word encoding.
        /// </summary>
        /// <param name="text">
        /// Text to examine.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if the specified text requires
        /// encoding; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="text"/> is
        /// <see langword="null"/>.
        /// </exception>
        public static bool MustEncode(string text)
        {
            if(text == null){
                throw new ArgumentNullException(nameof(text));
            }

            // Encoding is needed only for non-ASCII chars.

            foreach(char c in text){
                if(c > 127){
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region static method EncodeS

        /// <summary>
        /// Encodes the specified text as an RFC 2047 encoded-word if required.
        /// </summary>
        /// <param name="encoding">
        /// Encoded-word encoding method.
        /// </param>
        /// <param name="charset">
        /// Character set used to encode text. UTF-8 is recommended unless
        /// another character set is required.
        /// </param>
        /// <param name="split">
        /// Specifies whether the encoded text may be split into multiple
        /// encoded-words.
        /// </param>
        /// <param name="text">
        /// Text to encode.
        /// </param>
        /// <returns>
        /// Returns the encoded text if encoding is required; otherwise returns
        /// the original text unchanged.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="charset"/> or
        /// <paramref name="text"/> is <see langword="null"/>.
        /// </exception>
        public static string EncodeS(MIME_EncodedWordEncoding encoding,Encoding charset,bool split,string text)
        {
            if(charset == null){
                throw new ArgumentNullException(nameof(charset));
            }
            if(text == null){
                throw new ArgumentNullException(nameof(text));
            }

            /* RFC 2047 2.
                encoded-word = "=?" charset "?" encoding "?" encoded-text "?="
             
                An 'encoded-word' may not be more than 75 characters long, including
                'charset', 'encoding', 'encoded-text', and delimiters.  If it is
                desirable to encode more text than will fit in an 'encoded-word' of
                75 characters, multiple 'encoded-word's (separated by CRLF SPACE) may
                be used.
             
               RFC 2231 (updates syntax)
                encoded-word := "=?" charset ["*" language] "?" encoded-text "?="
            */

            if(MustEncode(text)){
                List<string> parts = new List<string>();
                if(split){
                    int index = 0;
                    // We just split text to 30 char words, then if some chars encoded, we don't exceed 75 chars lenght limit.
                    while(index < text.Length){
                        int countReaded = Math.Min(30,text.Length - index);
                        parts.Add(text.Substring(index,countReaded));                        
                        index += countReaded;                        
                    }
                }
                else{
                    parts.Add(text);
                }

                StringBuilder retVal = new StringBuilder();
                for(int i=0;i<parts.Count;i++){
                    string part = parts[i];
                    byte[] data = charset.GetBytes(part);

                    #region B encode

                    if(encoding == MIME_EncodedWordEncoding.B){
                        retVal.Append("=?" + charset.WebName + "?B?" + Convert.ToBase64String(data) + "?=");
                    }

                    #endregion

                    #region Q encode

                    else{
                        retVal.Append("=?" + charset.WebName + "?Q?");
                        int stored = 0;
                        foreach(byte b in data){
                            string val;
                            // We need to encode byte. Defined in RFC 2047 4.2.
                            if(b > 127 || b == '=' || b == '?' || b == '_' || b == ' '){
                                val = "=" + b.ToString("X2");
                            }
                            else{
                                val = ((char)b).ToString();
                            }

                            retVal.Append(val);
                            stored += val.Length;
                        }
                        retVal.Append("?=");
                    }

                    #endregion

                    if(i < (parts.Count - 1)){
                        retVal.Append("\r\n ");
                    }
                }

                return retVal.ToString();
            }
            else{
                return text;
            }
        }

        #endregion

        #region static method DecodeS

        /// <summary>
        /// Decodes the specified RFC 2047 encoded-word.
        /// </summary>
        /// <param name="word">
        /// Encoded-word value to decode.
        /// </param>
        /// <returns>
        /// Returns the decoded text.
        /// </returns>
        /// <remarks>
        /// If <paramref name="word"/> is not a valid encoded-word, the
        /// original value is returned unchanged.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="word"/> is
        /// <see langword="null"/>.
        /// </exception>
        public static string DecodeS(string word)
        {
            if(word == null){
                throw new ArgumentNullException(nameof(word));
            }

            return DecodeTextS(word);
        }

        #endregion

        #region static method DecodeTextS

        /// <summary>
        /// Decodes RFC 2047 encoded-words in the specified text.
        /// </summary>
        /// <param name="text">
        /// Text to decode.
        /// </param>
        /// <returns>
        /// Returns the decoded text.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="text"/> is
        /// <see langword="null"/>.
        /// </exception>
        public static string DecodeTextS(string text)
        {
            if(text == null){
                throw new ArgumentNullException(nameof(text));
            }

            /* RFC 2047 2.
                encoded-word = "=?" charset "?" encoding "?" encoded-text "?="
             
                encoded-text = 1*<Any printable ASCII character other than "?" or SPACE>
                               ; (but see "Use of encoded-words in message
                               ; headers", section 5)
            
                An 'encoded-word' may not be more than 75 characters long, including
                'charset', 'encoding', 'encoded-text', and delimiters.  If it is
                desirable to encode more text than will fit in an 'encoded-word' of
                75 characters, multiple 'encoded-word's (separated by CRLF SPACE) may
                be used.
             
                RFC 2231 updates.
                    encoded-word := "=?" charset ["*" language] "?" encoded-text "?="
            */

            string retVal = text;

            retVal = encodedword_regex.Replace(retVal,delegate(Match m){
                // We have encoded word, try to decode it.
                // Also if we have continuing encoded word, we need to skip all whitespaces between words.
             
                string encodedWord = m.Value;
                try{
                    if(string.Equals(m.Groups["encoding"].Value,"Q",StringComparison.InvariantCultureIgnoreCase)){
                        encodedWord =  MIME_Utils.QDecode(Encoding.GetEncoding(m.Groups["charset"].Value),m.Groups["value"].Value);
                    }
                    else if(string.Equals(m.Groups["encoding"].Value,"B",StringComparison.InvariantCultureIgnoreCase)){
                        encodedWord = Encoding.GetEncoding(m.Groups["charset"].Value).GetString(Convert.FromBase64String(m.Groups["value"].Value));
                    }
                    // Failed to parse encoded-word, leave it as is. RFC 2047 6.3.
                    // else{

                    // No continuing encoded-word, append whitespaces to retval.
                    Match mNext = encodedword_regex.Match(retVal,m.Index + m.Length);
                    if(!(mNext.Success && mNext.Index == (m.Index + m.Length))){
                        encodedWord += m.Groups["whitespaces"].Value;
                    }
                    // We have continuing encoded-word, so skip all whitespaces.
                    //else{

                    return encodedWord;
                }
                catch{
                    // Failed to parse encoded-word, leave it as is. RFC 2047 6.3.
                    return encodedWord;
                }
            });   

            return retVal;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets or sets a value indicating whether long encoded-words are split
        /// into multiple encoded-words.
        /// </summary>
        public bool Split
        {
            get{ return m_Split; }

            set{ m_Split = value; }
        }

        #endregion

    }
}
