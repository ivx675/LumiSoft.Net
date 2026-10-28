using LumiSoft.Net.IMAP.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>BODY</c> FETCH request data‑item as defined
    /// in RFC 3501 section 6.4.5 and section 7.4.2. When included in a FETCH
    /// command, this attribute instructs the server to return the specified
    /// message body section.
    /// <para>
    /// The client may specify:
    /// <list type="bullet">
    /// <item><description>A body section (e.g., <c>1</c>, <c>2.1</c>, <c>HEADER</c>,
    /// <c>TEXT</c>, <c>MIME</c>)</description></item>
    /// <item><description>An optional partial fetch using an offset and optional
    /// maximum byte count</description></item>
    /// </list>
    /// A null section requests the entire message body.
    /// </para>
    /// <para>
    /// <b>Semantics:</b>
    /// <c>BODY</c> returns the requested message body section. Unlike
    /// <c>BODY.PEEK</c>, retrieving a body section using <c>BODY</c> may cause
    /// the server to set the <c>\Seen</c> flag on the message, depending on
    /// server implementation.
    /// Partial fetches return a substring beginning at the specified byte offset.
    /// The server echoes only the offset in the response; the literal size
    /// indicates how many bytes were actually returned.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_Body : IMAP_t_Fetch_i
    {
        private string? m_Section  = null;
        private long?   m_Offset   = -1;
        private long?   m_MaxCount = -1;

        /// <summary>
        /// Initializes a new <see cref="IMAP_t_Fetch_i_Body"/> instance with no
        /// section and no partial‑fetch parameters specified.
        /// <para>
        /// This corresponds to the IMAP <c>BODY[]</c> FETCH data‑item, which requests
        /// the entire message body. No offset or length is included, so the server
        /// returns the complete content of the message body section.
        /// </para>
        /// <para>
        /// <b>Semantics:</b>
        /// Unlike <c>BODY.PEEK</c>, retrieving a body section using <c>BODY</c> may
        /// cause the server to set the <c>\Seen</c> flag on the message, depending on
        /// server implementation.
        /// </para>
        /// </summary>
        public IMAP_t_Fetch_i_Body()
        {
        }

        /// <summary>
        /// Initializes a new <see cref="IMAP_t_Fetch_i_Body"/> instance with an
        /// optional section name and optional partial‑fetch parameters.
        /// <para>
        /// This constructor models the IMAP <c>BODY</c> FETCH data‑item as defined
        /// in RFC 3501 section 6.4.5 and section 7.4.2. The parameters correspond
        /// to the syntax:
        /// </para>
        /// <para>
        /// <c>BODY[section]&lt;offset[.length]&gt;</c>
        /// </para>
        /// <para>
        /// <list type="bullet">
        /// <item><description>A body section (e.g., <c>1</c>, <c>2.1</c>, <c>HEADER</c>,
        /// <c>TEXT</c>, <c>MIME</c>). A value of <c>null</c> requests the entire
        /// message body.</description></item>
        /// <item><description>An optional partial fetch using a starting byte offset
        /// and an optional maximum byte count. When <c>offset</c> is non‑null,
        /// a partial‑fetch specifier is emitted using the syntax
        /// <c>&lt;offset[.length]&gt;</c>.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Semantics:</b>
        /// <c>BODY</c> returns the requested message body section. Unlike
        /// <c>BODY.PEEK</c>, retrieving a section using <c>BODY</c> may cause the
        /// server to set the <c>\Seen</c> flag on the message, depending on server
        /// implementation. Partial fetches return data beginning at the specified
        /// offset, and the server echoes only the offset in the response.
        /// </para>
        /// </summary>
        /// <param name="section">Body section name, or <c>null</c> if not specified.</param>
        /// <param name="offset">Starting byte offset, or <c>null</c> if not specified.</param>
        /// <param name="maxCount">Maximum number of bytes to return, or <c>null</c> if not specified.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="offset"/> or <paramref name="maxCount"/> is
        /// non‑null and less than zero.
        /// </exception>
        public IMAP_t_Fetch_i_Body(string? section,long? offset,long? maxCount)
        {
            if(offset != null && offset < 0){
                throw new ArgumentException("Arument 'offset' value must be >=0.",nameof(offset));
            }
            if(maxCount != null && maxCount < 0){
                throw new ArgumentException("Arument 'maxCount' value must be >=0.",nameof(maxCount));
            }

            m_Section  = section;
            m_Offset   = offset;
            m_MaxCount = maxCount;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_i_Body"/> class.
        /// </summary>
        /// <param name="isPeek">Indicates whether this is a BODY.PEEK operation (does not set the \Seen flag).</param>
        /// <param name="partSpecifier">The MIME part specifier (e.g., "1.2"), or null if none.</param>
        /// <param name="sectionType">The section type (e.g., "HEADER", "TEXT", "HEADER.FIELDS"), or null if none.</param>
        /// <param name="headerFields">The header fields specified for a HEADER.FIELDS section, or null.</param>
        /// <param name="headerFieldsNot">The header fields specified for a HEADER.FIELDS.NOT section, or null.</param>
        /// <param name="offset">The starting octet offset for partial fetches, or null.</param>
        /// <param name="maxCount">The maximum number of octets to fetch in a partial request, or null.</param>
        internal IMAP_t_Fetch_i_Body(bool isPeek,string? partSpecifier,string? sectionType,string[]? headerFields,string[]? headerFieldsNot,long? offset,long? maxCount)
        {
            IsPeek          = isPeek;
            PartSpecifier   = partSpecifier;
            SectionType     = sectionType;
            HeaderFields    = headerFields;
            HeaderFieldsNot = headerFieldsNot;
            m_Offset        = offset;
            m_MaxCount      = maxCount;

            // Build section string.
            var sb = new System.Text.StringBuilder();
            bool hasPart = !string.IsNullOrEmpty(PartSpecifier);
            bool hasSection = !string.IsNullOrEmpty(SectionType);
            if(hasPart){
                sb.Append(PartSpecifier);
                if(hasSection){
                    sb.Append('.');
                    sb.Append(SectionType);
                }
            }
            else if (hasSection){
                sb.Append(SectionType);
            }
            // Append header fields filter if specified
            if(HeaderFields != null && HeaderFields.Length > 0){
                sb.Append(" (");
                sb.Append(string.Join(" ", HeaderFields));
                sb.Append(')');
            }
            else if (HeaderFieldsNot != null && HeaderFieldsNot.Length > 0){
                sb.Append(" HEADER.FIELDS.NOT (");
                sb.Append(string.Join(" ", HeaderFieldsNot));
                sb.Append(')');
            }
            m_Section = sb.ToString();
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses a FETCH <c>BODY</c> or <c>BODY.PEEK</c> data item from the
        /// specified <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="imapReader">
        /// The <see cref="_IMAP_Reader"/> positioned at the FETCH data item
        /// to parse.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel asynchronous
        /// parsing operations.
        /// </param>
        /// <returns>
        /// A task whose result contains a new
        /// <see cref="IMAP_t_Fetch_i_Body"/> instance representing the
        /// parsed FETCH data item.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="imapReader"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the FETCH attribute is not a valid
        /// <c>BODY</c> or <c>BODY.PEEK</c> specification, a body section
        /// is malformed, a header field list is invalid, or a partial
        /// fetch specification cannot be parsed.
        /// </exception>
        /// <remarks>
        /// Supports all standard RFC 9051 BODY section forms including
        /// part specifiers, HEADER, TEXT, MIME,
        /// HEADER.FIELDS, HEADER.FIELDS.NOT, and optional partial-fetch
        /// specifiers.
        /// <para>
        /// HEADER.FIELDS and HEADER.FIELDS.NOT field names are parsed
        /// using IMAP astring rules and therefore may be represented as
        /// atoms, quoted strings, or literals.
        /// </para>
        /// <para>
        /// This method performs complete BODY section parsing and
        /// validation and returns a normalized representation that can
        /// be consumed directly by FETCH processing logic.
        /// </para>
        /// </remarks>
        internal static async Task<IMAP_t_Fetch_i_Body> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            /*
            RFC 9051 FETCH BODY syntax:

                BODY[<section>]
                BODY[<section>]<offset>
                BODY[<section>]<offset.length>

                BODY.PEEK[<section>]
                BODY.PEEK[<section>]<offset>
                BODY.PEEK[<section>]<offset.length>

            Supported section forms:

                BODY[]
                BODY[HEADER]
                BODY[TEXT]
                BODY[MIME]

                BODY[1]
                BODY[1.2]
                BODY[1.2.3]

                BODY[1.HEADER]
                BODY[1.TEXT]
                BODY[1.MIME]

                BODY[HEADER.FIELDS (...)]
                BODY[HEADER.FIELDS.NOT (...)]

                BODY[1.HEADER.FIELDS (...)]
                BODY[1.HEADER.FIELDS.NOT (...)]

            HEADER.FIELDS and HEADER.FIELDS.NOT field-name lists
            are parsed and validated by this method. Field names
            are accepted using IMAP astring syntax and therefore
            may be supplied as atoms, quoted strings, or literals.

            BODY and BODY.PEEK share the same parsing logic and
            section syntax. The only semantic difference is that
            BODY.PEEK does not implicitly set the \Seen flag.

            This method performs full BODY/BODY.PEEK section parsing
            and validation. The returned object contains a normalized
            representation of the requested section so FETCH execution
            code does not need to reparse section syntax.
            */

            ArgumentNullException.ThrowIfNull(imapReader);

            bool isPeek = false;

            string atom = imapReader.ReadAtom();
            if(string.Equals(atom,"BODY",StringComparison.OrdinalIgnoreCase)){
            }
            else if(string.Equals(atom,"BODY.PEEK",StringComparison.OrdinalIgnoreCase)){
                isPeek = true;
            }
            else{
                throw new ParseException("Expected 'BODY' or 'BODY.PEEK'.");
            }

            string?   partSpecifier   = null;
            string?   sectionType     = null;
            string[]? headerFields    = null;
            string[]? headerFieldsNot = null;
            long?     offset          = null;
            long?     count           = null;

            if(imapReader.PeekIs('[')){
                imapReader.ReadChar();
            }
            else{
                throw new ParseException("Expected '[' for BODY.");
            }

            if(imapReader.PeekIs(']')){
                imapReader.ReadChar();
            }
            else{
                // Read the full section atom (e.g., "1.2.HEADER.FIELDS" or "HEADER" or "1.2")
                string sectionToken = imapReader.ReadAtom();
    
                // Check if this section type requires a header field list
                bool isHeaderFields = sectionToken.EndsWith("HEADER.FIELDS", StringComparison.OrdinalIgnoreCase);
                bool isHeaderFieldsNot = sectionToken.EndsWith("HEADER.FIELDS.NOT", StringComparison.OrdinalIgnoreCase);
    
                string baseSection = sectionToken;
                if(isHeaderFields || isHeaderFieldsNot){
                    // Strip off ".HEADER.FIELDS" or ".HEADER.FIELDS.NOT" to isolate any preceding part specifier (e.g., "1.2")
                    int suffixLen = isHeaderFields ? "HEADER.FIELDS".Length : "HEADER.FIELDS.NOT".Length;
                    baseSection = sectionToken.Substring(0,sectionToken.Length - suffixLen);
                    if (baseSection.EndsWith('.')){
                        baseSection = baseSection.Substring(0,baseSection.Length - 1);
                    }
        
                    sectionType = isHeaderFields ? "HEADER.FIELDS" : "HEADER.FIELDS.NOT";
                    List<string> targetList = new List<string>();

                    // Expect opening parenthesis for the field list (ReadAtom automatically consumes leading whitespace)
                    if(!imapReader.PeekIs('(')){
                        throw new ParseException("Expected '(' for header fields list.");
                    }
                    imapReader.ReadChar();

                    while(true){
                        if(!imapReader.HasMore){
                            throw new ParseException("Unexpected end of stream while parsing header fields.");
                        }
                        if(imapReader.PeekIs(')')){
                            imapReader.ReadChar();
                            break;
                        }

                        string? fieldName = await imapReader.ReadStringAsync();
                        if(fieldName != null){
                            targetList.Add(fieldName);
                        }
                    }

                    if(isHeaderFields){
                        headerFields = targetList.ToArray();
                    }
                    else{
                        headerFieldsNot = targetList.ToArray();
                    }
                }
                else{
                    // Determine section type from the end of the token if it's not a header fields query
                    if(sectionToken.EndsWith(".HEADER", StringComparison.OrdinalIgnoreCase)){
                        sectionType = "HEADER";
                        baseSection = sectionToken.Substring(0, sectionToken.Length - ".HEADER".Length);
                    }
                    else if(sectionToken.EndsWith(".TEXT", StringComparison.OrdinalIgnoreCase)){
                        sectionType = "TEXT";
                        baseSection = sectionToken.Substring(0, sectionToken.Length - ".TEXT".Length);
                    }
                    else if(sectionToken.EndsWith(".MIME", StringComparison.OrdinalIgnoreCase)){
                        sectionType = "MIME";
                        baseSection = sectionToken.Substring(0, sectionToken.Length - ".MIME".Length);
                    }
                    else if(string.Equals(sectionToken, "HEADER", StringComparison.OrdinalIgnoreCase)){
                        sectionType = "HEADER";
                        baseSection = string.Empty;
                    }
                    else if(string.Equals(sectionToken, "TEXT", StringComparison.OrdinalIgnoreCase)){
                        sectionType = "TEXT";
                        baseSection = string.Empty;
                    }
                    else if(string.Equals(sectionToken, "MIME", StringComparison.OrdinalIgnoreCase)){
                        sectionType = "MIME";
                        baseSection = string.Empty;
                    }
                    else {
                        // If it doesn't end with a known keyword, the entire token is treated as a part specifier (e.g., "1.2.3")
                        baseSection = sectionToken;
                        sectionType = null; 
                    }
                }

                // Validate and store the part specifier if present
                if(!string.IsNullOrEmpty(baseSection)){
                    string[] partParts = baseSection.Split('.');
                    foreach(string part in partParts){
                        if(!int.TryParse(part, out _)){
                            throw new ParseException($"Invalid body part specifier: {part}");
                        }
                    }
                    partSpecifier = baseSection;
                }

                if(!imapReader.PeekIs(']')){
                    throw new ParseException("Expected ']' at the end of BODY section.");
                }
                imapReader.ReadChar();
            }

            if(imapReader.PeekIs('<')){
                string partialAtom = imapReader.ReadAtom();

                if(!partialAtom.StartsWith('<') || !partialAtom.EndsWith('>')){
                    throw new ParseException("Invalid BODY partial section format.");
                }

                string inner = partialAtom.Substring(1, partialAtom.Length - 2);
                string[] offset_count = inner.Split('.');

                if(long.TryParse(offset_count[0], out long offsetValue)){
                    offset = offsetValue;
                }
                else{
                    throw new ParseException("Invalid BODY partial section offset.");
                }

                if(offset_count.Length == 2){
                    if(long.TryParse(offset_count[1], out long countValue)){
                        count = countValue;
                    }
                    else{
                        throw new ParseException("Invalid BODY partial section count.");
                    }
                }
                else if(offset_count.Length > 2){
                    throw new ParseException("Invalid BODY partial section format.");
                }
            }

            if(string.Equals(sectionType,"MIME",StringComparison.OrdinalIgnoreCase) && partSpecifier == null){
                throw new ParseException("MIME section specifiers require a part number (e.g., BODY[1.MIME]).");
            }

            return new IMAP_t_Fetch_i_Body(isPeek,partSpecifier,sectionType,headerFields,headerFieldsNot,offset,count);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>BODY</c> FETCH attribute string for this instance.
        /// <para>
        /// The returned value includes the section specifier and optional partial
        /// fetch parameters exactly as defined in RFC 3501. Examples:
        /// </para>
        /// <para>
        /// <code>
        /// BODY[1]
        /// BODY[2.1]&lt;0&gt;
        /// BODY[HEADER]&lt;0.2048&gt;
        /// BODY[]&lt;4096&gt;
        /// </code>
        /// </para>
        /// <para>
        /// If no section is specified, an empty section (<c>[]</c>) is emitted.
        /// If <c>Offset</c> is non‑null, a partial fetch is emitted using the
        /// <c>&lt;offset[.length]&gt;</c> syntax. When <c>MaxCount</c> is also
        /// non‑null, the length component is included; otherwise only the offset
        /// is emitted.
        /// </para>
        /// <para>
        /// <b>Semantics:</b>
        /// <c>BODY</c> returns the requested message body section. Unlike
        /// <c>BODY.PEEK</c>, retrieving a section using <c>BODY</c> may cause the
        /// server to set the <c>\Seen</c> flag on the message, depending on server
        /// implementation.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append("BODY[");
            if(m_Section != null){
                retVal.Append(m_Section);
            }
            retVal.Append("]");                        
            if(m_Offset != null){
                retVal.Append("<" + m_Offset);
                if(m_MaxCount != null){
                    retVal.Append("." + m_MaxCount);
                }
                retVal.Append(">");
            }

            return retVal.ToString();
        }

        #endregion


        #region method GetBodySignature

        /// <summary>
        /// Generates a canonical string signature representing this body request for deduplication.
        /// </summary>
        internal string GetBodySignature()
        {
            var sb = new StringBuilder();
            sb.Append(IsPeek ? "PEEK:" : "BODY:");
            sb.Append(PartSpecifier ?? string.Empty);
            sb.Append(':');
            sb.Append(SectionType ?? string.Empty);
            sb.Append(':');
            sb.Append(m_Offset?.ToString() ?? "null");
            sb.Append(':');
            sb.Append(m_MaxCount?.ToString() ?? "null");

            if(HeaderFields != null){
                sb.Append(":HF[");
                sb.Append(string.Join(",", HeaderFields));
                sb.Append(']');
            }

            if(HeaderFieldsNot != null){
                sb.Append(":HFN[");
                sb.Append(string.Join(",", HeaderFieldsNot));
                sb.Append(']');
            }

            return sb.ToString();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the body section identifier used in the <c>BODY</c> FETCH request.
        /// A value of <c>null</c> indicates that no specific section was requested,
        /// which corresponds to an empty section specifier (<c>[]</c>) in the IMAP
        /// command.
        /// <para>
        /// Examples of valid section identifiers include:
        /// <c>1</c>, <c>2.1</c>, <c>HEADER</c>, <c>TEXT</c>, <c>MIME</c>.
        /// These follow the section‑numbering rules defined in RFC 3501.
        /// </para>
        /// <para>
        /// <b>Semantics:</b>
        /// When <c>Section</c> is <c>null</c>, the FETCH request retrieves the entire
        /// message body. When a section name is provided, only that specific MIME
        /// part or header subset is returned.
        /// </para>
        /// </summary>
        public string? Section
        {
            get{ return m_Section; }
        }

        /// <summary>
        /// Gets the starting byte offset used for partial <c>BODY</c> FETCH
        /// requests. When this value is <c>null</c>, no partial‑fetch offset was
        /// specified and the body section is retrieved in full.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the offset is non‑null, the FETCH request includes a partial
        /// specification using the syntax <c>&lt;offset[.length]&gt;</c>. The server
        /// returns data beginning at this byte position within the requested
        /// body section.
        /// </para>
        /// </remarks>
        public long? Offset
        {
            get{ return m_Offset; }
        }

        /// <summary>
        /// Gets the maximum number of bytes to return for a partial <c>BODY</c>
        /// FETCH request. When this value is <c>null</c>, no explicit partial‑fetch
        /// length was specified and the body section is retrieved in full.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <c>MaxCount</c> is non‑null, the FETCH request includes a partial
        /// specification using the syntax <c>&lt;offset.length&gt;</c>. The server
        /// returns at most this number of bytes starting at the specified offset
        /// within the requested body section.
        /// </para>
        /// </remarks>
        public long? MaxCount
        {
            get{ return m_MaxCount; }
        }


        /// <summary>
        /// Gets a value indicating whether this is a BODY.PEEK operation.
        /// </summary>
        internal bool IsPeek
        {
            get;
        }

        /// <summary>
        /// Gets the MIME part specifier (e.g., "1.2"), or null if none.
        /// </summary>
        internal string? PartSpecifier
        {
            get;
        }

        /// <summary>
        /// Gets the section type (e.g., "HEADER", "TEXT"), or null if none.
        /// </summary>
        internal string? SectionType
        {
            get;
        }

        /// <summary>
        /// Gets the header fields specified for a HEADER.FIELDS section, or null.
        /// </summary>
        internal string[]? HeaderFields
        {
            get;
        }

        /// <summary>
        /// Gets the header fields specified for a HEADER.FIELDS.NOT section, or null.
        /// </summary>
        internal string[]? HeaderFieldsNot
        {
            get;
        }

        #endregion
    }
}
