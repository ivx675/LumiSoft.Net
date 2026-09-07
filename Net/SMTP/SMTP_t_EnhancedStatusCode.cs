using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.SMTP 
{
    /// <summary>
    /// Represents an SMTP Enhanced Status Code as defined in RFC 3463 and RFC 5248.
    /// Enhanced status codes provide machine‑readable diagnostic information for
    /// both successful and failed SMTP operations, using the structured format
    /// <c>class.subject.detail</c> (for example: <c>2.1.5</c>, <c>4.4.2</c>, <c>5.1.1</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enhanced status codes extend the traditional 3‑digit SMTP reply codes by
    /// providing additional semantic detail. They allow clients to distinguish
    /// between transient failures (<c>4.x.x</c>), permanent failures (<c>5.x.x</c>),
    /// and successful operations (<c>2.x.x</c>), and to identify the specific
    /// subsystem involved (addressing, mailbox, network, protocol, content, or
    /// security).
    /// </para>
    /// <para>
    /// The three components of an enhanced status code have the following meaning:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Class</b> — Indicates success (<c>2</c>), transient failure (<c>4</c>),
    /// or permanent failure (<c>5</c>).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Subject</b> — Identifies the functional area (addressing, mailbox,
    /// mail system, network, protocol, content, or security).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Detail</b> — Provides fine‑grained diagnostic information within the
    /// subject category.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// This class stores the parsed components and exposes the raw string form
    /// via <see cref="Raw"/>. Use <see cref="Parse(string)"/> to construct an
    /// instance from a server‑provided enhanced status code.
    /// </para>
    /// </remarks>
    public class SMTP_t_EnhancedStatusCode 
    {
        private int    m_Class   = 0;
        private int    m_Subject = 0;
        private int    m_Detail  = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="SMTP_t_EnhancedStatusCode"/>
        /// class using the specified enhanced status code components.
        /// </summary>
        /// <param name="errorClass">
        /// The status class component (<c>2</c> = success, <c>4</c> = transient failure,
        /// <c>5</c> = permanent failure).
        /// </param>
        /// <param name="subject">
        /// The subject component identifying the functional area (addressing, mailbox,
        /// mail system, network, protocol, content, or security).
        /// </param>
        /// <param name="detail">
        /// The detail component providing fine‑grained diagnostic information.
        /// </param>
        SMTP_t_EnhancedStatusCode(int errorClass,int subject,int detail)
        {
            m_Class = errorClass;
            m_Subject = subject;
            m_Detail = detail;
        }


        #region static method Parse

        /// <summary>
        /// Parses an SMTP enhanced status code from its string representation.
        /// </summary>
        /// <param name="value">
        /// The enhanced status code string in <c>class.subject.detail</c> format
        /// (for example: <c>"5.1.1"</c>, <c>"4.4.2"</c>, <c>"2.1.5"</c>).
        /// </param>
        /// <returns>
        /// A <see cref="SMTP_t_EnhancedStatusCode"/> instance containing the parsed
        /// components of the enhanced status code.
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when <paramref name="value"/> is not a valid enhanced status code
        /// or does not contain exactly three numeric components separated by dots.
        /// </exception>
        public static SMTP_t_EnhancedStatusCode Parse(string value)
        {
            try{
                var parts = value.Split('.');

                return new SMTP_t_EnhancedStatusCode(int.Parse(parts[0]),int.Parse(parts[1]),int.Parse(parts[2]));
            }
            catch{
                throw new FormatException($"Invalid enhanced status code: '{value}'.");
            }            
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets the status class component (<c>2</c> = success, <c>4</c> = transient failure,
        /// <c>5</c> = permanent failure).
        /// </summary>
        public int Class 
        { 
            get{ return m_Class; }
        }

        /// <summary>
        /// Gets the subject component identifying the functional area associated with
        /// the status code (addressing, mailbox, mail system, network, protocol,
        /// content, or security).
        /// </summary>
        public int Subject 
        { 
            get{ return m_Subject; }
        }

        /// <summary>
        /// Gets the detail component providing fine‑grained diagnostic information
        /// within the subject category.
        /// </summary>
        public int Detail 
        { 
            get{ return m_Detail; }
        }

        /// <summary>
        /// Gets the raw enhanced status code string in <c>class.subject.detail</c> format.
        /// </summary>
        public string Raw 
        { 
            get{ return m_Class + "." + m_Subject + "." + m_Detail; }
        }

        #endregion
    }
}
