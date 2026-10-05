using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// Represents a parsed RCPT TO envelope recipient and any associated
    /// DSN (Delivery Status Notification) parameters as defined in RFC 3461.
    /// </summary>
    /// <remarks>
    /// This type contains the forward‑path mailbox specified in the RCPT TO
    /// command, the DSN NOTIFY settings that determine when delivery status
    /// notifications should be generated, and the optional ORCPT value that
    /// identifies the original recipient. These values correspond directly
    /// to the parameters supplied by the SMTP client during the RCPT TO
    /// stage of an SMTP transaction.
    /// </remarks>
    public class SMTP_t_RcptTo
    {
        private string            m_Mailbox = "";
        private SMTP_t_DSN_Notify m_Notify  = SMTP_t_DSN_Notify.NotSpecified;
        private string?           m_ORCPT   = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_t_RcptTo"/> class,
        /// representing the parsed RCPT TO envelope recipient and any DSN
        /// (Delivery Status Notification) parameters supplied with the command.
        /// </summary>
        /// <param name="mailbox">
        /// The forward‑path mailbox extracted from the RCPT TO command. This
        /// value identifies the envelope recipient and must not be null.
        /// </param>
        /// <param name="notify">
        /// The DSN NOTIFY value associated with the recipient, as defined in
        /// RFC 3461. This flag set indicates under which conditions a delivery
        /// status notification should be generated:
        /// <list type="bullet">
        /// <item><description><c>Success</c> — notify on successful delivery.</description></item>
        /// <item><description><c>Failure</c> — notify on delivery failure.</description></item>
        /// <item><description><c>Delay</c> — notify on delayed delivery.</description></item>
        /// <item><description><c>Never</c> — no DSN should be generated under any
        /// circumstances. This value must not be combined with any other
        /// NOTIFY flags.</description></item>
        /// <item><description><c>NotSpecified</c> — the NOTIFY parameter was not
        /// supplied in the RCPT TO command. When absent, servers may interpret
        /// this as either <c>FAILURE</c> or <c>FAILURE,DELAY</c> for compatibility
        /// with clients that do not implement DSN extensions.</description></item>
        /// </list>
        /// </param>
        /// <param name="orcpt">
        /// The optional ORCPT (original recipient) value defined in RFC 3461.
        /// When present, it specifies the original recipient address in the form
        /// <c>type;address</c> (for example: <c>rfc822;alias@example.com</c>).
        /// May be null if the ORCPT parameter was not supplied.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="mailbox"/> is null.
        /// </exception>
        public SMTP_t_RcptTo(string mailbox,SMTP_t_DSN_Notify notify,string? orcpt)
        {
            if(mailbox == null){
                throw new ArgumentNullException("mailbox");
            }

            m_Mailbox = mailbox;
            m_Notify  = notify;
            m_ORCPT   = orcpt;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the forward‑path mailbox specified in the RCPT TO command.
        /// This value represents the envelope recipient as defined in RFC 5321.
        /// </summary>
        public string Mailbox
        {
            get{ return m_Mailbox; }
        }

        /// <summary>
        /// Gets the DSN NOTIFY value associated with this recipient, as defined
        /// in RFC 3461. The NOTIFY setting determines under which conditions a
        /// delivery status notification should be generated.
        /// </summary>
        /// <remarks>
        /// The NOTIFY parameter may specify one or more of the following values:
        /// <c>Success</c>, <c>Failure</c>, <c>Delay</c>. These may be combined
        /// unless <c>Never</c> is used. When <c>Never</c> is specified, it must
        /// not be combined with any other flags and indicates that no DSN should
        /// be generated under any circumstances.
        ///
        /// If the NOTIFY parameter is not present in the RCPT TO command, the
        /// value is <c>NotSpecified</c>. In this case, servers may interpret the
        /// absence of NOTIFY as either <c>Failure</c> or <c>Failure,Delay</c>
        /// for compatibility with clients that do not implement DSN extensions.
        /// </remarks>
        public SMTP_t_DSN_Notify Notify
        {
            get{ return m_Notify; }
        }

        /// <summary>
        /// Gets the optional ORCPT (original recipient) value supplied with the
        /// RCPT TO command, as defined in RFC 3461.
        /// </summary>
        /// <remarks>
        /// The ORCPT parameter specifies the original recipient address in the
        /// form <c>type;address</c> (for example: <c>rfc822;alias@example.com</c>).
        /// This value is present only when the client includes an ORCPT argument
        /// in the RCPT TO command; otherwise the property is <c>null</c>.
        /// </remarks>
        public string? ORCPT
        {
            get{ return m_ORCPT; }
        }

        #endregion
    }
}
