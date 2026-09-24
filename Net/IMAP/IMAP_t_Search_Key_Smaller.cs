using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>SMALLER</c> search key, which selects messages
    /// whose size in octets is strictly less than the specified value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>SMALLER</c> search key is defined by RFC 3501 and compares the
    /// message’s total size (in octets) to the provided numeric threshold.
    /// </para>
    /// <para>
    /// Messages whose size is less than the threshold satisfy the search
    /// criterion. The comparison is strictly “less than”, not “less than or
    /// equal to”.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Smaller : IMAP_t_Search_Key
    {
        private int m_Value = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Smaller"/>
        /// class with the specified size threshold.
        /// </summary>
        /// <param name="value">
        /// The size in octets used by the IMAP <c>SMALLER</c> search key.
        /// Messages whose size is strictly less than this value will satisfy
        /// the search criterion.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>SMALLER</c> search key is defined by RFC 3501 and compares the
        /// message’s total size (in octets) to the provided numeric threshold.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Smaller(int value)
        {
            m_Value = value;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>SMALLER</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Smaller"/> instance containing
        /// the parsed size threshold.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>SMALLER</c> or when the numeric
        /// value is missing or cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>SMALLER</c> search key is defined by RFC 3501 and selects messages
        /// whose size in octets is strictly less than the specified value.
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Smaller Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"SMALLER",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'SMALLER' key.");
            }
            string? value = r.ReadWord();
            if(value == null){
                throw new ParseException("Parse error: Invalid 'SMALLER' value.");
            }
            int size = 0;
            if(!int.TryParse(value,out size)){
                throw new ParseException("Parse error: Invalid 'SMALLER' value.");
            }

            return new IMAP_t_Search_Key_Smaller(size);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>SMALLER</c> search key with its numeric argument.
        /// </summary>
        /// <returns>
        /// A string of the form <c>SMALLER &lt;value&gt;</c>, selecting messages
        /// whose size in octets is strictly less than the specified value.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>SMALLER</c> search key is defined by RFC 3501 and compares the
        /// message’s total size (in octets) to the provided numeric threshold.
        /// </para>
        /// <para>
        /// Messages whose size is less than <c>m_Value</c> satisfy the criterion.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "SMALLER " + m_Value;
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>SMALLER</c> search key and its numeric argument
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>SMALLER</c> search key is defined by RFC 3501 and selects messages
        /// whose size in octets is strictly less than the specified value.
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
        /// Gets the size threshold used by the IMAP <c>SMALLER</c> search key.
        /// </summary>
        /// <remarks>
        /// Messages whose size in octets is strictly less than this value
        /// will satisfy the <c>SMALLER</c> search criterion, as defined by RFC 3501.
        /// </remarks>
        public int Value
        {
            get{ return m_Value; }
        }

        #endregion
    }
}
