// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.StreamEx;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class UserHandlerFactory : SystemFileHandlerFactory<UserHandler>
    {
        public override void CreateSystemFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
			
			string notificationsPath = this.CreateNotificationsPath(path);
			Directory.CreateDirectory(notificationsPath);			
        }

        public override UserHandler OpenFile(string path, FileId fileId)
        {
			var databaseFilename = this.CreateDatabaseFilename(path);
			string notificationsPath = this.CreateNotificationsPath(path);
			
			return new UserHandler(
				new PersistedObject<UserHandler.UserData>(
					databaseFilename,
					() => new UserHandler.UserData(),
					this.Deserialize,
					this.Serialize),
				new PersistedObjectSequence_BinaryFormatter<UserHandler.Notification>(notificationsPath, 5 * 1024 * 1024, 1024 * 1024 * 1024, this.FileHandlerFactoryLocator),
				this.FileHandlerFactoryLocator);
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
			return Path.Combine(path, "namevaluepairs");
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateNotificationsPath(string path)
        {
			return Path.Combine(path, "notifications");
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("Users can not be copied");
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
            throw new NotImplementedException("Users can not be copied");
        }

		private UserHandler.UserData Deserialize(Stream stream)
		{
			// Version
			stream.Read<int>();

			var userData = new UserHandler.UserData();

			var numNameValuePairs = stream.Read<int>();
			userData.nameValuePairs = new System.Collections.Generic.Dictionary<string, string>(numNameValuePairs);
			for (var ctr = 0; ctr < numNameValuePairs; ctr++)
			{
				var key = stream.ReadString();
				var value = stream.ReadString();

				userData.nameValuePairs[key] = value;
			}

			var numTrusted = stream.Read<int>();
			userData.trusted = new System.Collections.Generic.Dictionary<string, UserHandler.Trusted>(numTrusted);
			for (var ctr = 0; ctr < numTrusted; ctr++)
			{
				var identity = stream.ReadString();
				var trusted = new UserHandler.Trusted()
				{
					link = stream.ReadNullable<bool>(),
					login = stream.ReadNullable<bool>()
				};

				userData.trusted[identity] = trusted;
			}

			return userData;
		}

		private void Serialize(Stream stream, UserHandler.UserData userData)
		{
			// Version
			stream.Write(0);

			stream.Write(userData.nameValuePairs.Count);
			foreach (var kvp in userData.nameValuePairs)
			{
				stream.Write(kvp.Key);
				stream.Write(kvp.Value);
			}

			stream.Write(userData.trusted.Count);
			foreach (var trustedKVP in userData.trusted)
			{
				var identity = trustedKVP.Key;
				var trusted = trustedKVP.Value;

				stream.Write(identity);
				stream.WriteNullable(trusted.link);
				stream.WriteNullable(trusted.login);
			}
		}
    }
}
