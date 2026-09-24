using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP 
{
    /// <summary>
    /// Represents the Gmail‑specific IMAP extension data item <c>X-GM-LABELS</c>
    /// returned within a <c>FETCH</c> response. Gmail exposes all labels assigned
    /// to a message as a parenthesized list of IMAP strings, where each string
    /// corresponds to either a system label (such as <c>\Inbox</c> or
    /// <c>\Important</c>) or a user‑defined label.
    /// <para>
    /// Labels are Gmail’s organizational model and a single message may have zero,
    /// one, or many labels simultaneously. The values stored in this object are
    /// the raw UTF‑8 label strings exactly as transmitted by Gmail, without
    /// normalization, mapping, or client‑side interpretation.
    /// </para>
    /// <para>
    /// <c>X-GM-LABELS</c> is part of Gmail’s proprietary IMAP extensions and is not
    /// defined by RFC 3501. Servers other than Gmail do not return this data item.
    /// </para>
    /// </summary>
    public class IMAP_t_Fetch_r_i_xGmailLabels : IMAP_t_Fetch_r_i
    {
        private string[] m_pLabels;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_xGmailLabels"/>
        /// class using the set of Gmail labels returned in the <c>X-GM-LABELS</c>
        /// FETCH data item.
        /// <para>
        /// Gmail transmits labels as a parenthesized list of IMAP strings, where each
        /// string represents either a system label (such as <c>\Inbox</c> or
        /// <c>\Important</c>) or a user‑defined label. The values supplied to this
        /// constructor are the raw UTF‑8 label strings exactly as parsed from the
        /// server response, without normalization or client‑side mapping.
        /// </para>
        /// <para>
        /// The <paramref name="labels"/> array must not be <c>null</c>. An empty array
        /// indicates that the message has no labels assigned.
        /// </para>
        /// </summary>
        /// <param name="labels">
        /// The array of Gmail label strings parsed from the <c>X-GM-LABELS</c> FETCH
        /// response. Each element corresponds to one IMAP string in the label list.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="labels"/> is <c>null</c>.
        /// </exception>
        public IMAP_t_Fetch_r_i_xGmailLabels(string[] labels)
        {
            if(labels == null){
                throw new ArgumentNullException(nameof(labels));
            }

            m_pLabels = labels;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the Gmail‑specific IMAP extension data item <c>X-GM-LABELS</c> from a
        /// <c>FETCH</c> response. Gmail exposes all labels assigned to a message as a
        /// parenthesized list of IMAP strings, where each string represents either a
        /// system label (such as <c>\Inbox</c> or <c>\Important</c>) or a user‑defined
        /// label.
        /// <para>
        /// The method expects the next atom in the protocol stream to be
        /// <c>X-GM-LABELS</c>, followed by a parenthesized list containing one or more
        /// IMAP strings. Each label may be transmitted as a quoted string or as an IMAP
        /// literal. The parser reads each string using <see cref="_IMAP_Reader.ReadStringAsync"/>
        /// to ensure correct handling of quoted strings, escaped characters, UTF‑8
        /// content, and literal blocks.
        /// </para>
        /// <para>
        /// <c>X-GM-LABELS</c> is part of Gmail’s proprietary IMAP extensions and is not
        /// defined by RFC 3501. Servers other than Gmail do not return this data item.
        /// </para>
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the <c>X-GM-LABELS</c> data item within a
        /// <c>FETCH</c> response.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token used for asynchronous operations. This method observes
        /// cancellation when reading IMAP string values.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_t_Fetch_r_i_xGmailLabels"/> instance containing the parsed
        /// Gmail label list.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown if the <c>X-GM-LABELS</c> atom is missing, if the label list does not
        /// begin with a parenthesis, or if any label value is malformed or cannot be
        /// parsed as a valid IMAP string.
        /// </exception>
        internal static async Task<IMAP_t_Fetch_r_i_xGmailLabels> ParseAsync(_IMAP_Reader imapReader,CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /* Gmail IMAP Extension: X-GM-LABELS
               Reference: https://developers.google.com/gmail/imap_extensions

               X-GM-LABELS is a Gmail-specific FETCH data item that exposes the complete
               set of Gmail labels assigned to a message. Labels may include both system
               labels (e.g., "\Inbox", "\Important", "\Starred") and user-defined labels
               (e.g., "Work", "ProjectA", "Invoices").

               Syntax (FETCH response):
                   X-GM-LABELS SP label-list

               Example:
                   S: * 23 FETCH (X-GM-LABELS ("\\Inbox" "Work" "Important"))

               Notes:
                 • The label list is a parenthesized sequence of quoted strings.
                 • System labels begin with a backslash (e.g., "\Inbox").
                 • User labels are arbitrary UTF-8 strings chosen by the user.
                 • A message may have zero, one, or many labels.
                 • Labels are Gmail’s organizational model; messages may appear in
                   multiple labels simultaneously.
                 • Useful for:
                     – synchronizing Gmail’s label model,
                     – mapping labels to client-side metadata,
                     – implementing Gmail-style message organization,
                     – cross-folder correlation.

               This parser reads the X-GM-LABELS atom and then parses the following
               parenthesized list of quoted strings. If the atom is missing or the list
               is malformed, a ParseException is thrown.
            */

            if(!string.Equals("X-GM-LABELS", imapReader.ReadAtom(), StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: X-GM-LABELS data-item not found.");
            }

            if(imapReader.ReadChar() != '('){
                throw new ParseException("Invalid FETCH response: X-GM-LABELS data-item not found.");
            }

            List<string> labels = new List<string>();
            while(true){
                if(imapReader.PeekIs(')')){
                    imapReader.ReadChar();

                    break;
                }
                else{
                    string? label = await imapReader.ReadStringAsync(cancellationToken);
                    if(label == null){
                        throw new ParseException("Invalid X-GM-LABELS value: expected string.");
                    }

                    labels.Add(label);
                }
            }

            return new IMAP_t_Fetch_r_i_xGmailLabels(labels.ToArray());
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the complete set of Gmail labels associated with the
        /// <c>X-GM-LABELS</c> FETCH data item. Gmail represents labels as a
        /// parenthesized list of IMAP strings, where each string corresponds to
        /// either a system label (such as <c>\Inbox</c> or <c>\Important</c>) or a
        /// user‑defined label.
        /// <para>
        /// Labels are Gmail’s organizational model and a single message may have
        /// zero, one, or many labels simultaneously. The values returned here are
        /// the raw UTF‑8 label strings exactly as transmitted by Gmail, without
        /// normalization or client‑side mapping.
        /// </para>
        /// <para>
        /// Each label may be transmitted as a quoted string or an IMAP literal.
        /// The parser ensures correct handling of quoted strings, escaped
        /// characters, and UTF‑8 content, and exposes the resulting label list as
        /// a <see cref="string"/> array.
        /// </para>
        /// </summary>
        public string[] Labels 
        { 
            get{ return m_pLabels; }
        }

        #endregion
    }
}
