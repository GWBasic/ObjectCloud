// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

using Common.Logging;
using ExtremeSwank.OpenId;

using ObjectCloud.Common;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.FileHandlers
{
    public partial class UserManagerHandler : FileHandler, IUserManagerHandler
    {
        private static ILog log = LogManager.GetLogger<UserManagerHandler>();
		
		internal class UserManagerData
		{
			public Dictionary<ID<IUserOrGroup, Guid>, UserInt> users = new Dictionary<ID<IUserOrGroup, Guid>, UserInt>();
			public Dictionary<ID<IUserOrGroup, Guid>, GroupInt> groups = new Dictionary<ID<IUserOrGroup, Guid>, GroupInt>();
			public Dictionary<string, UserBase> byName = new Dictionary<string, UserBase>();
			public Dictionary<string, Sender> sendersByToken = new Dictionary<string, Sender>();
			public Dictionary<string, Sender> sendersByIdentity = new Dictionary<string, Sender>();
			
			public UserInt GetUser(ID<IUserOrGroup, Guid> userId)
			{
				UserInt user;
				if (!this.users.TryGetValue(userId, out user))
					throw new UnknownUser("Unknown user");
				
				return user;
			}
			
			public GroupInt GetGroup(ID<IUserOrGroup, Guid> groupId)
			{
				GroupInt group;
				if (!this.groups.TryGetValue(groupId, out group))
	                throw new UnknownUser("Unknown group");
				
				return group;
			}
		}
		
		internal class UserBase
		{
			public ID<IUserOrGroup, Guid> id;
			public string name;
			public bool builtIn;
			public string displayName;
			
			public override int GetHashCode ()
			{
				return this.id.GetHashCode();
			}
			
			public override bool Equals (object obj)
			{
				return this == obj;
			}
			
			public override string ToString ()
			{
				return this.name;
			}
		}
		
		internal class UserInt : UserBase
		{
			public byte[] passwordMD5;
			public int identityProviderCode;
			public string identityProviderArgs;
			public Dictionary<string, DateTime> associationHandles = new Dictionary<string, DateTime>();
			public HashSet<GroupInt> groups = new HashSet<GroupInt>();
			public Dictionary<string, string> receiveNotificationEndpointsBySenderToken = new Dictionary<string, string>();
			public Dictionary<string, string> receiveNotificationSenderTokensByEndpoint = new Dictionary<string, string>();
		}
		
		internal class GroupInt : UserBase
		{
			public UserInt owner;
			public bool automatic;
			public GroupType type;
			public HashSet<UserInt> users = new HashSet<UserInt>();
			public Dictionary<UserInt, string> aliases = new Dictionary<UserInt, string>();
		}
		
		internal class Sender
		{
			public string identity;
			public string token;
			public string loginURL;
			public string loginURLOpenID;
			public string loginURLWebFinger;
			public string loginURLRedirect;
			//long senderID;
		}

        internal UserManagerHandler(PersistedObject<UserManagerData> persistedUserManagerData, FileHandlerFactoryLocator fileHandlerFactoryLocator, int? maxLocalUsers)
            : base(fileHandlerFactoryLocator) 
        {
            this.MaxLocalUsers = maxLocalUsers;
			this.persistedUserManagerData = persistedUserManagerData;
        }

        public int? MaxLocalUsers { get; set; }
		
		private PersistedObject<UserManagerData> persistedUserManagerData;

        public IUser CreateUser(string name, string password, string displayName)
        {
            return CreateUser(name, password, displayName, new ID<IUserOrGroup, Guid>(Guid.NewGuid()), false);
        }

        public IUser CreateUser(string name, string displayName, string identityProviderArgs, IIdentityProvider identityProvider)
        {
            return CreateUser(
                name,
                null,
                displayName,
                new ID<IUserOrGroup, Guid>(Guid.NewGuid()),
                false,
                identityProviderArgs,
                identityProvider);
        }

        public IUser CreateUser(string name, string password, string displayName, ID<IUserOrGroup, Guid> userId, bool builtIn)
        {
            return CreateUser(
                name.ToLowerInvariant(),
                password,
                displayName,
                userId,
                builtIn,
                null,
                FileHandlerFactoryLocator.LocalIdentityProvider);
        }

        public IUser CreateUser(
			string name,
            string password,
            string displayName,
            ID<IUserOrGroup, Guid> userId,
            bool builtIn,
            string identityProviderArgs,
            IIdentityProvider identityProvider)
        {
			name = name.ToLowerInvariant();
			
            if (null != MaxLocalUsers)
                if (this.GetTotalLocalUsers() >= MaxLocalUsers.Value)
                    throw new MaximumUsersExceeded("The maximum number of users allowed on this server is met: " + MaxLocalUsers.Value.ToString());

            IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();

            byte[] passwordMD5;
            if (null != password)
                passwordMD5 = UserManagerHandler.CreateMD5(password);
			else
				passwordMD5 = null;

            IUserHandler newUser;
            IUser userObj = null;
			
			this.persistedUserManagerData.WriteReentrant(userManagerData =>
			{
				this.ThrowExceptionIfDuplicate(userManagerData, name);

                if (identityProvider == FileHandlerFactoryLocator.LocalIdentityProvider)
                {
                    if (usersDirectory.IsFilePresent(name))
                        throw new UserAlreadyExistsException("There is a pre-existing directory for " + name);

                    if (usersDirectory.IsFilePresent(name + " .user"))
                        throw new UserAlreadyExistsException("There is a pre-existing " + name + ".user");
                }
				
				var user = new UserInt()
				{
                    name = name,
					id = userId,
                    passwordMD5 = passwordMD5,
                    builtIn = builtIn,
                    displayName = displayName,
                    identityProviderCode = identityProvider.IdentityProviderCode,
                    identityProviderArgs = identityProviderArgs
				};
				
				userManagerData.users[userId] = user;
				userManagerData.byName[name] = user;
                userObj = this.CreateUserObject(user);
	
	            if (identityProvider == FileHandlerFactoryLocator.LocalIdentityProvider)
	            {
	                try
	                {
	                    // Careful here!!!  When calling the constructor of the user's .user object or the user's directory, a transaction will
	                    // be created against the user database!  That can cause a deadlock!
	                    usersDirectory.CreateFile(name, "directory", userObj.Id);
	
	                    string userFileName = name + ".user";
	                    newUser = usersDirectory.CreateSystemFile<IUserHandler>(userFileName, "user", userObj.Id);
	                    newUser.Name = name;
	                    usersDirectory.SetPermission(
	                        null,
	                        userFileName,
	                        new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
	                        FilePermissionEnum.Read,
	                        false,
	                        false);
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

						throw;
	                }
	
	                if (!builtIn)
	                    this.CreateGroupInt(
							userManagerData,
							"friends",
							displayName + "'s friends",
							userObj.Id,
							new ID<IUserOrGroup, Guid>(Guid.NewGuid()),
							false,
							false,
							GroupType.Personal);
				}
            });

            return userObj;
        }

        public IGroup CreateGroup(
            string name,
            string displayName,
            ID<IUserOrGroup, Guid>? ownerId,
            GroupType groupType)
        {
            return CreateGroup(name, displayName, ownerId, new ID<IUserOrGroup, Guid>(Guid.NewGuid()), false, false, groupType);
        }

        public IGroup CreateGroup(
            string name,
            string displayName,
            ID<IUserOrGroup, Guid>? ownerId,
            ID<IUserOrGroup, Guid> groupId,
            bool builtIn,
            bool automatic,
            GroupType groupType)
        {
            if (GroupType.Personal == groupType && null == ownerId)
                throw new ArgumentException("Personal groups must have a declared owner");

			IGroup toReturn = null;
			this.persistedUserManagerData.WriteReentrant(userManagerData =>
				toReturn = this.CreateGroupInt(userManagerData, name, displayName, ownerId, groupId, builtIn, automatic, groupType));
			
			return toReturn;
        }
		
		private IGroup CreateGroupInt(
			UserManagerData userManagerData,
            string name,
            string displayName,
            ID<IUserOrGroup, Guid>? ownerId,
            ID<IUserOrGroup, Guid> groupId,
            bool builtIn,
            bool automatic,
            GroupType groupType)
		{
            name = name.ToLowerInvariant();
			
			if (groupType >= GroupType.Private)
				this.ThrowExceptionIfDuplicate(userManagerData, name);
			
			UserInt owner = null;
			if (null != ownerId)
				owner = userManagerData.GetUser(ownerId.Value);
			
			var group = new GroupInt()
			{
				name = groupType > GroupType.Personal ? name : groupId.ToString(),
				id = groupId,
                owner = owner,
                builtIn = builtIn,
                automatic = automatic,
                type = groupType,
                displayName = displayName
			};
			
			if (GroupType.Personal == groupType)
				group.aliases[owner] = name;
			
			userManagerData.groups[groupId] = group;

			if (groupType != GroupType.Personal)
				userManagerData.byName[name] = group;
			
			var groupObj = this.CreateGroupObject(group);
			
            IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();
            string groupFileName = name + ".group";
			
            if (!automatic)
            {
                // Decide where the object goes, for personal groups in the user's directory, for system groups in the users directory
                IDirectoryHandler groupObjectDestinationDirectory;
                if (groupType == GroupType.Personal)
                    groupObjectDestinationDirectory = usersDirectory.OpenFile(owner.name).CastFileHandler<IDirectoryHandler>();
                else
                    groupObjectDestinationDirectory = usersDirectory;

                INameValuePairsHandler groupDB;
                try
                {
                    groupDB = groupObjectDestinationDirectory.CreateFile(groupFileName, "group", ownerId).FileContainer.CastFileHandler<INameValuePairsHandler>(); ;
                }
                catch (DuplicateFile)
                {
                    throw new UserAlreadyExistsException(name + " already exists");
                }

				IUser ownerObj = null;
				if (null != owner)
					ownerObj = this.CreateUserObject(owner);
				
                groupObjectDestinationDirectory.SetPermission(
                    ownerObj, 
                    groupFileName, 
                    new ID<IUserOrGroup, Guid>[] { groupId }, 
                    FilePermissionEnum.Read, 
                    true, 
                    true);

                // Everyone can read a public group
                if (GroupType.Public == groupType)
                    usersDirectory.SetPermission(
                        ownerObj, 
                        groupFileName, 
                        new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id }, 
                        FilePermissionEnum.Read, 
                        true, 
                        false);

                groupDB.Set(ownerObj, "GroupId", groupId.Value.ToString());
            }
			
			log.Info("Created group: " + name);

            return groupObj;
		}

        /// <summary>
        /// Throws a UserAlreadyExistsException if there is a user or group with the same name
        /// </summary>
        /// <param name="name">
        /// A <see cref="System.String"/>
        /// </param>
        private void ThrowExceptionIfDuplicate(UserManagerData userManagerData, string name)
        {
			if (userManagerData.byName.ContainsKey(name))
                throw new UserAlreadyExistsException("Duplicate user: " + name);
        }

        public IUser GetUserNoException(string nameOrGroupOrIdentity)
        {
            nameOrGroupOrIdentity = this.FilterIdentityToLocalNameIfNeeded(nameOrGroupOrIdentity);

			return this.persistedUserManagerData.Read(userManagerData =>
			{
				UserBase userObj;
				UserInt user = null;
				
				if (userManagerData.byName.TryGetValue(nameOrGroupOrIdentity, out userObj))
					user = userObj as UserInt;
				
				if (null == user)
					if (userManagerData.byName.TryGetValue(nameOrGroupOrIdentity.ToLowerInvariant(), out userObj))
						user = userObj as UserInt;

	            if (null == user)
    	            return null;

        	    return this.CreateUserObject(user);
			});
        }

        private UserInt GetUserInt(string nameOrGroupOrIdentity)
        {
            nameOrGroupOrIdentity = FilterIdentityToLocalNameIfNeeded(nameOrGroupOrIdentity);

			return this.persistedUserManagerData.Read(userManagerData =>
			{
				UserBase userObj;
				UserInt user = null;
				
				if (userManagerData.byName.TryGetValue(nameOrGroupOrIdentity, out userObj))
					user = userObj as UserInt;
				
				if (null == user)
					if (userManagerData.byName.TryGetValue(nameOrGroupOrIdentity.ToLowerInvariant(), out userObj))
						user = userObj as UserInt;

	            if (null == user)
    	            throw new UnknownUser("Unknown user");

				return user;
			});			
        }

        /// <summary>
        /// Filter formatting that a local user would use for various identity federation schemes
        /// </summary>
        /// <param name="nameOrGroupOrIdentity"></param>
        /// <returns></returns>
        private string FilterIdentityToLocalNameIfNeeded(string nameOrGroupOrIdentity)
        {
            foreach (IIdentityProvider identityProvider in FileHandlerFactoryLocator.IdentityProviders.Values)
                nameOrGroupOrIdentity = identityProvider.FilterIdentityToLocalNameIfNeeded(nameOrGroupOrIdentity);

            return nameOrGroupOrIdentity;
        }

        public IUser GetUser(ID<IUserOrGroup, Guid> userId)
        {
            IUser toReturn = GetUserNoException(userId);
            if (null != toReturn)
                return toReturn;

            throw new UnknownUser("Unknown user");
        }

        public IUser GetUserNoException(ID<IUserOrGroup, Guid> userId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				UserInt user;
				if (userManagerData.users.TryGetValue(userId, out user))
	        	    return this.CreateUserObject(user);
				
				return null;
			});
        }

        public IGroup GetGroup(string name)
        {
            // Allow /Users/[username].user
            if (name.StartsWith("/Users/") && name.EndsWith(".group"))
            {
                name = name.Substring(7);
                name = name.Substring(0, name.Length - 6);
				
				// If this is a personal group, then the name must be loaded
				if (name.Contains("/"))
				{
					string filename = "/Users/" + name + ".group";
					IFileContainer groupFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
					INameValuePairsHandler groupData = groupFileContainer.CastFileHandler<INameValuePairsHandler>();
					
					name = groupData["GroupId"];
				}
            }

			return this.persistedUserManagerData.Read(userManagerData =>
			{
				UserBase groupObject;
				if (userManagerData.byName.TryGetValue(name, out groupObject))
					if (groupObject is GroupInt)
						return this.CreateGroupObject((GroupInt)groupObject);
			
                throw new UnknownUser("Unknown group: " + name);
			});
        }

        public IGroup GetGroup(ID<IUserOrGroup, Guid> groupId)
        {
            var group = this.GetGroupInt(groupId);

            return this.CreateGroupObject(group);
        }

        private GroupInt GetGroupInt(ID<IUserOrGroup, Guid> groupId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				return userManagerData.GetGroup(groupId);
			});
		}

        public IUserOrGroup GetUserOrGroup(ID<IUserOrGroup, Guid> userOrGroupId)
        {
			return this.persistedUserManagerData.Read<IUserOrGroup>(userManagerData =>
			{
				UserInt user;
				if (userManagerData.users.TryGetValue(userOrGroupId, out user))
					return this.CreateUserObject(user);
				
				GroupInt group;
				if (userManagerData.groups.TryGetValue(userOrGroupId, out group))
					return this.CreateGroupObject(group);

				throw new UnknownUser("Unknown user or group");
			});
        }

        public IUserOrGroup GetUserOrGroupNoException(ID<IUserOrGroup, Guid> userOrGroupId)
        {
			return this.persistedUserManagerData.Read<IUserOrGroup>(userManagerData =>
			{
				UserInt user;
				if (userManagerData.users.TryGetValue(userOrGroupId, out user))
					return this.CreateUserObject(user);
				
				GroupInt group;
				if (userManagerData.groups.TryGetValue(userOrGroupId, out group))
					return this.CreateGroupObject(group);

				return null;
			});
        }

        public IUser GetUser(string name)
        {
            var user = this.GetUserInt(name);

            return this.CreateUserObject(user);
        }

        public IUserOrGroup GetUserOrGroupOrOpenId(string nameOrGroupOrIdentity)
        {
            return this.GetUserOrGroupOrOpenId(nameOrGroupOrIdentity, false);
        }

        public IUserOrGroup GetUserOrGroupOrOpenId(string nameOrGroupOrIdentity, bool onlyInLocalDB)
        {
            nameOrGroupOrIdentity = FilterIdentityToLocalNameIfNeeded(nameOrGroupOrIdentity);

			return this.persistedUserManagerData.Read<IUserOrGroup>(userManagerData =>
			{
				UserBase userOrGroup = null;
				
				if (!userManagerData.byName.TryGetValue(nameOrGroupOrIdentity, out userOrGroup))
					userManagerData.byName.TryGetValue(nameOrGroupOrIdentity.ToLowerInvariant(), out userOrGroup);
				
				if (userOrGroup is UserInt)
					return this.CreateUserObject((UserInt)userOrGroup);
				
				if (userOrGroup is GroupInt)
					return this.CreateGroupObject((GroupInt)userOrGroup);

	            if (!onlyInLocalDB)
	                foreach (IIdentityProvider identityProvider in FileHandlerFactoryLocator.IdentityProviders.Values)
	                {
	                    IUser createdUser = identityProvider.GetOrCreateUserIfCorrectFormOfIdentity(nameOrGroupOrIdentity);
	                    if (null != createdUser)
	                        return createdUser;
	                }
	
	            throw new UnknownUser(nameOrGroupOrIdentity + " is not a known user, group, or identity");
			});
        }

        public IUser GetUser(string name, string password)
        {
            var user = GetUserInt(name);

            var passwordMD5 = UserManagerHandler.CreateMD5(password);

			if (passwordMD5.Length != user.passwordMD5.Length)
                throw new WrongPasswordException("Incorrect password");
			
			for (var ctr = 0; ctr < passwordMD5.Length; ctr++)
				if (passwordMD5[ctr] != user.passwordMD5[ctr])
	                throw new WrongPasswordException("Incorrect password");

            return this.CreateUserObject(user);
        }

        public IEnumerable<IUserOrGroup> GetUsersAndGroups(IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIdsArg)
        {
			var userOrGroupIds = userOrGroupIdsArg.ToArray();
			
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var toReturn = new List<IUserOrGroup>(userOrGroupIds.Length);
				
				foreach (var userOrGroupId in userOrGroupIds)
				{
					UserInt user;
					if (userManagerData.users.TryGetValue(userOrGroupId, out user))
						toReturn.Add(this.CreateUserObject(user));
					
					GroupInt group;
					if (userManagerData.groups.TryGetValue(userOrGroupId, out group))
						toReturn.Add(this.CreateGroupObject(group));
				}
				
				return toReturn.ToArray();
			});
        }

        /// <summary>
        /// Untested
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public IEnumerable<IUserOrGroup> GetUsersAndGroups(IEnumerable<string> namesArg)
        {
			var names = namesArg.ToArray();
			for (int ctr = 0; ctr < names.Length; ctr++)
				if (names[ctr].StartsWith("/Users/"))
					if (names[ctr].EndsWith(".user")) 
                    {
                        names[ctr] = names[ctr].Substring(7);
                        names[ctr] = names[ctr].Substring(0, names[ctr].Length - 5);
                    }
					else if (names[ctr].EndsWith(".group"))
                    {
                        names[ctr] = names[ctr].Substring(7);
                        names[ctr] = names[ctr].Substring(0, names[ctr].Length - 6);
				
						// If this is a personal group, then the name must be loaded
						if (names[ctr].Contains("/"))
						{
							string filename = "/Users/" + names[ctr] + ".group";
							IFileContainer groupFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
							INameValuePairsHandler groupData = groupFileContainer.CastFileHandler<INameValuePairsHandler>();
							
							names[ctr] = groupData["GroupId"];
						}
                    }

			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var toReturn = new List<IUserOrGroup>(names.Length);
				
				foreach (var name in names)
				{
					UserBase userBase;
					if (userManagerData.byName.TryGetValue(name, out userBase))
					{
						if (userBase is UserInt)
							toReturn.Add(this.CreateUserObject((UserInt)userBase));
						else if (userBase is GroupInt)
							toReturn.Add(this.CreateGroupObject((GroupInt)userBase));
					}
				}
				
				return toReturn;
			});
        }

        public IEnumerable<IUser> GetUsersAndResolveGroupsToUsers(IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var toReturn = new Dictionary<ID<IUserOrGroup, Guid>, IUser>();
			
				foreach (var userOrGroupId in userOrGroupIds)
				{
					UserInt user;
					if (userManagerData.users.TryGetValue(userOrGroupId, out user))
						toReturn[userOrGroupId] = this.CreateUserObject(user);
					
					if (userManagerData.groups.ContainsKey(userOrGroupId))
						foreach (var userObj in this.GetUsersInGroup(userOrGroupId))
							if (!toReturn.ContainsKey(userObj.Id))
								toReturn[userObj.Id] = userObj;
				}
				
				return toReturn.Values.ToArray();
			});
        }

        /// <summary>
        /// Creates an MD5 for a password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        private static byte[] CreateMD5(string password)
        {
            string saltedPassword = string.Format(PasswordSalt, password);
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(saltedPassword);

            return (new System.Security.Cryptography.MD5CryptoServiceProvider()).ComputeHash(passwordBytes);
        }

        /// <summary>
        /// The salt for the password.  Insert a {0} where the password goes
        /// </summary>
        private static string PasswordSalt = "{0} objectCloud!!!!salt {0}xyzbhjkbk {0} {0} {0} !!!!!!!!!";

        /// <summary>
        /// Creates the user object
        /// </summary>
        /// <param name="userFromDB"></param>
        /// <returns></returns>
        private IUser CreateUserObject(UserInt user)
        {
            IIdentityProvider identityProvider = FileHandlerFactoryLocator.IdentityProviders[user.identityProviderCode];

            IUser toReturn = identityProvider.CreateUserObject(
                FileHandlerFactoryLocator,
                user.id,
                user.name,
                user.builtIn,
                user.displayName,
                user.identityProviderArgs);

            return toReturn;
        }

        /// <summary>
        /// Creates the group object
        /// </summary>
        /// <param name="groupFromDB"></param>
        /// <returns></returns>
        private IGroup CreateGroupObject(GroupInt group)
        {
            return new Group(
                group.owner != null ? (ID<IUserOrGroup, Guid>?)group.owner.id : (ID<IUserOrGroup, Guid>?)null,
                group.id,
                group.name,
                group.builtIn,
                group.automatic,
                group.type,
                FileHandlerFactoryLocator,
                group.displayName);
        }

        /// <summary>
        /// Creates the group object
        /// </summary>
        /// <param name="groupFromDB"></param>
        /// <returns></returns>
        private IGroupAndAlias CreateGroupAndAliasObject(GroupInt group, string alias)
        {
            return new GroupAndAlias(
                group.owner.id,
                group.id,
                group.name,
                group.builtIn,
                group.automatic,
                group.type,
                alias,
                FileHandlerFactoryLocator,
                group.displayName);
        }

        public void DeleteUser(string name)
        {
            name = name.ToLowerInvariant();

			this.persistedUserManagerData.Write(userManagerData =>
			{
				UserBase userObj;
				if (!userManagerData.byName.TryGetValue(name, out userObj))
                    throw new UnknownUser("Unknown user");
				
				if (!(userObj is UserInt))
                    throw new InvalidCastException("Unknown user");
				
				var user = (UserInt)userObj;

                if (user.builtIn)
                    throw new CanNotDeleteBuiltInUserOrGroup();

                // Delete user's old files
                var usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();
                usersDirectory.DeleteFile(null, name + ".user");
                usersDirectory.DeleteFile(null, name);
				
				userManagerData.byName.Remove(name);
				userManagerData.users.Remove(user.id);
				
				foreach (var group in userManagerData.groups.Values)
					group.aliases.Remove(user);
            });
			
			// TODO: Need to delete all permissions assigned to the user!
        }

        public void DeleteGroup(string name)
        {
            name = name.ToLowerInvariant();

			this.persistedUserManagerData.Write(userManagerData =>
			{
				UserBase groupObj;
				if (!userManagerData.byName.TryGetValue(name, out groupObj))
                    throw new UnknownUser("Unknown group");
				
				if (!(groupObj is GroupInt))
                    throw new InvalidCastException("Unknown group");
				
				var group = (GroupInt)groupObj;

                if (group.builtIn)
                    throw new CanNotDeleteBuiltInUserOrGroup();

                var usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();

                try
                {
                    if (group.type > GroupType.Personal)
                        usersDirectory.DeleteFile(null, name + ".group");
                    else if (null != group.owner)
                    {
						var alias = group.aliases[group.owner];
                        var ownerDirectory = usersDirectory.OpenFile(group.owner.name).CastFileHandler<IDirectoryHandler>();
                        ownerDirectory.DeleteFile(null, alias + ".group");
                    }
                }
                catch (Exception e)
                {
                    log.Warn("Exception deleting group's object", e);
                }

				userManagerData.byName.Remove(name);
				userManagerData.groups.Remove(group.id);
            });
			
			// TODO: Need to delete all permissions assigned to the group!
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
            throw new NotImplementedException();

            /*int depth = xmlReader.Depth;

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

            } while (xmlReader.Depth >= depth);*/
        }

        public string CreateAssociationHandle(ID<IUserOrGroup, Guid> userId)
        {
            byte[] associationHandleBytes = new byte[64];
            SRandom.NextBytes(associationHandleBytes);
			
			var associationHandle = Convert.ToBase64String(associationHandleBytes);

			this.persistedUserManagerData.Write(userManagerData =>
			{
				var user = userManagerData.users[userId];
				user.associationHandles[associationHandle] = DateTime.UtcNow;
			});

            return associationHandle;
        }

        public bool VerifyAssociationHandle(ID<IUserOrGroup, Guid> userId, string associationHandle)
        {
            // First, delete all old associations

            DateTime maxAssociationAge = DateTime.Now.AddMinutes(-1);

			return this.persistedUserManagerData.Write(userManagerData =>
			{
				var user = userManagerData.GetUser(userId);
				
				// clean out old association handles
				var outdatedAssociationHandles = user.associationHandles.Where(a => a.Value < maxAssociationAge).Select(a => a.Key);
				foreach (var outdatedAssociationHandle in outdatedAssociationHandles.ToArray())
					user.associationHandles.Remove(outdatedAssociationHandle);
				
                // Only allow an association handle to be used once, for security reasons
				var isValid = user.associationHandles.Remove(associationHandle);

                return isValid;
            });
        }

        public void AddUserToGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId)
        {
			this.persistedUserManagerData.Write(userManagerData =>
			{
				var group = userManagerData.GetGroup(groupId);
				var user = userManagerData.GetUser(userId);

                if (group.automatic)
                    throw new CanNotEditMembershipOfSystemGroup();
				
				group.users.Add(user);
				user.groups.Add(group);
            });
        }

        public void RemoveUserFromGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId)
        {
			this.persistedUserManagerData.Write(userManagerData =>
			{
				var group = userManagerData.GetGroup(groupId);
				var user = userManagerData.GetUser(userId);

                if (group.automatic)
                    throw new CanNotEditMembershipOfSystemGroup();
				
				group.users.Remove(user);
				user.groups.Remove(group);
            });
        }

        public IEnumerable<ID<IUserOrGroup, Guid>> GetGroupIdsThatUserIsIn(ID<IUserOrGroup, Guid> userId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var user = userManagerData.GetUser(userId);
				
				var groupIds = new List<ID<IUserOrGroup, Guid>>(user.groups.Count);
				
				// wtf??? TODO: nulls are getting into user.groups!!!
				
				groupIds.AddRange(user.groups.Where(g => null != g).Select(g => g.id));

				return groupIds;
			});
        }

        public IEnumerable<IGroupAndAlias> GetGroupsThatUserIsIn(ID<IUserOrGroup, Guid> userId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var user = userManagerData.GetUser(userId);
				
				var groups = new List<IGroupAndAlias>(user.groups.Count);
				// wtf??? TODO: nulls are getting into user.groups!!!
				
				foreach (var group in user.groups.Where(g => null != g))
				{
					string alias = null;
					group.aliases.TryGetValue(user, out alias);
					
					groups.Add(this.CreateGroupAndAliasObject(group, alias));
				}

				return groups;
			});
        }

        public IEnumerable<IGroup> GetAllGroups()
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var groups = new List<IGroup>(userManagerData.groups.Count);
				groups.AddRange(userManagerData.groups.Values.Select(g => this.CreateGroupObject(g)));
				
				return groups;
			});
        }

        public IEnumerable<IGroupAndAlias> GetAllGroups(ID<IUserOrGroup, Guid> userId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var user = userManagerData.GetUser(userId);

				var groups = new List<IGroupAndAlias>(userManagerData.groups.Count);

				foreach (var group in userManagerData.groups.Values)
				{
					string alias = null;
					group.aliases.TryGetValue(user, out alias);
					
					groups.Add(this.CreateGroupAndAliasObject(group, alias));
				}

				return groups;
			});
        }

        public IEnumerable<IGroupAndAlias> GetGroupsThatUserOwns(ID<IUserOrGroup, Guid> userId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var user = userManagerData.GetUser(userId);

				var groups = new List<IGroupAndAlias>();

				foreach (var group in userManagerData.groups.Values.Where(g => g.owner == user))
				{
					string alias = null;
					group.aliases.TryGetValue(user, out alias);
					
					groups.Add(this.CreateGroupAndAliasObject(group, alias));
				}

				return groups;
			});
        }

        public IEnumerable<IUser> GetUsersInGroup(ID<IUserOrGroup, Guid> groupId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var group = userManagerData.GetGroup(groupId);
				
				var users = new List<IUser>(group.users.Count);
				users.AddRange(group.users.Select(u => this.CreateUserObject(u)));
				
				return users;
			});
        }

        public void SetPassword(ID<IUserOrGroup, Guid> userId, string password)
        {
			this.persistedUserManagerData.Write(userManagerData =>
            {
				var user = userManagerData.GetUser(userId);
				user.passwordMD5 = UserManagerHandler.CreateMD5(password);
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
			return this.persistedUserManagerData.Read(userManagerData =>
            {
				var group = userManagerData.GetGroup(groupId);
				var user = userManagerData.GetUser(userId);
				
				return group.users.Contains(user);
			});
        }

        public void SetGroupAlias(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId, string alias)
        {
			this.persistedUserManagerData.Write(userManagerData =>
            {
				var group = userManagerData.GetGroup(groupId);
				var user = userManagerData.GetUser(userId);

                // Make sure that the user has permission to set the alias
                bool hasPermission = false;
                if (group.users.Contains(user))
                    hasPermission = true;
                else if (group.type != GroupType.Personal)
                    hasPermission = true;

                if (!hasPermission)
                    throw new SecurityException("Invalid group");
				
				if (null != alias)
					group.aliases[user] = alias;
				else
					group.aliases.Remove(user);
            });
        }

        public IGroupAndAlias GetGroupAndAlias(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
            {
				var group = userManagerData.GetGroup(groupId);
				var user = userManagerData.GetUser(userId);
				
				string alias = null;
				group.aliases.TryGetValue(user, out alias);

				return this.CreateGroupAndAliasObject(group, alias);
			});
        }

        public IEnumerable<IUserOrGroup> SearchUsersAndGroups(string query, int max)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var usersAndGroups = new List<IUserOrGroup>(max);
				
				foreach (var userOrGroup in userManagerData.byName.Values.Where(u => u.name.Contains(query)))
				{
					if (userOrGroup is UserInt)
						usersAndGroups.Add(this.CreateUserObject((UserInt)userOrGroup));
					else
						usersAndGroups.Add(this.CreateGroupObject((GroupInt)userOrGroup));
					
					if (usersAndGroups.Count >= max)
						return usersAndGroups;
				}
				
				return usersAndGroups;
			});
        }

        public IEnumerable<ID<IUserOrGroup, Guid>> GetAllLocalUserIds()
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				var userIds = new List<ID<IUserOrGroup, Guid>>(userManagerData.users.Count);
				userIds.AddRange(userManagerData.users.Values.Select(u => u.id));
				
				return userIds;
			});
        }

        public int GetTotalLocalUsers()
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				return userManagerData.users.Count;
			});
        }
    }
}
