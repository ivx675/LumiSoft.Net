using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.IMAP
{
    /// <summary>
    /// Represents a single Access Control List (ACL) entry for a mailbox.
    /// </summary>
    /// <remarks>
    /// An ACL entry associates an identifier with a set of rights as defined in RFC 4314.
    /// The identifier specifies the subject (user, group, or special identifier), and the
    /// rights string contains one or more right characters indicating the permissions
    /// granted to that subject. The entry is expected to be fully parsed and validated
    /// before being constructed.
    /// </remarks>
    public class IMAP_t_Acl_Entry
    {
        private string m_Identifier = "";
        private string m_Rights     = "";

        /// <summary>
        /// Represents a single Access Control List (ACL) entry consisting of an
        /// identifier and the rights granted to that identifier.
        /// </summary>
        /// <param name="identifier">
        /// The ACL identifier. Must be a non-empty string. The identifier is expected
        /// to be fully parsed and resolved before being passed to this constructor.
        /// </param>
        /// <param name="rights">
        /// The rights granted to the identifier. According to RFC 4314, the rights
        /// string must contain one or more right characters (for example: l, r, w,
        /// s, t, p, e, i, a, x). Empty rights are not valid in ACL responses.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="identifier"/> or <paramref name="rights"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="identifier"/> or <paramref name="rights"/> is an empty string.
        /// </exception>
        public IMAP_t_Acl_Entry(string identifier,string rights)
        {
            if(identifier == null){
                throw new ArgumentNullException(nameof(identifier));
            }
            if(identifier == string.Empty){
                throw new ArgumentException("Argument 'identifier' value must be specified.",nameof(identifier));
            }
            if(rights == null){
                throw new ArgumentNullException(nameof(rights));
            }
            if(rights == string.Empty){
                throw new ArgumentException("Argument 'rights' value must be specified.",nameof(rights));
            }

            m_Identifier = identifier;
            m_Rights     = rights;
        }


        #region Properties implementation

        /// <summary>
        /// Gets the ACL identifier associated with this entry.
        /// </summary>
        /// <remarks>
        /// The identifier uniquely specifies the subject (user, group, or special
        /// identifier) to which the rights in this ACL entry apply. The value is
        /// expected to be fully parsed and validated before being assigned.
        /// </remarks>
        public string Identifier
        {
            get{ return m_Identifier; }
        }

        /// <summary>
        /// Gets the rights granted to the identifier in this ACL entry.
        /// </summary>
        /// <remarks>
        /// The rights string contains one or more IMAP ACL right characters as
        /// defined in RFC 4314 (for example: l, r, w, s, t, p, e, i, a, x).  
        /// Empty rights are not valid in ACL responses; identifiers with no
        /// rights are omitted entirely from the ACL response.
        /// </remarks>
        public string Rights
        {
            get{ return m_Rights; }
        }

        /// <summary>
        /// Gets a value indicating whether the identifier has the Lookup right ('l').
        /// </summary>
        /// <remarks>
        /// The Lookup right allows the user to discover the mailbox's existence.
        /// </remarks>
        public bool CanLookup => m_Rights.Contains('l');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Read right ('r').
        /// </summary>
        /// <remarks>
        /// The Read right allows the user to read message contents.
        /// </remarks>
        public bool CanRead => m_Rights.Contains('r');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Keep Seen/Unseen right ('s').
        /// </summary>
        /// <remarks>
        /// The Seen right allows the user to set or clear the \Seen flag.
        /// </remarks>
        public bool CanSeen => m_Rights.Contains('s');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Write Flags right ('w').
        /// </summary>
        /// <remarks>
        /// The Write Flags right allows the user to modify message flags other than \Seen.
        /// </remarks>
        public bool CanWriteFlags => m_Rights.Contains('w');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Insert right ('i').
        /// </summary>
        /// <remarks>
        /// The Insert right allows the user to add messages to the mailbox.
        /// </remarks>
        public bool CanInsert => m_Rights.Contains('i');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Delete Messages right ('t').
        /// </summary>
        /// <remarks>
        /// The Delete Messages right allows the user to mark messages as \Deleted.
        /// </remarks>
        public bool CanDeleteMessages => m_Rights.Contains('t');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Expunge right ('e').
        /// </summary>
        /// <remarks>
        /// The Expunge right allows the user to permanently remove messages marked as \Deleted.
        /// </remarks>
        public bool CanExpunge => m_Rights.Contains('e');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Admin right ('a').
        /// </summary>
        /// <remarks>
        /// The Admin right allows the user to modify ACLs for the mailbox.
        /// </remarks>
        public bool CanAdmin => m_Rights.Contains('a');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Create Mailbox right ('c').
        /// </summary>
        /// <remarks>
        /// The Create right allows the user to create child mailboxes.
        /// </remarks>
        public bool CanCreate => m_Rights.Contains('c');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Delete Mailbox right ('d').
        /// </summary>
        /// <remarks>
        /// The Delete Mailbox right allows the user to delete the mailbox.
        /// </remarks>
        public bool CanDeleteMailbox => m_Rights.Contains('d');

        /// <summary>
        /// Gets a value indicating whether the identifier has the Post right ('p').
        /// </summary>
        /// <remarks>
        /// The Post right allows the user to send messages to the mailbox's submission address.
        /// </remarks>
        public bool CanPost => m_Rights.Contains('p');

        #endregion
    }
}
