using System;
using System.Collections;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a collection of MIME header fields.
    /// </summary>
    public class MIME_h_Collection : IEnumerable<MIME_h>
    {
        private bool            m_IsModified = false;
        private MIME_h_Provider m_pProvider;
        private List<MIME_h>    m_pFields;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MIME_h_Collection"/> class.
        /// </summary>
        /// <param name="provider">
        /// Header field provider.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="provider"/> is
        /// <see langword="null"/>.
        /// </exception>
        public MIME_h_Collection(MIME_h_Provider provider)
        {
            if(provider == null){
                throw new ArgumentNullException(nameof(provider));
            }

            m_pProvider = provider;

            m_pFields = new List<MIME_h>();
        }


        #region method Insert

        /// <summary>
        /// Inserts a header field into the collection at the specified index.
        /// </summary>
        /// <param name="index">
        /// Zero-based index at which to insert the header field.
        /// </param>
        /// <param name="field">
        /// Header field to insert.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Raised when <paramref name="index"/> is out of range.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="field"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Insert(int index,MIME_h field)
        {
            if(index < 0 || index > m_pFields.Count){
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if(field == null){
                throw new ArgumentNullException(nameof(field));
            }

            m_pFields.Insert(index,field);
            m_IsModified = true;
        }

        #endregion

        #region method Add

        /// <summary>
        /// Parses and adds a header field to the end of the collection.
        /// </summary>
        /// <param name="field">
        /// Header field in the format <c>name: value</c>.
        /// </param>
        /// <returns>
        /// Returns the added header field.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="field"/> is
        /// <see langword="null"/>.
        /// </exception>
        public MIME_h Add(string field)
        {
            if(field == null){
                throw new ArgumentNullException(nameof(field));
            }
   
            MIME_h h = m_pProvider.Parse(field);
            m_pFields.Add(h);
            m_IsModified = true;

            return h;
        }

        /// <summary>
        /// Adds specified header field to the end of the collection.
        /// </summary>
        /// <param name="field">Header field to add.</param>
        /// <exception cref="ArgumentNullException">Is raised when <b>field</b> is null reference value.</exception>
        public void Add(MIME_h field)
        {
            if(field == null){
                throw new ArgumentNullException("field");
            }

            m_pFields.Add(field);
            m_IsModified = true;
        }

        #endregion

        #region method Remove

        /// <summary>
        /// Removes the specified header field from the collection.
        /// </summary>
        /// <param name="field">
        /// Header field to remove.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="field"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Remove(MIME_h field)
        {
            if(field == null){
                throw new ArgumentNullException(nameof(field));
            }

            m_pFields.Remove(field);
            m_IsModified = true;
        }

        #endregion

        #region method RemoveAll

        /// <summary>
        /// Removes all header fields with the specified name.
        /// </summary>
        /// <param name="name">
        /// Header field name.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="name"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="name"/> is empty.
        /// </exception>
        public void RemoveAll(string name)
        {
            if(name == null){
                throw new ArgumentNullException(nameof(name));
            }
            if(name == string.Empty){
                throw new ArgumentException("Argument 'name' value must be specified.",nameof(name));
            }

            foreach(MIME_h field in m_pFields.ToArray()){
                if(string.Compare(name,field.Name,true) == 0){
                    m_pFields.Remove(field);
                }
            }
            m_IsModified = true;
        }

        #endregion

        #region method Clear

        /// <summary>
        /// Removes all header fields from the collection.
        /// </summary>
        public void Clear()
        {
            m_pFields.Clear();
            m_IsModified = true;
        }

        #endregion

        #region method Contains

        /// <summary>
        /// Determines whether the collection contains a header field with the
        /// specified name.
        /// </summary>
        /// <param name="name">
        /// Header field name.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if a matching header field exists;
        /// otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="name"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="name"/> is empty.
        /// </exception>
        public bool Contains(string name)
        {
            if(name == null){
                throw new ArgumentNullException(nameof(name));
            }
            if(name == string.Empty){
                throw new ArgumentException("Argument 'name' value must be specified.",nameof(name));
            }

            foreach(MIME_h field in m_pFields.ToArray()){
                if(string.Compare(name,field.Name,true) == 0){
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the collection contains the specified header
        /// field.
        /// </summary>
        /// <param name="field">
        /// Header field to locate in the collection.
        /// </param>
        /// <returns>
        /// Returns <see langword="true"/> if the specified header field exists
        /// in the collection; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="field"/> is
        /// <see langword="null"/>.
        /// </exception>
        public bool Contains(MIME_h field)
        {
            if(field == null){
                throw new ArgumentNullException(nameof(field));
            }

            return m_pFields.Contains(field);
        }

        #endregion

        #region method GetFirst

        /// <summary>
        /// Gets the first header field with the specified name.
        /// </summary>
        /// <param name="name">
        /// Header field name.
        /// </param>
        /// <returns>
        /// Returns the first matching header field, or
        /// <see langword="null"/> if no matching header field exists in the
        /// collection.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="name"/> is
        /// <see langword="null"/>.
        /// </exception>
        public MIME_h? GetFirst(string name)
        {
            if(name == null){
                throw new ArgumentNullException(nameof(name));
            }

            foreach(MIME_h field in m_pFields.ToArray()){
                if(string.Equals(name,field.Name,StringComparison.InvariantCultureIgnoreCase)){
                    return field;
                }
            }

            return null;
        }

        #endregion

        #region method ReplaceFirst

        /// <summary>
        /// Replaces the first header field with the same name as the specified
        /// header field.
        /// </summary>
        /// <param name="field">
        /// Header field to use as the replacement.
        /// </param>
        /// <remarks>
        /// If no matching header field exists, no action is taken.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="field"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void ReplaceFirst(MIME_h field)
        {
            if(field == null){
                throw new ArgumentNullException(nameof(field));
            }

            for(int i=0;i<m_pFields.Count;i++){
                if(string.Equals(field.Name,m_pFields[i].Name,StringComparison.CurrentCultureIgnoreCase)){
                    m_pFields.RemoveAt(i);
                    m_pFields.Insert(i,field);

                    return;
                }
            }
        }

        #endregion

        #region method ToArray

        /// <summary>
        /// Copies the header fields in the collection to a new array.
        /// </summary>
        /// <returns>
        /// Returns a new array containing the header fields in the collection.
        /// </returns>
        public MIME_h[] ToArray()
        {
            return m_pFields.ToArray();
        }

        #endregion


        #region method ToStream

        /// <summary>
        /// Stores the header collection to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream to which to write the header collection.
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
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="stream"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// This method blocks until the header collection has been completely
        /// written. For asynchronous operation, use
        /// <see cref="ToStreamAsync(Stream,bool,bool,CancellationToken)"/>.
        /// </remarks>
        public void ToStream(Stream stream,bool utf8Headers = false,bool reEncodeHeaders = false)
        {
            ToStreamAsync(stream,utf8Headers,reEncodeHeaders,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToStreamAsync

        /// <summary>
        /// Writes the header collection to the specified stream.
        /// </summary>
        /// <param name="stream">
        /// Stream to which to write the headers.
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
        internal protected async Task ToStreamAsync(Stream stream,bool utf8Headers,bool reEncodeHeaders,CancellationToken cancellationToken = default)
        {
            if(stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            byte[] header = Encoding.UTF8.GetBytes(ToString(utf8Headers,reEncodeHeaders));
            await stream.WriteAsync(header,cancellationToken);
        }

        #endregion

        #region method ToFile

        /// <summary>
        /// Stores the header collection to the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File to which to write the header collection.
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
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        /// <remarks>
        /// This method blocks until the header collection has been completely
        /// written. For asynchronous operation, use
        /// <see cref="ToFileAsync(string,bool,bool,CancellationToken)"/>.
        /// </remarks>
        public void ToFile(string fileName,bool utf8Headers = false,bool reEncodeHeaders = false)
        {
            ToFileAsync(fileName,utf8Headers,reEncodeHeaders,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToFileAsync

        /// <summary>
        /// Stores the header collection to the specified file.
        /// </summary>
        /// <param name="fileName">
        /// File to which to write the header collection.
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
        /// Raised when <paramref name="fileName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="fileName"/> is empty.
        /// </exception>
        public async Task ToFileAsync(
            string fileName,
            bool utf8Headers = false,
            bool reEncodeHeaders = false,
            CancellationToken cancellationToken = default
        )
        {
            if(fileName == null){
                throw new ArgumentNullException(nameof(fileName));
            }
            if(fileName.Length == 0){
                throw new ArgumentException("Argument 'fileName' cannot be empty.",nameof(fileName));
            }

            await using(FileStream stream = File.Create(fileName)){
                await ToStreamAsync(stream,utf8Headers,reEncodeHeaders,cancellationToken);
            }
        }

        #endregion

        #region method ToByteArray

        /// <summary>
        /// Stores the header collection to a byte array.
        /// </summary>
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
        /// <returns>
        /// Returns the serialized header collection.
        /// </returns>
        /// <remarks>
        /// This method blocks until the header collection has been completely
        /// serialized. For asynchronous operation, use
        /// <see cref="ToByteArrayAsync(bool,bool,CancellationToken)"/>.
        /// </remarks>
        public byte[] ToByteArray(bool utf8Headers,bool reEncodeHeaders)
        {
            return ToByteArrayAsync(utf8Headers,reEncodeHeaders,CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region method ToByteArrayAsync

        /// <summary>
        /// Stores the header collection to a byte array.
        /// </summary>
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
        /// <returns>
        /// Returns the serialized header collection.
        /// </returns>
        public async Task<byte[]> ToByteArrayAsync(
            bool utf8Headers,
            bool reEncodeHeaders,
            CancellationToken cancellationToken = default
        )
        {
            using(MemoryStream stream = new MemoryStream()){
                await ToStreamAsync(stream,utf8Headers,reEncodeHeaders,cancellationToken);

                return stream.ToArray();
            }
        }

        #endregion

        #region override method ToString

        /// <summary>
        /// Returns the serialized MIME header collection.
        /// </summary>
        /// <returns>
        /// Returns the serialized MIME header collection.
        /// </returns>
        public override string ToString()
        {
            return ToString(true,false);
        }

        /// <summary>
        /// Returns a string representation of the header collection.
        /// </summary>
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
        /// <returns>
        /// Returns the serialized header collection.
        /// </returns>
        public string ToString(bool utf8Headers,bool reEncodeHeaders)
        {
            StringBuilder retVal = new StringBuilder();
            foreach(MIME_h field in m_pFields){
                retVal.Append(field.ToString(utf8Headers,reEncodeHeaders));
            }

            return retVal.ToString();
        }

        #endregion


        #region method ParseFromString

        /// <summary>
        /// Parses a raw MIME header string into individual header fields while strictly 
        /// enforcing RFC 5322 CRLF line endings, preventing bare CR/LF vulnerabilities, and preserving 
        /// exact raw byte-level formatting and folding.
        /// </summary>
        /// <param name="rawHeader">The raw MIME header string to parse.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="rawHeader"/> is null.</exception>
        /// <exception cref="InvalidDataException">Thrown if a bare CR or bare LF line ending is encountered.</exception>
        internal void ParseFromString(string rawHeader)
        {
            if(rawHeader == null){
                throw new ArgumentNullException(nameof(rawHeader));
            }

            ReadOnlySpan<char> span = rawHeader.AsSpan();
            int lineStart = 0;
            string? currentHeader = null;

            while(lineStart < span.Length){
                int idx = span.Slice(lineStart).IndexOfAny('\r','\n');
                if(idx == -1){
                    // Tail end without newline terminator
                    int remainingLength = span.Length - lineStart;
                    if(remainingLength > 0){
                        ReadOnlySpan<char> tail = span.Slice(lineStart, remainingLength).TrimEnd();
                        if(!tail.IsEmpty){
                            if(tail[0] == ' ' || tail[0] == '\t'){
                                if (currentHeader != null) currentHeader += "\r\n" + tail.ToString();
                            }
                            else{
                                if (currentHeader != null) Add(currentHeader);
                                currentHeader = tail.ToString();
                            }
                        }
                    }
                    break;
                }

                int absoluteIdx = lineStart + idx;

                if(span[absoluteIdx] == '\r'){
                    if(absoluteIdx + 1 >= span.Length || span[absoluteIdx + 1] != '\n'){
                        throw new InvalidDataException("Malformed MIME header: Bare CR detected without following LF.");
                    }

                    int lineEnd = absoluteIdx + 2;
                    ReadOnlySpan<char> lineSpan = span.Slice(lineStart, (lineEnd - 2) - lineStart);

                    // Empty line marks the end of the header block
                    if(lineSpan.IsEmpty){
                        lineStart = lineEnd;
                        break;
                    }

                    // Check for multiline folding (FWS: starts with space or tab)
                    if(lineSpan[0] == ' ' || lineSpan[0] == '\t'){
                        if(currentHeader != null){
                            currentHeader += "\r\n" + lineSpan.ToString();
                        }
                    }
                    else{
                        // Flush the previous completed header field
                        if(currentHeader != null){
                            Add(currentHeader);
                        }
                        currentHeader = lineSpan.ToString();
                    }

                    lineStart = lineEnd;
                }
                else{
                    throw new InvalidDataException("Malformed MIME header: Bare LF detected without preceding CR.");
                }
            }

            // Flush the final remaining header
            if(currentHeader != null){
                Add(currentHeader);
            }
        }

        #endregion


        #region interface IEnumerator

        /// <summary>
        /// Returns an enumerator that iterates through the collection of typed MIME headers.
        /// </summary>
        /// <returns>A generic enumerator for <see cref="MIME_h"/> instances.</returns>
        public IEnumerator<MIME_h> GetEnumerator()
        {
            return m_pFields.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection of MIME headers.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

		#endregion

        #region Properties implementation

        /// <summary>
        /// Gets whether the collection or any contained header field has been modified since it was loaded.
        /// </summary>
        public bool IsModified
        {            
            get{
               if(m_IsModified){
                   return true;
               }

                foreach(MIME_h f in m_pFields){
                    if(f.IsModified){
                        return true;
                    }
                }

                return false; 
            }
        }

        /// <summary>
        /// Gets the number of header fields in the collection.
        /// </summary>
        public int Count
        {
            get{ return m_pFields.Count; }
        }

        /// <summary>
        /// Gets the header field at the specified index.
        /// </summary>
        /// <param name="index">
        /// Zero-based index of the header field to get.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Is raised when <paramref name="index"/> is outside the bounds of the collection.
        /// </exception>
        public MIME_h this[int index]
        {
            get{ 
                if(index < 0 || index >= m_pFields.Count){
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return m_pFields[index]; 
            }
        }

        /// <summary>
        /// Gets all header fields with the specified name.
        /// </summary>
        /// <param name="name">
        /// Header field name.
        /// </param>
        /// <returns>
        /// Returns an array containing all matching header fields.
        /// Returns an empty array if no matching header fields exist.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Is raised when <paramref name="name"/> is null.
        /// </exception>
        public MIME_h[] this[string name]
        {
            get{
                if(name == null){
                    throw new ArgumentNullException(nameof(name));
                }

                List<MIME_h> retVal = new List<MIME_h>();
                foreach(MIME_h field in m_pFields.ToArray()){
                    if(string.Compare(name,field.Name,true) == 0){
                        retVal.Add(field);
                    }
                }

                return retVal.ToArray(); 
            }
        }

        /// <summary>
        /// Gets header fields provider.
        /// </summary>
        public MIME_h_Provider FieldsProvider
        {
            get{ return m_pProvider; }
        }

        #endregion
    }
}
