using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP RECENT untagged server status response as defined
    /// in RFC 3501 section 7.3.2.
    /// </summary>
    /// <remarks>
    /// The RECENT response reports the number of messages in the selected
    /// mailbox that have the <c>\Recent</c> flag set. This response is sent
    /// during a SELECT or EXAMINE command and may also appear whenever the
    /// mailbox size changes (for example, when new messages arrive).
    ///
    /// The RECENT count does not necessarily correspond to a contiguous range
    /// of the newest messages. Multiple concurrent sessions or external
    /// mailbox reordering can cause non‑contiguous recent message numbering.
    /// The only reliable way to identify recent messages is by checking the
    /// <c>\Recent</c> flag on individual messages or performing a SEARCH RECENT.
    ///
    /// Clients MUST record updates from the RECENT response.
    /// </remarks>
    public class IMAP_r_u_Recent : IMAP_r_u
    {
        private int m_MessageCount = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Recent"/> class
        /// using the message count reported by the IMAP RECENT response.
        /// </summary>
        /// <param name="messageCount">
        /// The number of messages in the selected mailbox that have the
        /// <c>\Recent</c> flag set, as reported by the IMAP RECENT response
        /// (RFC 3501, section 7.3.2). The value must be greater than or equal
        /// to zero.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="messageCount"/> is less than zero.
        /// </exception>
        /// <remarks>
        /// The RECENT response is an untagged server status response that informs
        /// the client how many messages in the selected mailbox are marked with
        /// the <c>\Recent</c> flag. This constructor stores that value for later
        /// retrieval and serialization.
        /// </remarks>
        public IMAP_r_u_Recent(int messageCount)
        {
            if(messageCount < 0){
                throw new ArgumentException("Arguments 'messageCount' value must be >= 0.","messageCount");
            }

            m_MessageCount = messageCount;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP RECENT response as defined in RFC 3501 section 7.3.2.
        /// </summary>
        /// <param name="response">
        /// The raw IMAP server response string. Expected format:
        /// <c>* &lt;number&gt; RECENT</c>.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_r_u_Recent"/> instance containing the parsed
        /// count of messages marked with the <c>\Recent</c> flag.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is null.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not conform to the RECENT syntax:
        /// <list type="bullet">
        /// <item><description>Missing or invalid untagged marker "*".</description></item>
        /// <item><description>Missing or non-numeric message count.</description></item>
        /// <item><description>Missing or incorrect "RECENT" keyword.</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// The RECENT response reports the number of messages in the selected
        /// mailbox that have the <c>\Recent</c> flag set. This response is sent
        /// during SELECT or EXAMINE and may also appear when the mailbox size
        /// changes (e.g., new messages arriving).
        ///
        /// The RECENT count does not necessarily correspond to the newest
        /// messages or a contiguous range. Multiple concurrent sessions or
        /// external mailbox reordering can cause non‑contiguous recent message
        /// numbering.
        ///
        /// Clients MUST record updates from the RECENT response.
        /// </remarks>
        public static IMAP_r_u_Recent Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException("response");
            }

            /* RFC 3501 7.3.2.  RECENT Response
                Contents:   none

                  The RECENT response reports the number of messages with the
                  \Recent flag set.  This response occurs as a result of a SELECT or
                  EXAMINE command, and if the size of the mailbox changes (e.g., new
                  messages).

                       Note: It is not guaranteed that the message sequence
                       numbers of recent messages will be a contiguous range of
                       the highest n messages in the mailbox (where n is the
                       value reported by the RECENT response).  Examples of
                       situations in which this is not the case are: multiple
                       clients having the same mailbox open (the first session
                       to be notified will see it as recent, others will
                       probably see it as non-recent), and when the mailbox is
                       re-ordered by a non-IMAP agent.

                       The only reliable way to identify recent messages is to
                       look at message flags to see which have the \Recent flag
                       set, or to do a SEARCH RECENT.

                  The update from the RECENT response MUST be recorded by the
                  client.

                Example:    S: * 5 RECENT
            */

            StringReader r = new StringReader(response);
            
            // "*"
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP RECENT response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP RECENT response (expected '*'): {response}");
            }

            // Messages count
            string? word = r.ReadWord();
            if(word == null || !int.TryParse(word,out int messagesCount)){
                throw new ParseException($"Invalid IMAP RECENT response (invalid message count): {response}");
            }

            // "RECENT"
            word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP RECENT response (missing RECENT): {response}");
            }            
            if(!string.Equals(word,"RECENT",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP RECENT response (expected 'RECENT'): {response}");
            }

                                               
            return new IMAP_r_u_Recent(messagesCount);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Converts this RECENT response to its IMAP wire‑format representation.
        /// </summary>
        /// <returns>
        /// A string formatted according to RFC 3501 section 7.3.2, representing an
        /// untagged RECENT response. The format is:
        /// <c>* &lt;messageCount&gt; RECENT\r\n</c>
        /// </returns>
        /// <remarks>
        /// The RECENT response reports the number of messages in the selected
        /// mailbox that have the <c>\Recent</c> flag set. This method produces the
        /// exact string an IMAP server would send to indicate the current count of
        /// recent messages.
        /// </remarks>
        public override string ToString()
        {
            // Example:    S: * 5 RECENT

            return "* " + m_MessageCount.ToString() + " RECENT\r\n";
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the number of messages reported by the IMAP server for this
        /// response type.
        /// </summary>
        /// <remarks>
        /// For EXISTS responses, this value represents the total number of
        /// messages currently present in the selected mailbox (RFC 3501,
        /// section 7.3.1).
        ///
        /// For RECENT responses, this value represents the number of messages
        /// marked with the <c>\Recent</c> flag (RFC 3501, section 7.3.2).
        ///
        /// The value is taken directly from the untagged server status response
        /// and reflects the mailbox state at the time the response was parsed.
        /// </remarks>
        public int MessageCount
        {
            get{ return m_MessageCount; }
        }

        #endregion
    }
}
