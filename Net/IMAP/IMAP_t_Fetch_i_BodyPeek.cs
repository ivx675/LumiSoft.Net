using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>BODY.PEEK</c> FETCH request data‑item as defined
    /// in RFC 3501 section 6.4.5 and section 7.4.2. When included in a FETCH
    /// command, this attribute instructs the server to return the specified
    /// message body section without setting the <c>\Seen</c> flag.
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
    /// <c>BODY.PEEK</c> behaves identically to <c>BODY</c> for content retrieval,
    /// except that it does <b>not</b> set the <c>\Seen</c> flag on the message.
    /// Partial fetches return a substring beginning at the specified byte offset.
    /// The server echoes only the offset in the response; the literal size
    /// indicates how many bytes were actually returned.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_i_BodyPeek : IMAP_t_Fetch_i
    {
        private string? m_Section  = null;
        private long?   m_Offset   = null;
        private long?   m_MaxCount = null;

        /// <summary>
        /// Initializes a new <see cref="IMAP_t_Fetch_i_BodyPeek"/> instance with
        /// no section and no partial‑fetch parameters specified.
        /// <para>
        /// This corresponds to the IMAP <c>BODY.PEEK[]</c> FETCH data‑item with an
        /// empty section and no offset or length. In IMAP syntax this requests the
        /// entire message body without setting the <c>\Seen</c> flag.
        /// </para>
        /// </summary>
        public IMAP_t_Fetch_i_BodyPeek()
        {
        }

        /// <summary>
        /// Initializes a new <see cref="IMAP_t_Fetch_i_BodyPeek"/> instance with an
        /// optional section name and optional partial‑fetch parameters.
        /// <para>
        /// This constructor models the IMAP <c>BODY.PEEK</c> FETCH data‑item as
        /// defined in RFC 3501. The parameters correspond to the syntax:
        /// </para>
        /// <para>
        /// <c>BODY.PEEK[section]&lt;offset[.length]&gt;</c>
        /// </para>
        /// <para>
        /// <list type="bullet">
        /// <item><description><c>section</c> — The body section to fetch (e.g.,
        /// <c>1</c>, <c>2.1</c>, <c>HEADER</c>, <c>TEXT</c>). A value of <c>null</c>
        /// means no section was specified.</description></item>
        /// <item><description><c>offset</c> — The starting byte position for a
        /// partial fetch. When <c>offset</c> is <c>null</c>, no partial fetch is
        /// requested and the entire section is returned.</description></item>
        /// <item><description><c>maxCount</c> — The maximum number of bytes to
        /// return. When <c>maxCount</c> is <c>null</c>, the server may return all
        /// remaining bytes starting at the specified offset.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Semantics:</b>
        /// <c>BODY.PEEK</c> returns the requested message body section without
        /// setting the <c>\Seen</c> flag. Partial fetches return data beginning at
        /// the specified offset, and the server echoes only the offset in the
        /// response; the literal size indicates how many bytes were actually
        /// returned.
        /// </para>
        /// </summary>
        /// <param name="section">Body section name, or <c>null</c> if not specified.</param>
        /// <param name="offset">Starting byte offset, or <c>null</c> if not specified.</param>
        /// <param name="maxCount">Maximum number of bytes to return, or <c>null</c> if not specified.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="offset"/> or <paramref name="maxCount"/> is
        /// non‑null and less than zero.
        /// </exception>
        public IMAP_t_Fetch_i_BodyPeek(string? section,long? offset,long? maxCount)
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


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>BODY.PEEK</c> FETCH attribute string for this instance.
        /// <para>
        /// The returned value includes the section specifier and optional partial
        /// fetch parameters exactly as defined in RFC 3501. Examples:
        /// </para>
        /// <para>
        /// <code>
        /// BODY.PEEK[1]
        /// BODY.PEEK[2.1]&lt;0&gt;
        /// BODY.PEEK[HEADER]&lt;0.2048&gt;
        /// BODY.PEEK[]&lt;4096&gt;
        /// </code>
        /// </para>
        /// <para>
        /// If no section is specified, an empty section (<c>[]</c>) is emitted.
        /// When <c>Offset</c> is non‑null, a partial fetch is emitted using the
        /// <c>&lt;offset[.length]&gt;</c> syntax. If <c>MaxCount</c> is also non‑null,
        /// the length component is included; otherwise only the offset is emitted.
        /// </para>
        /// <para>
        /// <b>Semantics:</b>
        /// <c>BODY.PEEK</c> returns the requested message body section without
        /// setting the <c>\Seen</c> flag. Partial fetches return data beginning at
        /// the specified offset, and the server echoes only the offset in the
        /// response; the literal size indicates how many bytes were actually
        /// returned.
        /// </para>
        /// </summary>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append("BODY.PEEK[");
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


        #region Properties implementation

        /// <summary>
        /// Gets the body section identifier used in the <c>BODY.PEEK</c> FETCH
        /// request. A value of <c>null</c> indicates that no specific section
        /// was requested, which corresponds to an empty section specifier
        /// (<c>[]</c>) in the IMAP command.
        /// <para>
        /// Examples of section identifiers include:
        /// <c>1</c>, <c>2.1</c>, <c>HEADER</c>, <c>TEXT</c>, <c>MIME</c>.
        /// </para>
        /// </summary>
        public string? Section
        {
            get{ return m_Section; }
        }

        /// <summary>
        /// Gets the starting byte offset used for partial <c>BODY.PEEK</c> or
        /// <c>BODY</c> FETCH requests. When this value is <c>null</c>, no
        /// partial‑fetch offset was specified and the body section is retrieved
        /// in full.
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
        /// Gets the maximum number of bytes to return for a partial
        /// <c>BODY.PEEK</c> or <c>BODY</c> FETCH request. When this value is
        /// <c>null</c>, no explicit partial‑fetch length was specified and the
        /// body section is retrieved in full.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <c>MaxCount</c> is non‑null, the FETCH request includes a
        /// partial‑fetch length using the syntax <c>&lt;offset.length&gt;</c>.
        /// The server returns at most this number of bytes starting at the
        /// specified offset.
        /// </para>
        /// </remarks>
        public long? MaxCount
        {
            get{ return m_MaxCount; }
        }

        #endregion
    }
}
