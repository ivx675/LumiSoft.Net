using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP EXPUNGE untagged server status response as defined
    /// in RFC 3501 section 7.4.1.
    /// </summary>
    /// <remarks>
    /// The EXPUNGE response reports that the message with the specified
    /// sequence number has been permanently removed from the mailbox. After an
    /// EXPUNGE, all messages with higher sequence numbers are immediately
    /// decremented by one, and this renumbering is reflected in subsequent
    /// server responses.
    ///
    /// An EXPUNGE response MUST NOT be sent when no command is in progress,
    /// nor while responding to a FETCH, STORE, or SEARCH command. It MAY be
    /// sent during UID FETCH, UID STORE, or UID SEARCH.
    ///
    /// Clients MUST record updates from the EXPUNGE response.
    /// </remarks>
    public class IMAP_r_u_Expunge : IMAP_r_u
    {
        private int m_SeqNo = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Expunge"/> class
        /// using the message sequence number reported by the IMAP EXPUNGE response.
        /// </summary>
        /// <param name="seqNo">
        /// The message sequence number of the expunged message, as reported by the
        /// IMAP EXPUNGE response (RFC 3501, section 7.4.1). The value must be
        /// greater than or equal to 1.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="seqNo"/> is less than 1.
        /// </exception>
        /// <remarks>
        /// The EXPUNGE response indicates that the message with the specified
        /// sequence number has been permanently removed from the mailbox. After an
        /// EXPUNGE, all messages with higher sequence numbers are immediately
        /// decremented by one, and this renumbering is reflected in subsequent
        /// server responses.
        ///
        /// This constructor stores the sequence number of the expunged message for
        /// later retrieval and serialization.
        /// </remarks>
        public IMAP_r_u_Expunge(int seqNo)
        {
            if(seqNo < 1){
                throw new ArgumentException("Arguments 'seqNo' value must be >= 1.","seqNo");
            }

            m_SeqNo = seqNo;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP EXPUNGE response as defined in RFC 3501 section 7.4.1.
        /// </summary>
        /// <param name="response">
        /// The raw IMAP server response string. Expected format:
        /// <c>* &lt;sequenceNumber&gt; EXPUNGE</c>.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_r_u_Expunge"/> instance containing the parsed
        /// message sequence number of the expunged message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is null.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not conform to the EXPUNGE syntax:
        /// <list type="bullet">
        /// <item><description>Missing or invalid untagged marker "*".</description></item>
        /// <item><description>Missing or non-numeric message sequence number.</description></item>
        /// <item><description>Missing or incorrect "EXPUNGE" keyword.</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// The EXPUNGE response reports that a specific message sequence number
        /// has been permanently removed from the mailbox. After an EXPUNGE, all
        /// higher-numbered messages are immediately decremented by one, and this
        /// renumbering is reflected in subsequent server responses.
        ///
        /// An EXPUNGE response MUST NOT be sent when no command is in progress,
        /// nor while responding to a FETCH, STORE, or SEARCH command. It MAY be
        /// sent during UID FETCH, UID STORE, or UID SEARCH.
        ///
        /// Clients MUST record updates from the EXPUNGE response.
        /// </remarks>
        public static IMAP_r_u_Expunge Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException("response");
            }

            /* RFC 3501 7.4.1. 7.4.1. EXPUNGE Response.
                Contents:   none

                The EXPUNGE response reports that the specified message sequence
                number has been permanently removed from the mailbox.  The message
                sequence number for each successive message in the mailbox is
                immediately decremented by 1, and this decrement is reflected in
                message sequence numbers in subsequent responses (including other
                untagged EXPUNGE responses).

                The EXPUNGE response also decrements the number of messages in the
                mailbox; it is not necessary to send an EXISTS response with the
                new value.
    
                As a result of the immediate decrement rule, message sequence
                numbers that appear in a set of successive EXPUNGE responses
                depend upon whether the messages are removed starting from lower
                numbers to higher numbers, or from higher numbers to lower
                numbers.  For example, if the last 5 messages in a 9-message
                mailbox are expunged, a "lower to higher" server will send five
                untagged EXPUNGE responses for message sequence number 5, whereas
                a "higher to lower server" will send successive untagged EXPUNGE
                responses for message sequence numbers 9, 8, 7, 6, and 5.

                An EXPUNGE response MUST NOT be sent when no command is in
                progress, nor while responding to a FETCH, STORE, or SEARCH
                command.  This rule is necessary to prevent a loss of
                synchronization of message sequence numbers between client and
                server.  A command is not "in progress" until the complete command
                has been received; in particular, a command is not "in progress"
                during the negotiation of command continuation.

                    Note: UID FETCH, UID STORE, and UID SEARCH are different
                    commands from FETCH, STORE, and SEARCH.  An EXPUNGE
                    response MAY be sent during a UID command.

                The update from the EXPUNGE response MUST be recorded by the
                client.

                Example:    S: * 44 EXPUNGE
            */

            StringReader r = new StringReader(response);
            
            // "*"
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP EXPUNGE response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP EXPUNGE response (expected '*'): {response}");
            }

            // Messages count
            string? word = r.ReadWord();
            if(word == null || !int.TryParse(word,out int seqNo)){
                throw new ParseException($"Invalid IMAP EXPUNGE response (invalid message sequence number): {response}");
            }

            // "EXPUNGE"
            word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP EXPUNGE response (missing EXPUNGE): {response}");
            }            
            if(!string.Equals(word,"EXPUNGE",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP EXPUNGE response (expected 'EXPUNGE'): {response}");
            }

                                               
            return new IMAP_r_u_Expunge(seqNo);
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Converts this EXPUNGE response to its IMAP wire‑format representation.
        /// </summary>
        /// <returns>
        /// A string formatted according to RFC 3501 section 7.4.1, representing an
        /// untagged EXPUNGE response. The format is:
        /// <c>* &lt;sequenceNumber&gt; EXPUNGE\r\n</c>
        /// </returns>
        /// <remarks>
        /// The EXPUNGE response reports that the message with the specified
        /// sequence number has been permanently removed from the mailbox. After
        /// an EXPUNGE, all higher‑numbered messages are immediately decremented
        /// by one, and this renumbering is reflected in subsequent server
        /// responses.
        ///
        /// This method produces the exact string an IMAP server would send to
        /// indicate the sequence number of the expunged message.
        /// </remarks>
        public override string ToString()
        {
            // Example:    S: * 44 EXPUNGE

            return "* " + m_SeqNo.ToString() + " EXPUNGE\r\n";
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the message sequence number of the message that was permanently
        /// removed from the mailbox.
        /// </summary>
        /// <remarks>
        /// This value is taken directly from the IMAP EXPUNGE response
        /// (RFC 3501, section 7.4.1). When a message is expunged, all messages
        /// with higher sequence numbers are immediately decremented by one, and
        /// this renumbering is reflected in subsequent server responses.
        ///
        /// The sequence number identifies the position of the expunged message
        /// at the moment the EXPUNGE response was issued.
        /// </remarks>
        public int SeqNo
        {
            get{ return m_SeqNo; }
        }

        #endregion
    }
}
