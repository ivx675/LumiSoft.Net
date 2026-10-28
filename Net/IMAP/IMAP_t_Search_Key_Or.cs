using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>OR</c> search key, which performs logical
    /// disjunction between two operand search keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>OR</c> search key is defined by RFC 9051 and matches messages
    /// that satisfy <em>either</em> of its two operand search keys. IMAP
    /// SEARCH syntax requires exactly two operands for <c>OR</c>, making it
    /// the only binary logical operator in the SEARCH grammar.
    /// </para>
    /// <para>
    /// Each operand may be a simple search key (such as <c>FROM</c>,
    /// <c>KEYWORD</c>, or <c>TEXT</c>) or a composite expression (such as
    /// another <c>OR</c>, a <c>NOT</c>, or grouped constructs)
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Or : IMAP_t_Search_Key
    {
        private IMAP_t_Search_Key m_pSearchKey1;
        private IMAP_t_Search_Key m_pSearchKey2;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Or"/>
        /// class with the two operand search keys.
        /// </summary>
        /// <param name="key1">
        /// The first operand search key. A message matches the <c>OR</c> expression
        /// if it satisfies this key or the second operand.
        /// </param>
        /// <param name="key2">
        /// The second operand search key. IMAP SEARCH syntax requires exactly two
        /// operands for the <c>OR</c> operator.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="key1"/> or <paramref name="key2"/> is
        /// <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>OR</c> search key is defined by RFC 9051 and performs logical
        /// disjunction between two search keys. It matches messages that satisfy
        /// <em>either</em> operand.
        /// </para>
        /// <para>
        /// Both operands may be simple or composite search expressions.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Or(IMAP_t_Search_Key key1,IMAP_t_Search_Key key2)
        {
            if(key1 == null){
                throw new ArgumentNullException("key1");
            }
            if(key2 == null){
                throw new ArgumentNullException("key2");
            }

            m_pSearchKey1 = key1;
            m_pSearchKey2 = key2;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses a SEARCH <c>OR</c> key from the specified
        /// <see cref="_IMAP_Reader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="_IMAP_Reader"/> positioned at the search key to
        /// parse.
        /// </param>
        /// <returns>
        /// A task whose result contains a new
        /// <see cref="IMAP_t_Search_Key_Or"/> instance representing the
        /// parsed <c>OR</c> search key.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>OR</c> or when either
        /// operand search key cannot be parsed.
        /// </exception>
        /// <remarks>
        /// The <c>OR</c> search key matches messages that satisfy either of
        /// its two operand search keys.
        /// <para>
        /// Unlike most SEARCH keys, <c>OR</c> requires exactly two operand
        /// search keys which are parsed immediately following the
        /// <c>OR</c> token.
        /// </para>
        /// </remarks>
        internal static async Task<IMAP_t_Search_Key_Or> ParseAsync(_IMAP_Reader r)
        {
            if(r == null){
                throw new ArgumentNullException(nameof(r));
            }

            string word = r.ReadAtom();
            if(!string.Equals(word,"OR",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'OR' key.");
            }

            return new IMAP_t_Search_Key_Or(await IMAP_t_Search_Key.ParseKeyAsync(r),await IMAP_t_Search_Key.ParseKeyAsync(r));
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a human‑readable representation of the IMAP <c>OR</c>
        /// search key and its two operand search keys.
        /// </summary>
        /// <returns>
        /// A string of the form <c>OR &lt;key1&gt; &lt;key2&gt;</c>, where
        /// <c>key1</c> and <c>key2</c> are the operand search keys that are
        /// combined using logical OR.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>OR</c> search key is defined by RFC 9051 and matches messages
        /// that satisfy <em>either</em> of the two operand search keys. IMAP
        /// SEARCH syntax requires exactly two operands for <c>OR</c>.
        /// </para>
        /// <para>
        /// This method produces a simplified, human‑readable representation
        /// intended for logging and debugging. It does not include IMAP literal
        /// formatting or parentheses; wire‑format serialization is handled by
        /// <c>ToCommandBuilder</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "OR " + m_pSearchKey1.ToString() + " " + m_pSearchKey2.ToString();
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>OR</c> search key and its two operand search keys
        /// to the command under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>OR</c> search key is defined by RFC 9051 and matches messages
        /// that satisfy <em>either</em> of the two operand search keys. It is the
        /// only binary logical operator in IMAP SEARCH syntax.
        /// </para>
        /// <para>
        /// Serialization writes the <c>OR</c> operator followed by the two operand
        /// search keys in sequence. Parentheses are not required; IMAP SEARCH
        /// grammar defines <c>OR</c> as always taking exactly two operands.
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString("OR ");
            m_pSearchKey1.ToCommandBuilder(builder);
            builder.AddString(" ");
            m_pSearchKey2.ToCommandBuilder(builder);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the first operand search key of this IMAP <c>OR</c> expression.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>OR</c> search key, defined by RFC 9051, performs logical
        /// disjunction between two operand search keys. A message matches the
        /// <c>OR</c> expression if it satisfies <em>either</em> operand.
        /// </para>
        /// <para>
        /// This property exposes the left‑hand operand exactly as provided during
        /// construction or parsing. The operand may itself be a simple search key
        /// (such as <c>FROM</c> or <c>KEYWORD</c>) or a composite expression
        /// (such as another <c>OR</c> or a <c>NOT</c> key).
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key SearchKey1
        {
            get{ return m_pSearchKey1; }
        }

        /// <summary>
        /// Gets the second operand search key of this IMAP <c>OR</c> expression.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>OR</c> search key, defined by RFC 9051, performs logical
        /// disjunction between two operand search keys. A message matches the
        /// <c>OR</c> expression if it satisfies either the first operand or this
        /// second operand.
        /// </para>
        /// <para>
        /// This property exposes the right‑hand operand exactly as provided during
        /// construction or parsing. The operand may be a simple search key (such as
        /// <c>FROM</c>, <c>KEYWORD</c>, or <c>TEXT</c>) or a composite expression
        /// (such as another <c>OR</c> or a <c>NOT</c> key).
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key SearchKey2
        {
            get{ return m_pSearchKey2; }
        }

        #endregion
    }
}
