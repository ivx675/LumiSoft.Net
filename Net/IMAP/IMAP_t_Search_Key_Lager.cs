using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// This class represents IMAP SEARCH <b>LARGER (n)</b> key. Defined in RFC 3501 6.4.4.
    /// </summary>
    /// <remarks>Messages with an [RFC-2822] size larger than the specified number of octets.</remarks>
    public class IMAP_t_Search_Key_Larger : IMAP_t_Search_Key
    {
        private int m_Value = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Larger"/>
        /// class with the specified size threshold.
        /// </summary>
        /// <param name="value">
        /// The size in octets used by the IMAP <c>LARGER</c> search key.
        /// Messages whose size is strictly greater than this value will satisfy
        /// the search criterion.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>LARGER</c> search key is defined by RFC 3501 and compares the
        /// message’s total size (in octets) to the provided numeric threshold.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Larger(int value)
        {
            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>LARGER</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Larger"/> instance containing
        /// the parsed size threshold.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>LARGER</c> or when the numeric
        /// value is missing or cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>LARGER</c> search key is defined by RFC 3501 and selects messages
        /// whose size in octets is strictly greater than the specified value.
        /// </para>
        /// <para>
        /// The numeric argument is parsed using <see cref="int.TryParse(string,out int)"/>.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Larger Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"LARGER",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'LARGER' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'LARGER' value.");
            }
            int size = 0;
            if(!int.TryParse(value,out size)){
                throw new ParseException("Parse error: Invalid 'LARGER' value.");
            }

            return new IMAP_t_Search_Key_Larger(size);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>LARGER</c> search key with its numeric argument.
        /// </summary>
        /// <returns>
        /// A string of the form <c>LARGER &lt;value&gt;</c>, selecting messages
        /// whose size in octets is strictly greater than the specified value.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>LARGER</c> search key is defined by RFC 3501 and compares the
        /// message’s size (in octets) to the provided numeric threshold.
        /// </para>
        /// <para>
        /// Messages whose size is greater than <c>m_Value</c> satisfy the criterion.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "LARGER " + m_Value;
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>LARGER</c> search key and its numeric argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>LARGER</c> search key is defined by RFC 3501 and selects messages
        /// whose size in octets is strictly greater than the specified value.
        /// </para>
        /// <para>
        /// The numeric value is written verbatim into the IMAP command without
        /// additional formatting.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddString(this.ToString());
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the size threshold used by the IMAP <c>LARGER</c> search key.
        /// </summary>
        /// <remarks>
        /// Messages whose size in octets is strictly greater than this value
        /// will satisfy the <c>LARGER</c> search criterion, as defined by RFC 3501.
        /// </remarks>
        public int Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
