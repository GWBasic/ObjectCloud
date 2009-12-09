using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;

namespace ObjectCloud.DataAccess.Log
{
	public class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }
	
	public partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>
	{
		Classes_Table Classes { get; }
		Log_Table Log { get; }
		Lifespan_Table Lifespan { get; }
	}
	
	public interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<ObjectCloud.DataAccess.Log.IDatabaseConnector, ObjectCloud.DataAccess.Log.IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IEmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.IEmbeddedDatabaseCreator { }
	
	public interface IClasses_Readable
	{
		System.String Name { get; }
		System.Int64 ClassId { get; }
	}

	public interface IClasses_Writable : IClasses_Readable
	{
		new System.String Name { set; }
		bool Name_Changed { get; }
		new System.Int64 ClassId { set; }
		bool ClassId_Changed { get; }
	}

	public abstract partial class Classes_Table : ITable<IClasses_Writable, IClasses_Readable>
	{
		public void Insert(DataAccessDelegate<IClasses_Writable> writeDelegate)
		{
			Classes_Inserter inserter = new Classes_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Classes_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IClasses_Writable> writeDelegate)
		{
			Classes_Inserter inserter = new Classes_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Classes_Inserter inserter);
		
		public abstract IEnumerable<IClasses_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IClasses_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IClasses_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IClasses_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IClasses_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IClasses_Readable);
			
			IClasses_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IClasses_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IClasses_Writable> writeDelegate)
		{
			Classes_Inserter inserter = new Classes_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Classes_Inserter inserter);
		
		public static Column Name
		{
			get
			{
				return _Name;
			}
		}
		protected static Column _Name;

		public static Column ClassId
		{
			get
			{
				return _ClassId;
			}
		}
		protected static Column _ClassId;

		
		protected class Classes_Inserter : IClasses_Writable
		{
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
			
			public System.Int64 ClassId
			{
				get { return _ClassId; }
				set
				{
					_ClassId = value;
					_ClassId_Changed = true;
				}
			}
			public System.Int64 _ClassId;
			public bool ClassId_Changed
			{
				get { return _ClassId_Changed; }
			}
			private bool _ClassId_Changed = false;
			
		}
	}
	public interface ILog_Readable
	{
		System.Int64 ClassId { get; }
		System.DateTime TimeStamp { get; }
		ObjectCloud.Interfaces.Disk.LoggingLevel Level { get; }
		System.Int32 ThreadId { get; }
		System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>> SessionId { get; }
		System.String RemoteEndPoint { get; }
		System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> UserId { get; }
		System.String Message { get; }
		System.Nullable<System.Int64> ExceptionClassId { get; }
		System.String ExceptionMessage { get; }
		System.String ExceptionStackTrace { get; }
	}

	public interface ILog_Writable : ILog_Readable
	{
		new System.Int64 ClassId { set; }
		bool ClassId_Changed { get; }
		new System.DateTime TimeStamp { set; }
		bool TimeStamp_Changed { get; }
		new ObjectCloud.Interfaces.Disk.LoggingLevel Level { set; }
		bool Level_Changed { get; }
		new System.Int32 ThreadId { set; }
		bool ThreadId_Changed { get; }
		new System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>> SessionId { set; }
		bool SessionId_Changed { get; }
		new System.String RemoteEndPoint { set; }
		bool RemoteEndPoint_Changed { get; }
		new System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> UserId { set; }
		bool UserId_Changed { get; }
		new System.String Message { set; }
		bool Message_Changed { get; }
		new System.Nullable<System.Int64> ExceptionClassId { set; }
		bool ExceptionClassId_Changed { get; }
		new System.String ExceptionMessage { set; }
		bool ExceptionMessage_Changed { get; }
		new System.String ExceptionStackTrace { set; }
		bool ExceptionStackTrace_Changed { get; }
	}

	public abstract partial class Log_Table : ITable<ILog_Writable, ILog_Readable>
	{
		public void Insert(DataAccessDelegate<ILog_Writable> writeDelegate)
		{
			Log_Inserter inserter = new Log_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Log_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<ILog_Writable> writeDelegate)
		{
			Log_Inserter inserter = new Log_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Log_Inserter inserter);
		
		public abstract IEnumerable<ILog_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<ILog_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<ILog_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public ILog_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<ILog_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(ILog_Readable);
			
			ILog_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<ILog_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<ILog_Writable> writeDelegate)
		{
			Log_Inserter inserter = new Log_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Log_Inserter inserter);
		
		public static Column ClassId
		{
			get
			{
				return _ClassId;
			}
		}
		protected static Column _ClassId;

		public static Column TimeStamp
		{
			get
			{
				return _TimeStamp;
			}
		}
		protected static Column _TimeStamp;

		public static Column Level
		{
			get
			{
				return _Level;
			}
		}
		protected static Column _Level;

		public static Column ThreadId
		{
			get
			{
				return _ThreadId;
			}
		}
		protected static Column _ThreadId;

		public static Column SessionId
		{
			get
			{
				return _SessionId;
			}
		}
		protected static Column _SessionId;

		public static Column RemoteEndPoint
		{
			get
			{
				return _RemoteEndPoint;
			}
		}
		protected static Column _RemoteEndPoint;

		public static Column UserId
		{
			get
			{
				return _UserId;
			}
		}
		protected static Column _UserId;

		public static Column Message
		{
			get
			{
				return _Message;
			}
		}
		protected static Column _Message;

		public static Column ExceptionClassId
		{
			get
			{
				return _ExceptionClassId;
			}
		}
		protected static Column _ExceptionClassId;

		public static Column ExceptionMessage
		{
			get
			{
				return _ExceptionMessage;
			}
		}
		protected static Column _ExceptionMessage;

		public static Column ExceptionStackTrace
		{
			get
			{
				return _ExceptionStackTrace;
			}
		}
		protected static Column _ExceptionStackTrace;

		
		protected class Log_Inserter : ILog_Writable
		{
			public System.Int64 ClassId
			{
				get { return _ClassId; }
				set
				{
					_ClassId = value;
					_ClassId_Changed = true;
				}
			}
			public System.Int64 _ClassId;
			public bool ClassId_Changed
			{
				get { return _ClassId_Changed; }
			}
			private bool _ClassId_Changed = false;
			
			public System.DateTime TimeStamp
			{
				get { return _TimeStamp; }
				set
				{
					_TimeStamp = value;
					_TimeStamp_Changed = true;
				}
			}
			public System.DateTime _TimeStamp;
			public bool TimeStamp_Changed
			{
				get { return _TimeStamp_Changed; }
			}
			private bool _TimeStamp_Changed = false;
			
			public ObjectCloud.Interfaces.Disk.LoggingLevel Level
			{
				get { return _Level; }
				set
				{
					_Level = value;
					_Level_Changed = true;
				}
			}
			public ObjectCloud.Interfaces.Disk.LoggingLevel _Level;
			public bool Level_Changed
			{
				get { return _Level_Changed; }
			}
			private bool _Level_Changed = false;
			
			public System.Int32 ThreadId
			{
				get { return _ThreadId; }
				set
				{
					_ThreadId = value;
					_ThreadId_Changed = true;
				}
			}
			public System.Int32 _ThreadId;
			public bool ThreadId_Changed
			{
				get { return _ThreadId_Changed; }
			}
			private bool _ThreadId_Changed = false;
			
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>> SessionId
			{
				get { return _SessionId; }
				set
				{
					_SessionId = value;
					_SessionId_Changed = true;
				}
			}
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.WebServer.ISession, System.Guid>> _SessionId;
			public bool SessionId_Changed
			{
				get { return _SessionId_Changed; }
			}
			private bool _SessionId_Changed = false;
			
			public System.String RemoteEndPoint
			{
				get { return _RemoteEndPoint; }
				set
				{
					_RemoteEndPoint = value;
					_RemoteEndPoint_Changed = true;
				}
			}
			public System.String _RemoteEndPoint;
			public bool RemoteEndPoint_Changed
			{
				get { return _RemoteEndPoint_Changed; }
			}
			private bool _RemoteEndPoint_Changed = false;
			
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> UserId
			{
				get { return _UserId; }
				set
				{
					_UserId = value;
					_UserId_Changed = true;
				}
			}
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> _UserId;
			public bool UserId_Changed
			{
				get { return _UserId_Changed; }
			}
			private bool _UserId_Changed = false;
			
			public System.String Message
			{
				get { return _Message; }
				set
				{
					_Message = value;
					_Message_Changed = true;
				}
			}
			public System.String _Message;
			public bool Message_Changed
			{
				get { return _Message_Changed; }
			}
			private bool _Message_Changed = false;
			
			public System.Nullable<System.Int64> ExceptionClassId
			{
				get { return _ExceptionClassId; }
				set
				{
					_ExceptionClassId = value;
					_ExceptionClassId_Changed = true;
				}
			}
			public System.Nullable<System.Int64> _ExceptionClassId;
			public bool ExceptionClassId_Changed
			{
				get { return _ExceptionClassId_Changed; }
			}
			private bool _ExceptionClassId_Changed = false;
			
			public System.String ExceptionMessage
			{
				get { return _ExceptionMessage; }
				set
				{
					_ExceptionMessage = value;
					_ExceptionMessage_Changed = true;
				}
			}
			public System.String _ExceptionMessage;
			public bool ExceptionMessage_Changed
			{
				get { return _ExceptionMessage_Changed; }
			}
			private bool _ExceptionMessage_Changed = false;
			
			public System.String ExceptionStackTrace
			{
				get { return _ExceptionStackTrace; }
				set
				{
					_ExceptionStackTrace = value;
					_ExceptionStackTrace_Changed = true;
				}
			}
			public System.String _ExceptionStackTrace;
			public bool ExceptionStackTrace_Changed
			{
				get { return _ExceptionStackTrace_Changed; }
			}
			private bool _ExceptionStackTrace_Changed = false;
			
		}
	}
	public interface ILifespan_Readable
	{
		System.TimeSpan Timespan { get; }
		ObjectCloud.Interfaces.Disk.LoggingLevel Level { get; }
	}

	public interface ILifespan_Writable : ILifespan_Readable
	{
		new System.TimeSpan Timespan { set; }
		bool Timespan_Changed { get; }
		new ObjectCloud.Interfaces.Disk.LoggingLevel Level { set; }
		bool Level_Changed { get; }
	}

	public abstract partial class Lifespan_Table : ITable<ILifespan_Writable, ILifespan_Readable>
	{
		public void Insert(DataAccessDelegate<ILifespan_Writable> writeDelegate)
		{
			Lifespan_Inserter inserter = new Lifespan_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Lifespan_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<ILifespan_Writable> writeDelegate)
		{
			Lifespan_Inserter inserter = new Lifespan_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Lifespan_Inserter inserter);
		
		public abstract IEnumerable<ILifespan_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<ILifespan_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<ILifespan_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public ILifespan_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<ILifespan_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(ILifespan_Readable);
			
			ILifespan_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<ILifespan_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<ILifespan_Writable> writeDelegate)
		{
			Lifespan_Inserter inserter = new Lifespan_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Lifespan_Inserter inserter);
		
		public static Column Timespan
		{
			get
			{
				return _Timespan;
			}
		}
		protected static Column _Timespan;

		public static Column Level
		{
			get
			{
				return _Level;
			}
		}
		protected static Column _Level;

		
		protected class Lifespan_Inserter : ILifespan_Writable
		{
			public System.TimeSpan Timespan
			{
				get { return _Timespan; }
				set
				{
					_Timespan = value;
					_Timespan_Changed = true;
				}
			}
			public System.TimeSpan _Timespan;
			public bool Timespan_Changed
			{
				get { return _Timespan_Changed; }
			}
			private bool _Timespan_Changed = false;
			
			public ObjectCloud.Interfaces.Disk.LoggingLevel Level
			{
				get { return _Level; }
				set
				{
					_Level = value;
					_Level_Changed = true;
				}
			}
			public ObjectCloud.Interfaces.Disk.LoggingLevel _Level;
			public bool Level_Changed
			{
				get { return _Level_Changed; }
			}
			private bool _Level_Changed = false;
			
		}
	}

	public partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }

}