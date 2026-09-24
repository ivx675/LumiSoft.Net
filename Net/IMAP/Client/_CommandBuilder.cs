using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP.Client 
{    
    #region struct CommandPart

    /// <summary>
    /// Represents a single segment of an IMAP command, either a command-line
    /// fragment or a literal payload.
    /// </summary>
    /// <param name="isLiteral">
    /// Indicates whether this item contains a literal payload (<c>true</c>) or
    /// a command-line fragment (<c>false</c>).
    /// </param>
    /// <param name="commandline">
    /// The command-line text to send when this item is not a literal. This is
    /// <c>null</c> when <c>IsLiteral</c> is true.
    /// </param>
    /// <param name="literal">
    /// The literal byte array to send when <c>IsLiteral</c> is true. This is
    /// <c>null</c> for non-literal items.
    /// </param>
    internal readonly struct CommandPart(bool isLiteral,string? commandline,byte[]? literal)
    {
        /// <summary>
        /// True if this item represents a literal payload; otherwise false.
        /// </summary>
        public bool IsLiteral { get; } = isLiteral;

        /// <summary>
        /// The command-line text associated with this item, or null when this
        /// item is a literal.
        /// </summary>
        public string? CommandLine   { get; } = commandline;

        /// <summary>
        /// The literal byte content for this item, or null when this item is not
        /// a literal.
        /// </summary>
        public byte[]? Literal { get; } = literal;
    }

    #endregion

    /// <summary>
    /// Builds an IMAP command consisting of textual segments and optional literal
    /// byte blocks. A new instance is intended to be used for each command.
    /// </summary>
    /// <remarks>
    /// The builder accumulates command text in a line buffer until a literal is
    /// required. When a literal is added, the current line is flushed as a
    /// <see cref="CommandPart"/> and the literal bytes are emitted as a separate
    /// item. The caller is responsible for appending the final CRLF that terminates
    /// the IMAP command.
    /// 
    /// UTF‑8 handling and literal-plus behavior are controlled by constructor
    /// parameters and apply to the entire command.
    /// </remarks>
    internal class CommandBuilder 
    {
        private List<CommandPart> m_pCommandItems = new List<CommandPart>();
        private bool              m_Utf8Strings   = false;
        private bool              m_LiteralPluss  = false;
        private StringBuilder     m_CmdLine       = new StringBuilder();

        /// <summary>
        /// Creates a new <see cref="CommandBuilder"/> for constructing a single IMAP command.
        /// </summary>
        /// <param name="utf8Strings">
        /// If true, quoted strings may contain UTF‑8 characters. If false, quoted
        /// strings are restricted to ASCII.
        /// </param>
        /// <param name="literalPluss">
        /// If true, literal-plus syntax (<c>{size+}</c>) is used and the server is
        /// assumed to support LITERAL+. If false, standard literal syntax (<c>{size}</c>)
        /// is used and the caller must wait for a continuation response before sending
        /// literal bytes.
        /// </param>
        public CommandBuilder(bool utf8Strings,bool literalPluss)
        {
            m_Utf8Strings  = utf8Strings;
            m_LiteralPluss = literalPluss;
        }


        #region method AddString

        /// <summary>
        /// Appends a raw string directly to the current command line.
        /// </summary>
        /// <param name="value">The text to append. Must not be null.</param>
        public void AddString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            m_CmdLine.Append(value);
        }

        #endregion

        #region method AddStringOrLiteral

        /// <summary>
        /// Appends a value either as a quoted string or as a literal block,
        /// depending on whether the value satisfies IMAP quoted-string rules.
        /// </summary>
        /// <param name="value">The value to append. Must not be null.</param>
        /// <remarks>
        /// If the value contains characters that cannot appear in an IMAP quoted
        /// string (control characters, CR/LF, quotes, backslashes, or non‑ASCII
        /// when UTF‑8 quoting is disabled), it is emitted as a literal. The current
        /// line is flushed before the literal header, and the literal bytes are
        /// emitted as a separate <see cref="CommandPart"/>.
        /// </remarks>
        public void AddStringOrLiteral(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if(CanBeQuoted(value)){
                m_CmdLine.Append("\"" + value + "\"");
                return;
            }

            // Literal required
            byte[] bytes = Encoding.UTF8.GetBytes(value);

            string literalHeader = m_LiteralPluss ? $"{{{bytes.Length}+}}\r\n" : $" {{{bytes.Length}}}\r\n";

            // Append literal header to current line
            m_CmdLine.Append(literalHeader);

            // Flush current line
            m_pCommandItems.Add(new CommandPart(false,m_CmdLine.ToString(),null));

            // Add literal bytes
            m_pCommandItems.Add(new CommandPart(true, null, bytes));

            // Reset line for continuation
            m_CmdLine.Clear();
        }

        #endregion

        #region method Finish

        /// <summary>
        /// Finalizes the command by flushing the remaining text in the line buffer
        /// into a <see cref="CommandPart"/>. The caller must append the terminating
        /// CRLF when sending the command to the server.
        /// </summary>
        public CommandPart[] Finish()
        {
            m_pCommandItems.Add(new CommandPart(false,m_CmdLine.ToString(),null));

            return m_pCommandItems.ToArray();
        }

        #endregion


        #region method CanBeQuoted

        /// <summary>
        /// Determines whether the specified value can be represented as an IMAP
        /// quoted string under the current UTF‑8 rules.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns>
        /// True if the value can be safely emitted as a quoted string; otherwise false.
        /// </returns>
        /// <remarks>
        /// A quoted string cannot contain CR, LF, control characters, double quotes,
        /// or backslashes. If UTF‑8 quoting is disabled, all characters must be ASCII.
        /// </remarks>
        private bool CanBeQuoted(string value)
        {
            if (value == null) return false;

            foreach (char c in value){
                // CR or LF cannot appear in quoted strings
                if(c == '\r' || c == '\n'){
                    return false;
                }
                // Quoted string cannot contain double quote or backslash
                if(c == '"' || c == '\\'){
                    return false;
                }

                // Control characters are forbidden
                if(c < 0x20){
                    return false;
                }

                // If UTF8=SEARCH is NOT enabled, quoted strings must be ASCII
                if(!m_Utf8Strings && c > 0x7F){
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
