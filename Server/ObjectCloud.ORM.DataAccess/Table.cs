// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.ORM.DataAccess
{
    public abstract class Table<T_Writable, T_Readable, T_Inserter> : ITable<T_Writable, T_Readable>
        where T_Inserter : T_Writable, new()
    {
        public void Insert(DataAccessDelegate<T_Writable> writeDelegate)
        {
            T_Inserter inserter = new T_Inserter();
            writeDelegate(inserter);

            DoInsert(inserter);
        }

        protected abstract void DoInsert(T_Inserter inserter);

        public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<T_Writable> writeDelegate)
        {
            T_Inserter inserter = new T_Inserter();
            writeDelegate(inserter);

            return DoInsertAndReturnPrimaryKey<TKey>(inserter);
        }

        protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(T_Inserter inserter);

        public abstract IEnumerable<T_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);

        public IEnumerable<T_Readable> Select(ComparisonCondition condition)
        {
            return Select(condition, null, default(OrderBy), null);
        }

        public IEnumerable<T_Readable> Select()
        {
            return Select(null, null, default(OrderBy), null);
        }

        public T_Readable SelectSingle(ComparisonCondition condition)
        {
            IEnumerator<T_Readable> results = Select(condition).GetEnumerator();

            if (!results.MoveNext())
                return default(T_Readable);

            T_Readable result = results.Current;

            if (results.MoveNext())
                throw new QueryException("More then one object returned");

            return result;
        }

        public abstract int Delete(ComparisonCondition condition);

        public int Delete()
        {
            return Delete(null);
        }


        public int Update(DataAccessDelegate<T_Writable> writeDelegate)
        {
            return Update(null, writeDelegate);
        }

        public int Update(ComparisonCondition condition, DataAccessDelegate<T_Writable> writeDelegate)
        {
            T_Inserter inserter = new T_Inserter();
            writeDelegate(inserter);

            return DoUpdate(condition, inserter);
        }

        protected abstract int DoUpdate(ComparisonCondition condition, T_Inserter inserter);

        public void Upsert(ComparisonCondition condition, DataAccessDelegate<T_Writable> writeDelegate)
        {
            throw new NotImplementedException();
        }
    }
}
