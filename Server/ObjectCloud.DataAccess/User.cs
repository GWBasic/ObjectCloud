using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;

namespace ObjectCloud.DataAccess.User
{
	public class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }
	
	public partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>
	{
		Pairs_Table Pairs { get; }
		Notification_Table Notification { get; }
		ChangeData_Table ChangeData { get; }
		Sender_Table Sender { get; }
		Token_Table Token { get; }
		Blocked_Table Blocked { get; }
		ObjectState_Table ObjectState { get; }
		Deleted_Table Deleted { get; }
	}
	
	public interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<ObjectCloud.DataAccess.User.IDatabaseConnector, ObjectCloud.DataAccess.User.IDatabaseConnection, IDatabaseTransaction> { }
	
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
	public interface INotification_Readable
	{
		System.DateTime TimeStamp { get; }
		System.String Sender { get; }
		System.String ObjectUrl { get; }
		System.String Title { get; }
		System.String DocumentType { get; }
		System.String MessageSummary { get; }
		ObjectCloud.Interfaces.Disk.NotificationState State { get; }
		System.Int64 NotificationId { get; }
	}

	public interface INotification_Writable : INotification_Readable
	{
		new System.DateTime TimeStamp { set; }
		bool TimeStamp_Changed { get; }
		new System.String Sender { set; }
		bool Sender_Changed { get; }
		new System.String ObjectUrl { set; }
		bool ObjectUrl_Changed { get; }
		new System.String Title { set; }
		bool Title_Changed { get; }
		new System.String DocumentType { set; }
		bool DocumentType_Changed { get; }
		new System.String MessageSummary { set; }
		bool MessageSummary_Changed { get; }
		new ObjectCloud.Interfaces.Disk.NotificationState State { set; }
		bool State_Changed { get; }
		new System.Int64 NotificationId { set; }
		bool NotificationId_Changed { get; }
	}

	public abstract partial class Notification_Table : ITable<INotification_Writable, INotification_Readable>
	{
		public void Insert(DataAccessDelegate<INotification_Writable> writeDelegate)
		{
			Notification_Inserter inserter = new Notification_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Notification_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<INotification_Writable> writeDelegate)
		{
			Notification_Inserter inserter = new Notification_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Notification_Inserter inserter);
		
		public abstract IEnumerable<INotification_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<INotification_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<INotification_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public INotification_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<INotification_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(INotification_Readable);
			
			INotification_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<INotification_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<INotification_Writable> writeDelegate)
		{
			Notification_Inserter inserter = new Notification_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Notification_Inserter inserter);
		
		public static Column TimeStamp
		{
			get
			{
				return _TimeStamp;
			}
		}
		protected static Column _TimeStamp;

		public static Column Sender
		{
			get
			{
				return _Sender;
			}
		}
		protected static Column _Sender;

		public static Column ObjectUrl
		{
			get
			{
				return _ObjectUrl;
			}
		}
		protected static Column _ObjectUrl;

		public static Column Title
		{
			get
			{
				return _Title;
			}
		}
		protected static Column _Title;

		public static Column DocumentType
		{
			get
			{
				return _DocumentType;
			}
		}
		protected static Column _DocumentType;

		public static Column MessageSummary
		{
			get
			{
				return _MessageSummary;
			}
		}
		protected static Column _MessageSummary;

		public static Column State
		{
			get
			{
				return _State;
			}
		}
		protected static Column _State;

		public static Column NotificationId
		{
			get
			{
				return _NotificationId;
			}
		}
		protected static Column _NotificationId;

		
		protected class Notification_Inserter : INotification_Writable
		{
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
			
			public System.String Sender
			{
				get { return _Sender; }
				set
				{
					_Sender = value;
					_Sender_Changed = true;
				}
			}
			public System.String _Sender;
			public bool Sender_Changed
			{
				get { return _Sender_Changed; }
			}
			private bool _Sender_Changed = false;
			
			public System.String ObjectUrl
			{
				get { return _ObjectUrl; }
				set
				{
					_ObjectUrl = value;
					_ObjectUrl_Changed = true;
				}
			}
			public System.String _ObjectUrl;
			public bool ObjectUrl_Changed
			{
				get { return _ObjectUrl_Changed; }
			}
			private bool _ObjectUrl_Changed = false;
			
			public System.String Title
			{
				get { return _Title; }
				set
				{
					_Title = value;
					_Title_Changed = true;
				}
			}
			public System.String _Title;
			public bool Title_Changed
			{
				get { return _Title_Changed; }
			}
			private bool _Title_Changed = false;
			
			public System.String DocumentType
			{
				get { return _DocumentType; }
				set
				{
					_DocumentType = value;
					_DocumentType_Changed = true;
				}
			}
			public System.String _DocumentType;
			public bool DocumentType_Changed
			{
				get { return _DocumentType_Changed; }
			}
			private bool _DocumentType_Changed = false;
			
			public System.String MessageSummary
			{
				get { return _MessageSummary; }
				set
				{
					_MessageSummary = value;
					_MessageSummary_Changed = true;
				}
			}
			public System.String _MessageSummary;
			public bool MessageSummary_Changed
			{
				get { return _MessageSummary_Changed; }
			}
			private bool _MessageSummary_Changed = false;
			
			public ObjectCloud.Interfaces.Disk.NotificationState State
			{
				get { return _State; }
				set
				{
					_State = value;
					_State_Changed = true;
				}
			}
			public ObjectCloud.Interfaces.Disk.NotificationState _State;
			public bool State_Changed
			{
				get { return _State_Changed; }
			}
			private bool _State_Changed = false;
			
			public System.Int64 NotificationId
			{
				get { return _NotificationId; }
				set
				{
					_NotificationId = value;
					_NotificationId_Changed = true;
				}
			}
			public System.Int64 _NotificationId;
			public bool NotificationId_Changed
			{
				get { return _NotificationId_Changed; }
			}
			private bool _NotificationId_Changed = false;
			
		}
	}
	public interface IChangeData_Readable
	{
		System.String ChangeData { get; }
		System.Int64 NotificationId { get; }
	}

	public interface IChangeData_Writable : IChangeData_Readable
	{
		new System.String ChangeData { set; }
		bool ChangeData_Changed { get; }
		new System.Int64 NotificationId { set; }
		bool NotificationId_Changed { get; }
	}

	public abstract partial class ChangeData_Table : ITable<IChangeData_Writable, IChangeData_Readable>
	{
		public void Insert(DataAccessDelegate<IChangeData_Writable> writeDelegate)
		{
			ChangeData_Inserter inserter = new ChangeData_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(ChangeData_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IChangeData_Writable> writeDelegate)
		{
			ChangeData_Inserter inserter = new ChangeData_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(ChangeData_Inserter inserter);
		
		public abstract IEnumerable<IChangeData_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IChangeData_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IChangeData_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IChangeData_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IChangeData_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IChangeData_Readable);
			
			IChangeData_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IChangeData_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IChangeData_Writable> writeDelegate)
		{
			ChangeData_Inserter inserter = new ChangeData_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, ChangeData_Inserter inserter);
		
		public static Column ChangeData
		{
			get
			{
				return _ChangeData;
			}
		}
		protected static Column _ChangeData;

		public static Column NotificationId
		{
			get
			{
				return _NotificationId;
			}
		}
		protected static Column _NotificationId;

		
		protected class ChangeData_Inserter : IChangeData_Writable
		{
			public System.String ChangeData
			{
				get { return _ChangeData; }
				set
				{
					_ChangeData = value;
					_ChangeData_Changed = true;
				}
			}
			public System.String _ChangeData;
			public bool ChangeData_Changed
			{
				get { return _ChangeData_Changed; }
			}
			private bool _ChangeData_Changed = false;
			
			public System.Int64 NotificationId
			{
				get { return _NotificationId; }
				set
				{
					_NotificationId = value;
					_NotificationId_Changed = true;
				}
			}
			public System.Int64 _NotificationId;
			public bool NotificationId_Changed
			{
				get { return _NotificationId_Changed; }
			}
			private bool _NotificationId_Changed = false;
			
		}
	}
	public interface ISender_Readable
	{
		System.String SenderToken { get; }
		System.String RecipientToken { get; }
		System.String OpenID { get; }
	}

	public interface ISender_Writable : ISender_Readable
	{
		new System.String SenderToken { set; }
		bool SenderToken_Changed { get; }
		new System.String RecipientToken { set; }
		bool RecipientToken_Changed { get; }
		new System.String OpenID { set; }
		bool OpenID_Changed { get; }
	}

	public abstract partial class Sender_Table : ITable<ISender_Writable, ISender_Readable>
	{
		public void Insert(DataAccessDelegate<ISender_Writable> writeDelegate)
		{
			Sender_Inserter inserter = new Sender_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Sender_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<ISender_Writable> writeDelegate)
		{
			Sender_Inserter inserter = new Sender_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Sender_Inserter inserter);
		
		public abstract IEnumerable<ISender_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<ISender_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<ISender_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public ISender_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<ISender_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(ISender_Readable);
			
			ISender_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<ISender_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<ISender_Writable> writeDelegate)
		{
			Sender_Inserter inserter = new Sender_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Sender_Inserter inserter);
		
		public static Column SenderToken
		{
			get
			{
				return _SenderToken;
			}
		}
		protected static Column _SenderToken;

		public static Column RecipientToken
		{
			get
			{
				return _RecipientToken;
			}
		}
		protected static Column _RecipientToken;

		public static Column OpenID
		{
			get
			{
				return _OpenID;
			}
		}
		protected static Column _OpenID;

		
		protected class Sender_Inserter : ISender_Writable
		{
			public System.String SenderToken
			{
				get { return _SenderToken; }
				set
				{
					_SenderToken = value;
					_SenderToken_Changed = true;
				}
			}
			public System.String _SenderToken;
			public bool SenderToken_Changed
			{
				get { return _SenderToken_Changed; }
			}
			private bool _SenderToken_Changed = false;
			
			public System.String RecipientToken
			{
				get { return _RecipientToken; }
				set
				{
					_RecipientToken = value;
					_RecipientToken_Changed = true;
				}
			}
			public System.String _RecipientToken;
			public bool RecipientToken_Changed
			{
				get { return _RecipientToken_Changed; }
			}
			private bool _RecipientToken_Changed = false;
			
			public System.String OpenID
			{
				get { return _OpenID; }
				set
				{
					_OpenID = value;
					_OpenID_Changed = true;
				}
			}
			public System.String _OpenID;
			public bool OpenID_Changed
			{
				get { return _OpenID_Changed; }
			}
			private bool _OpenID_Changed = false;
			
		}
	}
	public interface IToken_Readable
	{
		System.String Token { get; }
		System.DateTime Created { get; }
		System.String OpenId { get; }
	}

	public interface IToken_Writable : IToken_Readable
	{
		new System.String Token { set; }
		bool Token_Changed { get; }
		new System.DateTime Created { set; }
		bool Created_Changed { get; }
		new System.String OpenId { set; }
		bool OpenId_Changed { get; }
	}

	public abstract partial class Token_Table : ITable<IToken_Writable, IToken_Readable>
	{
		public void Insert(DataAccessDelegate<IToken_Writable> writeDelegate)
		{
			Token_Inserter inserter = new Token_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Token_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IToken_Writable> writeDelegate)
		{
			Token_Inserter inserter = new Token_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Token_Inserter inserter);
		
		public abstract IEnumerable<IToken_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IToken_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IToken_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IToken_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IToken_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IToken_Readable);
			
			IToken_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IToken_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IToken_Writable> writeDelegate)
		{
			Token_Inserter inserter = new Token_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Token_Inserter inserter);
		
		public static Column Token
		{
			get
			{
				return _Token;
			}
		}
		protected static Column _Token;

		public static Column Created
		{
			get
			{
				return _Created;
			}
		}
		protected static Column _Created;

		public static Column OpenId
		{
			get
			{
				return _OpenId;
			}
		}
		protected static Column _OpenId;

		
		protected class Token_Inserter : IToken_Writable
		{
			public System.String Token
			{
				get { return _Token; }
				set
				{
					_Token = value;
					_Token_Changed = true;
				}
			}
			public System.String _Token;
			public bool Token_Changed
			{
				get { return _Token_Changed; }
			}
			private bool _Token_Changed = false;
			
			public System.DateTime Created
			{
				get { return _Created; }
				set
				{
					_Created = value;
					_Created_Changed = true;
				}
			}
			public System.DateTime _Created;
			public bool Created_Changed
			{
				get { return _Created_Changed; }
			}
			private bool _Created_Changed = false;
			
			public System.String OpenId
			{
				get { return _OpenId; }
				set
				{
					_OpenId = value;
					_OpenId_Changed = true;
				}
			}
			public System.String _OpenId;
			public bool OpenId_Changed
			{
				get { return _OpenId_Changed; }
			}
			private bool _OpenId_Changed = false;
			
		}
	}
	public interface IBlocked_Readable
	{
		System.String OpenIdorDomain { get; }
	}

	public interface IBlocked_Writable : IBlocked_Readable
	{
		new System.String OpenIdorDomain { set; }
		bool OpenIdorDomain_Changed { get; }
	}

	public abstract partial class Blocked_Table : ITable<IBlocked_Writable, IBlocked_Readable>
	{
		public void Insert(DataAccessDelegate<IBlocked_Writable> writeDelegate)
		{
			Blocked_Inserter inserter = new Blocked_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Blocked_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IBlocked_Writable> writeDelegate)
		{
			Blocked_Inserter inserter = new Blocked_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Blocked_Inserter inserter);
		
		public abstract IEnumerable<IBlocked_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IBlocked_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IBlocked_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IBlocked_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IBlocked_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IBlocked_Readable);
			
			IBlocked_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IBlocked_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IBlocked_Writable> writeDelegate)
		{
			Blocked_Inserter inserter = new Blocked_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Blocked_Inserter inserter);
		
		public static Column OpenIdorDomain
		{
			get
			{
				return _OpenIdorDomain;
			}
		}
		protected static Column _OpenIdorDomain;

		
		protected class Blocked_Inserter : IBlocked_Writable
		{
			public System.String OpenIdorDomain
			{
				get { return _OpenIdorDomain; }
				set
				{
					_OpenIdorDomain = value;
					_OpenIdorDomain_Changed = true;
				}
			}
			public System.String _OpenIdorDomain;
			public bool OpenIdorDomain_Changed
			{
				get { return _OpenIdorDomain_Changed; }
			}
			private bool _OpenIdorDomain_Changed = false;
			
		}
	}
	public interface IObjectState_Readable
	{
		System.Int32 ObjectState { get; }
		System.String ObjectUrl { get; }
	}

	public interface IObjectState_Writable : IObjectState_Readable
	{
		new System.Int32 ObjectState { set; }
		bool ObjectState_Changed { get; }
		new System.String ObjectUrl { set; }
		bool ObjectUrl_Changed { get; }
	}

	public abstract partial class ObjectState_Table : ITable<IObjectState_Writable, IObjectState_Readable>
	{
		public void Insert(DataAccessDelegate<IObjectState_Writable> writeDelegate)
		{
			ObjectState_Inserter inserter = new ObjectState_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(ObjectState_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IObjectState_Writable> writeDelegate)
		{
			ObjectState_Inserter inserter = new ObjectState_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(ObjectState_Inserter inserter);
		
		public abstract IEnumerable<IObjectState_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IObjectState_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IObjectState_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IObjectState_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IObjectState_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IObjectState_Readable);
			
			IObjectState_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IObjectState_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IObjectState_Writable> writeDelegate)
		{
			ObjectState_Inserter inserter = new ObjectState_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, ObjectState_Inserter inserter);
		
		public static Column ObjectState
		{
			get
			{
				return _ObjectState;
			}
		}
		protected static Column _ObjectState;

		public static Column ObjectUrl
		{
			get
			{
				return _ObjectUrl;
			}
		}
		protected static Column _ObjectUrl;

		
		protected class ObjectState_Inserter : IObjectState_Writable
		{
			public System.Int32 ObjectState
			{
				get { return _ObjectState; }
				set
				{
					_ObjectState = value;
					_ObjectState_Changed = true;
				}
			}
			public System.Int32 _ObjectState;
			public bool ObjectState_Changed
			{
				get { return _ObjectState_Changed; }
			}
			private bool _ObjectState_Changed = false;
			
			public System.String ObjectUrl
			{
				get { return _ObjectUrl; }
				set
				{
					_ObjectUrl = value;
					_ObjectUrl_Changed = true;
				}
			}
			public System.String _ObjectUrl;
			public bool ObjectUrl_Changed
			{
				get { return _ObjectUrl_Changed; }
			}
			private bool _ObjectUrl_Changed = false;
			
		}
	}
	public interface IDeleted_Readable
	{
		System.String OpenId { get; }
		System.String ObjectUrl { get; }
	}

	public interface IDeleted_Writable : IDeleted_Readable
	{
		new System.String OpenId { set; }
		bool OpenId_Changed { get; }
		new System.String ObjectUrl { set; }
		bool ObjectUrl_Changed { get; }
	}

	public abstract partial class Deleted_Table : ITable<IDeleted_Writable, IDeleted_Readable>
	{
		public void Insert(DataAccessDelegate<IDeleted_Writable> writeDelegate)
		{
			Deleted_Inserter inserter = new Deleted_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Deleted_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IDeleted_Writable> writeDelegate)
		{
			Deleted_Inserter inserter = new Deleted_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Deleted_Inserter inserter);
		
		public abstract IEnumerable<IDeleted_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IDeleted_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IDeleted_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IDeleted_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IDeleted_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IDeleted_Readable);
			
			IDeleted_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IDeleted_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IDeleted_Writable> writeDelegate)
		{
			Deleted_Inserter inserter = new Deleted_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Deleted_Inserter inserter);
		
		public static Column OpenId
		{
			get
			{
				return _OpenId;
			}
		}
		protected static Column _OpenId;

		public static Column ObjectUrl
		{
			get
			{
				return _ObjectUrl;
			}
		}
		protected static Column _ObjectUrl;

		
		protected class Deleted_Inserter : IDeleted_Writable
		{
			public System.String OpenId
			{
				get { return _OpenId; }
				set
				{
					_OpenId = value;
					_OpenId_Changed = true;
				}
			}
			public System.String _OpenId;
			public bool OpenId_Changed
			{
				get { return _OpenId_Changed; }
			}
			private bool _OpenId_Changed = false;
			
			public System.String ObjectUrl
			{
				get { return _ObjectUrl; }
				set
				{
					_ObjectUrl = value;
					_ObjectUrl_Changed = true;
				}
			}
			public System.String _ObjectUrl;
			public bool ObjectUrl_Changed
			{
				get { return _ObjectUrl_Changed; }
			}
			private bool _ObjectUrl_Changed = false;
			
		}
	}

	public partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }

}