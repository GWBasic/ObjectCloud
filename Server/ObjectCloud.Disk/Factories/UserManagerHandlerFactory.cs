// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.StreamEx;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Factories
{
    public class UserManagerHandlerFactory : SystemFileHandlerFactory<UserManagerHandler>
    {

        public override void CreateSystemFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
			var databaseFilename = this.CreateDatabaseFilename(path);
			this.ConstructUserManagerHandler(databaseFilename);
        }

        public override UserManagerHandler OpenFile(string path, FileId fileId)
        {
			var databaseFilename = this.CreateDatabaseFilename(path);
			return this.ConstructUserManagerHandler(databaseFilename);
		}
		
		private UserManagerHandler ConstructUserManagerHandler(string databaseFilename)
		{
			var persistedUserManagerData = new PersistedObject<UserManagerHandler.UserManagerData>(
				databaseFilename,
				() => new UserManagerHandler.UserManagerData(),
				this.Deserialize,
				this.Serialize);
        	
			return new UserManagerHandler(persistedUserManagerData, this.FileHandlerFactoryLocator, this.MaxLocalUsers);
		}

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}users", path, Path.DirectorySeparatorChar);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("A UserManager can not be copied");
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException();
            /*IUserManagerHandler toReturn = CreateFile(fileId);

            using (XmlReader xmlReader = XmlReader.Create(pathToRestoreFrom))
            {
                xmlReader.MoveToContent();

                toReturn.Restore(xmlReader, userId);
            }

            return toReturn;*/
        }

        /// <summary>
        /// The maximum number of local users allowed in the database
        /// </summary>
        public int? MaxLocalUsers { get; set; }

		private UserManagerHandler.UserManagerData Deserialize(Stream stream)
		{
			// Version
			stream.Read<int>();

			var userManagerData = new UserManagerHandler.UserManagerData();

			// Users
			var numUsers = stream.Read<int>();
			for (var ctr = 0; ctr < numUsers; ctr++)
				this.DeserializeUser(stream, userManagerData);

			// Groups
			var numGroups = stream.Read<int>();
			for (var ctr = 0; ctr < numGroups; ctr++)
				this.DeserializeGroup(stream, userManagerData);

			// Senders and their identities
			var numSenders = stream.Read<int>();
			for (var ctr = 0; ctr < numSenders; ctr++)
				this.DeserializeSender(stream, userManagerData);

			return userManagerData;
		}

		private void DeserializeUser(Stream stream, UserManagerHandler.UserManagerData userManagerData)
		{
			var user = new UserManagerHandler.UserInt();

			this.DeserializeUserBase(stream, userManagerData, user);
			userManagerData.users[user.id] = user;

			user.identityProviderArgs = stream.ReadString();
			user.identityProviderCode = stream.Read<int>();
			user.passwordMD5 = stream.ReadBytes();
			user.salt = stream.ReadBytes();

			// Association Handles
			var numAssociationHandles = stream.Read<int>();
			for (var ctr = 0; ctr < numAssociationHandles; ctr++)
			{
				var associationHandle = stream.ReadString();
				var dateTime = stream.Read<DateTime>();

				user.associationHandles[associationHandle] = dateTime;
			}

			// notification endpoints
			var numRecieveNotificationEndpoints = stream.Read<int>();
			for (var ctr = 0; ctr < numRecieveNotificationEndpoints; ctr++)
			{
				var senderToken = stream.ReadString();
				var endpoint = stream.ReadString();

				user.receiveNotificationEndpointsBySenderToken[senderToken] = endpoint;
				user.receiveNotificationSenderTokensByEndpoint[endpoint] = senderToken;
			}
		}

		private void DeserializeGroup(Stream stream, UserManagerHandler.UserManagerData userManagerData)
		{
			var group = new UserManagerHandler.GroupInt();

			this.DeserializeUserBase(stream, userManagerData, group);
			userManagerData.groups[group.id] = group;

			group.automatic = stream.Read<bool>();

			var ownerId = stream.ReadNullable<ID<IUserOrGroup, Guid>>();
			if (null != ownerId)
				group.owner = userManagerData.users[ownerId.Value];

			group.type = (GroupType)stream.Read<int>();

			// Aliases
			var numAliases = stream.Read<int>();
			for (var ctr = 0; ctr < numAliases; ctr++)
			{
				var user = userManagerData.users[stream.Read<ID<IUserOrGroup, Guid>>()];
				var alias = stream.ReadString();

				group.aliases[user] = alias;
			}

			// Users
			var numUsers = stream.Read<int>();
			for (var ctr = 0; ctr < numUsers; ctr++)
			{
				var user = userManagerData.users[stream.Read<ID<IUserOrGroup, Guid>>()];

				group.users.Add(user);
				user.groups.Add(group);
			}
		}

		private void DeserializeUserBase(Stream stream, UserManagerHandler.UserManagerData userManagerData, UserManagerHandler.UserBase userBase)
		{
			userBase.id = stream.Read<ID<IUserOrGroup, Guid>>();
			userBase.name = stream.ReadString();
			userBase.builtIn = stream.Read<bool>();
			userBase.displayName = stream.ReadString();

			userManagerData.byName[userBase.name] = userBase;
		}

		private void DeserializeSender(Stream stream, UserManagerHandler.UserManagerData userManagerData)
		{
			var sender = new UserManagerHandler.Sender()
			{
				identity = stream.ReadString(),
				loginURL = stream.ReadString(),
				loginURLOpenID = stream.ReadString(),
				loginURLRedirect = stream.ReadString(),
				loginURLWebFinger = stream.ReadString(),
				token = stream.ReadString()
			};

			userManagerData.sendersByToken[sender.token] = sender;
			userManagerData.sendersByIdentity[sender.identity] = sender;
		}

		private void Serialize(Stream stream, UserManagerHandler.UserManagerData userManagerData)
		{
			// Version
			stream.Write(0);

			// Users
			stream.Write(userManagerData.users.Count);
			foreach(var user in userManagerData.users.Values)
				this.Serialize(stream, user);

			// Groups
			stream.Write(userManagerData.groups.Count);
			foreach(var group in userManagerData.groups.Values)
				this.Serialize(stream, group);

			// Senders and their identities
			stream.Write(userManagerData.sendersByIdentity.Count);
			foreach (var sender in userManagerData.sendersByToken.Values)
				this.Serialize(stream, sender);
		}

		private void Serialize(Stream stream, UserManagerHandler.UserInt user)
		{
			this.Serialize(stream, user as UserManagerHandler.UserBase);

			stream.Write(user.identityProviderArgs);
			stream.Write(user.identityProviderCode);
			stream.WriteBytes(user.passwordMD5);
			stream.WriteBytes(user.salt);

			// Association Handles
			stream.Write(user.associationHandles.Count);
			foreach (var associationHandleKVP in user.associationHandles)
			{
				var associationHandle = associationHandleKVP.Key;
				var dateTime = associationHandleKVP.Value;

				stream.Write(associationHandle);
				stream.Write(dateTime);
			}

			// notification endpoints
			stream.Write(user.receiveNotificationEndpointsBySenderToken.Count);
			foreach (var receiveNotificationKVP in user.receiveNotificationEndpointsBySenderToken)
			{
				var senderToken = receiveNotificationKVP.Key;
				var endpoint = receiveNotificationKVP.Value;

				stream.Write(senderToken);
				stream.Write(endpoint);
			}
		}

		private void Serialize(Stream stream, UserManagerHandler.GroupInt group)
		{
			this.Serialize(stream, group as UserManagerHandler.UserBase);

			stream.Write(group.automatic);

			if (null != group.owner)
				stream.WriteNullable<ID<IUserOrGroup, Guid>>(group.owner.id);
			else
				stream.WriteNullable<ID<IUserOrGroup, Guid>>(null);

			stream.Write((int)group.type);

			// aliases
			stream.Write(group.aliases.Count);
			foreach (var userAndAlias in group.aliases)
			{
				var user = userAndAlias.Key;
				var alias = userAndAlias.Value;

				stream.Write(user.id);
				stream.Write(alias);
			}

			// users
			stream.Write(group.users.Count);
			foreach (var user in group.users)
				stream.Write(user.id);
		}

		private void Serialize(Stream stream, UserManagerHandler.UserBase userBase)
		{
			stream.Write(userBase.id);
			stream.Write(userBase.name);
			stream.Write(userBase.builtIn);
			stream.Write(userBase.displayName);
		}

		private void Serialize(Stream stream, UserManagerHandler.Sender sender)
		{
			stream.Write(sender.identity);
			stream.Write(sender.loginURL);
			stream.Write(sender.loginURLOpenID);
			stream.Write(sender.loginURLRedirect);
			stream.Write(sender.loginURLWebFinger);
			stream.Write(sender.token);
		}
    }
}
