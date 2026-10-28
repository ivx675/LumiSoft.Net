using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents the IMAP APPENDUID optional response code defined in
    /// RFC 4315.
    /// </summary>
    public class IMAP_t_orc_AppendUid : IMAP_t_orc
    {
        private long m_UidValidity = 0;
        private int  m_MessageUid  = 0;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="folderUidValidity">
        /// The UIDVALIDITY value of the destination mailbox.
        /// </param>
        /// <param name="messageUid">
        /// The UID assigned to the appended message.
        /// </param>
        public IMAP_t_orc_AppendUid(long folderUidValidity,int messageUid)
        {
            m_UidValidity = folderUidValidity;
            m_MessageUid  = messageUid;
        }


        #region static method Parse

        /// <summary>
        /// Parses APPENDUID optional response from reader.
        /// </summary>
        /// <param name="r">APPENDUID optional response reader.</param>
        /// <returns>Returns APPENDUID optional response.</returns>
        /// <exception cref="ArgumentNullException">Is raised when <b>r</b> is null reference.</exception>
        public new static IMAP_t_orc_AppendUid Parse(StringReader r)
        {
            if(r == null){
                throw new ArgumentNullException("r");
            }

            /* RFC 4315 3.
                APPENDUID
                  Followed by the UIDVALIDITY of the destination mailbox and the UID
                  assigned to the appended message in the destination mailbox,
                  indicates that the message has been appended to the destination
                  mailbox with that UID.
            */

            string[] code_uidValidity_messageUid = r.ReadParenthesized().Split(new char[]{' '},3);
            if(!string.Equals("APPENDUID",code_uidValidity_messageUid[0],StringComparison.InvariantCultureIgnoreCase)){
                throw new ArgumentException("Invalid APPENDUID response value.","r");
            }
            if(code_uidValidity_messageUid.Length != 3){
                throw new ArgumentException("Invalid APPENDUID response value.","r");
            }

            return new IMAP_t_orc_AppendUid(Convert.ToInt64(code_uidValidity_messageUid[1]),Convert.ToInt32(code_uidValidity_messageUid[2]));
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns this as string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "APPENDUID " + m_UidValidity + " " + m_MessageUid;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the UIDVALIDITY value of the destination folder.
        /// </summary>
        public long FolderUidValidity
        {
            get{ return m_UidValidity; }
        }

        /// <summary>
        /// Gets the UID assigned to the appended message.
        /// </summary>
        public int MessageUid
        {
            get{ return m_MessageUid; }
        }

        #endregion
    }
}
