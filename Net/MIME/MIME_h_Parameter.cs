using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME header field parameter.
    /// </summary>
    public class MIME_h_Parameter
    {
        private bool    m_IsModified = false;
        private string  m_Name       = "";
        private string? m_Value      = null;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MIME_h_Parameter"/> class.
        /// </summary>
        /// <param name="name">
        /// Parameter name.
        /// </param>
        /// <param name="value">
        /// Parameter value, or <see langword="null"/> if not specified.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="name"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Raised when <paramref name="name"/> is empty.
        /// </exception>
        public MIME_h_Parameter(string name,string? value)
        {
            if(name == null){
                throw new ArgumentNullException(nameof(name));
            }
            if(string.IsNullOrEmpty(name)) {
                throw new ArgumentException("Parameter name cannot be empty.",nameof(name));
            }

            m_Name  = name;
            m_Value = value;
        }


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this parameter has been modified
        /// since it was loaded.
        /// </summary>
        public bool IsModified
        {
            get{ return m_IsModified; }
        }

        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        public string Name
        {
            get{ return m_Name; }
        }

        /// <summary>
        /// Gets or sets the parameter value, or <see langword="null"/> if the
        /// value is not specified.
        /// </summary>
        public string? Value
        {
            get{ return m_Value; }

            set{ 
                m_Value      = value;
                m_IsModified = true;
            }
        }

        #endregion

    }
}
