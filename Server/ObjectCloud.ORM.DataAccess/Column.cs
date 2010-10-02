// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.ORM.DataAccess
{
    public class Column
    {
        public static Column Construct<TTable, T_Writable, T_Readable>(string name, GenericArgument<object, object> writeDelegate)
            where TTable : ITable<T_Writable, T_Readable>
        {
            Column toReturn = new Column(name, typeof(TTable));
            toReturn.WriteDelegate = writeDelegate;

            return toReturn;
        }

        private Column(string name, Type table)
        {
            _Name = name;
            _Table = table;
        }

        /// <summary>
        /// The column's database name
        /// </summary>
        public string Name
        {
            get { return _Name; }
        }
        private readonly string _Name;

        /// <summary>
        /// The type of table
        /// </summary>
        public Type Table
        {
            get { return _Table; }
        }
        private readonly Type _Table;

        public void Write(object writer, object value)
        {
            WriteDelegate(writer, value);
        }
        private GenericArgument<object, object> WriteDelegate;
		
		/// <summary>
		/// Specifies that a query includes values from this column in the given values 
		/// </summary>
		/// <param name="inContents">
		/// A <see cref="IEnumerable"/>
		/// </param>
		/// <returns>
		/// A <see cref="ComparisonCondition"/>
		/// </returns>
		public ComparisonCondition In(IEnumerable inContents)
		{
			return new ComparisonCondition(false, this, inContents);
		}
		
		/// <summary>
		/// Specifies that a query includes values from this column not in the given values 
		/// </summary>
		/// <param name="inContents">
		/// A <see cref="IEnumerable"/>
		/// </param>
		/// <returns>
		/// A <see cref="ComparisonCondition"/>
		/// </returns>
		public ComparisonCondition NotIn(IEnumerable inContents)
		{
			return new ComparisonCondition(true, this, inContents);
		}

        /// <summary>
        /// Specifies that a query includes values from this column that match the given like syntax
        /// </summary>
        /// <param name="likeComparison"></param>
        /// <returns></returns>
        public ComparisonCondition Like(string likeComparison)
        {
            return new ComparisonCondition(false, this, likeComparison);
        }

        /// <summary>
        /// Specifies that a query includes values from this column that match the given like syntax
        /// </summary>
        /// <param name="likeComparison"></param>
        /// <returns></returns>
        public ComparisonCondition NotLike(string likeComparison)
        {
            return new ComparisonCondition(true, this, likeComparison);
        }

        #region comparison operator overloads

        public static ComparisonCondition operator ==(Column lhs, object rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.Equals, rhs);
        }

        public static ComparisonCondition operator >(Column lhs, object rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.GreaterThen, rhs);
        }

        public static ComparisonCondition operator >=(Column lhs, object rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.GreaterThenEquals, rhs);
        }

        public static ComparisonCondition operator <(Column lhs, object rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.LessThen, rhs);
        }

        public static ComparisonCondition operator <=(Column lhs, object rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.LessThenEquals, rhs);
        }

        public static ComparisonCondition operator !=(Column lhs, object rhs)
        {
            ComparisonCondition toReturn = new ComparisonCondition(lhs, ComparisonOperator.Equals, rhs);
            toReturn.Not = true;
            return toReturn;
        }

        public static ComparisonCondition operator ==(object lhs, Column rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.Equals, rhs);
        }

        public static ComparisonCondition operator <(object lhs, Column rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.GreaterThen, rhs);
        }

        public static ComparisonCondition operator <=(object lhs, Column rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.GreaterThenEquals, rhs);
        }

        public static ComparisonCondition operator >(object lhs, Column rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.LessThen, rhs);
        }

        public static ComparisonCondition operator >=(object lhs, Column rhs)
        {
            return new ComparisonCondition(lhs, ComparisonOperator.LessThenEquals, rhs);
        }

        public static ComparisonCondition operator !=(object lhs, Column rhs)
        {
            ComparisonCondition toReturn = new ComparisonCondition(lhs, ComparisonOperator.Equals, rhs);
            toReturn.Not = true;
            return toReturn;
        }

        #endregion

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
