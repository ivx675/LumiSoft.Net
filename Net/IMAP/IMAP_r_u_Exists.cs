using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP EXISTS untagged server status response as defined
    /// in RFC 3501 section 7.3.1.
    /// </summary>
    /// <remarks>
    /// The EXISTS response reports the total number of messages currently
    /// present in the selected mailbox. It is an untagged server status
    /// response that may be sent at any time while a mailbox is selected,
    /// including during SELECT/EXAMINE, after new message delivery, or
    /// following EXPUNGE operations.
    ///
    /// This class stores the message count and provides methods for parsing
    /// and serializing the EXISTS response in IMAP wire format.
    /// </remarks>
    public class IMAP_r_u_Exists : IMAP_r_u
    {
        private int m_MessageCount = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Exists"/> class
        /// using the message count reported by the IMAP EXISTS response.
        /// </summary>
        /// <param name="messageCount">
        /// The total number of messages in the selected mailbox as reported by
        /// the IMAP EXISTS response (RFC 3501, section 7.3.1). The value must be
        /// greater than or equal to zero.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="messageCount"/> is less than zero.
        /// </exception>
        /// <remarks>
        /// The EXISTS response is an untagged server status response that informs
        /// the client of the current number of messages present in the selected
        /// mailbox. This constructor stores that value for later retrieval.
        /// </remarks>
        public IMAP_r_u_Exists(int messageCount)
        {
            if(messageCount < 0){
                throw new ArgumentException("Arguments 'messageCount' value must be >= 0.","messageCount");
            }

            m_MessageCount = messageCount;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP EXISTS response as defined in RFC 3501 section 7.3.1.
        /// </summary>
        /// <param name="response">
        /// Raw IMAP server response string. Expected format: "* &lt;number&gt; EXISTS".
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_r_u_Exists"/> instance containing the parsed message count.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is null.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not conform to the EXISTS syntax:
        /// <list type="bullet">
        /// <item><description>Missing or invalid untagged marker "*".</description></item>
        /// <item><description>Missing or non-numeric message count.</description></item>
        /// <item><description>Missing or incorrect "EXISTS" keyword.</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// The EXISTS response is an untagged server status response reporting the
        /// total number of messages currently present in the selected mailbox.
        /// It may appear at any time while a mailbox is selected.
        /// </remarks>
        public static IMAP_r_u_Exists Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException("response");
            }

            /*
              IMAP EXISTS Response
              RFC 3501 — Section 7.3.1

              Type:
                Untagged server status response.

              Syntax:
                "*" SP <number> SP "EXISTS"

              Meaning:
                Reports the total number of messages currently present
                in the selected mailbox.

              Properties:
                - Always untagged.
                - Never includes an optional response code.
                - May be sent at any time while a mailbox is selected.

              When sent:
                - During SELECT/EXAMINE to report initial mailbox size.
                - When new messages are delivered (count increases).
                - After EXPUNGE operations (count decreases).
                - Whenever the mailbox's message count changes.

              Example:
                * 42 EXISTS
            */

            StringReader r = new StringReader(response);
            
            // "*"
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP EXISTS response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP EXISTS response (expected '*'): {response}");
            }

            // Messages count
            string? word = r.ReadWord();
            if(word == null || !int.TryParse(word,out int messagesCount)){
                throw new ParseException($"Invalid IMAP RECENT response (invalid message count): {response}");
            }

            // "EXISTS"
            word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP EXISTS response (missing EXISTS): {response}");
            }            
            if(!string.Equals(word,"EXISTS",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP EXISTS response (expected 'EXISTS'): {response}");
            }

                                               
            return new IMAP_r_u_Exists(messagesCount);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Converts this EXISTS response to its IMAP wire-format representation.
        /// </summary>
        /// <returns>
        /// A string formatted according to RFC 3501 section 7.3.1, representing an
        /// untagged EXISTS response. The format is:
        /// <c>* &lt;messageCount&gt; EXISTS\r\n</c>
        /// </returns>
        /// <remarks>
        /// The EXISTS response reports the total number of messages currently present
        /// in the selected mailbox. This method produces the exact string that an IMAP
        /// server would send to the client to indicate the current message count.
        /// </remarks>
        public override string ToString()
        {
            // Example:    S: * 23 EXISTS

            return "* " + m_MessageCount.ToString() + " EXISTS\r\n";
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the total number of messages currently known in the mailbox.
        /// </summary>
        /// <remarks>
        /// This value reflects the message count reported by the IMAP EXISTS
        /// response (RFC 3501, section 7.3.1). It represents the number of
        /// messages present in the selected mailbox at the time the EXISTS
        /// response was parsed.
        /// </remarks>
        public int MessageCount
        {
            get{ return m_MessageCount; }
        }

        #endregion
    }
}
