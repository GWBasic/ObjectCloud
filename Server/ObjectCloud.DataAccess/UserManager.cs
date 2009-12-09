using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;

namespace ObjectCloud.DataAccess.UserManager
{
	public class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }
	
	public partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>
	{
		Users_Table Users { get; }
		Groups_Table Groups { get; }
		UserInGroups_Table UserInGroups { get; }
		AssociationHandles_Table AssociationHandles { get; }
	}
	
	public interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<ObjectCloud.DataAccess.UserManager.IDatabaseConnector, ObjectCloud.DataAccess.UserManager.IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IEmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.IEmbeddedDatabaseCreator { }
	
	public interface IUsers_Readable
	{
		System.String PasswordMD5 { get; }
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID { get; }
		System.Boolean BuiltIn { get; }
		System.String Name { get; }
	}

	public interface IUsers_Writable : IUsers_Readable
	{
		new System.String PasswordMD5 { set; }
		bool PasswordMD5_Changed { get; }
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID { set; }
		bool ID_Changed { get; }
		new System.Boolean BuiltIn { set; }
		bool BuiltIn_Changed { get; }
		new System.String Name { set; }
		bool Name_Changed { get; }
	}

	public abstract partial class Users_Table : ITable<IUsers_Writable, IUsers_Readable>
	{
		public void Insert(DataAccessDelegate<IUsers_Writable> writeDelegate)
		{
			Users_Inserter inserter = new Users_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Users_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IUsers_Writable> writeDelegate)
		{
			Users_Inserter inserter = new Users_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Users_Inserter inserter);
		
		public abstract IEnumerable<IUsers_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IUsers_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IUsers_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IUsers_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IUsers_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IUsers_Readable);
			
			IUsers_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IUsers_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IUsers_Writable> writeDelegate)
		{
			Users_Inserter inserter = new Users_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Users_Inserter inserter);
		
		public static Column PasswordMD5
		{
			get
			{
				return _PasswordMD5;
			}
		}
		protected static Column _PasswordMD5;

		public static Column ID
		{
			get
			{
				return _ID;
			}
		}
		protected static Column _ID;

		public static Column BuiltIn
		{
			get
			{
				return _BuiltIn;
			}
		}
		protected static Column _BuiltIn;

		public static Column Name
		{
			get
			{
				return _Name;
			}
		}
		protected static Column _Name;

		
		protected class Users_Inserter : IUsers_Writable
		{
			public System.String PasswordMD5
			{
				get { return _PasswordMD5; }
				set
				{
					_PasswordMD5 = value;
					_PasswordMD5_Changed = true;
				}
			}
			public System.String _PasswordMD5;
			public bool PasswordMD5_Changed
			{
				get { return _PasswordMD5_Changed; }
			}
			private bool _PasswordMD5_Changed = false;
			
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID
			{
				get { return _ID; }
				set
				{
					_ID = value;
					_ID_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _ID;
			public bool ID_Changed
			{
				get { return _ID_Changed; }
			}
			private bool _ID_Changed = false;
			
			public System.Boolean BuiltIn
			{
				get { return _BuiltIn; }
				set
				{
					_BuiltIn = value;
					_BuiltIn_Changed = true;
				}
			}
			public System.Boolean _BuiltIn;
			public bool BuiltIn_Changed
			{
				get { return _BuiltIn_Changed; }
			}
			private bool _BuiltIn_Changed = false;
			
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
	public interface IGroups_Readable
	{
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID { get; }
		System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerID { get; }
		System.Boolean BuiltIn { get; }
		System.Boolean Automatic { get; }
		System.String Name { get; }
	}

	public interface IGroups_Writable : IGroups_Readable
	{
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID { set; }
		bool ID_Changed { get; }
		new System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerID { set; }
		bool OwnerID_Changed { get; }
		new System.Boolean BuiltIn { set; }
		bool BuiltIn_Changed { get; }
		new System.Boolean Automatic { set; }
		bool Automatic_Changed { get; }
		new System.String Name { set; }
		bool Name_Changed { get; }
	}

	public abstract partial class Groups_Table : ITable<IGroups_Writable, IGroups_Readable>
	{
		public void Insert(DataAccessDelegate<IGroups_Writable> writeDelegate)
		{
			Groups_Inserter inserter = new Groups_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Groups_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IGroups_Writable> writeDelegate)
		{
			Groups_Inserter inserter = new Groups_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Groups_Inserter inserter);
		
		public abstract IEnumerable<IGroups_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IGroups_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IGroups_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IGroups_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IGroups_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IGroups_Readable);
			
			IGroups_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IGroups_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IGroups_Writable> writeDelegate)
		{
			Groups_Inserter inserter = new Groups_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Groups_Inserter inserter);
		
		public static Column ID
		{
			get
			{
				return _ID;
			}
		}
		protected static Column _ID;

		public static Column OwnerID
		{
			get
			{
				return _OwnerID;
			}
		}
		protected static Column _OwnerID;

		public static Column BuiltIn
		{
			get
			{
				return _BuiltIn;
			}
		}
		protected static Column _BuiltIn;

		public static Column Automatic
		{
			get
			{
				return _Automatic;
			}
		}
		protected static Column _Automatic;

		public static Column Name
		{
			get
			{
				return _Name;
			}
		}
		protected static Column _Name;

		
		protected class Groups_Inserter : IGroups_Writable
		{
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> ID
			{
				get { return _ID; }
				set
				{
					_ID = value;
					_ID_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _ID;
			public bool ID_Changed
			{
				get { return _ID_Changed; }
			}
			private bool _ID_Changed = false;
			
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerID
			{
				get { return _OwnerID; }
				set
				{
					_OwnerID = value;
					_OwnerID_Changed = true;
				}
			}
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> _OwnerID;
			public bool OwnerID_Changed
			{
				get { return _OwnerID_Changed; }
			}
			private bool _OwnerID_Changed = false;
			
			public System.Boolean BuiltIn
			{
				get { return _BuiltIn; }
				set
				{
					_BuiltIn = value;
					_BuiltIn_Changed = true;
				}
			}
			public System.Boolean _BuiltIn;
			public bool BuiltIn_Changed
			{
				get { return _BuiltIn_Changed; }
			}
			private bool _BuiltIn_Changed = false;
			
			public System.Boolean Automatic
			{
				get { return _Automatic; }
				set
				{
					_Automatic = value;
					_Automatic_Changed = true;
				}
			}
			public System.Boolean _Automatic;
			public bool Automatic_Changed
			{
				get { return _Automatic_Changed; }
			}
			private bool _Automatic_Changed = false;
			
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
	public interface IUserInGroups_Readable
	{
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserID { get; }
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> GroupID { get; }
	}

	public interface IUserInGroups_Writable : IUserInGroups_Readable
	{
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserID { set; }
		bool UserID_Changed { get; }
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> GroupID { set; }
		bool GroupID_Changed { get; }
	}

	public abstract partial class UserInGroups_Table : ITable<IUserInGroups_Writable, IUserInGroups_Readable>
	{
		public void Insert(DataAccessDelegate<IUserInGroups_Writable> writeDelegate)
		{
			UserInGroups_Inserter inserter = new UserInGroups_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(UserInGroups_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IUserInGroups_Writable> writeDelegate)
		{
			UserInGroups_Inserter inserter = new UserInGroups_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(UserInGroups_Inserter inserter);
		
		public abstract IEnumerable<IUserInGroups_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IUserInGroups_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IUserInGroups_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IUserInGroups_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IUserInGroups_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IUserInGroups_Readable);
			
			IUserInGroups_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IUserInGroups_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IUserInGroups_Writable> writeDelegate)
		{
			UserInGroups_Inserter inserter = new UserInGroups_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, UserInGroups_Inserter inserter);
		
		public static Column UserID
		{
			get
			{
				return _UserID;
			}
		}
		protected static Column _UserID;

		public static Column GroupID
		{
			get
			{
				return _GroupID;
			}
		}
		protected static Column _GroupID;

		
		protected class UserInGroups_Inserter : IUserInGroups_Writable
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
			
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> GroupID
			{
				get { return _GroupID; }
				set
				{
					_GroupID = value;
					_GroupID_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _GroupID;
			public bool GroupID_Changed
			{
				get { return _GroupID_Changed; }
			}
			private bool _GroupID_Changed = false;
			
		}
	}
	public interface IAssociationHandles_Readable
	{
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid> UserID { get; }
		System.String AssociationHandle { get; }
		System.DateTime Timestamp { get; }
	}

	public interface IAssociationHandles_Writable : IAssociationHandles_Readable
	{
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid> UserID { set; }
		bool UserID_Changed { get; }
		new System.String AssociationHandle { set; }
		bool AssociationHandle_Changed { get; }
		new System.DateTime Timestamp { set; }
		bool Timestamp_Changed { get; }
	}

	public abstract partial class AssociationHandles_Table : ITable<IAssociationHandles_Writable, IAssociationHandles_Readable>
	{
		public void Insert(DataAccessDelegate<IAssociationHandles_Writable> writeDelegate)
		{
			AssociationHandles_Inserter inserter = new AssociationHandles_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(AssociationHandles_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IAssociationHandles_Writable> writeDelegate)
		{
			AssociationHandles_Inserter inserter = new AssociationHandles_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(AssociationHandles_Inserter inserter);
		
		public abstract IEnumerable<IAssociationHandles_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IAssociationHandles_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IAssociationHandles_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IAssociationHandles_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IAssociationHandles_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IAssociationHandles_Readable);
			
			IAssociationHandles_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IAssociationHandles_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IAssociationHandles_Writable> writeDelegate)
		{
			AssociationHandles_Inserter inserter = new AssociationHandles_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, AssociationHandles_Inserter inserter);
		
		public static Column UserID
		{
			get
			{
				return _UserID;
			}
		}
		protected static Column _UserID;

		public static Column AssociationHandle
		{
			get
			{
				return _AssociationHandle;
			}
		}
		protected static Column _AssociationHandle;

		public static Column Timestamp
		{
			get
			{
				return _Timestamp;
			}
		}
		protected static Column _Timestamp;

		
		protected class AssociationHandles_Inserter : IAssociationHandles_Writable
		{
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid> UserID
			{
				get { return _UserID; }
				set
				{
					_UserID = value;
					_UserID_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUser, System.Guid> _UserID;
			public bool UserID_Changed
			{
				get { return _UserID_Changed; }
			}
			private bool _UserID_Changed = false;
			
			public System.String AssociationHandle
			{
				get { return _AssociationHandle; }
				set
				{
					_AssociationHandle = value;
					_AssociationHandle_Changed = true;
				}
			}
			public System.String _AssociationHandle;
			public bool AssociationHandle_Changed
			{
				get { return _AssociationHandle_Changed; }
			}
			private bool _AssociationHandle_Changed = false;
			
			public System.DateTime Timestamp
			{
				get { return _Timestamp; }
				set
				{
					_Timestamp = value;
					_Timestamp_Changed = true;
				}
			}
			public System.DateTime _Timestamp;
			public bool Timestamp_Changed
			{
				get { return _Timestamp_Changed; }
			}
			private bool _Timestamp_Changed = false;
			
		}
	}

	public partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }

}