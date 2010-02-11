// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

using ExtremeSwank.OpenId;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.UserManager;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.Disk.FileHandlers
{
    public class UserManagerHandler : HasDatabaseFileHandler<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction>, IUserManagerHandler
    {
        public UserManagerHandler(IDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(databaseConnector, fileHandlerFactoryLocator) 
        {
            GroupIdsThatUserIsInCache = new Cache<ID<IUserOrGroup, Guid>, ICollection<ID<IUserOrGroup, Guid>>>(GetGroupIdsThatUserIsInForCache);
        }

        public IUser CreateUser(string name, string password)
        {
            return CreateUser(name, password, new ID<IUserOrGroup, Guid>(Guid.NewGuid()), false);
        }

        public IUser CreateUser(string name, string password, ID<IUserOrGroup, Guid> userId, bool builtIn)
        {
            name = name.ToLowerInvariant();

            IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();

            string passwordMD5 = CreateMD5(password);

            IUserHandler newUser;
            IUser userObj = null;

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                // Make sure there isn't a duplicate user/group
                ThrowExceptionIfDuplicate(transaction, name);

                if (usersDirectory.IsFilePresent(name))
                    throw new UserAlreadyExistsException("There is a pre-existing directory for " + name);

                if (usersDirectory.IsFilePresent(name + " .user"))
                    throw new UserAlreadyExistsException("There is a pre-existing " + name + ".user");

                DatabaseConnection.Users.Insert(delegate(IUsers_Writable user)
                {
                    user.Name = name;
                    user.PasswordMD5 = passwordMD5;
                    user.ID = userId;
                    user.BuiltIn = builtIn;
                });

                // Reload the user
                IUsers_Readable userFromDB = DatabaseConnection.Users.SelectSingle(Users_Table.ID == userId);
                userObj = CreateUserObject(userFromDB);

                transaction.Commit();
            });

            try
            {
                // Careful here!!!  When calling the constructor of the user's .user object or the user's directory, a transaction will
                // be created against the user database!  That can cause a deadlock!
                usersDirectory.CreateFile(name, "directory", userObj.Id);

                string userFileName = name + ".user";
                newUser = usersDirectory.CreateSystemFile<IUserHandler>(userFileName, "user", userObj.Id);
                newUser.Name = name;
                usersDirectory.SetPermission(null, userFileName, FileHandlerFactoryLocator.UserFactory.Everybody.Id, FilePermissionEnum.Read, false, false);
            }
            catch
            {
                if (null != usersDirectory)
                {
                    if (usersDirectory.IsFilePresent(name))
                        try
                        {
                            usersDirectory.DeleteFile(null, name);
                        }
                        catch { }

                    if (usersDirectory.IsFilePresent(name + ".user"))
                        try
                        {
                            usersDirectory.DeleteFile(null, name + ".user");
                        }
                        catch { }
                }

                // If there is an error creating the user's .user object or directory, try to delete the user
                DatabaseConnection.Users.Delete(Users_Table.ID == userId);

                throw;
            }

            return userObj;
        }

        public IGroup CreateGroup(
            string name,
            ID<IUserOrGroup, Guid>? ownerId,
            GroupType groupType)
        {
            return CreateGroup(name, ownerId, new ID<IUserOrGroup, Guid>(Guid.NewGuid()), false, false, groupType);
        }

        public IGroup CreateGroup(
            string name,
            ID<IUserOrGroup, Guid>? ownerId,
            ID<IUserOrGroup, Guid> groupId,
            bool builtIn,
            bool automatic,
            GroupType groupType)
        {
            name = name.ToLowerInvariant();

            if (GroupType.Personal == groupType && null == ownerId)
                throw new ArgumentException("Personal groups must have a declared owner");

            IGroup groupObj = null;

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                // Make sure there isn't a duplicate user/group
                ThrowExceptionIfDuplicate(transaction, name);

                DatabaseConnection.Groups.Insert(delegate(IGroups_Writable group)
                {
                    if (groupType > GroupType.Personal)
                        group.Name = name;
                    else
                        group.Name = groupId.ToString();

                    group.ID = groupId;
                    group.OwnerID = ownerId;
                    group.BuiltIn = builtIn;
                    group.Automatic = automatic;
                    group.Type = groupType;
                });

                if (GroupType.Personal == groupType)
                    DatabaseConnection.GroupAliases.Insert(delegate(IGroupAliases_Writable groupAlias)
                    {
                        groupAlias.Alias = name;
                        groupAlias.GroupID = groupId;
                        groupAlias.UserID = ownerId.Value;
                    });

                try
                {
                    // Reload the group
                    IGroups_Readable groupFromDB = DatabaseConnection.Groups.SelectSingle(Groups_Table.ID == groupId);
                    groupObj = CreateGroupObject(groupFromDB);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }

                transaction.Commit();
            });

            IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();
            string groupFileName = name + ".group";

            if (!automatic)
            {
                // Decide where the object goes, for personal groups in the user's directory, for system groups in the users directory
                IDirectoryHandler groupObjectDestinationDirectory;
                if (groupType == GroupType.Personal)
                {
                    IUser owner = FileHandlerFactoryLocator.UserManagerHandler.GetUser(ownerId.Value);
                    groupObjectDestinationDirectory = usersDirectory.OpenFile(owner.Name).CastFileHandler<IDirectoryHandler>();
                }
                else
                    groupObjectDestinationDirectory = usersDirectory;

                IDatabaseHandler groupDB = groupObjectDestinationDirectory.CreateFile(groupFileName, "database", ownerId).FileContainer.CastFileHandler<IDatabaseHandler>(); ;
                groupObjectDestinationDirectory.SetPermission(ownerId, groupFileName, groupId, FilePermissionEnum.Read, true, true);

                // Everyone can read a public group
                if (GroupType.Public == groupType)
                    usersDirectory.SetPermission(ownerId, groupFileName, FileHandlerFactoryLocator.UserFactory.Everybody.Id, FilePermissionEnum.Read, true, false);

                using (DbCommand command = groupDB.Connection.CreateCommand())
                {
                    command.CommandText =
@"create table Metadata 
(
	Value			string not null,
	Name			string not null	primary key
);
Create index Metadata_Name on Metadata (Name);
insert into Metadata (Name, Value) values ('GroupId', @groupId);
";

                    DbParameter parameter = command.CreateParameter();
                    parameter.ParameterName = "@groupId";
                    parameter.Value = groupId;
                    command.Parameters.Add(parameter);

                    command.ExecuteNonQuery();
                }
            }

            return groupObj;
        }

        /// <summary>
        /// Throws a UserAlreadyExistsException if there is a user or group with the same name
        /// </summary>
        /// <param name="name">
        /// A <see cref="System.String"/>
        /// </param>
        private void ThrowExceptionIfDuplicate(IDatabaseTransaction transaction, string name)
        {
            IEnumerator enumerator;

            enumerator = DatabaseConnection.Users.Select(Users_Table.Name == name).GetEnumerator();

            if (enumerator.MoveNext())
            {
                // clean out enumerator
                while (enumerator.MoveNext()) ;

                throw new UserAlreadyExistsException("Duplicate user: " + name);
            }

            enumerator = DatabaseConnection.Groups.Select(Groups_Table.Name == name).GetEnumerator();

            if (enumerator.MoveNext())
            {
                // clean out enumerator
                while (enumerator.MoveNext()) ;

                throw new UserAlreadyExistsException("Duplicate user: " + name);
            }
        }

        private IUsers_Readable GetUserInt(string name)
        {
            name = name.ToLowerInvariant();

            IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.Name == name);

            if (null == user)
                throw new UnknownUser("Unknown user");

            return user;
        }

        /// <summary>
        /// TODO:  This is okay because users are immutable, except for their password, but if users become mutable, this needs to be updated
        /// </summary>
        Dictionary<ID<IUserOrGroup, Guid>, IUser> UsersCache = new Dictionary<ID<IUserOrGroup, Guid>, IUser>();

        public IUser GetUser(ID<IUserOrGroup, Guid> userId)
        {
            /*IUser toReturn = null;

            UsersCache.TryGetValue(userId, out toReturn);
            if (null != toReturn)
                return toReturn;

            IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.ID == userId.Value);

            if (null == user)
                throw new UnknownUser("Unknown user");

            toReturn = CreateUserObject(user);
            UsersCache[userId] = toReturn;
            return toReturn;*/

            IUser toReturn = GetUserNoException(userId);
            if (null != toReturn)
                return toReturn;

            throw new UnknownUser("Unknown user");
        }

        public IUser GetUserNoException(ID<IUserOrGroup, Guid> userId)
        {
            IUser toReturn = null;

            UsersCache.TryGetValue(userId, out toReturn);
            if (null != toReturn)
                return toReturn;

            IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.ID == userId.Value);

            if (null == user)
                return null;

            toReturn = CreateUserObject(user);
            UsersCache[userId] = toReturn;
            return toReturn;
        }

        public IGroup GetGroup(string name)
        {
            IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.Name == name);

            if (null == group)
                throw new UnknownUser("Unknown group");

            return CreateGroupObject(group);
        }

        public IGroup GetGroup(ID<IUserOrGroup, Guid> groupId)
        {
            IGroups_Readable group = GetGroupInt(ref groupId);

            return CreateGroupObject(group);
        }

        private IGroups_Readable GetGroupInt(ref ID<IUserOrGroup, Guid> groupId)
        {
            IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.ID == groupId.Value);

            if (null == group)
                throw new UnknownUser("Unknown group");

            return group;
        }

        public IUserOrGroup GetUserOrGroup(ID<IUserOrGroup, Guid> userOrGroupId)
        {
            IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.ID == userOrGroupId.Value);

            if (null != user)
                return CreateUserObject(user);

            IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.ID == userOrGroupId.Value);

            if (null == group)
                throw new UnknownUser("Unknown user or group");

            return CreateGroupObject(group);
        }

        public IUserOrGroup GetUserOrGroupNoException(ID<IUserOrGroup, Guid> userOrGroupId)
        {
            IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.ID == userOrGroupId.Value);

            if (null != user)
                return CreateUserObject(user);

            IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.ID == userOrGroupId.Value);

            if (null != group)
                return CreateGroupObject(group);

            return null;
        }

        public IUser GetUser(string name)
        {
            IUsers_Readable user = GetUserInt(name);

            IUser toReturn = CreateUserObject(user);

            return toReturn;
        }

        public IUserOrGroup GetUserOrGroupOrOpenId(string nameOrGroupOrIdentity)
        {
            IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.Name == nameOrGroupOrIdentity.ToLowerInvariant());

            // If there is a matching user, return it
            if (null != user)
                return CreateUserObject(user);

            IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.Name == nameOrGroupOrIdentity.ToLowerInvariant());

            // If there is a matching group, return it
            if (null != group)
                return CreateGroupObject(group);

            NameValueCollection openIdClientArgs = new NameValueCollection();

            OpenIdClient openIdClient = new OpenIdClient(openIdClientArgs);
            openIdClient.Identity = nameOrGroupOrIdentity;
            openIdClient.TrustRoot = null;

            openIdClient.ReturnUrl = new Uri(string.Format("http://" + FileHandlerFactoryLocator.HostnameAndPort));

            // The proper identity is encoded in the URL
            Uri requestUri = openIdClient.CreateRequest(false, false);

            if (openIdClient.ErrorState == ErrorCondition.NoErrors)
                if (openIdClient.IsValidIdentity())
                {
                    RequestParameters openIdRequestParameters = new RequestParameters(requestUri.Query.Substring(1));
                    string identity = openIdRequestParameters["openid.identity"];

                    return GetOpenIdUser(identity);
                }

            throw new UnknownUser(nameOrGroupOrIdentity + " is not a known user, group, or OpenId");
        }

        public IUser GetUser(string name, string password)
        {
            IUsers_Readable user = GetUserInt(name);

            string passwordMD5 = CreateMD5(password);

            if (!user.PasswordMD5.Equals(passwordMD5))
                throw new WrongPasswordException("Incorrect password");

            IUser toReturn = CreateUserObject(user);

            return toReturn;
        }

        public IEnumerable<IUserOrGroup> GetUsersAndGroups(IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds)
        {
            foreach (IUsers_Readable user in DatabaseConnection.Users.Select(Users_Table.ID.In(userOrGroupIds)))
                yield return CreateUserObject(user);

            foreach (IGroups_Readable group in DatabaseConnection.Groups.Select(Groups_Table.ID.In(userOrGroupIds)))
                yield return CreateGroupObject(group);
        }

        public IEnumerable<IUser> GetUsersAndResolveGroupsToUsers(IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds)
        {
            Set<IUser> toReturn = new Set<IUser>();

            foreach (IUsers_Readable user in DatabaseConnection.Users.Select(Users_Table.ID.In(userOrGroupIds)))
                toReturn.Add(CreateUserObject(user));

            foreach (ID<IUserOrGroup, Guid> userOrGroupId in userOrGroupIds)
                foreach (IUser user in GetUsersInGroup(userOrGroupId))
                    toReturn.Add(user);

            return toReturn;
        }

        /// <summary>
        /// Creates an MD5 for a password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        private static string CreateMD5(string password)
        {
            string saltedPassword = string.Format(PasswordSalt, password);
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(saltedPassword);

            byte[] passwordHash = HashAlgorithm.ComputeHash(passwordBytes);

            return Convert.ToBase64String(passwordHash);
        }

        /// <summary>
        /// The salt for the password.  Insert a {0} where the password goes
        /// </summary>
        private static string PasswordSalt = "{0} objectCloud!!!!salt {0}xyzbhjkbk {0} {0} {0} !!!!!!!!!";

        /// <summary>
        /// The object that calculates the MD5
        /// </summary>
        private static HashAlgorithm HashAlgorithm = new System.Security.Cryptography.MD5CryptoServiceProvider();

        /// <summary>
        /// Creates the user object
        /// </summary>
        /// <param name="userFromDB"></param>
        /// <returns></returns>
        private IUser CreateUserObject(IUsers_Readable userFromDB)
        {
            IUser toReturn = new User(userFromDB.ID, userFromDB.Name, userFromDB.BuiltIn, !("openid".Equals(userFromDB.PasswordMD5)), FileHandlerFactoryLocator);

            return toReturn;
        }

        /// <summary>
        /// Creates the group object
        /// </summary>
        /// <param name="groupFromDB"></param>
        /// <returns></returns>
        private IGroup CreateGroupObject(IGroups_Readable groupFromDB)
        {
            return new Group(
                groupFromDB.OwnerID,
                groupFromDB.ID,
                groupFromDB.Name,
                groupFromDB.BuiltIn,
                groupFromDB.Automatic,
                groupFromDB.Type,
                FileHandlerFactoryLocator);
        }

        /// <summary>
        /// Creates the group object
        /// </summary>
        /// <param name="groupFromDB"></param>
        /// <returns></returns>
        private IGroupAndAlias CreateGroupAndAliasObject(IGroups_Readable groupFromDB, IGroupAliases_Readable groupAliasFromDB)
        {
            return new GroupAndAlias(
                groupFromDB.OwnerID,
                groupFromDB.ID,
                groupFromDB.Name,
                groupFromDB.BuiltIn,
                groupFromDB.Automatic,
                groupFromDB.Type,
                groupAliasFromDB != null ? groupAliasFromDB.Alias : null,
                FileHandlerFactoryLocator);
        }

        public void DeleteUser(string name)
        {
            name = name.ToLowerInvariant();

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.Name == name);

                if (null == user)
                    throw new UnknownUser("Unknown user");

                if (user.BuiltIn)
                    throw new CanNotDeleteBuiltInUserOrGroup();

                // Delete user's old files
                IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();
                usersDirectory.DeleteFile(null, name + ".user");
                usersDirectory.DeleteFile(null, name);

                DatabaseConnection.GroupAliases.Delete(GroupAliases_Table.UserID == user.ID);
                DatabaseConnection.UserInGroups.Delete(UserInGroups_Table.UserID == user.ID);
                DatabaseConnection.Users.Delete(Users_Table.ID == user.ID);

                transaction.Commit();
            });
        }

        public void DeleteGroup(string name)
        {
            name = name.ToLowerInvariant();

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.Name == name);

                if (null == group)
                    throw new UnknownUser("Unknown group");

                if (group.BuiltIn)
                    throw new CanNotDeleteBuiltInUserOrGroup();

                IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();
                usersDirectory.DeleteFile(null, name + ".group");

                DatabaseConnection.GroupAliases.Delete(GroupAliases_Table.GroupID == group.ID);
                DatabaseConnection.UserInGroups.Delete(UserInGroups_Table.GroupID == group.ID);
                DatabaseConnection.Groups.Delete(Groups_Table.ID == group.ID);

                transaction.Commit();
            });
        }

        public IUser Root
        {
            get
            {
                if (null == _Root)
                    _Root = GetUser("root");

                return _Root;
            }
        }
        private IUser _Root;

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            if (Root.Id != userId)
                throw new SecurityException("Only root can dump the user database");

            throw new NotImplementedException();

            /*
            using (XmlWriter xmlWriter = XmlWriter.Create(path))
            {
                xmlWriter.WriteStartDocument();

                foreach (IUsers_Readable user in DatabaseConnection.Users.Select())
                {
                    xmlWriter.WriteStartElement("User");

                    xmlWriter.WriteAttributeString("ID", user.ID.Value.ToString());
                    xmlWriter.WriteAttributeString("Name", user.Name);
                    xmlWriter.WriteAttributeString("PasswordMD5", user.PasswordMD5);

                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndDocument();

                xmlWriter.Flush();
                xmlWriter.Close();
            }*/
        }

        public void Restore(XmlReader xmlReader, ID<IUserOrGroup, Guid> userId)
        {
            int depth = xmlReader.Depth;

            do
            {
                xmlReader.Read();

                if ("User".Equals(xmlReader.Name))
                {
                    string idString = xmlReader.GetAttribute("ID");
                    string name = xmlReader.GetAttribute("Name");
                    string passwordMD5 = xmlReader.GetAttribute("PasswordMD5");

                    ID<IUserOrGroup, Guid> id = new ID<IUserOrGroup, Guid>(new Guid(idString));

                    DatabaseConnection.Users.Insert(delegate(IUsers_Writable user)
                    {
                        user.ID = id;
                        user.Name = name;
                        user.PasswordMD5 = passwordMD5;
                    });
                }

            } while (xmlReader.Depth >= depth);
        }

        public IUser GetOpenIdUser(string openIdIdentity)
        {
            Uri openIdUri = new Uri(openIdIdentity);
            openIdIdentity = openIdUri.AbsoluteUri;

            return DatabaseConnection.CallOnTransaction<IUser>(delegate(IDatabaseTransaction transaction)
            {
                IUsers_Readable user = DatabaseConnection.Users.SelectSingle(Users_Table.Name == openIdIdentity);

                if (null == user)
                {
                    ID<IUserOrGroup, Guid> userId = new ID<IUserOrGroup, Guid>(Guid.NewGuid());

                    DatabaseConnection.Users.Insert(delegate(IUsers_Writable newUser)
                    {
                        newUser.Name = openIdIdentity;
                        newUser.PasswordMD5 = "openid";
                        newUser.ID = userId;
                        newUser.BuiltIn = false;
                    });

                    transaction.Commit();

                    IUser toReturn = new User(userId, openIdIdentity, false, false, FileHandlerFactoryLocator);

                    return toReturn;
                }

                return CreateUserObject(user);
            });
        }

        public string CreateAssociationHandle(ID<IUser, Guid> userId)
        {
            byte[] associationHandleBytes = new byte[64];
            SRandom.NextBytes(associationHandleBytes);

            string associationHandle = Convert.ToBase64String(associationHandleBytes);

            DatabaseConnection.AssociationHandles.Insert(delegate(IAssociationHandles_Writable newHandle)
            {
                newHandle.UserID = userId;
                newHandle.AssociationHandle = associationHandle;
                newHandle.Timestamp = DateTime.UtcNow;
            });

            return associationHandle;
        }

        public bool VerifyAssociationHandle(ID<IUser, Guid> userId, string associationHandle)
        {
            // First, delete all old associations

            DateTime maxAssociationAge = DateTime.Now.AddMinutes(-1);

            DatabaseConnection.AssociationHandles.Delete(AssociationHandles_Table.Timestamp <= maxAssociationAge);

            // Now, see if the association is valid
            return DatabaseConnection.CallOnTransaction<bool>(delegate(IDatabaseTransaction transaction)
            {
                // Now, see if the handle is valid
                IAssociationHandles_Readable existingHandle = DatabaseConnection.AssociationHandles.SelectSingle(
                    AssociationHandles_Table.UserID == userId & AssociationHandles_Table.AssociationHandle == associationHandle);

                bool isValid = existingHandle != null;

                // Only allow an association handle to be used once, for security reasons
                if (isValid)
                {
                    DatabaseConnection.AssociationHandles.Delete(
                        AssociationHandles_Table.AssociationHandle == associationHandle & AssociationHandles_Table.UserID == userId);

                    transaction.Commit();
                }

                return isValid;
            });
        }

        public void AddUserToGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.ID == groupId);

                if (null == group)
                    throw new UnknownUser("Unknown group");

                if (group.Automatic)
                    throw new CanNotEditMembershipOfSystemGroup();

                // Only insert if there isn't already a matching entry
                if (null == DatabaseConnection.UserInGroups.SelectSingle(
                    UserInGroups_Table.UserID == userId & UserInGroups_Table.GroupID == groupId))
                {
                    DatabaseConnection.UserInGroups.Insert(delegate(IUserInGroups_Writable userInGroup)
                    {
                        userInGroup.GroupID = groupId;
                        userInGroup.UserID = userId;
                    });

                    transaction.Commit();
                }
            });

            GroupIdsThatUserIsInCache.Remove(userId);
        }

        public void RemoveUserFromGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                IGroups_Readable group = DatabaseConnection.Groups.SelectSingle(Groups_Table.ID == groupId);

                if (null == group)
                    throw new UnknownUser("Unknown group");

                if (group.Automatic)
                    throw new CanNotEditMembershipOfSystemGroup();

                DatabaseConnection.UserInGroups.Delete(UserInGroups_Table.UserID == userId & UserInGroups_Table.GroupID == groupId);

                transaction.Commit();
            });

            GroupIdsThatUserIsInCache.Remove(userId);
        }

        /// <summary>
        /// Cache of groups that a user is in, this speeds access to files
        /// </summary>
        Cache<ID<IUserOrGroup, Guid>, ICollection<ID<IUserOrGroup, Guid>>> GroupIdsThatUserIsInCache;

        private ICollection<ID<IUserOrGroup, Guid>> GetGroupIdsThatUserIsInForCache(ID<IUserOrGroup, Guid> userId)
        {
            List<ID<IUserOrGroup, Guid>> toReturn = new List<ID<IUserOrGroup, Guid>>();

            foreach (IUserInGroups_Readable userInGroup in DatabaseConnection.UserInGroups.Select(UserInGroups_Table.UserID == userId))
                toReturn.Add(userInGroup.GroupID);

            return toReturn;
        }

        public IEnumerable<ID<IUserOrGroup, Guid>> GetGroupIdsThatUserIsIn(ID<IUserOrGroup, Guid> userId)
        {
            return GroupIdsThatUserIsInCache[userId];
        }

        public IEnumerable<IGroupAndAlias> GetGroupsThatUserIsIn(ID<IUserOrGroup, Guid> userId)
        {
            List<IGroups_Readable> groupsFromDB = new List<IGroups_Readable>();
            Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable> groupAliasesFromDB = new Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable>();

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                groupsFromDB.AddRange(DatabaseConnection.Groups.Select(Groups_Table.ID.In(GroupIdsThatUserIsInCache[userId])));

                foreach (IGroupAliases_Readable groupAliasFromDB in DatabaseConnection.GroupAliases.Select(GroupAliases_Table.UserID == userId))
                    groupAliasesFromDB[groupAliasFromDB.GroupID] = groupAliasFromDB;
            });

            // Only return non-personal groups
            foreach (IGroups_Readable groupfromDB in groupsFromDB)
                if (groupfromDB.Type > GroupType.Personal)
                {
                    IGroupAliases_Readable groupAliasFromDB = null;
                    groupAliasesFromDB.TryGetValue(groupfromDB.ID, out groupAliasFromDB);

                    yield return CreateGroupAndAliasObject(groupfromDB, groupAliasFromDB);
                }
        }

        public IEnumerable<IGroup> GetAllGroups()
        {
            foreach (IGroups_Readable group in DatabaseConnection.Groups.Select())
                yield return CreateGroupObject(group);
        }

        public IEnumerable<IGroupAndAlias> GetAllGroups(ID<IUserOrGroup, Guid> userId)
        {
            List<IGroups_Readable> groupsFromDB = new List<IGroups_Readable>();
            Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable> groupAliasesFromDB = new Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable>();

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                groupsFromDB.AddRange(DatabaseConnection.Groups.Select());

                List<ID<IUserOrGroup, Guid>> groupIds = new List<ID<IUserOrGroup, Guid>>();
                foreach (IGroups_Readable groupFromDB in groupsFromDB)
                    groupIds.Add(groupFromDB.ID);

                foreach (IGroupAliases_Readable groupAliasFromDB in DatabaseConnection.GroupAliases.Select(
                    GroupAliases_Table.GroupID.In(groupIds) & GroupAliases_Table.UserID == userId))
                {
                    groupAliasesFromDB[groupAliasFromDB.GroupID] = groupAliasFromDB;
                }
            });

            foreach (IGroups_Readable groupfromDB in groupsFromDB)
            {
                IGroupAliases_Readable groupAliasFromDB = null;
                groupAliasesFromDB.TryGetValue(groupfromDB.ID, out groupAliasFromDB);

                yield return CreateGroupAndAliasObject(groupfromDB, groupAliasFromDB);
            }
        }

        public IEnumerable<IGroupAndAlias> GetGroupsThatUserOwns(ID<IUserOrGroup, Guid> userId)
        {
            /*foreach (IGroups_Readable groupFromDB in DatabaseConnection.Groups.Select(Groups_Table.OwnerID == userId))
                yield return CreateGroupObject(groupFromDB);*/

            List<IGroups_Readable> groupsFromDB = new List<IGroups_Readable>();
            Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable> groupAliasesFromDB = new Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable>();

            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                groupsFromDB.AddRange(DatabaseConnection.Groups.Select(Groups_Table.OwnerID == userId));

                List<ID<IUserOrGroup, Guid>> groupIds = new List<ID<IUserOrGroup, Guid>>();
                foreach (IGroups_Readable groupFromDB in groupsFromDB)
                    groupIds.Add(groupFromDB.ID);

                foreach (IGroupAliases_Readable groupAliasFromDB in DatabaseConnection.GroupAliases.Select(
                    GroupAliases_Table.GroupID.In(groupIds) & GroupAliases_Table.UserID == userId))
                {
                    groupAliasesFromDB[groupAliasFromDB.GroupID] = groupAliasFromDB;
                }
            });

            foreach (IGroups_Readable groupfromDB in groupsFromDB)
            {
                IGroupAliases_Readable groupAliasFromDB = null;
                groupAliasesFromDB.TryGetValue(groupfromDB.ID, out groupAliasFromDB);

                yield return CreateGroupAndAliasObject(groupfromDB, groupAliasFromDB);
            }
        }

        public IEnumerable<IUser> GetUsersInGroup(ID<IUserOrGroup, Guid> groupId)
        {
            List<Guid> userIds = new List<Guid>();
            foreach (IUserInGroups_Readable userInGroup in DatabaseConnection.UserInGroups.Select(UserInGroups_Table.GroupID == groupId))
                userIds.Add(userInGroup.UserID.Value);

            foreach (IUsers_Readable user in DatabaseConnection.Users.Select(Users_Table.ID.In(userIds)))
                yield return CreateUserObject(user);
        }

        public void SetPassword(ID<IUserOrGroup, Guid> userId, string password)
        {
            string passwordMD5 = CreateMD5(password);

            DatabaseConnection.Users.Update(Users_Table.ID == userId,
                delegate(IUsers_Writable user)
                {
                    user.PasswordMD5 = passwordMD5;
                });
        }

        /// <summary>
        /// Returns true if the user is in the group
        /// </summary>
        /// <param name="userId">UserId</param>
        /// <param name="groupId">GroupId</param>
        /// <returns></returns>
        public bool IsUserInGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId)
        {
            return GroupIdsThatUserIsInCache[userId].Contains(groupId);
        }

        public void SetGroupAlias(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId, string alias)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                // Setting the alias to be the same as the group name should delete it
                IGroup group = GetGroup(groupId);
                if (alias == group.Name)
                    alias = null;

                // update or insert if the alias isn't null
                if (null != alias)
                {
                    // Make sure that the user has permission to set the alias
                    bool hasPermission = false;
                    if (null != DatabaseConnection.UserInGroups.Select(UserInGroups_Table.UserID == userId & UserInGroups_Table.GroupID == groupId))
                        hasPermission = true;
                    else if (null != DatabaseConnection.Groups.Select(Groups_Table.ID == groupId & Groups_Table.Type != GroupType.Personal))
                        hasPermission = true;

                    if (!hasPermission)
                        throw new SecurityException("Invalid group");

                    if (null == DatabaseConnection.GroupAliases.SelectSingle(GroupAliases_Table.UserID == userId & GroupAliases_Table.GroupID == groupId))
                        DatabaseConnection.GroupAliases.Insert(delegate(IGroupAliases_Writable groupAliasWritable)
                        {
                            groupAliasWritable.Alias = alias;
                            groupAliasWritable.GroupID = groupId;
                            groupAliasWritable.UserID = userId;
                        });
                    else
                        DatabaseConnection.GroupAliases.Update(
                            GroupAliases_Table.UserID == userId & GroupAliases_Table.GroupID == groupId,
                            delegate(IGroupAliases_Writable groupAliasWritable)
                            {
                                groupAliasWritable.Alias = alias;
                            });
                }
                else
                    DatabaseConnection.GroupAliases.Delete(GroupAliases_Table.UserID == userId & GroupAliases_Table.GroupID == groupId);

                transaction.Commit();
            });
        }

        public IGroupAndAlias GetGroupAndAlias(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId)
        {
            IGroupAliases_Readable groupAliasFromDB =
                DatabaseConnection.GroupAliases.SelectSingle(GroupAliases_Table.UserID == userId & GroupAliases_Table.GroupID == groupId);

            IGroups_Readable groupFromDB = DatabaseConnection.Groups.SelectSingle(Groups_Table.ID == groupId);

            return CreateGroupAndAliasObject(groupFromDB, groupAliasFromDB);
        }

        public IEnumerable<IUserOrGroup> SearchUsersAndGroups(string query, uint? max)
        {
            uint returned = 0;

            foreach (IUsers_Readable userFromDB in DatabaseConnection.Users.Select(Users_Table.Name.Like(query), max, ObjectCloud.ORM.DataAccess.OrderBy.Asc))
            {
                returned++;
                yield return CreateUserObject(userFromDB);
            }

            if (null != max)
                max = max.Value - returned;

            Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable> groupsIdsMatchedByAlias = new Dictionary<ID<IUserOrGroup, Guid>, IGroupAliases_Readable>();
            foreach (IGroupAliases_Readable groupAliasFromDB in DatabaseConnection.GroupAliases.Select(GroupAliases_Table.Alias.Like(query)))
                groupsIdsMatchedByAlias[groupAliasFromDB.GroupID] = groupAliasFromDB;

            Dictionary<ID<IUserOrGroup, Guid>, IGroups_Readable> groupsFromDBMatchingQuery = new Dictionary<ID<IUserOrGroup, Guid>, IGroups_Readable>();

            foreach (IGroups_Readable groupFromDB in DatabaseConnection.Groups.Select(
                (Groups_Table.Type != GroupType.Personal & Groups_Table.Name.Like(query)) | (Groups_Table.ID.In(groupsIdsMatchedByAlias.Keys)), max, ObjectCloud.ORM.DataAccess.OrderBy.Asc))
                groupsFromDBMatchingQuery[groupFromDB.ID] = groupFromDB;

            foreach (IGroupAliases_Readable groupAliasFromDB in DatabaseConnection.GroupAliases.Select(GroupAliases_Table.GroupID.In(groupsFromDBMatchingQuery.Keys)))
                groupsIdsMatchedByAlias[groupAliasFromDB.GroupID] = groupAliasFromDB;

            foreach (IGroups_Readable groupFromDB in groupsFromDBMatchingQuery.Values)
            {
                IGroupAliases_Readable groupAliasFromDB = null;
                groupsIdsMatchedByAlias.TryGetValue(groupFromDB.ID, out groupAliasFromDB);

                yield return CreateGroupAndAliasObject(groupFromDB, groupAliasFromDB);
            }
        }
    }
}
