// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.ORM.DataAccess
{
    /// <summary>
    /// Represents a table
    /// </summary>
    public interface ITable<T_Writable, T_Readable>
    {
        /// <summary>
        /// Inserts a value into the table; the delegate sets the actual values in the database
        /// </summary>
        /// <param name="writeDelegate"></param>
        void Insert(DataAccessDelegate<T_Writable> writeDelegate);

        /// <summary>
        /// Inserts a value into the table; the delegate sets the actual values in the database.  Returns the automatically-generated primary key
        /// </summary>
        /// <param name="writeDelegate"></param>
        TKey InsertAndReturnPK<TKey>(DataAccessDelegate<T_Writable> writeDelegate);

        /// <summary>
        /// Selects items from the table
        /// </summary>
        /// <typeparam name="T_Select"></typeparam>
        /// <param name="condition"></param>
        /// <returns></returns>
        IEnumerable<T_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);

        /// <summary>
        /// Selects items from the table
        /// </summary>
        /// <typeparam name="T_Select"></typeparam>
        /// <param name="condition"></param>
        /// <returns></returns>
        IEnumerable<T_Readable> Select(ComparisonCondition condition);

        /// <summary>
        /// Selects items from the table
        /// </summary>
        /// <typeparam name="T_Select"></typeparam>
        /// <returns></returns>
        IEnumerable<T_Readable> Select();

        /// <summary>
        /// Selects a single item from the table
        /// </summary>
        /// <typeparam name="T_Select"></typeparam>
        /// <param name="condition"></param>
        /// <returns>The matching object, or null if no matching object exists</returns>
        /// <exception cref="MoreThenOneObjectReturned">Thrown if there is more then one object meeting the condition</exception>
        T_Readable SelectSingle(ComparisonCondition condition);

        /// <summary>
        /// Deletes items from the table
        /// </summary>
        /// <typeparam name="T_Select"></typeparam>
        /// <param name="condition"></param>
        /// <returns>Number of rows deleted</returns>
        int Delete(ComparisonCondition condition);

        /// <summary>
        /// Deletes items from the table
        /// </summary>
        /// <typeparam name="T_Select"></typeparam>
        /// <returns>Number of rows deleted</returns>
        int Delete();

        /// <summary>
        /// Updates the items in the table
        /// </summary>
        /// <param name="writeDelegate"></param>
        /// <returns></returns>
        int Update(DataAccessDelegate<T_Writable> writeDelegate);

        /// <summary>
        /// Updates the items in the table
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="writeDelegate"></param>
        /// <returns></returns>
        int Update(ComparisonCondition condition, DataAccessDelegate<T_Writable> writeDelegate);

        /// <summary>
        /// Updates the items in the table that match the condition.  If no items in the table match the condition, then inserts an item.  See restrictions on the condition
        /// </summary>
        /// <param name="condition">Must only be ==.  Multiple columns supported only with &.  No duplicate columns allowed</param>
        /// <param name="writeDelegate"></param>
        /// <returns></returns>
        void Upsert(ComparisonCondition condition, DataAccessDelegate<T_Writable> writeDelegate);
    }
}
