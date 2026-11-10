using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using LumiSoft.Net.MIME;

namespace LumiSoft.Net.Mail
{
    /// <summary>
    /// Represents a <c>Received</c> header field.
    /// </summary>
    /// <remarks>
    /// Defined by RFC 5321, section 4.4. A received field records the hosts,
    /// protocols, and timestamp associated with an SMTP hop.
    ///
    /// <code>
    /// RFC 5321 4.4.
    ///     Time-stamp-line = "Received:" FWS Stamp CRLF
    ///     
    ///     Stamp           = From-domain By-domain Opt-info [CFWS] ";" FWS date-time
    ///                     ; where "date-time" is as defined in RFC 5322 [4]
    ///                     ; but the "obs-" forms, especially two-digit
    ///                     ; years, are prohibited in SMTP and MUST NOT be used.
    ///                     
    ///     From-domain     = "FROM" FWS Extended-Domain
    ///
    ///     By-domain       = CFWS "BY" FWS Extended-Domain
    ///
    ///     Extended-Domain = Domain / ( Domain FWS "(" TCP-info ")" ) / ( address-literal FWS "(" TCP-info ")" )
    ///
    ///     TCP-info        = address-literal / ( Domain FWS address-literal )
    ///                     ; Information derived by server from TCP connection not client EHLO.
    ///
    ///     Opt-info        = [Via] [With] [ID] [For] [Additional-Registered-Clauses]
    ///
    ///     Via             = CFWS "VIA" FWS Link
    ///
    ///     With            = CFWS "WITH" FWS Protocol
    ///
    ///     ID              = CFWS "ID" FWS ( Atom / msg-id )
    ///                     ; msg-id is defined in RFC 5322 [4]
    ///
    ///     For            = CFWS "FOR" FWS ( Path / Mailbox )
    ///     
    ///     Additional-Registered-Clauses  = CFWS Atom FWS String
    ///
    ///     Link           = "TCP" / Addtl-Link
    ///
    ///     Addtl-Link     = Atom
    ///     
    ///     Protocol       = "ESMTP" / "SMTP" / Attdl-Protocol
    ///     
    ///     Mailbox        = Local-part "@" ( Domain / address-literal )
    /// </code>
    /// </remarks>
    public class Mail_h_Received : MIME_h
    {
        private bool            m_IsModified    = false;
        private string?         m_ParseValue    = null;
        private string          m_From          = "";
        private Mail_t_TcpInfo? m_pFrom_TcpInfo = null;
        private string          m_By            = "";
        private Mail_t_TcpInfo? m_pBy_TcpInfo   = null;
        private string?         m_Via           = null;
        private string?         m_With          = null;
        private string?         m_ID            = null;
        private string?         m_For           = null;
        private DateTime        m_Time;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mail_h_Received"/> class.
        /// </summary>
        /// <param name="from">The host from which the message was received.</param>
        /// <param name="by">The host that received the message.</param>
        /// <param name="time">The date and time when the message was received.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="from"/> or <paramref name="by"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="from"/> or <paramref name="by"/> is empty.</exception>
        public Mail_h_Received(string from,string by,DateTime time)
        {
            if(from == null){
                throw new ArgumentNullException(nameof(from));
            }
            if(from == string.Empty){
                throw new ArgumentException("The source host cannot be empty.",nameof(from));
            }
            if(by == null){
                throw new ArgumentNullException(nameof(by));
            }
            if(by == string.Empty){
                throw new ArgumentException("The receiving host cannot be empty.",nameof(by));
            }

            m_From = from;
            m_By   = by;
            m_Time = time;
        }


        #region static method Parse

