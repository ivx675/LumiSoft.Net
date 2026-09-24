using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents an untagged IMAP <c>BYE</c> response as defined in
    /// RFC 3501 section 7.1.5. A <c>BYE</c> response indicates
    /// that the server is about to close the connection and may include
    /// human‑readable text describing the reason for the disconnection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>BYE</c> response is always untagged and may occur under several
    /// conditions, including normal logout, inactivity autologout, panic
    /// shutdown, or refusal of a new connection at startup. The accompanying
    /// text may include an optional IMAP response code (for example,
    /// <c>[ALERT]</c>) followed by explanatory text.
    /// </para>
    /// </remarks>
    public class IMAP_r_u_Bye : IMAP_r_u
    {
        private string m_Text;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_r_u_Bye"/> class
        /// using the human‑readable text supplied by the IMAP server in an
        /// untagged <c>BYE</c> response.
        /// </summary>
        /// <param name="text">
        /// The human‑readable text following the <c>BYE</c> keyword. This may
        /// describe the reason for the server closing the connection, and may
        /// include an optional IMAP response code (for example, <c>[ALERT]</c>).
        /// Must not be <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="text"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The IMAP <c>BYE</c> response is defined in RFC 3501 section 7.1.5.
        /// It indicates that the server is about to close the connection, either
        /// as part of a normal logout sequence or due to conditions such as
        /// inactivity autologout, panic shutdown, or refusal of a new connection.
        /// </para>
        /// </remarks>
        public IMAP_r_u_Bye(string text)
        {
            if(text == null){
                throw new ArgumentNullException("text");
            }

            m_Text = text;
        }
        

        #region static method Parse

        /// <summary>
        /// Parses an untagged IMAP <c>BYE</c> response and returns a
        /// <see cref="IMAP_r_u_Bye"/> instance containing the server‑supplied
        /// human‑readable text.
        /// </summary>
        /// <param name="response">
        /// The raw IMAP <c>BYE</c> response line, including the leading
        /// untagged marker (e.g. <c>"* BYE Autologout; idle for too long"</c>).
        /// The value must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// An <see cref="IMAP_r_u_Bye"/> object whose <c>Text</c> property contains
        /// the remainder of the response after the <c>BYE</c> keyword. If the server
        /// provides no additional text, the property contains an empty string.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="response"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Thrown when the response does not conform to the IMAP <c>BYE</c> syntax,
        /// such as when the leading <c>"*"</c> or the <c>"BYE"</c> keyword is missing.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The IMAP <c>BYE</c> response is defined in RFC 3501 section 7.1.5.
        /// It is always untagged and indicates that the server is about to close the
        /// connection. The accompanying human‑readable text may describe the reason
        /// for the disconnection, such as a normal logout, inactivity autologout,
        /// panic shutdown, or refusal of a new connection.
        /// </para>
        /// </remarks>
        public static IMAP_r_u_Bye Parse(string response)
        {
            if(response == null){
                throw new ArgumentNullException(nameof(response));
            }

            /* RFC 3501 7.1.5. BYE Response.
               Contents:   OPTIONAL response code
                           human-readable text

                  The BYE response is always untagged, and indicates that the server
                  is about to close the connection.  The human-readable text MAY be
                  displayed to the user in a status report by the client.  The BYE
                  response is sent under one of four conditions:

                     1) as part of a normal logout sequence.  The server will close
                        the connection after sending the tagged OK response to the
                        LOGOUT command.

                     2) as a panic shutdown announcement.  The server closes the
                        connection immediately.

                     3) as an announcement of an inactivity autologout.  The server
                        closes the connection immediately.

                     4) as one of three possible greetings at connection startup,
                        indicating that the server is not willing to accept a
                        connection from this client.  The server closes the
                        connection immediately.

                  The difference between a BYE that occurs as part of a normal
                  LOGOUT sequence (the first case) and a BYE that occurs because of
                  a failure (the other three cases) is that the connection closes
                  immediately in the failure case.  In all cases the client SHOULD
                  continue to read response data from the server until the
                  connection is closed; this will ensure that any pending untagged
                  or completion responses are read and processed.

               Example:    S: * BYE Autologout; idle for too long
            */

            StringReader r = new StringReader(response);
            
            // "*"
            string? commandTag = r.ReadWord();
            if(commandTag == null){
                throw new ParseException($"Invalid IMAP BYE response (missing *): {response}");
            }
            if(commandTag != "*"){
                throw new ParseException($"Invalid IMAP BYE response (expected '*'): {response}");
            }

            // "BYE"
            string? word = r.ReadWord();
            if(word == null){
                throw new ParseException($"Invalid IMAP BYE response (missing BYE): {response}");
            }            
            if(!string.Equals(word,"BYE",StringComparison.OrdinalIgnoreCase)){
                throw new ParseException($"Invalid IMAP BYE response (expected 'BYE'): {response}");
            }

            return new IMAP_r_u_Bye(r.ReadToEnd()?.Trim() ?? "");
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the IMAP <c>BYE</c> response string representing this
        /// untagged server notification. The returned value includes the
        /// <c>* BYE</c> prefix followed by the human‑readable text supplied
        /// by the server, and ends with CRLF.
        /// </summary>
        /// <returns>
        /// A properly formatted IMAP <c>BYE</c> response line, such as
        /// <c>* BYE Autologout; idle for too long\r\n</c>. The text portion
        /// corresponds to the value of the <see cref="Text"/> property and
        /// may include an optional IMAP response code (for example,
        /// <c>[ALERT]</c>) followed by explanatory text.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The IMAP <c>BYE</c> response is defined in RFC 3501 sectio 7.1.5.
        /// It indicates that the server is about to close the connection, either
        /// as part of a normal logout sequence or due to conditions such as
        /// inactivity autologout, panic shutdown, or refusal of a new connection.
        /// </para>
        /// </remarks>
        public override string ToString()
        {
            // Example:  S: * BYE Autologout; idle for too long

            return $"* BYE {m_Text}\r\n";
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the human‑readable text associated with the IMAP <c>BYE</c>
        /// response. This text may describe the reason why the server is
        /// closing the connection, such as autologout, panic shutdown,
        /// normal logout, or refusal of a new connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value of this property contains all text following the
        /// <c>BYE</c> keyword in the server's untagged response. It may
        /// include an optional IMAP response code (for example,
        /// <c>[ALERT]</c>) followed by explanatory text.
        /// </para>
        /// <para>
        /// If the server provides no additional information beyond the
        /// <c>BYE</c> keyword, this property contains an empty string.
        /// </para>
        /// </remarks>
        public string Text
        {
            get{ return m_Text; }
        }

        #endregion
    }
}
