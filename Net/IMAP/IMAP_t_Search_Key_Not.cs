using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using LumiSoft.Net.IMAP.Client;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP <c>NOT</c> search key, which negates the result of
    /// its operand search key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>NOT</c> search key is defined by RFC 3501 and provides logical
    /// negation within IMAP SEARCH expressions. It matches messages that do
    /// <em>not</em> satisfy the criteria of the enclosed operand.
    /// </para>
    /// <para>
    /// The operand may be any valid IMAP search key, including composite
    /// expressions such as <c>OR</c>, nested <c>NOT</c> keys, or grouped
    /// constructs.
    /// </para>
    /// </remarks>
    public class IMAP_t_Search_Key_Not : IMAP_t_Search_Key
    {
        private IMAP_t_Search_Key m_pSearchKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Search_Key_Not"/>
        /// class with the specified operand search key.
        /// </summary>
        /// <param name="key">
        /// The search key whose result is negated. The <c>NOT</c> operator matches
        /// messages that do <em>not</em> satisfy the criteria of this operand.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="key"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>NOT</c> search key is defined by RFC 3501 and provides logical
        /// negation within IMAP search expressions. It may wrap any valid search
        /// key, including composite expressions such as <c>OR</c> or nested
        /// <c>NOT</c> keys.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key_Not(IMAP_t_Search_Key key)
        {
            if(key == null){
                throw new ArgumentNullException("key");
            }

            m_pSearchKey = key;
        }


        #region static method Parse

        /// <summary>
        /// Parses the IMAP <c>NOT</c> search key from the provided
        /// <see cref="StringReader"/>.
        /// </summary>
        /// <param name="r">
        /// The <see cref="StringReader"/> positioned at the search key to parse.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Search_Key_Not"/> instance containing the
        /// parsed operand search key.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="r"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the next token is not <c>NOT</c>, or when the operand
        /// search key cannot be parsed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <c>NOT</c> search key is defined by RFC 3501 and negates the result
        /// of its operand. It matches messages that do <em>not</em> satisfy the
        /// enclosed search condition.
        /// </para>
        /// <para>
        /// After reading the <c>NOT</c> token, parsing continues by delegating to
        /// <see cref="IMAP_t_Search_Key.ParseKey(StringReader)"/> to parse the
        /// operand search key, which may itself be composite (e.g., <c>OR</c>,
        /// nested <c>NOT</c>, or grouped expressions).
        /// </para>
        /// </remarks>
        internal static IMAP_t_Search_Key_Not Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            string? word = r.ReadWord();
            if(!string.Equals(word,"NOT",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Parse error: Not a SEARCH 'NOT' key.");
            }

            return new IMAP_t_Search_Key_Not(IMAP_t_Search_Key.ParseKey(r));
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns a human‑readable representation of the IMAP <c>NOT</c>
        /// search key and its operand.
        /// </summary>
        /// <returns>
        /// A string of the form <c>NOT &lt;operand&gt;</c>, where the operand is
        /// the nested search key whose result is negated.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The <c>NOT</c> search key is defined by RFC 3501 and inverts the
        /// matching behavior of its operand. It selects messages that do
        /// <em>not</em> satisfy the enclosed search condition.
        /// </para>
        /// <para>
        /// This method produces a simplified, human‑readable representation
        /// intended for logging and debugging. It does not include parentheses
        /// or IMAP literal formatting; wire‑format serialization is handled by
        /// <c>ToCommandBuilder</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            return "NOT " + m_pSearchKey.ToString();
        }

        #endregion


        #region override method ToCommandBuilder

        /// <summary>
        /// Appends the IMAP <c>NOT</c> search key and its operand to the command
        /// under construction.
        /// </summary>
        /// <param name="builder">
        /// The command builder receiving the serialized search key.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>NOT</c> search key is defined by RFC 3501 and negates the result
        /// of the enclosed search key. It matches messages that do <em>not</em>
        /// satisfy the operand’s criteria.
        /// </para>
        /// <para>
        /// Serialization writes the <c>NOT</c> operator followed by the operand’s
        /// own IMAP representation. Parentheses are not required unless the operand
        /// is a composite expression (e.g., <c>OR</c> or multiple keys).
        /// </para>
        /// </remarks>
        internal override void ToCommandBuilder(CommandBuilder builder)
        {
            builder.AddString("NOT ");
            m_pSearchKey.ToCommandBuilder(builder);
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the operand search key whose result is negated by this
        /// <c>NOT</c> search key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>NOT</c> operator, defined by RFC 3501, inverts the matching
        /// behavior of its operand. Messages match this search key when they do
        /// <em>not</em> satisfy the criteria of the enclosed operand.
        /// </para>
        /// <para>
        /// The operand may itself be any valid IMAP search key, including composite
        /// expressions such as <c>OR</c>, nested <c>NOT</c>, or grouped constructs.
        /// </para>
        /// </remarks>
        public IMAP_t_Search_Key SearchKey
        {
            get{ return m_pSearchKey; }
        }

        #endregion
    }
}
