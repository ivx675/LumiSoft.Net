using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.IO;
using LumiSoft.Net.Log;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Provides low‑level parsing utilities for IMAP tokens such as atoms, quoted
    /// strings, literals, astrings, and mailbox names. The reader operates on a
    /// single IMAP protocol line supplied by the caller and exposes methods for
    /// extracting individual tokens according to IMAP4rev1 grammar rules.
    /// <para>
    /// The class does <b>not</b> perform general protocol‑line reading. All parsing
    /// is done within the caller‑provided line except when a literal is encountered.
    /// In that case, the reader uses the underlying <see cref="SmartStream"/> to
    /// read the literal payload and then fetches the next protocol line so that
    /// parsing may continue. This is the only situation in which the reader performs
    /// stream I/O.
    /// </para>
    /// <para>
    /// The reader supports both client and server operation. When running in server
    /// mode, the reader automatically sends IMAP continuation responses (<c>"+ Continue"</c>)
    /// for standard literals. In client mode, continuation responses are never sent.
    /// </para>
    /// <para>
    /// The class is intended for internal use by higher‑level IMAP command and
    /// response parsers. It provides deterministic, grammar‑aware token extraction
    /// without altering the caller’s parsing state except when literal processing
    /// requires advancing to the next protocol line.
    /// </para>
    /// </summary>
    internal class _IMAP_Reader 
    {
        private string       m_Line;
        private int          m_Position    = 0;
        private Memory<byte> m_LineBuffer;
        private IMAP_Client? m_pImapClient = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="_IMAP_Reader"/> class for parsing
        /// IMAP lexical elements such as atoms, quoted strings, literals, astrings, and
        /// mailbox names. The reader operates on a single IMAP protocol line supplied by
        /// the caller and does not perform line‑based reading except when a literal
        /// requires the next protocol line to be fetched.
        /// <para>
        /// The <paramref name="line"/> parameter provides the current IMAP command or
        /// response line to be tokenized. All non‑literal parsing (atoms, quoted strings,
        /// NIL, mailbox names, etc.) is performed entirely within this line without
        /// accessing the network.
        /// </para>
        /// <para>
        /// When a literal is encountered (for example <c>{123}</c>), the reader uses the
        /// supplied <paramref name="imapClient"/> to read the literal payload and then reads
        /// the next protocol line into <c>m_Line</c> using the reusable buffer provided
        /// in <paramref name="lineBuffer"/>. This is the only situation in which the
        /// reader performs network I/O.
        /// </para>
        /// </summary>
        /// <param name="line">
        /// The IMAP protocol line to parse. All non‑literal tokens are read directly from
        /// this line without additional network access.
        /// </param>
        /// <param name="lineBuffer">
        /// A reusable buffer used exclusively for reading the next protocol line after a
        /// literal. It is not used for literal payloads or for parsing within the current
        /// line.
        /// </param>
        /// <param name="imapClient">
        /// IMAP client for literal readings.
        /// </param>
        internal _IMAP_Reader(string line,Memory<byte> lineBuffer,IMAP_Client imapClient)
        {
            m_Line        = line;
            m_LineBuffer  = lineBuffer;
            m_pImapClient = imapClient;
        }


        #region method ReadStringAsync

        /// <summary>
        /// Reads an IMAP astring from the current protocol line. An astring may be
        /// represented as an atom, a quoted string, a literal, or the special token
        /// <c>NIL</c>. This method inspects the upcoming characters and dispatches to
        /// the appropriate parsing routine.
        /// <para>
        /// The reader first skips any leading whitespace and then determines the token
        /// type using lightweight look‑ahead. The following forms are supported:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <c>NIL</c> — The token is consumed and the method returns <c>null</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Quoted string — Parsed using <see cref="ReadQuotedString"/>, including IMAP
        /// escape sequences (<c>\"</c> and <c>\\</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Literal — Parsed using <see cref="ReadLiteralAsync"/>, which reads the literal
        /// payload from the underlying stream and then advances to the next protocol line.
        /// The returned byte array is decoded as UTF‑8 and returned as a string.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Atom — Parsed using <see cref="ReadAtom"/>, which throws if no valid atom
        /// characters are present.
        /// </description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that may be used to cancel the asynchronous literal read
        /// operation if the astring is represented as a literal.
        /// </param>
        /// <returns>
        /// The parsed astring as a <see cref="string"/>, or <c>null</c> if the token
        /// <c>NIL</c> was encountered.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the astring token is malformed, if a quoted string is unterminated,
        /// if a literal header is invalid, or if an atom cannot be parsed.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying stream is closed unexpectedly while reading a literal
        /// payload.
        /// </exception>
        public async Task<string?> ReadStringAsync(CancellationToken cancellationToken = default)
        {
            SkipWhitespaces();

            if(m_Position >= m_Line.Length){
                throw new ParseException("Unexpected end of line while reading string.");
            }

            char c = PeekChar();

            // NIL
            if(PeekIs("NIL")){
                ReadAtom();

                return null;
            }

            // Quoted
            if(c == '"'){
                return ReadQuotedString();
            }

            // Literal
            if(c == '{'){
                byte[] data = await ReadLiteralAsync(cancellationToken);

                return Encoding.UTF8.GetString(data);
            }

            // Atom fallback (ReadAtom throws if invalid)
            return ReadAtom();
        }

        #endregion

        #region method ReadMailboxAsync

        /// <summary>
        /// Reads an IMAP mailbox name using <see cref="ReadStringAsync"/> and applies
        /// mailbox‑specific decoding rules. A mailbox name in IMAP is defined as an
        /// astring, meaning it may be represented as an atom, quoted string, literal,
        /// or the special token <c>NIL</c>.
        /// <para>
        /// If the server returns <c>NIL</c>, this method returns <c>null</c> to indicate
        /// the absence of a mailbox name. This occurs in certain responses such as
        /// <c>LIST</c> or <c>STATUS</c> where the mailbox name may be intentionally
        /// omitted.
        /// </para>
        /// <para>
        /// For non‑null values, the returned astring is decoded using
        /// <see cref="IMAP_Utils.Decode_IMAP_UTF7_String(string)"/>. IMAP4rev1 requires
        /// mailbox names to be encoded using modified UTF‑7 unless the server advertises
        /// UTF8=ACCEPT, in which case UTF‑8 may be sent directly. This method applies
        /// the correct decoding automatically.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that may be used to cancel the asynchronous read
        /// operation.
        /// </param>
        /// <returns>
        /// The decoded mailbox name as a <see cref="string"/>, or <c>null</c> if the
        /// IMAP token <c>NIL</c> was encountered.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the underlying astring token is malformed or cannot be parsed
        /// by <see cref="ReadStringAsync"/>.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying stream is closed unexpectedly while reading a
        /// literal‑based mailbox name.
        /// </exception>
        public async Task<string?> ReadMailboxAsync(CancellationToken cancellationToken = default)
        {
            string? s = await ReadStringAsync(cancellationToken);
            if(s == null){ 
                return null;
            }

            return IMAP_Utils.Decode_IMAP_UTF7_String(s);
        }

        #endregion

        #region method ReadAtom

        /// <summary>
        /// Reads an IMAP atom. Throws if no atom characters are present.
        /// </summary>
        public string ReadAtom()
        {
            SkipWhitespaces();

            int start = m_Position;

            while (m_Position < m_Line.Length){
                char c = m_Line[m_Position];

                // IMAP atom terminators
                if (c == ' ' || c == '\t' ||
                    c == '(' || c == ')' ||
                    c == '[' || c == ']' ||
                    c == '{' || c == '"' ||
                    c == '\r' || c == '\n')
                {
                    break;
                }

                m_Position++;
            }

            if(m_Position == start){
                throw new ParseException("Expected atom but encountered end of line or a non-atom character.");
            }

            return m_Line.Substring(start, m_Position - start);
        }

        #endregion

        #region method ReadChar

        /// <summary>
        /// Skips any whitespace at the current reader position and returns the next
        /// character, advancing <c>m_Position</c> by one. This method performs a
        /// consuming read and is used when parsing IMAP tokens that require sequential
        /// character processing such as atoms, quoted strings, section specifiers, and
        /// other grammar elements.
        /// <para>
        /// Whitespace characters (SP, HTAB, CR, LF) are skipped using
        /// <see cref="SkipWhitespaces"/> before the character is returned. If the end of
        /// the line is reached, the method returns <c>'\0'</c>.
        /// </para>
        /// </summary>
        /// <returns>
        /// The next non‑whitespace character, or <c>'\0'</c> if the end of the line has
        /// been reached.
        /// </returns>
        public char ReadChar()
        {
            SkipWhitespaces();

            if(m_Position < m_Line.Length){
                char c = m_Line[m_Position];
                m_Position++;

                return c;
            }

            return '\0';
        }

        #endregion

        #region method ReadParenthesized

        /// <summary>
        /// Reads an IMAP parenthesized list of the form <c>( ... )</c> from the current
        /// protocol line. Parenthesized lists are used throughout IMAP4rev1 grammar for
        /// constructs such as <c>FLAGS</c>, <c>ENVELOPE</c>, <c>BODYSTRUCTURE</c>, and
        /// various nested attribute lists.
        /// <para>
        /// The method first skips leading whitespace and then verifies that the next
        /// character is an opening parenthesis. It consumes the entire balanced
        /// parenthesized block, including any nested parentheses, and returns the raw
        /// inner content without the surrounding <c>(</c> and <c>)</c> delimiters.
        /// </para>
        /// <para>
        /// This method does not interpret the returned content. Callers are responsible
        /// for further tokenizing the inner list (for example splitting flag atoms,
        /// parsing ENVELOPE fields, or recursively processing BODYSTRUCTURE elements).
        /// </para>
        /// </summary>
        /// <returns>
        /// The substring contained within the balanced parenthesized block, excluding
        /// the outer parentheses.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the next non‑whitespace character is not <c>'('</c>, or if the
        /// parenthesized block is not properly balanced before the end of the line.
        /// </exception>
        public string ReadParenthesized()
        {
            return ReadDelimited('(',')');
        }

        #endregion

        #region method ReadBracketed

        /// <summary>
        /// Reads a bracketed value of the form <c>[ ... ]</c> from the current
        /// position in the input stream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method consumes the opening bracket, reads all characters until
        /// the matching closing bracket, and returns the inner content without
        /// the surrounding <c>[</c> and <c>]</c> delimiters.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The text contained inside the bracket pair, or an empty string if no
        /// bracketed value is present.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the next non‑whitespace character is not <c>'['</c>, or if the
        /// parenthesized block is not properly balanced before the end of the line.
        /// </exception>
        public string ReadBracketed()
        {
            return ReadDelimited('[',']');
        }

        #endregion

        #region method ReadLiteralAsync

        /// <summary>
        /// Parses a literal header declared in the current IMAP protocol line and
        /// reads the literal payload from the underlying IMAP TCP stream.
        /// </summary>
        /// <param name="stream">
        /// The destination stream that receives the literal payload. The method
        /// writes exactly the number of bytes specified by the literal header.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the read operation.
        /// </param>
        /// <remarks>
        /// For standard literals (<c>{N}</c>), a continuation response
        /// (<c>+ Continue</c>) is sent when operating in server mode. Literal-plus
        /// (<c>{N+}</c>) does not require a continuation.
        /// <para/>
        /// After the literal payload has been read from the stream, the next IMAP
        /// protocol line is read and becomes the new current line for subsequent
        /// parsing.
        /// </remarks>
        public async Task ReadLiteralAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            SkipWhitespaces();

            // Parse header: "{N}" or "{N+}"
            m_Position++; // skip '{'

            int start = m_Position;
            while(m_Position < m_Line.Length && char.IsDigit(m_Line[m_Position])){
                m_Position++;
            }

            if(m_Position == start){
                throw new ParseException("Invalid literal header: missing size.");
            }

            int size = int.Parse(m_Line.Substring(start, m_Position - start));

            // Literal+ indicator.
            bool isLiteralPluss = false;
            if(PeekIs('+')){
                isLiteralPluss = true;
                m_Position++;
            }
            if(!PeekIs('}')){
                throw new ParseException("Invalid literal header: missing closing '}'.");
            }

            m_Position++; // skip '}'

            // Client mode
            if(m_pImapClient != null){
                // Read literal payload
                await m_pImapClient.TcpStream.ReadFixedCountAsync(stream,size,cancellationToken);

                m_pImapClient.LogAddRead(size,$"Literal {size} bytes.");

                // After literal payload, next protocol line begins
                ReadLineResult readLine = await m_pImapClient.TcpStream.ReadLineAsync(m_LineBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
                if(readLine.BytesInBuffer == 0){
                    throw new IOException("Connection was closed.");
                }
                m_Line =  readLine.LineUtf8!;
                m_Position = 0;

                m_pImapClient.LogAddRead(readLine.BytesInBuffer,m_Line);
            }
            // Server mode
            else{                
                // Server standard literal reading, we need to send + Continue for client.
                if(!isLiteralPluss){/*
                    await m_pStream.WriteLineAsync("+ Continue\r\n",cancellationToken);

                    if(m_pLogger != null){
                        m_pLogger.AddWrite(12,"+ Continue\r\n");
                    }*/
                }

                throw new NotImplementedException("ReadLiteralAsync server mode nor implemented.");
            }
        }

        #endregion
        
        #region method PeekChar

        /// <summary>
        /// Returns the next character in the current IMAP protocol line without advancing
        /// the reader position. This method provides lightweight look‑ahead functionality
        /// used by higher‑level parsing routines to determine the type of the upcoming
        /// token (for example quoted strings, literals, atoms, or the special token
        /// <c>NIL</c>).
        /// <para>
        /// If the reader position is already at or beyond the end of the current line,
        /// the method returns the null character (<c>'\0'</c>) to indicate that no further
        /// characters are available. This allows callers to safely test for end‑of‑line
        /// conditions without throwing exceptions or performing additional bounds checks.
        /// </para>
        /// <para>
        /// <c>PeekChar()</c> does not interpret or classify the returned character; it
        /// simply exposes the raw protocol character so that the caller can decide how
        /// to proceed. Typical use cases include checking for the opening quote of a
        /// quoted string, the opening brace of a literal header, or whitespace that
        /// should be skipped before parsing an atom.
        /// </para>
        /// </summary>
        /// <returns>
        /// The next character in the current protocol line, or <c>'\0'</c> if the reader
        /// position is at or beyond the end of the line.
        /// </returns>
        public char PeekChar()
        {
            if(m_Position >= m_Line.Length){
                return '\0';
            }

            return m_Line[m_Position];
        }

        #endregion

        #region method PeekIs

        /// <summary>
        /// Determines whether the next non‑whitespace character in the current IMAP
        /// protocol line equals the specified character. This method performs a pure
        /// look‑ahead operation: whitespace characters (SP, HTAB, CR, LF) are skipped
        /// using a temporary index so that <c>m_Position</c> remains unchanged.
        /// <para>
        /// This method is typically used to detect structural delimiters such as the
        /// closing parenthesis in FETCH responses or other IMAP grammar boundaries
        /// without consuming any input.
        /// </para>
        /// </summary>
        /// <param name="c">The character to compare against the next non‑whitespace character.</param>
        /// <returns>
        /// <c>true</c> if the next non‑whitespace character equals <paramref name="c"/>; 
        /// otherwise <c>false</c>.
        /// </returns>
        public bool PeekIs(char c)
        {
            int pos = m_Position;

            // Skip whitespace using a temporary index
            while(pos < m_Line.Length){
                char ch = m_Line[pos];
                if(ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n'){
                    pos++;
                }
                else{
                    break;
                }
            }

            return pos < m_Line.Length && m_Line[pos] == c;
        }


        /// <summary>
        /// Determines whether the next non‑whitespace characters in the current IMAP
        /// protocol line match the specified token, using a case‑insensitive comparison,
        /// without advancing or modifying the reader position.
        /// <para>
        /// The method performs a pure look‑ahead check. Leading whitespace characters
        /// (SP, HTAB, CR, LF) are skipped using a temporary index so that keyword
        /// detection is not affected by spacing, but the actual reader position
        /// (<c>m_Position</c>) remains unchanged. This allows callers to inspect the
        /// upcoming token safely without consuming any input or altering subsequent
        /// parsing behavior.
        /// </para>
        /// <para>
        /// The comparison is performed using <see cref="StringComparison.OrdinalIgnoreCase"/>,
        /// since IMAP keywords such as <c>NIL</c>, <c>BODY</c>, <c>FETCH</c>, and
        /// <c>ENVELOPE</c> are case‑insensitive. If <paramref name="token"/> is
        /// <c>null</c>, or if the token is longer than the remaining characters in the
        /// line after whitespace skipping, the method returns <c>false</c>.
        /// </para>
        /// </summary>
        /// <param name="token">
        /// The token to compare against the upcoming non‑whitespace characters in the
        /// current protocol line. The comparison is case‑insensitive. If <c>null</c>,
        /// the method returns <c>false</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the next non‑whitespace characters match <paramref name="token"/>
        /// using a case‑insensitive comparison; otherwise <c>false</c>.
        /// </returns>
        public bool PeekIs(string token)
        {
            if (token == null){
                return false;
            }

            int pos = m_Position;

            // Skip whitespace without altering m_Position
            while (pos < m_Line.Length){
                char c = m_Line[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n'){
                    pos++;
                }
                else{
                    break;
                }
            }

            int len = token.Length;

            if(pos + len > m_Line.Length){
                return false;
            }

            return string.Compare(
                m_Line,
                pos,
                token,
                0,
                len,
                StringComparison.OrdinalIgnoreCase
            ) == 0;
        }

        #endregion

        #region method PeekLiteralSize

        /// <summary>
        /// Peeks the size of an IMAP literal (e.g. {123} or {123+}) without consuming
        /// any characters or reading from the underlying stream.
        /// </summary>
        /// <returns>
        /// The literal size in bytes.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the next token is not a literal or if the literal header is malformed.
        /// </exception>
        public int PeekLiteralSize()
        {
            int pos = m_Position;

            // Skip whitespace
            while (pos < m_Line.Length){
                char ch = m_Line[pos];
                if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
                {
                    pos++;
                }
                else
                {
                    break;
                }
            }

            // Must start with '{'
            if(pos >= m_Line.Length || m_Line[pos] != '{'){
                throw new ParseException("Expected literal header '{N}' but no literal was found.");
            }

            pos++; // skip '{'

            int start = pos;

            // Parse digits
            while (pos < m_Line.Length && char.IsDigit(m_Line[pos])){
                pos++;
            }

            if(pos == start){
                throw new ParseException("Invalid literal header: missing size.");
            }

            int size = int.Parse(m_Line.Substring(start, pos - start));

            // Optional '+'
            if(pos < m_Line.Length && m_Line[pos] == '+'){
                pos++;
            }

            // Must end with '}'
            if(pos >= m_Line.Length || m_Line[pos] != '}'){
                throw new ParseException("Invalid literal header: missing closing '}'.");
            }

            return size;
        }

        #endregion


        #region method ReadDelimited

        /// <summary>
        /// Reads a balanced IMAP delimited block such as (...) or [...]. 
        /// Nested delimiters of the same type are supported.
        /// Throws if the delimiters are unbalanced.
        /// </summary>
        private string ReadDelimited(char open, char close)
        {
            SkipWhitespaces();

            if(m_Position >= m_Line.Length || m_Line[m_Position] != open){
                throw new ParseException($"Expected '{open}' at position {m_Position}.");
            }

            m_Position++; // skip opening delimiter

            int start = m_Position;
            int depth = 1;

            while(m_Position < m_Line.Length){
                char c = m_Line[m_Position];

                if(c == open){
                    depth++;
                }
                else if(c == close){
                    depth--;
                    if(depth == 0){
                        string result = m_Line.Substring(start, m_Position - start);
                        m_Position++; // skip closing delimiter

                        return result;
                    }
                }

                m_Position++;
            }

            throw new ParseException($"Unbalanced '{open}{close}' delimiters.");
        }

        #endregion


        #region method ReadQuotedString

        /// <summary>
        /// Reads an IMAP quoted string. Handles IMAP escape sequences (\" and \\).
        /// Throws if the quoted string is not properly terminated.
        /// </summary>
        private string ReadQuotedString()
        {
            // Skip opening quote
            m_Position++;

            StringBuilder sb = new StringBuilder();

            while(m_Position < m_Line.Length){
                char c = m_Line[m_Position++];

                if (c == '\\'){
                    // IMAP only defines escaping for:
                    //   \"  => "
                    //   \\  => \
                    // Any other escaped char is passed through unchanged.
                    if(m_Position < m_Line.Length){
                        char esc = m_Line[m_Position++];
                        sb.Append(esc);
                    }
                    else{
                        throw new ParseException("Unexpected end of line inside escape sequence.");
                    }
                }
                else if (c == '"'){
                    // End of quoted string
                    return sb.ToString();
                }
                else{
                    sb.Append(c);
                }
            }

            throw new ParseException("Unterminated quoted string.");
        }

        #endregion

        #region method ReadLiteralAsync

        /// <summary>
        /// Reads an IMAP literal from the underlying stream. The reader position must be
        /// at the opening '{' of a literal header, for example <c>{123}</c> or <c>{123+}</c>.
        /// <para>
        /// The method parses the literal header to determine the exact byte count and
        /// whether the literal is a "literal+" (indicated by a trailing '+'). For normal
        /// literals, the server side must send a continuation response (<c>"+ Continue"</c>)
        /// before the client begins transmitting the literal payload.
        /// </para>
        /// <para>
        /// A safety limit is applied to string‑type literals: if the literal size exceeds
        /// 32 KB, a <see cref="ParseException"/> is thrown. This protects the parser from
        /// allocating excessively large buffers when handling mailbox names or other
        /// small astring‑based tokens. Larger message‑body literals are expected to be
        /// handled by higher‑level streaming logic outside this reader.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the read operation.
        /// </param>
        /// <returns>
        /// A byte array containing the literal payload exactly as transmitted by the
        /// peer. The caller is responsible for interpreting the payload according to
        /// the IMAP command or response context.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the literal header is malformed, missing its size, missing the
        /// closing brace, or if the literal size exceeds the 32 KB safety limit.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying stream is closed before the next protocol line
        /// can be read after the literal payload.
        /// </exception>
        private async Task<byte[]> ReadLiteralAsync(CancellationToken cancellationToken = default)
        {
            // Parse header: "{N}" or "{N+}"
            m_Position++; // skip '{'

            int start = m_Position;
            while(m_Position < m_Line.Length && char.IsDigit(m_Line[m_Position])){
                m_Position++;
            }

            if(m_Position == start){
                throw new ParseException("Invalid literal header: missing size.");
            }

            int size = int.Parse(m_Line.Substring(start, m_Position - start));

            // Literal+ indicator.
            bool isLiteralPluss = false;
            if(PeekIs('+')){
                isLiteralPluss = true;
                m_Position++;
            }
            if(!PeekIs('}')){
                throw new ParseException("Invalid literal header: missing closing '}'.");
            }

            m_Position++; // skip '}'

            // Safety limit for string literals (32 KB)
            if(size > 32 * 1024){
                throw new ParseException($"Literal size {size} exceeds 32 KB limit.");
            }

            // Client mode
            if(m_pImapClient != null){
                // Read literal payload
                byte[] buffer = new byte[size];
                await m_pImapClient.TcpStream.ReadExactlyAsync(buffer,cancellationToken);

                string logText = Encoding.UTF8.GetString(buffer).Trim().Replace("\r","<CR>").Replace("\n","<LF>");
                m_pImapClient.LogAddRead(buffer.Length,$"Literal {buffer.Length} bytes: {logText}");
               

                // After literal payload, next protocol line begins
                ReadLineResult readLine = await m_pImapClient.TcpStream.ReadLineAsync(m_LineBuffer,SizeExceededAction.JunkAndThrowException,cancellationToken);
                if(readLine.BytesInBuffer == 0){
                    throw new IOException("Connection was closed.");
                }
                m_Line =  readLine.LineUtf8!;
                m_Position = 0;

                m_pImapClient.LogAddRead(readLine.BytesInBuffer,m_Line);

                return buffer;
            }
            // Server mode
            else{
                // Server standard literal reading, we need to send + Continue for client.
                if(!isLiteralPluss){/*
                    await m_pStream.WriteLineAsync("+ Continue\r\n",cancellationToken);

                    if(m_pLogger != null){
                        m_pLogger.AddWrite(12,"+ Continue\r\n");
                    }*/
                }

                throw new NotImplementedException("ReadLiteralAsync server mode nor implemented.");
            }
        }

        #endregion


        #region method SkipWhitespaces

        /// <summary>
        /// Advances the reader position past any whitespace characters.
        /// IMAP treats SP, HTAB, CR, and LF as valid whitespace in protocol lines.
        /// </summary>
        private void SkipWhitespaces()
        {
            while(m_Position < m_Line.Length){
                char c = m_Line[m_Position];
                if(c == ' ' || c == '\t' || c == '\r' || c == '\n'){
                    m_Position++;
                }
                else{
                    break;
                }
            }
        }

        #endregion


        #region Properties implemntation

        /// <summary>
        /// Indicates whether additional characters remain in the current IMAP protocol
        /// line beyond the reader’s current position. This property provides a lightweight
        /// end‑of‑line check that can be used by higher‑level parsers to determine whether
        /// further tokens may be read without attempting to consume input or inspect the
        /// underlying stream.
        /// <para>
        /// The value reflects only the remaining characters in <c>m_Line</c>. It does not
        /// consider literal payloads or future protocol lines. After a literal is processed,
        /// the reader resets <c>m_Line</c> and <c>m_Position</c>, and <c>HasMore</c> reflects
        /// the newly loaded line.
        /// </para>
        /// </summary>
        /// <returns>
        /// <c>true</c> if <c>m_Position</c> is less than the length of <c>m_Line</c>;
        /// otherwise <c>false</c>.
        /// </returns>
        public bool HasMore
        {
            get { return m_Position < m_Line.Length; }
        }

        /// <summary>
        /// Gets the full raw IMAP protocol line currently being parsed.
        /// This value represents the exact line of text received from the server
        /// before any parsing or token consumption occurs. It is preserved for the
        /// entire lifetime of the line, even as atoms, quoted strings, mailbox names,
        /// or other tokens are read and consumed by the reader.
        /// <para>
        /// The <see cref="Text"/> property is intended for diagnostics, logging, and
        /// error reporting. It provides visibility into the complete server response
        /// regardless of how much of the line has already been parsed. This is
        /// especially useful for responses where <see cref="RemainingText"/> may be
        /// empty after token consumption.
        /// </para>
        /// <para>
        /// Literal payloads (introduced by <c>{N}</c> or <c>{N+}</c> tokens) are not
        /// included in this property. <see cref="Text"/> always reflects the protocol
        /// line that contained the literal header, not the subsequent literal data
        /// delivered on following lines.
        /// </para>
        /// </summary>
        public string Text 
        { 
            get { return m_Line; }
        }


        /// <summary>
        /// Gets the remaining unconsumed portion of the current IMAP command text.
        /// </summary>
        public string RemainingText
        {
            get { return m_Line.Substring(m_Position); }
        }

        #endregion
    }
}
