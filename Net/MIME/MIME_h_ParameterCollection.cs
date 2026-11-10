using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents the parameters associated with a MIME header field.
    /// </summary>
    public class MIME_h_ParameterCollection : IEnumerable<MIME_h_Parameter>
    {
        #region class _ParameterBuilder

        /// <summary>
        /// Collects and combines the segments of one RFC 2231 parameter.
        /// </summary>
        internal class _ParameterBuilder
        {
            private string                  m_Name;
            private SortedList<int,string?> m_pParts;
            private Encoding?               m_pEncoding = null;

            /// <summary>
            /// Initializes a new instance of the <see cref="_ParameterBuilder"/> class.
            /// </summary>
            /// <param name="name">The parameter name.</param>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
            public _ParameterBuilder(string name)
            {
                if(name == null){
                    throw new ArgumentNullException("name");
                }

                m_Name = name;

                m_pParts = new SortedList<int,string?>();
            }


            #region method AddPart

            /// <summary>
            /// Adds one segment of an RFC 2231 parameter.
            /// </summary>
            /// <param name="index">The zero-based segment index.</param>
            /// <param name="encoded">Indicates whether the segment uses RFC 2231 extended encoding.</param>
            /// <param name="value">The segment value, or <see langword="null"/> for a valueless parameter.</param>
            public void AddPart(int index,bool encoded,string? value)
            {
                // We should have charset and language information available.
                if(value != null && encoded && index == 0){
                    // Syntax: <charset>'<language>'<value>
                    string[] charset_language_value = value.Split('\'');
                    // If charset missing, consider it as us-ascii.
                    m_pEncoding = Encoding.GetEncoding(string.IsNullOrEmpty(charset_language_value[0]) ? "us-ascii" : charset_language_value[0]);
                    value = charset_language_value[2];
                }

                // Add parameter to list.
                m_pParts[index] = value;
            }

            #endregion

            #region method GetParamter

            /// <summary>
            /// Combines the collected segments into a header field parameter.
            /// </summary>
            /// <returns>The combined and decoded header field parameter.</returns>
            public MIME_h_Parameter GetParamter()
            {
                // Concate parts values and decode value. (SortedList takes care for sorting part indexes)
                StringBuilder value = new StringBuilder();
                foreach(KeyValuePair<int,string?> v in m_pParts){
                    value.Append(v.Value);
                }

                if(m_pEncoding != null){
                    return new MIME_h_Parameter(m_Name,DecodeExtOctet(value.ToString(),m_pEncoding));
                }
                else{
                    return new MIME_h_Parameter(m_Name,value.ToString());
                }
            }

            #endregion


            #region Properties implementation

            /// <summary>
            /// Gets parameter name.
            /// </summary>
            public string Name
            {
                get{ return m_Name ; }
            }

            #endregion
        }

        #endregion

        private bool                                m_IsModified    = false;
        private MIME_h                              m_pOwner;
        private Dictionary<string,MIME_h_Parameter> m_pParameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_h_ParameterCollection"/> class.
        /// </summary>
        /// <param name="owner">The MIME header field that owns this collection.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="owner"/> is <see langword="null"/>.</exception>
        public MIME_h_ParameterCollection(MIME_h owner)
        {
            if(owner == null){
                throw new ArgumentNullException(nameof(owner));
            }

            m_pOwner = owner;

            m_pParameters = new Dictionary<string,MIME_h_Parameter>(StringComparer.CurrentCultureIgnoreCase);
        }


        #region method Remove

        /// <summary>
        /// Removes the specified parameter from the collection.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
        public void Remove(string name)
        {
            if(name == null){
                throw new ArgumentNullException("name");
            }

            if(m_pParameters.Remove(name)){
                m_IsModified = true;
            }
        }

        #endregion

        #region method Clear

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        public void Clear()
        {
            m_pParameters.Clear();
            m_IsModified = true;
        }

        #endregion

        #region method ToArray

        /// <summary>
        /// Copies the parameters to a new array.
        /// </summary>
        /// <returns>An array containing the parameters in the collection.</returns>
        public MIME_h_Parameter[] ToArray()
        {
            MIME_h_Parameter[] retVal = new MIME_h_Parameter[m_pParameters.Count];
            m_pParameters.Values.CopyTo(retVal,0);

            return retVal;
        }

        #endregion


        #region method ToString

        /// <summary>
        /// Returns the parameters in serialized header-field format.
        /// </summary>
        /// <returns>The serialized parameters.</returns>
        public override string ToString()
        {
            return ToString(true);
        }

        /// <summary>
        /// Returns header field parameters as a serialized string.
        /// </summary>
        /// <param name="utf8">
        /// If <see langword="true"/>, parameter values are written as UTF-8
        /// text without RFC 2231 encoding. If <see langword="false"/>,
        /// non-ASCII values are encoded using UTF-8 and RFC 2231.
        /// </param>
        /// <returns>The serialized header field parameters.</returns>
        public string ToString(bool utf8)
        {
            StringBuilder retVal = new StringBuilder();
            foreach(MIME_h_Parameter parameter in this.ToArray()){
                if(string.IsNullOrEmpty(parameter.Value)){
                    retVal.Append(";\r\n\t" + parameter.Name);
                }
                // We don't need to encode or split value.
                else if((utf8 ||Net_Utils.IsAscii(parameter.Value))){
                    retVal.Append(";\r\n\t" + parameter.Name + "=" + TextUtils.QuoteString(parameter.Value));
                }
                // We need to RFC 2231 encode/split value.
                else{
                    byte[] byteValue = Encoding.UTF8.GetBytes(parameter.Value);

                    List<string> values = new List<string>();            
                    // Do encoding/splitting.
                    int    offset    = 0;
                    char[] valueBuff = new char[50];
                    foreach(byte b in byteValue){                                        
                        // We need split value as RFC 2231 says.
                        if(offset >= (50 - 3)){
                            values.Add(new string(valueBuff,0,offset));
                            offset = 0;
                        }
                        
                        // Normal char, we don't need to encode.
                        if(MIME_Reader.IsAttributeChar((char)b)){
                            valueBuff[offset++] = (char)b;
                        }
                        // We need to encode byte as %X2.
                        else{
                            valueBuff[offset++] = '%';
                            valueBuff[offset++] = (b >> 4).ToString("X")[0];
                            valueBuff[offset++] = (b & 0xF).ToString("X")[0];
                        }
                    }
                    // Add pending buffer value.
                    if(offset > 0){
                        values.Add(new string(valueBuff,0,offset));
                    }

                    for(int i=0;i<values.Count;i++){
                        // Only fist value entry has charset and language info.
                        if(i == 0){
                            retVal.Append(";\r\n\t" + parameter.Name + "*" + i.ToString() + "*=" + Encoding.UTF8.WebName + "''" + values[i]);
                        }
                        else{
                            retVal.Append(";\r\n\t" + parameter.Name + "*" + i.ToString() + "*=" + values[i]);
                        }
                    }
                }
            }

            return retVal.ToString();
        }

        #endregion

        #region method Parse

        /// <summary>
        /// Parses parameters from the specified serialized value.
        /// </summary>
        /// <param name="value">The serialized parameter value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        public void Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            Parse(new MIME_Reader(value));
        }

        /// <summary>
        /// Parses parameters from the specified MIME reader.
        /// </summary>
        /// <param name="reader">The reader positioned at the parameter text.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>.</exception>
        public void Parse(MIME_Reader reader)
        {
            if(reader == null){
                throw new ArgumentNullException(nameof(reader));
            }

            /* RFC 2231.
                Asterisks ("*") are reused to provide the indicator that language and
                character set information is present and encoding is being used. A
                single quote ("'") is used to delimit the character set and language
                information at the beginning of the parameter value. Percent signs
                ("%") are used as the encoding flag, which agrees with RFC 2047.
                         
                Character set and language information may be combined with the
                parameter continuation mechanism. For example:

                Content-Type: application/x-stuff
                    title*0*=us-ascii'en'This%20is%20even%20more%20
                    title*1*=%2A%2A%2Afun%2A%2A%2A%20
                    title*2="isn't it!"

                Note that:

                (1) Language and character set information only appear at
                    the beginning of a given parameter value.

                (2) Continuations do not provide a facility for using more
                    than one character set or language in the same
                    parameter value.

                (3) A value presented using multiple continuations may
                    contain a mixture of encoded and unencoded segments.

                (4) The first segment of a continuation MUST be encoded if
                    language and character set information are given.

                (5) If the first segment of a continued parameter value is
                    encoded the language and character set field delimiters
                    MUST be present even when the fields are left blank.
            */

            KeyValueCollection<string,_ParameterBuilder> parameters = new KeyValueCollection<string,_ParameterBuilder>();

            // Parse all parameter parts.
            string[] parameterParts = TextUtils.SplitQuotedString(reader.ToEnd() ?? "",';');
            foreach(string part in parameterParts){
                if(string.IsNullOrEmpty(part)){
                    continue;
                }

                string[] name_value = part.Trim().Split(new char[]{'='},2);
                string   paramName  = name_value[0].Trim();
                string?  paramValue = null;
                if(name_value.Length == 2){
                    paramValue = TextUtils.UnQuoteString(MIME_Utils.UnfoldHeader(name_value[1].Trim()));
                }
                // Valueless parameter.
                //else{
                                
                string[] nameParts = paramName.Split('*');
                int      index     = 0;
                bool     encoded   = nameParts.Length == 3;
                // Get multi value parameter index.
                if(nameParts.Length >= 2){
                    try{
                        index = Convert.ToInt32(nameParts[1]);
                    }
                    catch{
                    }
                }

                // Single value parameter and we already have parameter with such name, skip it.
                if(nameParts.Length < 2 && parameters.ContainsKey(nameParts[0])){
                    continue;
                }

                // Parameter builder doesn't exist for the specified parameter, create it.
                if(!parameters.ContainsKey(nameParts[0])){
                    parameters.Add(nameParts[0],new _ParameterBuilder(nameParts[0]));
                }
              
                parameters[nameParts[0]].AddPart(index,encoded,paramValue);
            }

            // Build parameters from parts.
            foreach(_ParameterBuilder b in parameters){
                m_pParameters.Add(b.Name,b.GetParamter());
            }

            m_IsModified = false;
        }

        #endregion

        

        #region static method DecodeExtOctet

        /// <summary>
        /// Decodes RFC 2231 percent-encoded octets using the specified character encoding.
        /// </summary>
        /// <param name="text">The text containing RFC 2231 percent escapes.</param>
        /// <param name="charset">The character encoding used to decode the resulting bytes.</param>
        /// <returns>The decoded text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="charset"/> is <see langword="null"/>.</exception>
        private static string DecodeExtOctet(string text,Encoding charset)
        {
            if(text == null){
                throw new ArgumentNullException("text");
            }
            if(charset == null){
                throw new ArgumentNullException("charset");
            }

            int    offset        = 0;
            byte[] decodedBuffer = new byte[text.Length];            
            for(int i=0;i<text.Length;i++){
                if(text[i] == '%'){
                    decodedBuffer[offset++] = byte.Parse(text[i + 1].ToString() + text[i + 2].ToString(),System.Globalization.NumberStyles.HexNumber);
                    i += 2;
                }
                else{
                    decodedBuffer[offset++] = (byte)text[i];
                }
            }

            return charset.GetString(decodedBuffer,0,offset);
        }

        #endregion


        #region interface IEnumerator

        /// <summary>
        /// Returns an enumerator that iterates through the parameters.
        /// </summary>
        /// <returns>An enumerator for the parameters.</returns>
        public IEnumerator<MIME_h_Parameter> GetEnumerator()
        {
            return m_pParameters.Values.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the parameters.
        /// </summary>
        /// <returns>An enumerator for the parameters.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_pParameters.Values.GetEnumerator();
        }

        #endregion

        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether the parameters have been modified
        /// since they were parsed.
        /// </summary>
        public bool IsModified
        {
            get{
                if(m_IsModified){
                    return true;
                }
                else{
                    foreach(MIME_h_Parameter parameter in this.ToArray()){
                        if(parameter.IsModified){
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the MIME header field that owns this collection.
        /// </summary>
        public MIME_h Owner
        {
            get{ return m_pOwner; }
        }

        /// <summary>
        /// Gets the number of parameters in the collection.
        /// </summary>
        public int Count
        {
            get{ return m_pParameters.Count; }
        }

        /// <summary>
        /// Gets or sets a parameter value. A <see langword="null"/> value
        /// means that the parameter is not specified.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>The parameter value, or <see langword="null"/> if the parameter does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
        public string? this[string name]
        {
            get{
                if(name == null){
                    throw new ArgumentNullException(nameof(name));
                }

                MIME_h_Parameter? retVal = null;
                if(m_pParameters.TryGetValue(name,out retVal)){
                    return retVal.Value;
                }
                else{
                    return null;
                }
            }

            set{
                if(name == null){
                    throw new ArgumentNullException(nameof(name));
                }

                MIME_h_Parameter? retVal = null;
                if(m_pParameters.TryGetValue(name,out retVal)){
                    retVal.Value = value;
                }
                else{
                    m_pParameters.Add(name,new MIME_h_Parameter(name,value));
                }
            }
        }

        #endregion
    }
}
