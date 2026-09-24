using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP <c>SEARCH</c> response as defined in
    /// RFC 3501 section 7.2.5. A SEARCH response contains the set of
    /// identifiers returned by the server for messages matching the
    /// search criteria supplied by the client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A SEARCH response is always untagged and consists of the literal
    /// <c>*</c>, followed by the atom <c>SEARCH</c>, followed by zero or
    /// more integers. No other data items appear in a SEARCH response.
    /// </para>
    /// <para>
    /// The wire‑format response for <c>SEARCH</c> and <c>UID SEARCH</c>
    /// is identical (<c>* SEARCH &lt;numbers&gt;</c>). The server does not
    /// distinguish between the two in the response. The meaning of the
    /// returned integers depends entirely on which command was issued:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     For <c>SEARCH</c>, the integers are message <em>sequence numbers</em>.
    ///   </description></item>
    ///   <item><description>
    ///     For <c>UID SEARCH</c>, the integers are <em>UIDs</em>.
    ///   </description></item>
    /// </list>
    /// <para>
    /// If no messages match the criteria, the server sends:
    /// <c>* SEARCH</c>
    /// with no numbers following it.
    /// </para>
    /// <para>
    /// This class stores the returned identifiers exactly as provided by
    /// the server. Interpretation (sequence number vs. UID) is determined
    /// by the caller based on the command used.
    /// </para>
    /// <para>
    /// This class derives from <see cref="IMAP_r_u"/>, the base type for
    /// all untagged IMAP responses.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Search : IMAP_r_u
    {
        private long[] m_pIds;

        /// <summary>
        /// Initializes a new <see cref="IMAP_r_u_Search"/> instance using the
        /// identifiers returned by the server in the IMAP <c>SEARCH</c> response.
        /// </summary>
        /// <param name="ids">
        /// The list of integers returned by the server. The meaning of these
        /// integers depends entirely on which command was issued:
        /// <list type="bullet">
        ///   <item><description>
        ///     For <c>SEARCH</c>, the integers are message <em>sequence numbers</em>.
        ///   </description></item>
        ///   <item><description>
        ///     For <c>UID SEARCH</c>, the integers are <em>UIDs</em>.
        ///   </description></item>
        /// </list>
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="ids"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The wire-format response for <c>SEARCH</c> and <c>UID SEARCH</c>
        /// is identical (<c>* SEARCH ...</c>). The server always returns a list
        /// of integers, but their interpretation differs based on the command.
        /// </para>
        /// <para>
        /// If the server returns no numbers (e.g., <c>* SEARCH</c>), an empty
        /// array should be supplied.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Search(long[] ids)
        {
            if(ids == null){
                throw new ArgumentNullException(nameof(ids));
            }

            m_pIds = ids;
        }


        #region static method Parse

        /// <summary>
        /// Parses an IMAP <c>SEARCH</c> response and returns a corresponding
        /// <see cref="IMAP_r_u_Search"/> instance.
        /// </summary>
        /// <param name="response">
        /// The raw untagged IMAP SEARCH response line, for example:
        /// <c>* SEARCH 2 4 9 11 15</c>. The value must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// A populated <see cref="IMAP_r_u_Search"/> object containing the
        /// numbers returned by the server. If the SEARCH response contains
        /// no numbers (e.g., <c>* SEARCH</c>), the returned object will contain
        /// an empty sequence list.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not conform to the IMAP SEARCH
        /// response format defined in RFC 3501 section 7.2.5.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A SEARCH response is always untagged and consists of the literal
        /// <c>*</c>, followed by the atom <c>SEARCH</c>, followed by zero or
        /// more integers. No other data items appear in a SEARCH response.
        /// </para>
        /// <para>
        /// The wire‑format response for <c>SEARCH</c> and <c>UID SEARCH</c>
        /// is identical (<c>* SEARCH ...</c>). The meaning of the returned
        /// numbers depends entirely on which command was issued:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     For <c>SEARCH</c>, the numbers are message <em>sequence numbers</em>.
        ///   </description></item>
        ///   <item><description>
        ///     For <c>UID SEARCH</c>, the numbers are <em>UIDs</em>.
        ///   </description></item>
        /// </list>
        /// <para>
        /// If no messages match the criteria, the server sends:
        /// <c>* SEARCH</c>
        /// with no numbers following it.
        /// </para>
        /// </remarks>
        public static IMAP_r_u_Search Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException("response");
            }

            /*
                RFC 3501 SEARCH response (section 7.2.5)
                ----------------------------------------
                The server returns an untagged SEARCH response containing the message
                sequence numbers of all messages that match the search criteria supplied
                by the client.

                Example:
                    A01 SEARCH UNSEEN FROM "bob@example.com"
                    * SEARCH 2 4 9 11 15

                Key points:
                - SEARCH returns *message sequence numbers*, not UIDs.
                - To obtain UIDs, the client must use "UID SEARCH".
                - The server returns the complete set of matches in ascending order.
                - If no messages match, the server sends "* SEARCH" with no numbers.
                - SEARCH responses are always untagged and contain only integers.
                - No flags, attributes, or additional data items appear in SEARCH.
                - Sequence numbers are dynamic and may change as the mailbox changes.
            */

            StringReader r = new StringReader(response);

            // "*"
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP SEARCH response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP SEARCH response (expected '*'): {response}");
            }

            // "SEARCH"
            string? word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP SEARCH response (missing SEARCH): {response}");
            }            
            if(!string.Equals(word,"SEARCH",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP SEARCH response (expected 'SEARCH'): {response}");
            }

            // seqNo(s)
            r.ReadToFirstChar();
            string[] seqItems = r.ReadToEnd()?.Split((string[]?)null,StringSplitOptions.RemoveEmptyEntries) ?? [];

            List<long> values = new List<long>();
            foreach(string value in seqItems){
                if(!string.IsNullOrEmpty(value)){
                    if(long.TryParse(value, out long seqNo)){
                        values.Add(seqNo);
                    }
                }                
            }

            return new IMAP_r_u_Search(values.ToArray());
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Converts this IMAP <c>SEARCH</c> response to its wire‑format string
        /// representation.
        /// </summary>
        /// <returns>
        /// A correctly formatted IMAP SEARCH response line ending with CRLF,
        /// for example: <c>* SEARCH 2 3 6\r\n</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The wire‑format response for <c>SEARCH</c> and <c>UID SEARCH</c>
        /// is identical. The server always sends:
        /// <c>* SEARCH &lt;numbers&gt;</c>
        /// regardless of whether the client issued <c>SEARCH</c> or
        /// <c>UID SEARCH</c>.
        /// </para>
        /// <para>
        /// The meaning of the numbers depends entirely on which command was
        /// issued:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     For <c>SEARCH</c>, the numbers are message <em>sequence numbers</em>.
        ///   </description></item>
        ///   <item><description>
        ///     For <c>UID SEARCH</c>, the numbers are <em>UIDs</em>.
        ///   </description></item>
        /// </list>
        /// <para>
        /// If the response contains no numbers, the generated string will be
        /// <c>* SEARCH\r\n</c>.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            // Example:    S: * SEARCH 2 3 6

            return $"* SEARCH {string.Join(" ", m_pIds)}\r\n";
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the list of identifiers returned by the server in the IMAP
        /// <c>SEARCH</c> response. The meaning of the identifiers depends on
        /// which command was issued:
        /// </summary>
        /// <remarks>
        /// <para>
        /// The wire-format response for <c>SEARCH</c> and <c>UID SEARCH</c>
        /// is identical (<c>* SEARCH ...</c>). The server always returns a
        /// list of integers, but their interpretation differs:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     For <c>SEARCH</c>, the integers are message <em>sequence numbers</em>.
        ///   </description></item>
        ///   <item><description>
        ///     For <c>UID SEARCH</c>, the integers are <em>UIDs</em>.
        ///   </description></item>
        /// </list>
        /// <para>
        /// If no messages match the criteria, the server returns
        /// <c>* SEARCH</c> with no numbers, and this property will be an
        /// empty array.
        /// </para>
        /// </remarks>
        public long[] Ids
        {
            get { return m_pIds; }
        }

        #endregion
    }
}
