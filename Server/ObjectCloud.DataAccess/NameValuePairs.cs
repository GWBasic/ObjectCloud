using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;

namespace ObjectCloud.DataAccess.NameValuePairs
{
	public class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }
	
	public partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>
	{
		Pairs_Table Pairs { get; }
	}
	
	public interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<ObjectCloud.DataAccess.NameValuePairs.IDatabaseConnector, ObjectCloud.DataAccess.NameValuePairs.IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IEmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.IEmbeddedDatabaseCreator { }
	
	public interface IPairs_Readable
	{
		System.String Value { get; }
		System.String Name { get; }
	}

	public interface IPairs_Writable : IPairs_Readable
	{
		new System.String Value { set; }
		bool Value_Changed { get; }
		new System.String Name { set; }
		bool Name_Changed { get; }
	}

	public abstract partial class Pairs_Table : ITable<IPairs_Writable, IPairs_Readable>
	{
		public void Insert(DataAccessDelegate<IPairs_Writable> writeDelegate)
		{
			Pairs_Inserter inserter = new Pairs_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Pairs_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IPairs_Writable> writeDelegate)
		{
			Pairs_Inserter inserter = new Pairs_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Pairs_Inserter inserter);
		
		public abstract IEnumerable<IPairs_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IPairs_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IPairs_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IPairs_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IPairs_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IPairs_Readable);
			
			IPairs_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IPairs_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IPairs_Writable> writeDelegate)
		{
			Pairs_Inserter inserter = new Pairs_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Pairs_Inserter inserter);
		
		public static Column Value
		{
			get
			{
				return _Value;
			}
		}
		protected static Column _Value;

		public static Column Name
		{
			get
			{
				return _Name;
			}
		}
		protected static Column _Name;

		
		protected class Pairs_Inserter : IPairs_Writable
		{
			public System.String Value
			{
				get { return _Value; }
				set
				{
					_Value = value;
					_Value_Changed = true;
				}
			}
			public System.String _Value;
			public bool Value_Changed
			{
				get { return _Value_Changed; }
			}
			private bool _Value_Changed = false;
			
			public System.String Name
			{
				get { return _Name; }
				set
				{
					_Name = value;
					_Name_Changed = true;
				}
			}
			public System.String _Name;
			public bool Name_Changed
			{
				get { return _Name_Changed; }
			}
			private bool _Name_Changed = false;
			
		}
	}

	public partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }

}