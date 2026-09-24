using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the <c>BODYSTRUCTURE</c> data item returned within an IMAP
    /// <c>FETCH</c> response.
    /// </summary>
    /// <remarks>
    /// This class encapsulates a complete <c>BODYSTRUCTURE</c> element describing
    /// the MIME structure of a message. 
    /// </remarks>
    public class IMAP_t_Fetch_r_i_BodyStructure : IMAP_t_Fetch_r_i
    {
        private IMAP_t_BodyStructure m_pBodyStructure;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMAP_t_Fetch_r_i_BodyStructure"/>
        /// class using the specified <c>BODYSTRUCTURE</c> element.
        /// </summary>
        /// <param name="bodyStructure">
        /// The <c>BODYSTRUCTURE</c> element representing the MIME structure of the
        /// message. This value must not be <c>null</c>.
        /// </param>
        public IMAP_t_Fetch_r_i_BodyStructure(IMAP_t_BodyStructure bodyStructure)
        {
            ArgumentNullException.ThrowIfNull(bodyStructure);

            m_pBodyStructure = bodyStructure;
        }


        #region static method ParseAsync

        /// <summary>
        /// Parses the IMAP <c>BODYSTRUCTURE</c> data item from a <c>FETCH</c>
        /// response according to RFC 3501 section 7.4.2.
        /// </summary>
        /// <param name="imapReader">
        /// The IMAP reader positioned at the <c>BODYSTRUCTURE</c> atom.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that may be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// A new <see cref="IMAP_t_Fetch_r_i_BodyStructure"/> instance containing the
        /// parsed MIME structure of the message.
        /// </returns>
        /// <exception cref="ParseException">
        /// Thrown when the <c>BODYSTRUCTURE</c> token is missing or when the
        /// subsequent structure does not conform to RFC 3501.
        /// </exception>
        /// <remarks>
        /// This method validates the presence of the <c>BODYSTRUCTURE</c> data item
        /// and delegates the actual MIME structure parsing to
        /// <see cref="IMAP_t_BodyStructure.ParseAsync"/>. The returned object
        /// represents the complete MIME hierarchy of the message, including
        /// single‑part, multipart, and nested multipart entities.
        /// </remarks>
        internal static async Task<IMAP_t_Fetch_r_i_BodyStructure> ParseAsync(_IMAP_Reader imapReader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imapReader);

            /*  BODYSTRUCTURE Response Data Item RFC 3501 Section 7.4.2                

                Example (Multipart/Mixed):
                --------------------------
                BODYSTRUCTURE (
                    ("TEXT" "PLAIN"
                        ("CHARSET" "UTF-8")
                        NIL
                        NIL
                        "7BIT"
                        42
                        3
                        NIL
                        ("INLINE" NIL)
                        NIL
                        NIL
                    )
                    ("IMAGE" "JPEG"
                        NIL
                        NIL
                        "BASE64"
                        2048
                        NIL
                        NIL
                        ("ATTACHMENT" ("FILENAME" "photo.jpg"))
                        NIL
                        NIL
                    )
                    "MIXED"
                    ("BOUNDARY" "----XYZ")
                )
            */

            if(!string.Equals("BODYSTRUCTURE", imapReader.ReadAtom(), StringComparison.OrdinalIgnoreCase)){
                throw new ParseException("Invalid FETCH response: BODYSTRUCTURE data-item not found.");
            }

            return new IMAP_t_Fetch_r_i_BodyStructure(await IMAP_t_BodyStructure.ParseAsync(imapReader, cancellationToken));
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the IMAP <c>BODYSTRUCTURE</c> element associated with this
        /// <c>FETCH</c> response item.
        /// </summary>
        /// <remarks>
        /// The <c>BODYSTRUCTURE</c> element describes the MIME structure of the
        /// message, including single‑part entities, multipart hierarchies, and
        /// <c>message/rfc822</c> encapsulated messages. The value represents the
        /// complete BODYSTRUCTURE object supplied to this instance and is never
        /// <c>null</c>; construction of this type requires a valid BODYSTRUCTURE
        /// element.
        /// </remarks>
        public IMAP_t_BodyStructure BodyStructure
        {
            get{ return m_pBodyStructure; }
        }

        #endregion
    }
}
