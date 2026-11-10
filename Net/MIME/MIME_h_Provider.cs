using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Provides parsing and creation services for MIME header fields.
    /// </summary>
    public class MIME_h_Provider
    {
        private Type                    m_pDefaultHeaderField;
        private Dictionary<string,Type> m_pHeadrFields;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MIME_h_Provider"/> class.
        /// </summary>
        public MIME_h_Provider()
        {
            m_pDefaultHeaderField = typeof(MIME_h_Unstructured);

            m_pHeadrFields = new Dictionary<string,Type>(StringComparer.OrdinalIgnoreCase);
            m_pHeadrFields.Add("Content-Type",typeof(MIME_h_ContentType));
            m_pHeadrFields.Add("Content-Disposition",typeof(MIME_h_ContentDisposition));
        }


        #region method Parse

        /// <summary>
        /// Parses the specified header field.
        /// </summary>
        /// <param name="field">
        /// Header field in the format <c>name: value</c>.
        /// </param>
        /// <returns>
        /// Returns the parsed header field.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="field"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ParseException">
        /// Raised when the specified value is not a valid header field.
        /// </exception>
        public MIME_h Parse(string field)
        {
            if(field == null){
                throw new ArgumentNullException(nameof(field));
            }

            // <CRLF> is missing from the end, add it.
            if(!field.EndsWith("\r\n")){
                field += "\r\n";
            }

            MIME_h   headerField;
            string[] name_value  = field.Split(new char[]{':'},2);
            string   name        = name_value[0].Trim();
            if(name == string.Empty){
                throw new ParseException("Invalid header field value '" + field + "'.");
            }
            else{
                try{
                    if(m_pHeadrFields.ContainsKey(name)){ 
                        headerField = (MIME_h)m_pHeadrFields[name].GetMethod("Parse")!.Invoke(null,new object[]{field})!;
                    }
                    else{
                        headerField = (MIME_h)m_pDefaultHeaderField.GetMethod("Parse")!.Invoke(null,new object[]{field})!;
                    }
                }
                catch(Exception x){
                    headerField = new MIME_h_Unparsed(field,x);
                }
            }

            return headerField;
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets or sets the default header field type used to represent
        /// unrecognized header fields.
        /// </summary>
        /// <remarks>
        /// The specified type must derive from <see cref="MIME_h"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Raised when <see langword="null"/> is assigned.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when the specified type does not derive from
        /// <see cref="MIME_h"/>.
        /// </exception>
        public Type DefaultHeaderField
        {
            get{ return m_pDefaultHeaderField; }

            set{
                if(value == null){
                    throw new ArgumentNullException(nameof(value));
                }                
                if(!value.GetType().IsSubclassOf(typeof(MIME_h))){
                    throw new ArgumentException("Property 'DefaultHeaderField' value must be based on MIME_h class.");
                }

                m_pDefaultHeaderField = value;
            }
        }

        /// <summary>
        /// Gets the collection of registered header field parsers.
        /// </summary>
        /// <remarks>
        /// The dictionary key is the header field name and the value is the
        /// corresponding parser type.
        /// </remarks>
        public Dictionary<string,Type> HeaderFields
        {
            get{ return m_pHeadrFields; }
        }

        #endregion
    }
}
