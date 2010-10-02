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

        private IEnumerable<ComparisonCondition> ParseOutAndConditions(ComparisonCondition condition)
        {
            if (condition.Not)
                throw new QueryException("NOT is not supported in Upsert");

            if (condition.LikeComparison != null)
                throw new QueryException("LIKE is not supported in Upsert");

            if (null != condition.BooleanOperator)
                if (condition.BooleanOperator.Value == BooleanOperator.And)
                {
                    foreach (ComparisonCondition subCondition in ParseOutAndConditions((ComparisonCondition)condition.Lhs))
                        yield return subCondition;
                    foreach (ComparisonCondition subCondition in ParseOutAndConditions((ComparisonCondition)condition.Rhs))
                        yield return subCondition;
                }
                else
                    throw new QueryException("Only AND is supported in Upsert");
            else if (condition.ComparisonOperator != null)
                if (condition.ComparisonOperator.Value == ComparisonOperator.Equals)
                    yield return condition;
                else
                    throw new QueryException("Only == is supported in Upsert");
            else
                throw new QueryException("Condition is not supported, no more information is known");
        }

        private IEnumerable<KeyValuePair<Column, object>> GetColumnsAndValues(ComparisonCondition comparisonCondition)
        {
            foreach (ComparisonCondition condition in ParseOutAndConditions(comparisonCondition))
                if (condition.Lhs is Column && (!(condition.Rhs is Column)))
                    yield return new KeyValuePair<Column, object>((Column)condition.Lhs, condition.Rhs);
                else if (condition.Rhs is Column && (!(condition.Lhs is Column)))
                    yield return new KeyValuePair<Column, object>((Column)condition.Rhs, condition.Lhs);
                else
                    throw new QueryException("Conditions must be COLUMN == VALUE");
        }

        public void Upsert(ComparisonCondition condition, DataAccessDelegate<T_Writable> writeDelegate)
        {
            List<KeyValuePair<Column, object>> assigmentConditions = new List<KeyValuePair<Column, object>>(GetColumnsAndValues(condition));

            T_Inserter inserter = new T_Inserter();
            writeDelegate(inserter);

            if (0 == DoUpdate(condition, inserter))
            {
                // If there were no objects updated, then do an insert
                foreach (KeyValuePair<Column, object> columnAndValue in assigmentConditions)
                {
                    Column column = columnAndValue.Key;
                    object value = columnAndValue.Value;

                    column.Write(inserter, value);
                }

                DoInsert(inserter);
            }
        }
    }
}
