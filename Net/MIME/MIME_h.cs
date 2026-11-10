using System;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a MIME header field as defined in RFC 2045 section 3.
    /// </summary>
    /// <remarks>
    /// Derived classes provide strongly typed access to specific MIME
    /// header field values.
    /// </remarks>
    public abstract class MIME_h
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MIME_h"/> class.
        /// </summary>
        protected MIME_h()
        {
        }

                
        #region method ToString

        /// <summary>
        /// Returns the serialized header field.
        /// </summary>
        /// <returns>
        /// Returns the serialized header field.
        /// </returns>
        public override string ToString()
        {
            return ToString(true,false);
        }

        /// <summary>
        /// Returns the serialized header field, including the terminating CRLF.
        /// </summary>
        /// <param name="utf8">
        /// Specifies whether rewritten header fields may contain UTF-8 characters
        /// as defined by RFC 6532. If <see langword="false"/>, non-ASCII values
        /// are encoded using the applicable MIME encoding rules.
        /// </param>
        /// <param name="reEncode">
        /// Specifies whether the header field is regenerated. If
        /// <see langword="false"/> and the parsed value is unchanged, its original
        /// representation may be returned.
        /// </param>
        /// <returns>The serialized header field.</returns>
        public abstract string ToString(bool utf8,bool reEncode);

        #endregion

        #region method ValueToString

        /// <summary>
        /// Returns the serialized header field value.
        /// </summary>
        /// <returns>
        /// Returns the serialized header field value.
        /// </returns>
        public string ValueToString()
        {
            return ValueToString(true,false);
        }

        /// <summary>
        /// Returns the header field value as a string.
        /// </summary>
        /// <param name="utf8Headers">
        /// Specifies whether rewritten header fields are written as RFC 6532
        /// UTF-8 headers.
        /// </param>
        /// <param name="reEncodeHeaders">
        /// Specifies whether the header field is regenerated and re-encoded.
        /// </param>
        /// <returns>
        /// Returns the serialized header field value.
        /// </returns>
        public string ValueToString(bool utf8Headers,bool reEncodeHeaders)
        {
            return ToString(utf8Headers,reEncodeHeaders).Split(new char[]{':'},2)[1].TrimStart();
        }

        #endregion


        #region Properties implementation

        /// <summary>
        /// Gets a value indicating whether this header field has been modified
        /// since it was loaded.
        /// </summary>
        /// <remarks>
        /// Newly created header fields always have a value of
        /// <see langword="true"/>.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        /// Raised when this instance is disposed and this property is accessed.
        /// </exception>
        public abstract bool IsModified
        {
            get;
        }

        /// <summary>
        /// Gets the header field name.
        /// </summary>
        /// <remarks>
        /// Examples include <c>Content-Type</c>, <c>Content-Disposition</c>,
        /// and <c>Subject</c>.
        /// </remarks>
        public abstract string Name
        {
            get;
        }

        #endregion

    }
}
