using LumiSoft.Net.IMAP.Client;
using LumiSoft.Net.IMAP.Server;
using LumiSoft.Net.IO;
using LumiSoft.Net.Log;
using System.Globalization;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Provides low-level parsing utilities for IMAP tokens such as atoms,
    /// quoted strings, literals, astrings, mailbox names, and other grammar
    /// elements defined by IMAP4rev2.
    /// <para>
    /// The reader operates on a single IMAP protocol line supplied by the
    /// caller and exposes methods for extracting individual tokens according
    /// to IMAP grammar rules.
    /// </para>
    /// <para>
    /// The class does <b>not</b> perform general protocol-line reading. All
    /// parsing is performed within the current protocol line except when a
    /// literal is encountered. In that case, the reader reads the literal
    /// payload from the underlying <see cref="SmartStream"/> and then loads
    /// the next protocol line so that parsing may continue. This is the only
    /// situation in which the reader performs stream I/O.
    /// </para>
    /// <para>
    /// Supported literal forms include standard literals (<c>{N}</c>),
    /// non-synchronizing literals (<c>{N+}</c>), literal8
    /// (<c>~{N}</c>), and UTF8 literals (<c>UTF8 (~{N})</c>).
    /// </para>
    /// <para>
    /// The reader supports both client and server operation. When running in
    /// server mode, the reader automatically sends IMAP continuation responses
    /// (<c>"+ Continue"</c>) for synchronizing literals. In client mode,
    /// continuation responses are never sent.
    /// </para>
    /// <para>
    /// The class is intended for internal use by higher-level IMAP command and
    /// response parsers. It provides deterministic, grammar-aware token
    /// extraction without altering the caller's parsing state except when
    /// literal processing requires advancing to the next protocol line.
    /// </para>
    /// </summary>
    internal class _IMAP_Reader 
    {
        #region struct LiteralHeader

        /// <summary>
        /// Represents a parsed IMAP literal header.
        /// </summary>
        /// <remarks>
        /// A literal header describes an IMAP literal without reading its payload.
        /// Supported forms include standard literals (<c>{N}</c>),
        /// non-synchronizing literals (<c>{N+}</c>), literal8
        /// (<c>~{N}</c>), and UTF8 literals (<c>UTF8 (~{N})</c>).
        /// <para>
        /// For UTF8 literals, <see cref="IsLiteral8"/> is always
        /// <c>true</c> because a UTF8 literal is represented as a wrapper
        /// around a literal8 value.
        /// </para>
        /// </remarks>
        private readonly struct LiteralHeader
        {
            /// <summary>
            /// Gets the size of the literal payload in bytes.
            /// </summary>
            public int Size { get; }

            /// <summary>
            /// Gets a value indicating whether the literal uses literal8 syntax
            /// (<c>~{N}</c>).
            /// </summary>
            public bool IsLiteral8 { get; }

            /// <summary>
            /// Gets a value indicating whether the literal is non-synchronizing
            /// (<c>{N+}</c>).
            /// </summary>
            public bool IsNonSynchronizing { get; }

            /// <summary>
            /// Gets a value indicating whether the literal is wrapped in a UTF8
            /// literal construct (<c>UTF8 (~{N})</c>).
            /// </summary>
            /// <remarks>
            /// When this property is <c>true</c>, the reader must consume the
            /// closing parenthesis following the literal payload.
            /// </remarks>
            public bool IsUtf8Literal { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="LiteralHeader"/> struct.
            /// </summary>
            /// <param name="size">
            /// The size of the literal payload in bytes.
            /// </param>
            /// <param name="isLiteral8">
            /// Specifies whether the literal uses literal8 syntax
            /// (<c>~{N}</c>).
            /// </param>
            /// <param name="isNonSynchronizing">
            /// Specifies whether the literal is non-synchronizing
            /// (<c>{N+}</c>).
            /// </param>
            /// <param name="isUtf8Literal">
            /// Specifies whether the literal is wrapped in a UTF8 literal
            /// construct (<c>UTF8 (~{N})</c>).
            /// </param>
            public LiteralHeader(
                int size,
                bool isLiteral8,
                bool isNonSynchronizing,
                bool isUtf8Literal)
            {
                Size               = size;
                IsLiteral8         = isLiteral8;
                IsNonSynchronizing = isNonSynchronizing;
                IsUtf8Literal      = isUtf8Literal;
            }
        }

        #endregion

        private string        m_Line;
        private int           m_Position          = 0;
        private Memory<byte>  m_LineBuffer;
        private IMAP_Client?  m_pImapClient       = null;
        private IMAP_Session? m_ImapServerSession = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="_IMAP_Reader"/> class for parsing
        /// IMAP lexical elements from a caller-provided protocol line.
        /// <para>
        /// This constructor creates a line-only reader that operates exclusively on the
        /// supplied <paramref name="line"/>. Methods that parse atoms, quoted strings,
        /// dates, parenthesized lists, and other non-literal tokens work normally.
        /// </para>
        /// <para>
        /// Literal processing is not available because no client or server session is
        /// associated with the reader. Attempting to read a literal requires one of the
        /// constructors that accepts an <see cref="IMAP_Client"/> or
        /// <see cref="IMAP_Session"/> together with a line buffer.
        /// </para>
        /// </summary>
        /// <param name="line">
        /// The IMAP protocol line to parse.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="line"/> is <c>null</c>.
        /// </exception>
        internal _IMAP_Reader(string line)
        {
            m_Line = line;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="_IMAP_Reader"/> class for
        /// parsing IMAP protocol data in client mode.
        /// </summary>
        /// <param name="line">
        /// The IMAP protocol line currently being parsed.
        /// </param>
        /// <param name="lineBuffer">
        /// A reusable buffer used when reading subsequent protocol lines after
        /// literal processing.
        /// </param>
        /// <param name="imapClient">
        /// The IMAP client used to read literal payloads and protocol data from
        /// the server.
        /// </param>
        internal _IMAP_Reader(string line,Memory<byte> lineBuffer,IMAP_Client imapClient)
        {
            m_Line        = line;
            m_LineBuffer  = lineBuffer;
            m_pImapClient = imapClient;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="_IMAP_Reader"/> class for
        /// parsing IMAP protocol data in server mode.
        /// </summary>
        /// <param name="line">
        /// The IMAP command line currently being parsed.
        /// </param>
        /// <param name="lineBuffer">
        /// A reusable buffer used when reading subsequent protocol lines after
        /// literal processing.
        /// </param>
        /// <param name="imapServerSession">
        /// The IMAP server session associated with the command being parsed.
        /// </param>
        internal _IMAP_Reader(string line,Memory<byte> lineBuffer,IMAP_Session imapServerSession)
        {
            m_Line              = line;
            m_LineBuffer        = lineBuffer;
            m_ImapServerSession = imapServerSession;
        }


        #region method ReadStringAsync

        /// <summary>
        /// Reads an IMAP astring from the current protocol line.
        /// <para>
        /// An IMAP astring may be represented as an atom, a quoted string,
        /// a literal, a literal8 value, a UTF8 literal, or the special token
        /// <c>NIL</c>. This method automatically detects the token type and
        /// dispatches to the appropriate parsing routine.
        /// </para>
        /// <para>
        /// Leading whitespace is skipped before token parsing begins.
        /// </para>
        /// <para>
        /// Literal values are read using
        /// <see cref="ReadLiteralAsync(bool,System.Threading.CancellationToken)"/>.
        /// The literal payload is decoded as UTF-8 and returned as a
        /// <see cref="string"/>.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// The parsed string value, or <c>null</c> if the token is
        /// <c>NIL</c>.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the string token is malformed, if a quoted string is not
        /// terminated correctly, if a literal header is invalid, or if no valid
        /// atom can be parsed.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying connection is closed while reading a literal
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
            if(PeekIsLiteral()){
                byte[] data = await ReadLiteralAsync(true,cancellationToken);

                return Encoding.UTF8.GetString(data);
            }

            // Atom fallback (ReadAtom throws if invalid)
            return ReadAtom();
        }

        #endregion

        #region method ReadMailboxAsync

        /// <summary>
        /// Reads an IMAP mailbox name using <see cref="ReadStringAsync"/> and
        /// applies mailbox-specific decoding rules.
        /// <para>
        /// A mailbox name in IMAP is represented as an astring and may therefore
        /// be encoded as an atom, a quoted string, a literal, a literal8 value,
        /// a UTF8 literal, or the special token <c>NIL</c>.
        /// </para>
        /// <para>
        /// If the mailbox name is represented by <c>NIL</c>, the method returns
        /// <c>null</c>.
        /// </para>
        /// <para>
        /// For non-null values, the mailbox name is decoded using
        /// <see cref="IMAP_Utils.Decode_IMAP_UTF7_String(string)"/>. This preserves
        /// compatibility with mailbox names encoded using IMAP modified UTF-7
        /// while also allowing UTF-8 mailbox names when supported by the server.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// The decoded mailbox name, or <c>null</c> if the IMAP token
        /// <c>NIL</c> was encountered.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the mailbox-name token is malformed or cannot be parsed.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying connection is closed while reading a
        /// literal-based mailbox name.
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
        /// Reads an IMAP atom.
        /// </summary>
        /// <returns>Reurns IMAP atom.</returns>
        /// <exception cref="ParseException">Throws ParseException if no atom characters are present.</exception>
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

        #region method ReadDate

        /// <summary>
        /// Reads an IMAP date value from the current protocol line.
        /// </summary>
        /// <remarks>
        /// IMAP date values may be represented either as a date atom such as
        /// <c>12-Oct-2026</c> or as a quoted date string such as
        /// <c>"12-Oct-2026"</c>.
        /// <para>
        /// This method accepts both forms and returns the parsed value as a
        /// <see cref="DateOnly"/>.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The parsed date value.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not a valid IMAP date.
        /// </exception>
        public DateOnly ReadDate()
        {
            SkipWhitespaces();

            string value;

            if(PeekIs('"')){
                value = ReadQuotedString();
            }
            else{
                value = ReadAtom();
            }

            if(!DateOnly.TryParseExact(
                value,
                "d-MMM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
            {
                throw new ParseException($"Invalid IMAP date value '{value}'.");
            }

            return date;
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
        /// Parses a literal header from the current protocol line and writes the
        /// literal payload to the specified stream.
        /// <para>
        /// Supported literal forms include standard literals (<c>{N}</c>),
        /// non-synchronizing literals (<c>{N+}</c>), literal8 (<c>~{N}</c>),
        /// and UTF8 literals (<c>UTF8 (~{N})</c>).
        /// </para>
        /// <para>
        /// After the literal payload has been read, the next protocol line is
        /// loaded and becomes the current parsing line. For UTF8 literals, the
        /// closing parenthesis terminating the UTF8 literal wrapper is consumed
        /// automatically.
        /// </para>
        /// </summary>
        /// <param name="stream">
        /// The destination stream that receives the literal payload exactly as
        /// transmitted by the peer.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="stream"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown if a valid literal header is not present at the current reader
        /// position or if a UTF8 literal is not terminated correctly.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if the underlying connection is closed while reading the literal
        /// payload or the subsequent protocol line.
        /// </exception>
        public async Task ReadLiteralAsync(Stream stream,CancellationToken cancellationToken = default)
        {
            if(stream == null){
                throw new ArgumentNullException(nameof(stream));
            }

            if(!TryParseLiteralHeader(out LiteralHeader literalHeader)){
                throw new ParseException("Expected IMAP literal header.");
            }

            // Client mode.
            if(m_pImapClient != null){
                await m_pImapClient.TcpStream.ReadFixedCountAsync(
                    stream,
                    literalHeader.Size,
                    cancellationToken
                );

                m_pImapClient.LogAddRead(literalHeader.Size,$"Literal {literalHeader.Size} bytes.");

                // After literal payload, next protocol line begins.
                var readLine = await m_pImapClient.TcpStream.ReadLineAsync(
                    m_LineBuffer,
                    SizeExceededAction.JunkAndThrowException,
                    cancellationToken
                );

                if(readLine.BytesInBuffer == 0){
                    throw new IOException("Connection was closed.");
                }

                m_Line = readLine.LineUtf8!;
                m_Position = 0;

                m_pImapClient.LogAddRead(readLine.BytesInBuffer,m_Line);

                // UTF8 literals are terminated by a closing parenthesis
                // following the literal payload.
                if(literalHeader.IsUtf8Literal){
                    if(ReadChar() != ')'){
                        throw new ParseException("Expected ')' after UTF8 literal.");
                    }
                }
            }
            // Server mode.
            else{
                // Synchronizing literals require a continuation response.
                if(!literalHeader.IsNonSynchronizing){
                    await m_ImapServerSession!.TcpStream.WriteLineAsync("+ Continue\r\n",cancellationToken);

                    m_ImapServerSession.LogAddWrite(12,"+ Continue\r\n");
                }

                await m_ImapServerSession!.TcpStream.ReadFixedCountAsync(stream,literalHeader.Size,cancellationToken);

                m_ImapServerSession.LogAddRead(literalHeader.Size,$"Literal {literalHeader.Size} bytes.");

                // After literal payload, next protocol line begins.
                var readLine = await m_ImapServerSession.TcpStream.ReadLineAsync(
                    m_LineBuffer,
                    SizeExceededAction.JunkAndThrowException,
                    cancellationToken
                );

                if(readLine.BytesInBuffer == 0){
                    throw new IOException("Connection was closed.");
                }

                m_Line = readLine.LineUtf8!;
                m_Position = 0;

                m_ImapServerSession.LogAddRead(readLine.BytesInBuffer,m_Line);

                // UTF8 literals are terminated by a closing parenthesis
                // following the literal payload.
                if(literalHeader.IsUtf8Literal){
                    if(ReadChar() != ')'){
                        throw new ParseException("Expected ')' after UTF8 literal.");
                    }
                }
            }
        }

        #endregion

        #region method ReadLiteralAsync

        /// <summary>
    /// Reads a literal payload from the underlying connection and returns it as
    /// a byte array.
    /// <para>
    /// The literal header is parsed from the current protocol line and used to
    /// determine the literal size, synchronization mode, and literal type.
    /// Supported forms include standard literals (<c>{N}</c>),
    /// non-synchronizing literals (<c>{N+}</c>), literal8
    /// (<c>~{N}</c>), and UTF8 literals (<c>UTF8 (~{N})</c>).
    /// </para>
    /// <para>
    /// After the literal payload has been read, the next protocol line becomes
    /// the current parsing line. For UTF8 literals, the closing parenthesis
    /// terminating the UTF8 literal wrapper is consumed automatically.
    /// </para>
    /// <para>
    /// This overload buffers the entire literal payload in memory and therefore
    /// applies a 32 KB safety limit. Larger literals should be processed using
    /// <see cref="ReadLiteralAsync(Stream,System.Threading.CancellationToken)"/>
    /// to avoid excessive memory allocation.
    /// </para>
    /// </summary>
    /// <param name="logLiteralData">
    /// Specifies whether the literal payload should be written to protocol logs.
    /// When <c>false</c>, only the literal size is logged.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the operation.
    /// </param>
    /// <returns>
    /// A byte array containing the literal payload exactly as transmitted by
    /// the peer.
    /// </returns>
    /// <exception cref="ParseException">
    /// Thrown if a valid literal header is not present at the current reader
    /// position, if the literal size exceeds the configured safety limit, or
    /// if a UTF8 literal is not terminated correctly.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown if the underlying connection is closed while reading the literal
    /// payload or the subsequent protocol line.
    /// </exception>
        public async Task<byte[]> ReadLiteralAsync(bool logLiteralData,CancellationToken cancellationToken = default)
        {
            if(!TryParseLiteralHeader(out LiteralHeader literalHeader)){
                throw new ParseException("Expected IMAP literal header.");
            }

            // Safety limit for string-based literal reads.
            if(literalHeader.Size > 32 * 1024){
                throw new ParseException($"Literal size {literalHeader.Size} exceeds 32 KB limit.");
            }

            // Client mode.
            if(m_pImapClient != null){
                byte[] buffer = new byte[literalHeader.Size];

                await m_pImapClient.TcpStream.ReadExactlyAsync(buffer,cancellationToken);

                string logText = Encoding.UTF8.GetString(buffer).Trim().Replace("\r","<CR>").Replace("\n","<LF>");

                m_pImapClient.LogAddRead(buffer.Length,$"Literal {buffer.Length} bytes: {logText}");

                // After the literal payload, the next protocol line begins.
                var readLine = await m_pImapClient.TcpStream.ReadLineAsync(
                    m_LineBuffer,
                    SizeExceededAction.JunkAndThrowException,
                    cancellationToken
                );

                if(readLine.BytesInBuffer == 0){
                    throw new IOException("Connection was closed.");
                }

                m_Line = readLine.LineUtf8!;
                m_Position = 0;

                m_pImapClient.LogAddRead(readLine.BytesInBuffer,m_Line);

                if(literalHeader.IsUtf8Literal){
                    if(ReadChar() != ')'){
                        throw new ParseException("Expected ')' after UTF8 literal.");
                    }
                }

                return buffer;
            }
            // Server mode.
            else{
                // Synchronizing literals require a continuation response.
                if(!literalHeader.IsNonSynchronizing){
                    await m_ImapServerSession!.TcpStream.WriteLineAsync("+ Continue\r\n",cancellationToken);

                    m_ImapServerSession.LogAddWrite(12,"+ Continue\r\n");
                }

                byte[] buffer = new byte[literalHeader.Size];

                await m_ImapServerSession!.TcpStream.ReadExactlyAsync(buffer,cancellationToken);

                if(logLiteralData){
                    string logText = Encoding.UTF8.GetString(buffer).Trim().Replace("\r","<CR>").Replace("\n","<LF>");

                    m_ImapServerSession.LogAddRead(buffer.Length,$"Literal {buffer.Length} bytes: {logText}");
                }
                else{
                    m_ImapServerSession.LogAddRead(buffer.Length,$"Literal {buffer.Length} bytes.");
                }

                // After the literal payload, the next protocol line begins.
                var readLine = await m_ImapServerSession.TcpStream.ReadLineAsync(
                    m_LineBuffer,
                    SizeExceededAction.JunkAndThrowException,
                    cancellationToken
                );

                if(readLine.BytesInBuffer == 0){
                    throw new IOException("Connection was closed.");
                }

                m_Line = readLine.LineUtf8!;
                m_Position = 0;

                m_ImapServerSession.LogAddRead(readLine.BytesInBuffer,m_Line);

                if(literalHeader.IsUtf8Literal){
                    if(ReadChar() != ')'){
                        throw new ParseException("Expected ')' after UTF8 literal.");
                    }
                }

                return buffer;
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

        #region method PeekIsLiteral

        /// <summary>
        /// Determines whether the next non-whitespace token is an IMAP literal
        /// construct without consuming any input or modifying the current reader
        /// position.
        /// <para>
        /// This method performs a lightweight look-ahead operation and recognizes
        /// all literal forms supported by the reader, including standard literals
        /// (<c>{N}</c>), non-synchronizing literals (<c>{N+}</c>), literal8
        /// (<c>~{N}</c>), and UTF8 literals (<c>UTF8 (~{N})</c>).
        /// </para>
        /// <para>
        /// No literal payload data is read and no network or stream I/O is
        /// performed. The method only inspects the current protocol line.
        /// </para>
        /// <para>
        /// The method is intended for parser logic that needs to determine whether
        /// the next token requires literal processing before deciding how to read
        /// the value.
        /// </para>
        /// </summary>
        /// <returns>
        /// <c>true</c> if the next non-whitespace token is a valid IMAP literal
        /// construct; otherwise <c>false</c>.
        /// </returns>
        public bool PeekIsLiteral()
        {
            return TryParseLiteralHeader(out _);
        }

        #endregion

        #region method PeekLiteralSize

        /// <summary>
        /// Gets the size of the next IMAP literal without consuming any input.
        /// </summary>
        /// <returns>
        /// The size of the literal payload in bytes.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the next token is not a valid IMAP literal.
        /// </exception>
        public int PeekLiteralSize()
        {
            if(!TryParseLiteralHeader(out LiteralHeader literalHeader)){
                throw new ParseException(
                    "Expected IMAP literal but no valid literal header was found."
                );
            }

            return literalHeader.Size;
        }


        #endregion

        #region method DrainRemainingCommandAsync

        /// <summary>
        /// Reads and discards any remaining literal payloads associated with the
        /// current command so that protocol parsing remains synchronized.
        /// <para>
        /// This method is intended for error-recovery scenarios where command
        /// processing cannot continue but one or more literals may still need to
        /// be consumed from the underlying connection.
        /// </para>
        /// <para>
        /// The method scans the remaining command text for literal headers and
        /// discards their payloads. Scanning stops when no further valid literal
        /// headers can be found.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the operation.
        /// </param>
        /// <exception cref="IOException">
        /// Thrown if the underlying connection is closed while draining a
        /// literal payload.
        /// </exception>
        public async Task DrainRemainingCommandAsync(CancellationToken cancellationToken = default)
        {
            while(true){
                // Find the next literal header. All supported literal forms
                // contain a '{' size declaration.
                int literalPos = m_Line.IndexOf('{',m_Position);
                if(literalPos == -1){
                    m_Position = m_Line.Length;

                    return;
                }

                // Move to the start of the literal header.
                m_Position = literalPos;

                // Ensure a valid literal header is present.
                if(!TryParseLiteralHeader(out _)){
                    m_Position = m_Line.Length;

                    return;
                }

                // Discard the literal payload. ReadLiteralAsync reads the literal
                // data, loads the next protocol line, and resets m_Position.
                await ReadLiteralAsync(
                    Stream.Null,
                    cancellationToken
                );

                // Continue scanning in case additional literals follow.
            }
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
        /// <remarks>
        /// IMAP quoted strings support the escape sequences <c>\"</c>
        /// for a quotation mark and <c>\\</c> for a backslash.
        /// </remarks>
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

        #region method TryParseLiteralHeader

        /// <summary>
        /// Attempts to parse an IMAP literal header from the current reader position
        /// without consuming any input or modifying the reader state.
        /// <para>
        /// Supported forms include standard literals (<c>{N}</c>),
        /// non-synchronizing literals (<c>{N+}</c>), literal8
        /// (<c>~{N}</c>), and UTF8 literals (<c>UTF8 (~{N})</c>).
        /// </para>
        /// <para>
        /// Leading whitespace is ignored when locating the literal header.
        /// The current reader position is never advanced regardless of whether
        /// parsing succeeds or fails.
        /// </para>
        /// <para>
        /// For UTF8 literals, the returned <see cref="LiteralHeader"/> describes
        /// the embedded literal8 header. The closing parenthesis terminating the
        /// UTF8 literal wrapper is not validated because it is not available until
        /// after the literal payload has been read from the underlying stream.
        /// </para>
        /// </summary>
        /// <param name="literalHeader">
        /// When this method returns <c>true</c>, contains the parsed literal
        /// header information; otherwise contains the default value.
        /// </param>
        /// <returns>
        /// <c>true</c> if a valid literal header was found at the current reader
        /// position; otherwise <c>false</c>.
        /// </returns>
        private bool TryParseLiteralHeader(out LiteralHeader literalHeader)
        {
            literalHeader = default;

            int pos = m_Position;

            // Skip whitespace without modifying reader state.
            while(pos < m_Line.Length){
                char c = m_Line[pos];
                if(c == ' ' || c == '\t' || c == '\r' || c == '\n'){
                    pos++;
                }
                else{
                    break;
                }
            }

            bool isUtf8Literal = false;
            bool isLiteral8 = false;

            // UTF8 (~{123})
            if(pos + 6 <= m_Line.Length && string.Compare(m_Line,pos,"UTF8 (",0,6,StringComparison.OrdinalIgnoreCase) == 0){
                isUtf8Literal = true;
                isLiteral8 = true;
                pos += 6;
            }

            // ~{123}
            if(pos < m_Line.Length && m_Line[pos] == '~'){
                isLiteral8 = true;
                pos++;

                if(pos >= m_Line.Length || m_Line[pos] != '{'){
                    return false;
                }
            }
            // {123}
            else if(pos >= m_Line.Length || m_Line[pos] != '{'){
                return false;
            }

            pos++; // skip '{'

            int start = pos;

            while(pos < m_Line.Length && char.IsDigit(m_Line[pos])){
                pos++;
            }

            if(pos == start){
                return false;
            }

            if(!int.TryParse(m_Line.Substring(start,pos - start),out int size)){
                return false;
            }

            bool isNonSynchronizing = false;

            if(pos < m_Line.Length && m_Line[pos] == '+'){
                isNonSynchronizing = true;
                pos++;
            }

            if(pos >= m_Line.Length || m_Line[pos] != '}'){
                return false;
            }

            literalHeader = new LiteralHeader(
                size,
                isLiteral8,
                isNonSynchronizing,
                isUtf8Literal
            );

            return true;
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
