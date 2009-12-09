using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;

namespace ObjectCloud.DataAccess.SessionManager
{
	public class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }
	
	public partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>
	{
		Session_Table Session { get; }
	}
	
	public interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<ObjectCloud.DataAccess.SessionManager.IDatabaseConnector, ObjectCloud.DataAccess.SessionManager.IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IEmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.IEmbeddedDatabaseCreator { }
	
	public interface ISession_Readable
	{
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserID { get; }
		System.TimeSpan MaxAge { get; }
		System.DateTime WhenToDelete { get; }
		System.Boolean KeepAlive { get; }
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid> SessionID { get; }
	}

	public interface ISession_Writable : ISession_Readable
	{
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserID { set; }
		bool UserID_Changed { get; }
		new System.TimeSpan MaxAge { set; }
		bool MaxAge_Changed { get; }
		new System.DateTime WhenToDelete { set; }
		bool WhenToDelete_Changed { get; }
		new System.Boolean KeepAlive { set; }
		bool KeepAlive_Changed { get; }
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid> SessionID { set; }
		bool SessionID_Changed { get; }
	}

	public abstract partial class Session_Table : ITable<ISession_Writable, ISession_Readable>
	{
		public void Insert(DataAccessDelegate<ISession_Writable> writeDelegate)
		{
			Session_Inserter inserter = new Session_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Session_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<ISession_Writable> writeDelegate)
		{
			Session_Inserter inserter = new Session_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Session_Inserter inserter);
		
		public abstract IEnumerable<ISession_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<ISession_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<ISession_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public ISession_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<ISession_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(ISession_Readable);
			
			ISession_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<ISession_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<ISession_Writable> writeDelegate)
		{
			Session_Inserter inserter = new Session_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Session_Inserter inserter);
		
		public static Column UserID
		{
			get
			{
				return _UserID;
			}
		}
		protected static Column _UserID;

		public static Column MaxAge
		{
			get
			{
				return _MaxAge;
			}
		}
		protected static Column _MaxAge;

		public static Column WhenToDelete
		{
			get
			{
				return _WhenToDelete;
			}
		}
		protected static Column _WhenToDelete;

		public static Column KeepAlive
		{
			get
			{
				return _KeepAlive;
			}
		}
		protected static Column _KeepAlive;

		public static Column SessionID
		{
			get
			{
				return _SessionID;
			}
		}
		protected static Column _SessionID;

		
		protected class Session_Inserter : ISession_Writable
		{
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserID
			{
				get { return _UserID; }
				set
				{
					_UserID = value;
					_UserID_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _UserID;
			public bool UserID_Changed
			{
				get { return _UserID_Changed; }
			}
			private bool _UserID_Changed = false;
			
			public System.TimeSpan MaxAge
			{
				get { return _MaxAge; }
				set
				{
					_MaxAge = value;
					_MaxAge_Changed = true;
				}
			}
			public System.TimeSpan _MaxAge;
			public bool MaxAge_Changed
			{
				get { return _MaxAge_Changed; }
			}
			private bool _MaxAge_Changed = false;
			
			public System.DateTime WhenToDelete
			{
				get { return _WhenToDelete; }
				set
				{
					_WhenToDelete = value;
					_WhenToDelete_Changed = true;
				}
			}
			public System.DateTime _WhenToDelete;
			public bool WhenToDelete_Changed
			{
				get { return _WhenToDelete_Changed; }
			}
			private bool _WhenToDelete_Changed = false;
			
			public System.Boolean KeepAlive
			{
				get { return _KeepAlive; }
				set
				{
					_KeepAlive = value;
					_KeepAlive_Changed = true;
				}
			}
			public System.Boolean _KeepAlive;
			public bool KeepAlive_Changed
			{
				get { return _KeepAlive_Changed; }
			}
			private bool _KeepAlive_Changed = false;
			
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid> SessionID
			{
				get { return _SessionID; }
				set
				{
					_SessionID = value;
					_SessionID_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid> _SessionID;
			public bool SessionID_Changed
			{
				get { return _SessionID_Changed; }
			}
			private bool _SessionID_Changed = false;
			
		}
	}

	public partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }

}