using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace LumiSoft.Net.WebDav
{
    /// <summary>
    /// This class represents WebDav 'DAV:propstat' element. Defined in RFC 4918 14.22.
    /// </summary>
    public class WebDav_PropStat
    {
        private string      m_Status              = "";
        private string      m_ResponseDescription = "";
        private WebDav_Prop m_pProp;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <exception cref="ArgumentNullException">Is raised when when <b>status</b> or <b>prop</b> is null reference.</exception>
        internal WebDav_PropStat(string status,WebDav_Prop prop)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentNullException.ThrowIfNull(prop);

            m_Status = status;
            m_pProp  = prop;
        }


        #region static method Parse

        /// <summary>
        /// Parses WebDav_PropStat from 'DAV:propstat' element.
        /// </summary>
        /// <param name="propstatNode">The 'DAV:propstat' element</param>
        /// <returns>Returns DAV propstat.</returns>
        /// <exception cref="ArgumentNullException">Is raised when when <b>propstatNode</b> is null reference.</exception>
        /// <exception cref="ParseException">Is raised when there are any parsing error.</exception>
        internal static WebDav_PropStat Parse(XmlNode propstatNode)
        {
            if(propstatNode == null){
                throw new ArgumentNullException("propstatNode");
            }

            // Invalid response.
            if(!string.Equals(propstatNode.NamespaceURI + propstatNode.LocalName,"DAV:propstat",StringComparison.InvariantCultureIgnoreCase)){
                throw new ParseException("Invalid DAV:propstat value.");
            }            
            
            string?  status = null;
            XmlNode? prop   = null;
            foreach (XmlNode node in propstatNode.ChildNodes){
                if(string.Equals(node.LocalName,"status",StringComparison.InvariantCultureIgnoreCase)){
                    status = node.ChildNodes.Count > 0 ? node.ChildNodes[0]?.Value : null;
                }
                else if(string.Equals(node.LocalName,"prop",StringComparison.InvariantCultureIgnoreCase)){
                    prop = node;
                }                
            }

            if(status == null){
                throw new ParseException("DAV:propstat element 'status' value is missing.");
            }
            if(prop == null){
                throw new ParseException("DAV:propstat element 'prop' value is missing.");
            }            

            return new WebDav_PropStat(status,WebDav_Prop.Parse(prop));
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets property HTTP status.
        /// </summary>
        public string Status
        {
            get{ return m_Status; }
        }

        /// <summary>
        /// Gets human-readable status property description.
        /// </summary>
        public string ResponseDescription
        {
            get{ return m_ResponseDescription; }
        }
        
        /// <summary>
        /// Gets 'prop' element value.
        /// </summary>
        public WebDav_Prop Prop
        {
            get{ return m_pProp; }
        }
        
        #endregion
    }
}