        /// <summary>
        /// Parses a <c>Received</c> header field from its serialized representation.
        /// </summary>
        /// <param name="value">The serialized header field, including its name; for example, <c>Received: from host.example by relay.example; Tue, 1 Jan 2024 12:00:00 +0000</c>.</param>
        /// <returns>A parsed <see cref="Mail_h_Received"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ParseException">Thrown when the <c>For</c> clause contains an invalid mailbox.</exception>
        public static Mail_h_Received Parse(string value)
        {
            if(value == null){
                throw new ArgumentNullException(nameof(value));
            }

            string[] name_value = value.Split(new char[]{':'},2);
            if(name_value.Length != 2){
                throw new ParseException("The Received header field must contain a field name followed by a colon.");
            }

            Mail_h_Received retVal = new Mail_h_Received("a","b",DateTime.MinValue);

            MIME_Reader r = new MIME_Reader(name_value[1]);

            while(true){
                string? word = r.Word();
                // We processed all data.
                if(word == null){
                    break;
                }
                // We have comment, just eat it.
                else if(r.StartsWith("(")){
                    r.ReadParenthesized();
                }
                // We have date-time or unknown-data.
                else if(r.StartsWith(";")){
                    // Eat ';'
                    r.Char(false);

                    try{
                        retVal.m_Time = MIME_Utils.ParseRfc2822DateTime(r.QuotedReadToDelimiter(new char[]{';'}) ?? "");
                    }
                    catch{
                        // We have some unknown data, skip it.
                    }
                }
                else{
                    // We have some unexpected char like: .,= ... . Just eat it.
                    if(word == null){
                        r.Char(true);
                        continue;
                    }

                    word = word.ToUpperInvariant();

                    if(word == "FROM"){
                        retVal.m_From = r.DotAtom() ?? "";

                        r.ToFirstChar();
                        if(r.StartsWith("(")){
                            string[] parts = r.ReadParenthesized().Split(' ');
                            if(parts.Length == 1){
                                if(Net_Utils.IsIPAddress(parts[0])){
                                    retVal.m_pFrom_TcpInfo = new Mail_t_TcpInfo(IPAddress.Parse(parts[0]),null);
                                }
                            }
                            else if(parts.Length == 2){
                                if(Net_Utils.IsIPAddress(parts[1])){
                                    retVal.m_pFrom_TcpInfo = new Mail_t_TcpInfo(IPAddress.Parse(parts[1]),parts[0]);
                                }
                            }
                        }
                    }
                    else if(word == "BY"){
                        retVal.m_By = r.DotAtom() ?? "";

                        r.ToFirstChar();
                        if(r.StartsWith("(")){
                            string[] parts = r.ReadParenthesized().Split(' ');
                            if(parts.Length == 1){
                                if(Net_Utils.IsIPAddress(parts[0])){
                                    retVal.m_pBy_TcpInfo = new Mail_t_TcpInfo(IPAddress.Parse(parts[0]),null);
                                }
                            }
                            else if(parts.Length == 2){
                                if(Net_Utils.IsIPAddress(parts[1])){
                                    retVal.m_pBy_TcpInfo = new Mail_t_TcpInfo(IPAddress.Parse(parts[1]),parts[0]);
                                }
                            }
                        }
                    }
                    else if(word == "VIA"){
                        retVal.m_Via = r.Word();
                    }
                    else if(word == "WITH"){
                        retVal.m_With = r.Word();
                    }
                    else if(word == "ID"){
                        // msg-id = [CFWS] "<" id-left "@" id-right ">" [CFWS]

                        if(r.StartsWith("<")){
                            retVal.m_ID = r.ReadParenthesized();
                        }
                        else{
                            retVal.m_ID = r.Atom();
                        }
                    }
                    else if(word == "FOR"){
                        r.ToFirstChar();

                        // path / angle-address
                        if(r.StartsWith("<")){
                            retVal.m_For = r.ReadParenthesized();
                        }
                        else{
                            string? mailbox = Mail_Utils.SMTP_Mailbox(r);
                            if(mailbox == null){
                                throw new ParseException("The Received header field contains an invalid For mailbox.");
                            }
                            retVal.m_For = mailbox;  
                        }
                    }
                    // Unknown, just eat value.
                    else{
                         r.Word();
                    }
                }
            }

            retVal.m_ParseValue = value;

            return retVal;
        }

        #endregion


        #region override method ToString

