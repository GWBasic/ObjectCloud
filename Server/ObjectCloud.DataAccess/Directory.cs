using ObjectCloud.ORM.DataAccess;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using System;
using System.Collections.Generic;

namespace ObjectCloud.DataAccess.Directory
{
	public class DataAccessLocator : ObjectCloud.ORM.DataAccess.DataAccessLocator<IDatabaseConnectorFactory> { }
	
	public partial interface IDatabaseConnection : ObjectCloud.ORM.DataAccess.IDatabaseConnection<IDatabaseTransaction>
	{
		File_Table File { get; }
		Permission_Table Permission { get; }
		Metadata_Table Metadata { get; }
		Relationships_Table Relationships { get; }
		NamedPermission_Table NamedPermission { get; }
	}
	
	public interface IDatabaseConnector : ObjectCloud.ORM.DataAccess.IDatabaseConnector<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IDatabaseConnectorFactory : ObjectCloud.ORM.DataAccess.IDatabaseConnectorFactory<ObjectCloud.DataAccess.Directory.IDatabaseConnector, ObjectCloud.DataAccess.Directory.IDatabaseConnection, IDatabaseTransaction> { }
	
	public interface IEmbeddedDatabaseCreator : ObjectCloud.ORM.DataAccess.IEmbeddedDatabaseCreator { }
	
	public interface IFile_Readable
	{
		System.String Name { get; }
		System.String Extension { get; }
		System.String TypeId { get; }
		System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerId { get; }
		System.DateTime Created { get; }
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { get; }
	}

	public interface IFile_Writable : IFile_Readable
	{
		new System.String Name { set; }
		bool Name_Changed { get; }
		new System.String Extension { set; }
		bool Extension_Changed { get; }
		new System.String TypeId { set; }
		bool TypeId_Changed { get; }
		new System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerId { set; }
		bool OwnerId_Changed { get; }
		new System.DateTime Created { set; }
		bool Created_Changed { get; }
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { set; }
		bool FileId_Changed { get; }
	}

	public abstract partial class File_Table : ITable<IFile_Writable, IFile_Readable>
	{
		public void Insert(DataAccessDelegate<IFile_Writable> writeDelegate)
		{
			File_Inserter inserter = new File_Inserter();
			writeDelegate(inserter);

                    if (inserter.Name_Changed)
                        if (inserter.Name.Contains("."))
                            inserter.Extension = inserter.Name.Substring(inserter.Name.LastIndexOf('.') + 1);
                        else
                            inserter.Extension = "";
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(File_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IFile_Writable> writeDelegate)
		{
			File_Inserter inserter = new File_Inserter();
			writeDelegate(inserter);

                    if (inserter.Name_Changed)
                        if (inserter.Name.Contains("."))
                            inserter.Extension = inserter.Name.Substring(inserter.Name.LastIndexOf('.') + 1);
                        else
                            inserter.Extension = "";
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(File_Inserter inserter);
		
		public abstract IEnumerable<IFile_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IFile_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IFile_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IFile_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IFile_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IFile_Readable);
			
			IFile_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IFile_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IFile_Writable> writeDelegate)
		{
			File_Inserter inserter = new File_Inserter();
			writeDelegate(inserter);

                    if (inserter.Name_Changed)
                        if (inserter.Name.Contains("."))
                            inserter.Extension = inserter.Name.Substring(inserter.Name.LastIndexOf('.') + 1);
                        else
                            inserter.Extension = "";
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, File_Inserter inserter);
		
		public static Column Name
		{
			get
			{
				return _Name;
			}
		}
		protected static Column _Name;

		public static Column Extension
		{
			get
			{
				return _Extension;
			}
		}
		protected static Column _Extension;

		public static Column TypeId
		{
			get
			{
				return _TypeId;
			}
		}
		protected static Column _TypeId;

		public static Column OwnerId
		{
			get
			{
				return _OwnerId;
			}
		}
		protected static Column _OwnerId;

		public static Column Created
		{
			get
			{
				return _Created;
			}
		}
		protected static Column _Created;

		public static Column FileId
		{
			get
			{
				return _FileId;
			}
		}
		protected static Column _FileId;

		
		protected class File_Inserter : IFile_Writable
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
			
			public System.String Extension
			{
				get { return _Extension; }
				set
				{
					_Extension = value;
					_Extension_Changed = true;
				}
			}
			public System.String _Extension;
			public bool Extension_Changed
			{
				get { return _Extension_Changed; }
			}
			private bool _Extension_Changed = false;
			
