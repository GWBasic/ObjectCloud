using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;

namespace ObjectCloud.ORM.DataAccess.Test
{
	public class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }
	
	public partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>
	{
		TestTable_Table TestTable { get; }
	}
	
	public interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<ObjectCloud.ORM.DataAccess.Test.IDatabaseConnector, ObjectCloud.ORM.DataAccess.Test.IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IEmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.IEmbeddedDatabaseCreator { }
	
	public interface ITestTable_Readable
	{
		System.String TestColumn { get; }
	}

	public interface ITestTable_Writable : ITestTable_Readable
	{
		new System.String TestColumn { set; }
		bool TestColumn_Changed { get; }
	}

	public abstract partial class TestTable_Table : ITable<ITestTable_Writable, ITestTable_Readable>
	{
		public void Insert(DataAccessDelegate<ITestTable_Writable> writeDelegate)
		{
			TestTable_Inserter inserter = new TestTable_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(TestTable_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<ITestTable_Writable> writeDelegate)
		{
			TestTable_Inserter inserter = new TestTable_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(TestTable_Inserter inserter);
		
		public abstract IEnumerable<ITestTable_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<ITestTable_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<ITestTable_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public ITestTable_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<ITestTable_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(ITestTable_Readable);
			
			ITestTable_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<ITestTable_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<ITestTable_Writable> writeDelegate)
		{
			TestTable_Inserter inserter = new TestTable_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, TestTable_Inserter inserter);
		
		public static Column TestColumn
		{
			get
			{
				return _TestColumn;
			}
		}
		protected static Column _TestColumn;

		
		protected class TestTable_Inserter : ITestTable_Writable
		{
			public System.String TestColumn
			{
				get { return _TestColumn; }
				set
				{
					_TestColumn = value;
					_TestColumn_Changed = true;
				}
			}
			public System.String _TestColumn;
			public bool TestColumn_Changed
			{
				get { return _TestColumn_Changed; }
			}
			private bool _TestColumn_Changed = false;
			
		}
	}

	public partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }

}