        /// <summary>
        /// Returns the serialized <c>Received</c> header field.
        /// </summary>
        /// <param name="utf8">Specifies whether regenerated header values may contain UTF-8 characters as defined by RFC 6532.</param>
        /// <param name="reEncode">Specifies whether the header field is regenerated. If <see langword="false"/> and the parsed value is unchanged, its original representation may be returned.</param>
        /// <returns>The serialized header field.</returns>
        public override string ToString(bool utf8,bool reEncode)
        {
            if(m_ParseValue == null || reEncode || this.IsModified){
                StringBuilder retVal = new StringBuilder();

                retVal.Append("Received: ");
                retVal.Append("from " + m_From);
                if(m_pFrom_TcpInfo != null){
                    retVal.Append(" (" + m_pFrom_TcpInfo.ToString() + ")");
                }
                retVal.Append(" by " + m_By);
                if(m_pBy_TcpInfo != null){
                    retVal.Append(" (" + m_pBy_TcpInfo.ToString() + ")");
                }
                if(!string.IsNullOrEmpty(m_Via)){
                    retVal.Append(" via " + m_Via);
                }
                if(!string.IsNullOrEmpty(m_With)){
                    retVal.Append(" with " + m_With);
                }
                if(!string.IsNullOrEmpty(m_ID)){
                    retVal.Append(" id " + m_ID);
                }
                if(!string.IsNullOrEmpty(m_For)){
                    retVal.Append(" for " + m_For);
                }
                retVal.Append("; " + MIME_Utils.DateTimeToRfc2822(m_Time));
                retVal.Append("\r\n");

                return retVal.ToString();
            }
            else{
                return m_ParseValue;
            }
            
        }

        #endregion
                       

        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this header field has been modified
        /// since it was parsed.
        /// </summary>
        public override bool IsModified
        {
            get{ return m_IsModified; }
        }

        /// <summary>
        /// Gets the header field name, which is always <c>Received</c>.
        /// </summary>
        public override string Name
        {
            get { return "Received"; }
        }

        /// <summary>
        /// Gets or sets the host from which the message was received.
        /// </summary>
        /// <remarks>Normally this is the host name reported by the client in EHLO or HELO.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when the assigned value is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the assigned value is empty.</exception>
        public string From
        {
            get{ return m_From; }

            set{
                if(value == null){
                    throw new ArgumentNullException(nameof(From));
                }
                if(value == string.Empty){
                    throw new ArgumentException("The source host cannot be empty.",nameof(From));
                }

                m_From = value;
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets the TCP information reported for the receiving connection.
        /// </summary>
        /// <remarks>A <see langword="null"/> value means that this information is not specified. The value is based on server connection information, not client-reported EHLO or HELO information.
        /// </remarks>
        public Mail_t_TcpInfo? From_TcpInfo
        {
            get{ return m_pFrom_TcpInfo; }

            set{ 
                m_pFrom_TcpInfo = value; 
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets the host that received the message.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the assigned value is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the assigned value is empty.</exception>
        public string By
        {
            get{ return m_By; }

            set{
                if(value == null){
                    throw new ArgumentNullException(nameof(By));
                }
                if(value == string.Empty){
                    throw new ArgumentException("The receiving host cannot be empty.",nameof(By));
                }

                m_By = value;
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets the TCP information reported for the receiving host.
        /// </summary>
        /// <remarks>A <see langword="null"/> value means that this information is not specified.</remarks>
        public Mail_t_TcpInfo? By_TcpInfo
        {
            get{ return m_pBy_TcpInfo; }

            set{ 
                m_pBy_TcpInfo = value;
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets the non-Internet transport used for delivery.
        /// </summary>
        /// <remarks>A <see langword="null"/> value means that this information is not specified.</remarks>
        public string? Via
        {
            get{ return m_Via; }

            set{ 
                m_Via = value;
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets the protocol used to receive the message.
        /// </summary>
        /// <remarks>A <see langword="null"/> value means that this information is not specified.</remarks>
        public string? With
        {
            get{ return m_With; }

            set{ 
                m_With = value; 
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets the receiving server's message identifier.
        /// </summary>
        /// <remarks>A <see langword="null"/> value means that this information is not specified.</remarks>
        public string? ID
        {
            get{ return m_ID; }

            set{ 
                m_ID = value; 
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets mailbox for who message was received. Value null means not specified.
        /// </summary>
        public string? For
        {
            get{ return m_For; }

            set{ 
                m_For = value; 
                m_IsModified = true;
            }
        }

        /// <summary>
        /// Gets or sets the date and time when the message was received.
        /// </summary>
        public DateTime Time
        {
            get{ return m_Time; }

            set{ 
                m_Time = value;                 
                m_IsModified = true;
            }
        }

        #endregion
    }
}