			public System.String TypeId
			{
				get { return _TypeId; }
				set
				{
					_TypeId = value;
					_TypeId_Changed = true;
				}
			}
			public System.String _TypeId;
			public bool TypeId_Changed
			{
				get { return _TypeId_Changed; }
			}
			private bool _TypeId_Changed = false;
			
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> OwnerId
			{
				get { return _OwnerId; }
				set
				{
					_OwnerId = value;
					_OwnerId_Changed = true;
				}
			}
			public System.Nullable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid>> _OwnerId;
			public bool OwnerId_Changed
			{
				get { return _OwnerId_Changed; }
			}
			private bool _OwnerId_Changed = false;
			
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
			
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
			{
				get { return _FileId; }
				set
				{
					_FileId = value;
					_FileId_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId;
			public bool FileId_Changed
			{
				get { return _FileId_Changed; }
			}
			private bool _FileId_Changed = false;
			
		}
	}
	public interface IPermission_Readable
	{
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { get; }
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroupId { get; }
		ObjectCloud.Interfaces.Security.FilePermissionEnum Level { get; }
		System.Boolean Inherit { get; }
		System.Boolean SendNotifications { get; }
	}

	public interface IPermission_Writable : IPermission_Readable
	{
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { set; }
		bool FileId_Changed { get; }
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroupId { set; }
		bool UserOrGroupId_Changed { get; }
		new ObjectCloud.Interfaces.Security.FilePermissionEnum Level { set; }
		bool Level_Changed { get; }
		new System.Boolean Inherit { set; }
		bool Inherit_Changed { get; }
		new System.Boolean SendNotifications { set; }
		bool SendNotifications_Changed { get; }
	}

	public abstract partial class Permission_Table : ITable<IPermission_Writable, IPermission_Readable>
	{
		public void Insert(DataAccessDelegate<IPermission_Writable> writeDelegate)
		{
			Permission_Inserter inserter = new Permission_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Permission_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IPermission_Writable> writeDelegate)
		{
			Permission_Inserter inserter = new Permission_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Permission_Inserter inserter);
		
		public abstract IEnumerable<IPermission_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IPermission_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IPermission_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IPermission_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IPermission_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IPermission_Readable);
			
			IPermission_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IPermission_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IPermission_Writable> writeDelegate)
		{
			Permission_Inserter inserter = new Permission_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Permission_Inserter inserter);
		
		public static Column FileId
		{
			get
			{
				return _FileId;
			}
		}
		protected static Column _FileId;

		public static Column UserOrGroupId
		{
			get
			{
				return _UserOrGroupId;
			}
		}
		protected static Column _UserOrGroupId;

		public static Column Level
		{
			get
			{
				return _Level;
			}
		}
		protected static Column _Level;

		public static Column Inherit
		{
			get
			{
				return _Inherit;
			}
		}
		protected static Column _Inherit;

