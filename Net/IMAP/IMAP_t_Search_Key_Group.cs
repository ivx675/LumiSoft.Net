using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a parenthesized IMAP search‑key group, which combines
    /// multiple search keys into a single composite expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parenthesized groups are defined by RFC 3501 as a mechanism for
    /// controlling operator precedence within IMAP SEARCH expressions.
    /// A group may contain any number of search keys, including nested
    /// groups or logical operators such as <c>OR</c> and <c>NOT</c>.
    /// </para>
    /// <para>
    /// Grouping allows complex search expressions to be constructed, for
    /// example: <c>(OR FROM "alice" (NOT TO "bob"))</c>.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Group : IMAP_t_Search_Key
    {
        private IMAP_t_Search_Key[] m_pKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Group"/>
        /// class with the specified collection of operand search keys.
        /// </summary>
        /// <param name="keys">
        /// The array of search keys that form the contents of the parenthesized
        /// group. The array must contain at least one element.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="keys"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="keys"/> is empty.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Parenthesized groups are defined by RFC 3501 as composite search
        /// expressions that control operator precedence. A group may contain any
        /// number of search keys, including nested groups or logical operators
        /// such as <c>OR</c> and <c>NOT</c>.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Group(IMAP_t_Search_Key[] keys)
        {
            if(keys == null){
                throw new ArgumentNullException(nameof(keys));
            }
            if(keys.Length == 0){
                throw new ArgumentException("Argument 'keys' may not be empty.",nameof(keys));
            }

            m_pKeys = keys;
        }


        #region static method Parse

        /// <summary>
        /// Parses a parenthesized IMAP search‑key group from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the group to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Group"/> instance containing all
        /// search keys found inside the parenthesized expression.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Parenthesized groups are defined by RFC 3501 as composite search
        /// expressions that control operator precedence. A group may contain any
        /// number of search keys, including nested groups or logical operators
        /// such as <c>OR</c> and <c>NOT</c>.
        /// </para>
        /// <para>
        /// If the reader begins with an opening parenthesis, the method extracts
        /// the full parenthesized substring using <c>ReadParenthesized</c> and
        /// parses its contents as a standalone search expression. Each contained
        /// search key is parsed by delegating to
        /// <see cref="IMAP_t_Search_Key.ParseKey(StringReader)"/>.
        /// </para>
        /// </remarks>
        public static IMAP_t_Search_Key_Group Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            // Remove parenthesis, if any.
            if(r.StartsWith("(")){
                r = new StringReader(r.ReadParenthesized());
            }            

            List<IMAP_t_Search_Key> keys = new List<IMAP_t_Search_Key>();

            r.ReadToFirstChar();
            while(r.Available > 0){
                keys.Add(IMAP_t_Search_Key.ParseKey(r));
            }

            return new IMAP_t_Search_Key_Group(keys.ToArray());
        }

        #endregion


        #region method ToString

        /// <summary>
        /// Returns a human‑readable representation of a parenthesized group of
        /// IMAP search keys.
        /// </summary>
        /// <returns>
        /// A string of the form <c>(key1 key2 ...)</c> containing each search key
        /// in the group, separated by spaces.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Parenthesized groups are defined by RFC 3501 as composite search
        /// expressions that control operator precedence. A group may contain any
        /// number of search keys, including nested groups or logical operators
        /// such as <c>OR</c> and <c>NOT</c>.
        /// </para>
        /// <para>
        /// This method produces a simplified, human‑readable representation
        /// intended for logging and debugging. Wire‑format serialization is
        /// handled by <c>ToCommandBuilder</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            StringBuilder retVal = new StringBuilder();
            retVal.Append("(");
            for(int i=0;i<m_pKeys.Length;i++){
                if(i > 0){
                    retVal.Append(" ");
                }
                retVal.Append(m_pKeys[i].ToString());
            }
            retVal.Append(")");

            return retVal.ToString();
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends a parenthesized group of IMAP search keys to the command
        /// under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key group.
        /// </param>
        /// <remarks>
        /// <para>
        /// Parenthesized groups are defined by RFC 3501 as a way to combine
        /// multiple search keys into a single composite expression. Grouping
        /// affects operator precedence, allowing complex expressions such as
        /// <c>(OR FROM "alice" TO "bob")</c> or nested logical constructs.
        /// </para>
        /// <para>
        /// Serialization writes an opening parenthesis, followed by each search
        /// key in sequence separated by a single space, and then a closing
        /// parenthesis. Each contained search key is responsible for serializing
        /// its own IMAP representation via <c>ToCommandBuilder</c>.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString("(");
            for(int i=0;i<m_pKeys.Length;i++){
                if(i > 0){
                    builder.AddString(" ");
                }
                m_pKeys[i].ToCommandBuilder(builder);
            }
            builder.AddString(")");
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the collection of IMAP search keys contained in this
        /// parenthesized group.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Parenthesized groups are defined by RFC 3501 as composite search
        /// expressions that control operator precedence. A group may contain any
        /// number of search keys, including nested groups or logical operators
        /// such as <c>OR</c> and <c>NOT</c>.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key[] Keys
        {
            get{ return m_pKeys; }
        }

        #endregion
    }
}
