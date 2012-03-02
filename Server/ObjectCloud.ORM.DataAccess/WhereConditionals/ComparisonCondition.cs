// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.WhereConditionals
{
    public class ComparisonCondition
    {
        public ComparisonCondition(object lhs, ComparisonOperator comparisonOperator, object rhs)
        {
            this._Lhs = lhs;
            this._ComparisonOperator = comparisonOperator;
            this._Rhs = rhs;
        }

        public ComparisonCondition(object lhs, BooleanOperator booleanOperator, object rhs)
        {
            this._Lhs = lhs;
            this._BooleanOperator = booleanOperator;
            this._Rhs = rhs;
        }

        public ComparisonCondition(bool not, object lhs, IEnumerable inContents)
        {
            _not = not;
            _Lhs = lhs;
            _InContents = inContents;
        }

        public ComparisonCondition(bool not, object lhs, string likeComparison)
        {
            _not = not;
            _Lhs = lhs;
            _LikeComparison = likeComparison;
        }

        public object Lhs
        {
            get { return ConvertSpecialTypes(_Lhs); }
            set { _Lhs = value; }
        }
        private object _Lhs;

        public ComparisonOperator? ComparisonOperator
        {
            get { return _ComparisonOperator; }
            set { _ComparisonOperator = value; }
        }
        private ComparisonOperator? _ComparisonOperator = null;

        public BooleanOperator? BooleanOperator
        {
            get { return _BooleanOperator; }
            set { _BooleanOperator = value; }
        }
        private BooleanOperator? _BooleanOperator = null;

        public object Rhs
        {
            get { return ConvertSpecialTypes(_Rhs); }
            set { _Rhs = value; }
        }
        private object _Rhs;

        /// <summary>
        /// Assists in converting special known types to values more palpatable in the DB
        /// </summary>
        /// <param name="toConvert"></param>
        /// <returns></returns>
        private object ConvertSpecialTypes(object toConvert)
        {
            if (toConvert is TimeSpan)
                return ((TimeSpan)toConvert).Ticks;

            if (toConvert is DateTime)
                return ((DateTime)toConvert).Ticks;

            return toConvert;
        }

        /// <summary>
        /// Negates the operation
        /// </summary>
        public bool Not
        {
            get { return _not; }
            set { _not = value; }
        }
        private bool _not = false;
		
		/// <value>
		/// The contents of an "in" clause 
		/// </value>
        public IEnumerable InContents
		{
        	get { return _InContents; }
        	set { _InContents = value; }
        }
		private IEnumerable _InContents = null;

        public string LikeComparison
        {
            get { return _LikeComparison; }
            set { _LikeComparison = value; }
        }
        private string _LikeComparison = null;

        /// <summary>
        /// Allows foreach of all internal entities without sub-processing
        /// </summary>
        public IEnumerable Entities
        {
            get
            {
                if (_Lhs is ComparisonCondition)
                    foreach (object entity in ((ComparisonCondition)_Lhs).Entities)
                        yield return entity;
                else
                    yield return _Lhs;

                if (_Rhs is ComparisonCondition)
                    foreach (object entity in ((ComparisonCondition)_Rhs).Entities)
                        yield return entity;
                else
                    yield return _Rhs;
            }
        }


        public static ComparisonCondition operator &(ComparisonCondition lhs, ComparisonCondition rhs)
        {
            // Full namespace is due to compiler weirdness
            return new ComparisonCondition(lhs, ObjectCloud.ORM.DataAccess.WhereConditionals.BooleanOperator.And, rhs);
        }

        public static ComparisonCondition operator |(ComparisonCondition lhs, ComparisonCondition rhs)
        {
            return new ComparisonCondition(lhs, ObjectCloud.ORM.DataAccess.WhereConditionals.BooleanOperator.Or, rhs);
        }

        public static ComparisonCondition operator ^(ComparisonCondition lhs, ComparisonCondition rhs)
        {
            return new ComparisonCondition(lhs, ObjectCloud.ORM.DataAccess.WhereConditionals.BooleanOperator.Xor, rhs);
		}
		
		/// <summary>
		/// Condenses many comparison conditions into a single ComparisonCondition that is the ANDing of all of the conditions
		/// </summary>
		/// <param name="comparisonConditions">
		/// A <see cref="IEnumerable"/>
		/// </param>
		/// <returns>
		/// A <see cref="ComparisonCondition"/>
		/// </returns>
		public static ComparisonCondition Condense(IEnumerable<ComparisonCondition> comparisonConditions)
		{
			return Condense(comparisonConditions, ObjectCloud.ORM.DataAccess.WhereConditionals.BooleanOperator.And);
		}
		
		/// <summary>
		/// Condenses many comparison conditions into a single ComparisonCondition
		/// </summary>
		/// <param name="comparisonConditions">
		/// A <see cref="IEnumerable"/>
		/// </param>
		/// <returns>
		/// A <see cref="ComparisonCondition"/>
		/// </returns>
		/// <param name="booleanOperator">The operator to use between each condition</param>
		public static ComparisonCondition Condense(IEnumerable<ComparisonCondition> comparisonConditions, BooleanOperator booleanOperator)
		{
			ComparisonCondition toReturn = null;
			
			foreach (ComparisonCondition subCondition in comparisonConditions)
			{
				if (null == toReturn)
					toReturn = subCondition;
				else
					toReturn = new ComparisonCondition(toReturn, booleanOperator, subCondition);
			}
			
			if (null != toReturn)
				return toReturn;
			else
				return new ComparisonCondition(1, ObjectCloud.ORM.DataAccess.WhereConditionals.ComparisonOperator.Equals, 1);
        }
    }
}