		public static Column SendNotifications
		{
			get
			{
				return _SendNotifications;
			}
		}
		protected static Column _SendNotifications;

		
		protected class Permission_Inserter : IPermission_Writable
		{
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
			{
				get { return _FileId; }
				set
				{
					_FileId = value;
					_FileId_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId;
			public bool FileId_Changed
			{
				get { return _FileId_Changed; }
			}
			private bool _FileId_Changed = false;
			
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroupId
			{
				get { return _UserOrGroupId; }
				set
				{
					_UserOrGroupId = value;
					_UserOrGroupId_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _UserOrGroupId;
			public bool UserOrGroupId_Changed
			{
				get { return _UserOrGroupId_Changed; }
			}
			private bool _UserOrGroupId_Changed = false;
			
			public ObjectCloud.Interfaces.Security.FilePermissionEnum Level
			{
				get { return _Level; }
				set
				{
					_Level = value;
					_Level_Changed = true;
				}
			}
			public ObjectCloud.Interfaces.Security.FilePermissionEnum _Level;
			public bool Level_Changed
			{
				get { return _Level_Changed; }
			}
			private bool _Level_Changed = false;
			
			public System.Boolean Inherit
			{
				get { return _Inherit; }
				set
				{
					_Inherit = value;
					_Inherit_Changed = true;
				}
			}
			public System.Boolean _Inherit;
			public bool Inherit_Changed
			{
				get { return _Inherit_Changed; }
			}
			private bool _Inherit_Changed = false;
			
			public System.Boolean SendNotifications
			{
				get { return _SendNotifications; }
				set
				{
					_SendNotifications = value;
					_SendNotifications_Changed = true;
				}
			}
			public System.Boolean _SendNotifications;
			public bool SendNotifications_Changed
			{
				get { return _SendNotifications_Changed; }
			}
			private bool _SendNotifications_Changed = false;
			
		}
	}
	public interface IMetadata_Readable
	{
		System.String Value { get; }
		System.String Name { get; }
	}

	public interface IMetadata_Writable : IMetadata_Readable
	{
		new System.String Value { set; }
		bool Value_Changed { get; }
		new System.String Name { set; }
		bool Name_Changed { get; }
	}

	public abstract partial class Metadata_Table : ITable<IMetadata_Writable, IMetadata_Readable>
	{
		public void Insert(DataAccessDelegate<IMetadata_Writable> writeDelegate)
		{
			Metadata_Inserter inserter = new Metadata_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Metadata_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IMetadata_Writable> writeDelegate)
		{
			Metadata_Inserter inserter = new Metadata_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Metadata_Inserter inserter);
		
		public abstract IEnumerable<IMetadata_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IMetadata_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IMetadata_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IMetadata_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IMetadata_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IMetadata_Readable);
			
			IMetadata_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IMetadata_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IMetadata_Writable> writeDelegate)
		{
			Metadata_Inserter inserter = new Metadata_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Metadata_Inserter inserter);
		
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

		
		protected class Metadata_Inserter : IMetadata_Writable
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
	public interface IRelationships_Readable
	{
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { get; }
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> ReferencedFileId { get; }
		System.String Relationship { get; }
	}

	public interface IRelationships_Writable : IRelationships_Readable
	{
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { set; }
		bool FileId_Changed { get; }
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> ReferencedFileId { set; }
		bool ReferencedFileId_Changed { get; }
		new System.String Relationship { set; }
		bool Relationship_Changed { get; }
	}

	public abstract partial class Relationships_Table : ITable<IRelationships_Writable, IRelationships_Readable>
	{
		public void Insert(DataAccessDelegate<IRelationships_Writable> writeDelegate)
		{
			Relationships_Inserter inserter = new Relationships_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(Relationships_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<IRelationships_Writable> writeDelegate)
		{
			Relationships_Inserter inserter = new Relationships_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(Relationships_Inserter inserter);
		
		public abstract IEnumerable<IRelationships_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<IRelationships_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<IRelationships_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public IRelationships_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<IRelationships_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(IRelationships_Readable);
			
			IRelationships_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<IRelationships_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<IRelationships_Writable> writeDelegate)
		{
			Relationships_Inserter inserter = new Relationships_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, Relationships_Inserter inserter);
		
		public static Column FileId
		{
			get
			{
				return _FileId;
			}
		}
		protected static Column _FileId;

		public static Column ReferencedFileId
		{
			get
			{
				return _ReferencedFileId;
			}
		}
		protected static Column _ReferencedFileId;

		public static Column Relationship
		{
			get
			{
				return _Relationship;
			}
		}
		protected static Column _Relationship;

		
		protected class Relationships_Inserter : IRelationships_Writable
		{
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
			{
				get { return _FileId; }
				set
				{
					_FileId = value;
					_FileId_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId;
			public bool FileId_Changed
			{
				get { return _FileId_Changed; }
			}
			private bool _FileId_Changed = false;
			
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> ReferencedFileId
			{
				get { return _ReferencedFileId; }
				set
				{
					_ReferencedFileId = value;
					_ReferencedFileId_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _ReferencedFileId;
			public bool ReferencedFileId_Changed
			{
				get { return _ReferencedFileId_Changed; }
			}
			private bool _ReferencedFileId_Changed = false;
			
			public System.String Relationship
			{
				get { return _Relationship; }
				set
				{
					_Relationship = value;
					_Relationship_Changed = true;
				}
			}
			public System.String _Relationship;
			public bool Relationship_Changed
			{
				get { return _Relationship_Changed; }
			}
			private bool _Relationship_Changed = false;
			
		}
	}
	public interface INamedPermission_Readable
	{
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { get; }
		System.String NamedPermission { get; }
		ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroup { get; }
		System.Boolean Inherit { get; }
	}

	public interface INamedPermission_Writable : INamedPermission_Readable
	{
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId { set; }
		bool FileId_Changed { get; }
		new System.String NamedPermission { set; }
		bool NamedPermission_Changed { get; }
		new ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroup { set; }
		bool UserOrGroup_Changed { get; }
		new System.Boolean Inherit { set; }
		bool Inherit_Changed { get; }
	}

	public abstract partial class NamedPermission_Table : ITable<INamedPermission_Writable, INamedPermission_Readable>
	{
		public void Insert(DataAccessDelegate<INamedPermission_Writable> writeDelegate)
		{
			NamedPermission_Inserter inserter = new NamedPermission_Inserter();
			writeDelegate(inserter);
			
			DoInsert(inserter);
		}
		
		protected abstract void DoInsert(NamedPermission_Inserter inserter);
		
		public TKey InsertAndReturnPK<TKey>(DataAccessDelegate<INamedPermission_Writable> writeDelegate)
		{
			NamedPermission_Inserter inserter = new NamedPermission_Inserter();
			writeDelegate(inserter);
			
			return DoInsertAndReturnPrimaryKey<TKey>(inserter);
		}
		
		protected abstract TKey DoInsertAndReturnPrimaryKey<TKey>(NamedPermission_Inserter inserter);
		
		public abstract IEnumerable<INamedPermission_Readable> Select(ComparisonCondition condition, uint? max, OrderBy sortOrder, params Column[] orderBy);
		
		public IEnumerable<INamedPermission_Readable> Select(ComparisonCondition condition)
		{
			return Select(condition, null, default(OrderBy), null);
		}
		
		public IEnumerable<INamedPermission_Readable> Select()
		{
			return Select(null, null, default(OrderBy), null);
		}
		
		public INamedPermission_Readable SelectSingle(ComparisonCondition condition)
		{
			IEnumerator<INamedPermission_Readable> results = Select(condition).GetEnumerator();
			
			if (!results.MoveNext())
				return default(INamedPermission_Readable);
			
			INamedPermission_Readable result = results.Current;
			
			if (results.MoveNext())
				throw new QueryException("More then one object returned");
			return result;
		}
		
		public abstract int Delete(ComparisonCondition condition);
		
		public int Delete()
		{
			return Delete(null);
		}
		
		public int Update(DataAccessDelegate<INamedPermission_Writable> writeDelegate)
		{
			return Update(null, writeDelegate);
		}
		
		public int Update(ComparisonCondition condition, DataAccessDelegate<INamedPermission_Writable> writeDelegate)
		{
			NamedPermission_Inserter inserter = new NamedPermission_Inserter();
			writeDelegate(inserter);
			
			return DoUpdate(condition, inserter);
		}
		
		protected abstract int DoUpdate(ComparisonCondition condition, NamedPermission_Inserter inserter);
		
		public static Column FileId
		{
			get
			{
				return _FileId;
			}
		}
		protected static Column _FileId;

		public static Column NamedPermission
		{
			get
			{
				return _NamedPermission;
			}
		}
		protected static Column _NamedPermission;

		public static Column UserOrGroup
		{
			get
			{
				return _UserOrGroup;
			}
		}
		protected static Column _UserOrGroup;

		public static Column Inherit
		{
			get
			{
				return _Inherit;
			}
		}
		protected static Column _Inherit;

		
		protected class NamedPermission_Inserter : INamedPermission_Writable
		{
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> FileId
			{
				get { return _FileId; }
				set
				{
					_FileId = value;
					_FileId_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Disk.IFileContainer, System.Int64> _FileId;
			public bool FileId_Changed
			{
				get { return _FileId_Changed; }
			}
			private bool _FileId_Changed = false;
			
			public System.String NamedPermission
			{
				get { return _NamedPermission; }
				set
				{
					_NamedPermission = value;
					_NamedPermission_Changed = true;
				}
			}
			public System.String _NamedPermission;
			public bool NamedPermission_Changed
			{
				get { return _NamedPermission_Changed; }
			}
			private bool _NamedPermission_Changed = false;
			
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> UserOrGroup
			{
				get { return _UserOrGroup; }
				set
				{
					_UserOrGroup = value;
					_UserOrGroup_Changed = true;
				}
			}
			public ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, System.Guid> _UserOrGroup;
			public bool UserOrGroup_Changed
			{
				get { return _UserOrGroup_Changed; }
			}
			private bool _UserOrGroup_Changed = false;
			
			public System.Boolean Inherit
			{
				get { return _Inherit; }
				set
				{
					_Inherit = value;
					_Inherit_Changed = true;
				}
			}
			public System.Boolean _Inherit;
			public bool Inherit_Changed
			{
				get { return _Inherit_Changed; }
			}
			private bool _Inherit_Changed = false;
			
		}
	}

	public partial interface IDatabaseTransaction : ObjectCloud.ORM.DataAccess.IDatabaseTransaction { }

}