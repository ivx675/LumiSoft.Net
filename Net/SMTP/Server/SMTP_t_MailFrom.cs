using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP.Server
{
    /// <summary>
    /// This class holds MAIL FROM: command value.
    /// </summary>
    public class SMTP_t_MailFrom
    {
        private string         m_Mailbox = "";
        private int?           m_Size    = null;
        private string?        m_Body    = null;
        private SMTP_t_DSN_Ret m_RET     = SMTP_t_DSN_Ret.NotSpecified;
        private string?        m_ENVID   = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_t_MailFrom"/> class,
        /// representing the parsed MAIL FROM envelope and its optional parameters.
        /// </summary>
        /// <param name="mailbox">
        /// The reverse-path mailbox supplied in the MAIL FROM command. May be an
        /// empty string for the null sender.
        /// </param>
        /// <param name="size">
        /// The SIZE parameter value, indicating the message size declared by the
        /// client. A value of zero means the parameter was not supplied.
        /// </param>
        /// <param name="body">
        /// The BODY parameter value (7BIT, 8BITMIME, or BINARYMIME). Modern servers
        /// typically validate this parameter but do not enforce it, except for
        /// BINARYMIME which requires CHUNKING/BDAT support.
        /// </param>
        /// <param name="ret">
        /// The DSN RET parameter (FULL or HDRS), specifying the amount of message
        /// content to return in delivery status notifications.
        /// </param>
        /// <param name="envid">
        /// The DSN ENVID parameter, providing a client-supplied envelope identifier.
        /// May be <c>null</c> if not present.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="mailbox"/> is <c>null</c>.
        /// </exception>
        public SMTP_t_MailFrom(string mailbox,int? size,string? body,SMTP_t_DSN_Ret ret,string? envid)
        {
            if(mailbox == null){
                throw new ArgumentNullException("mailbox");
            }

            m_Mailbox = mailbox;
            m_Size    = size;
            m_Body    = body;
            m_RET     = ret;
            m_ENVID   = envid;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the reverse-path mailbox supplied in the MAIL FROM command.
        /// </summary>
        /// <value>
        /// The mailbox portion of the MAIL FROM envelope. This value is the
        /// address inside the angle brackets. For the null sender,
        /// the value is an empty string ("").
        /// </value>
        public string Mailbox
        {
            get{ return m_Mailbox; }
        }

        /// <summary>
        /// Gets the SIZE parameter value supplied in the MAIL FROM command.
        /// </summary>
        /// <value>
        /// The message size declared by the client. A value of <c>null</c>
        /// indicates that the SIZE parameter was not provided.
        /// </value>
        public int? Size
        {
            get{ return m_Size; }
        }

        /// <summary>
        /// Gets the BODY parameter value supplied in the MAIL FROM command.
        /// </summary>
        /// <value>
        /// The BODY parameter as provided by the client (7BIT, 8BITMIME, or
        /// BINARYMIME). Modern SMTP servers typically validate this parameter
        /// but do not enforce 7BIT or 8BITMIME, except that BINARYMIME requires
        /// CHUNKING/BDAT support. The value is <c>null</c> if the parameter was
        /// not supplied.
        /// </value>
        public string? Body
        {
            get{ return m_Body; }
        }

        /// <summary>
        /// Gets the DSN RET parameter value supplied in the MAIL FROM command.
        /// </summary>
        /// <value>
        /// The RET parameter specifies how much of the original message should be
        /// returned in Delivery Status Notifications. Valid values are:
        /// <see cref="SMTP_t_DSN_Ret.FullMessage"/> to return the entire message,
        /// and <see cref="SMTP_t_DSN_Ret.Headers"/> to return only the message
        /// headers. If not specified, <see cref="SMTP_t_DSN_Ret.NotSpecified"/> is used.
        /// </value>
        public SMTP_t_DSN_Ret RET
        {
            get{ return m_RET; }
        }

        /// <summary>
        /// Gets the DSN ENVID parameter value supplied in the MAIL FROM command.
        /// </summary>
        /// <value>
        /// The ENVID parameter provides a client‑supplied envelope identifier used
        /// to correlate Delivery Status Notifications with the original message.
        /// The value is <c>null</c> if the parameter was not provided.
        /// </value>
        public string? ENVID
        {
            get{ return m_ENVID; }
        }

        #endregion
    }
}
