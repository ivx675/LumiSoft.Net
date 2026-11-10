using LumiSoft.Net.Mail;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LumiSoft.Net.MIME
{
    /// <summary>
    /// Represents a collection of child MIME entities contained in a
    /// <c>multipart/*</c> entity.
    /// </summary>
    public class MIME_EntityCollection : IEnumerable<MIME_Entity>
    {
        private bool              m_IsModified  = false;
        private List<MIME_Entity> m_pCollection;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="MIME_EntityCollection"/> class.
        /// </summary>
        internal MIME_EntityCollection()
        {
            m_pCollection = new List<MIME_Entity>();
        }


        #region method Add
                
        /// <summary>
        /// Adds the specified MIME entity to the collection.
        /// </summary>
        /// <param name="entity">
        /// MIME entity to add.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="entity"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Add(MIME_Entity entity)
        {
            if(entity == null){
                throw new ArgumentNullException(nameof(entity));
            }

            m_pCollection.Add(entity);
            m_IsModified = true;
        }

        #endregion

        #region method Insert

        /// <summary>
        /// Inserts a MIME entity into the collection at the specified index.
        /// </summary>
        /// <param name="index">
        /// Zero-based index at which to insert the MIME entity.
        /// </param>
        /// <param name="entity">
        /// MIME entity to insert.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Raised when <paramref name="index"/> is out of range.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="entity"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Insert(int index,MIME_Entity entity)
        {
            if(entity == null){
                throw new ArgumentNullException(nameof(entity));
            }

            m_pCollection.Insert(index,entity);
            m_IsModified = true;
        }

        #endregion

        #region method Remove

        /// <summary>
        /// Removes the specified MIME entity from the collection.
        /// </summary>
        /// <param name="entity">
        /// MIME entity to remove.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="entity"/> is
        /// <see langword="null"/>.
        /// </exception>
        public void Remove(MIME_Entity entity)
        {
            if(entity == null){
                throw new ArgumentNullException(nameof(entity));
            }

            m_pCollection.Remove(entity);
            m_IsModified = true;
        }

        /// <summary>
        /// Removes MIME entity at the specified index from the collection.
        /// </summary>
        /// <param name="index">The index of the MIME entity to remove.</param>
        /// <exception cref="IndexOutOfRangeException">Is raised when <b>index</b> is out of range.</exception>
        public void Remove(int index)
        {
            m_pCollection.RemoveAt(index);
            m_IsModified = true;
        }

        #endregion

        #region method Clear

        /// <summary>
        /// Removes all MIME entities from the collection.
        /// </summary>
        public void Clear()
        {
            m_pCollection.Clear();
            m_IsModified = true;
        }

        #endregion

        #region mehtod Contains

        /// <summary>
        /// Removes the specified MIME entity from the collection.
        /// </summary>
        /// <param name="entity">
        /// MIME entity to remove.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Raised when <paramref name="entity"/> is
        /// <see langword="null"/>.
        /// </exception>
        public bool Contains(MIME_Entity entity)
        {
            if(entity == null){
                throw new ArgumentNullException(nameof(entity));
            }

            return m_pCollection.Contains(entity);
        }

        #endregion


        #region method SetModified

        /// <summary>
        /// Sets IsModified property value.
        /// </summary>
        /// <param name="isModified">Modified flag.</param>
        internal void SetModified(bool isModified)
        {
            m_IsModified = isModified;
        }

        #endregion


        #region interface IEnumerator
                
        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
		{
			return m_pCollection.GetEnumerator();
		}

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<MIME_Entity> GetEnumerator()
        {
            return m_pCollection.GetEnumerator();
        }

		#endregion

        #region Properties Implementation

        /// <summary>
        /// Gets a value indicating whether this collection or any contained
        /// MIME entity has been modified.
        /// </summary>
        public bool IsModified
        {
            get{
                if(m_IsModified){
                    return true;
                }

                foreach(MIME_Entity entity in m_pCollection){
                    if(entity.IsModified){
                        return true;
                    }
                }

                return false; 
            }
        }

        /// <summary>
        /// Gets the number of MIME entities in the collection.
        /// </summary>
        public int Count
        {
            get{ return m_pCollection.Count; }
        }

        /// <summary>
        /// Gets the MIME entity at the specified index.
        /// </summary>
        /// <param name="index">
        /// Zero-based index of the MIME entity to get.
        /// </param>
        /// <returns>
        /// Returns the MIME entity at the specified index.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Raised when <paramref name="index"/> is out of range.
        /// </exception>
        public MIME_Entity this[int index]
        {
            get{ return m_pCollection[index]; }
        }

        #endregion

    }
}
