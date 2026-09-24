using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>HEADER</c> search key, which selects messages
    /// whose specified header field contains the given value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>HEADER</c> search key is defined by RFC 3501. It matches messages
    /// that contain a header field with the specified field-name (as defined in
    /// RFC 2822) and whose header field value — the text appearing after the
    /// colon — contains the specified string using a case-insensitive substring
    /// search.
    /// </para>
    /// <para>
    /// If the value string is zero-length, the search matches all messages that
    /// contain the specified header field-name, regardless of the header’s
    /// contents.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Header : IMAP_t_Search_Key
    {
        private string m_FieldName = "";
        private string m_Value     = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Header"/>
        /// class with the specified header field name and match value.
        /// </summary>
        /// <param name="fieldName">
        /// The name of the header field to examine. This is an IMAP string and may
        /// originate from either a quoted string or a literal.
        /// </param>
        /// <param name="value">
        /// The value to match within the specified header field. Matching is
        /// performed as a case‑insensitive substring search, as defined by RFC 3501.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="fieldName"/> or <paramref name="value"/>
        /// is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>HEADER</c> search key accepts two IMAP strings: the header field
        /// name and the value to match. Both arguments may independently be quoted
        /// strings or IMAP literals, allowing full RFC‑compliant serialization.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Header(string fieldName,string value)
        {
            if(fieldName == null){
                throw new ArgumentNullException("fieldName");
            }
            if(value == null){
                throw new ArgumentNullException("value");
            }

            m_FieldName = fieldName;
            m_Value     = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>HEADER</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Header"/> instance containing the
        /// parsed header field name and value.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>HEADER</c>, or when either the
        /// field name or value cannot be parsed as valid IMAP strings.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>HEADER</c> search key is defined by RFC 3501 and selects messages
        /// whose specified header field contains the given value, using a
        /// case‑insensitive substring match.
        /// </para>
        /// <para>
        /// Both arguments are IMAP strings and may be supplied either as quoted
        /// strings or as literals. Parsing is performed using
        /// <see cref="IMAP_Utils.ReadString(StringReader)"/>, which supports both
        /// formats. This allows the <c>HEADER</c> key to contain two literals.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Header Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"HEADER",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'HEADER' key.");
            }
            string? fieldName = IMAP_Utils.ReadString(r);
            if(fieldName == null){
                throw new ParseException("Parse error: Invalid 'HEADER' field-name value.");
            }
            string? value = IMAP_Utils.ReadString(r);
            if(value == null){
                throw new ParseException("Parse error: Invalid 'HEADER' string value.");
            }

            return new IMAP_t_Search_Key_Header(fieldName,value);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>HEADER</c> search key with its field name and
        /// value arguments formatted as IMAP quoted strings.
        /// </summary>
        /// <returns>
        /// A string of the form <c>HEADER "field-name" "value"</c>, selecting
        /// messages whose specified header field contains the given value,
        /// using a case‑insensitive substring match as defined by RFC 3501.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>HEADER</c> search key accepts two IMAP strings: the header
        /// field name and the value to match. Both arguments may be quoted
        /// strings or literals when serialized through
        /// <see cref="ToCommandBuilder(CommandBuilder)"/>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "HEADER " + TextUtils.QuoteString(m_FieldName) + " " + TextUtils.QuoteString(m_Value);
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>HEADER</c> search key and its two string arguments
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>HEADER</c> search key is defined by RFC 3501 and selects messages
        /// whose specified header field contains the given value, using a
        /// case‑insensitive substring match.
        /// </para>
        /// <para>
        /// Both the header field name and the value are IMAP strings. Each may be
        /// emitted either as a quoted string or as a literal, depending on their
        /// content. Serialization is performed using
        /// <see cref="CommandBuilder.AddStringOrLiteral(string)"/> to ensure
        /// correct IMAP formatting for both arguments.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString("HEADER ");
            builder.AddStringOrLiteral(m_FieldName);
            builder.AddString(" ");
            builder.AddStringOrLiteral(m_Value);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the header field-name used by this IMAP <c>HEADER</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The field-name identifies which RFC 2822 header field is examined when
        /// evaluating the <c>HEADER</c> search key. Only the header field’s value
        /// (the text appearing after the colon) is searched for the specified
        /// match string.
        /// </para>
        /// </remarks>
        public string FieldName
        {
            get{ return m_FieldName; }
        }

        /// <summary>
        /// Gets the match string used by this IMAP <c>HEADER</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value represents the case‑insensitive substring to search for within
        /// the specified header field’s text (the portion appearing after the colon),
        /// as defined by RFC 3501.
        /// </para>
        /// <para>
        /// A zero‑length value matches all messages that contain the specified
        /// header field-name, regardless of the header’s contents.
        /// </para>
        /// </remarks>
        public string Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
