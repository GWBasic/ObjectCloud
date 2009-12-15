// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Implementation
{
    public class RootDirectoryCreator : IRootDirectoryCreator
    {
        public void CreateRootDirectoryHandler(IFileContainer rootDirectoryContainer)
        {
            // Construct the root directory on disk
            IDirectoryHandler rootDirectoryHandler = FileHandlerFactoryLocator.DirectoryFactory.CreateFile(
                new ID<IFileContainer, long>(FileHandlerFactoryLocator.FileSystemResolver.RootDirectoryId));

            // These weird lines ensure that there is only one rootDirectoryHandler in memory.  The FileContainer always re-constructs
            // the FileHandler; therefore, the one made in the above line will result in a duplicate in-memory object
            if (rootDirectoryHandler is IDisposable)
                ((IDisposable)rootDirectoryHandler).Dispose();

            rootDirectoryHandler = rootDirectoryContainer.CastFileHandler<IDirectoryHandler>();

            // Create users folder
            IDirectoryHandler usersDirectory = (IDirectoryHandler)rootDirectoryHandler.CreateFile("Users", "directory", null);

            IUserManagerHandler userManager = usersDirectory.CreateSystemFile<IUserManagerHandler>("UserDB", "usermanager", null);
			IUserFactory userFactory = FileHandlerFactoryLocator.UserFactory;

            //IUser anonymousUser = 
			userManager.CreateUser(
				"anonymous", 
			    "",
			    userFactory.AnonymousUser.Id,
			    true);

            usersDirectory.DeleteFile(null, "anonymous");
			    
            IUser rootUser = userManager.CreateUser(
            	"root",
            	DefaultRootPassword,
            	userFactory.RootUser.Id,
			    true);
			
			// Create groups
			IGroup everybody = userManager.CreateGroup(userFactory.Everybody.Name, null, userFactory.Everybody.Id, true, true, GroupType.Private);
            userManager.CreateGroup(userFactory.AuthenticatedUsers.Name, null, userFactory.AuthenticatedUsers.Id, true, true, GroupType.Private);
            userManager.CreateGroup(userFactory.LocalUsers.Name, null, userFactory.LocalUsers.Id, true, true, GroupType.Private);
            IGroup administrators = userManager.CreateGroup(userFactory.Administrators.Name, rootUser.Id, userFactory.Administrators.Id, true, false, GroupType.Private);
			
			// Add root user to administrators
			userManager.AddUserToGroup(rootUser.Id, administrators.Id);

            // Allow people who aren't logged in to read the user database
            usersDirectory.SetPermission(
                null,
                "UserDB",
                everybody.Id,
                ObjectCloud.Interfaces.Security.FilePermissionEnum.Read,
				false,
                false);

            // Allow administrators to administer the user database
            usersDirectory.SetPermission(
                null,
                "UserDB",
                administrators.Id,
                ObjectCloud.Interfaces.Security.FilePermissionEnum.Administer,
                false,
                false);

            // Create shell directory
            rootDirectoryHandler.RestoreFile(
                "Shell", 
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Shell",
                rootUser.Id);
			
			rootDirectoryHandler.SetPermission(
                null,
                "Shell",
			    everybody.Id,
			    FilePermissionEnum.Read,
                true,
                false);

            // Create API directory
            rootDirectoryHandler.RestoreFile(
                "API",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "API",
                rootUser.Id);
			
			rootDirectoryHandler.SetPermission(
                null,
                "API",
			    everybody.Id,
			    FilePermissionEnum.Read,
                true,
                false);

            // Create Templates directory
            rootDirectoryHandler.RestoreFile(
                "Templates",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Templates",
                rootUser.Id);
			
			rootDirectoryHandler.SetPermission(
                null,
                "Templates",
			    everybody.Id,
			    FilePermissionEnum.Read,
                true,
                false);

            // Create Tests directory
            rootDirectoryHandler.RestoreFile(
                "Tests",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Tests",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "Tests",
                everybody.Id,
                FilePermissionEnum.Read,
                true,
                false);

            // Create Pages directory
            rootDirectoryHandler.RestoreFile(
                "Pages",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Pages",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "Pages",
                everybody.Id,
                FilePermissionEnum.Read,
                true,
                false);

            // Create Pages directory
            rootDirectoryHandler.RestoreFile(
                "Docs",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Docs",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "Docs",
                everybody.Id,
                FilePermissionEnum.Read,
                true,
                false);

            // Create Classes directory
            rootDirectoryHandler.RestoreFile(
                "Classes",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Classes",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "Classes",
                everybody.Id,
                FilePermissionEnum.Read,
                true,
                false);

            // Create index.wchtml
            rootDirectoryHandler.RestoreFile(
                "index.page",
                "text",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "index.page",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "index.page",
                everybody.Id,
                FilePermissionEnum.Read,
                true,
                false);

            // Create favicon.ico
            rootDirectoryHandler.RestoreFile(
                "favicon.ico",
                "binary",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "favicon.ico",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "favicon.ico",
                everybody.Id,
                FilePermissionEnum.Read,
                true,
                false);
                
            rootDirectoryHandler.IndexFile = "index.page";

            // "/System/SessionManager"
            IDirectoryHandler SystemDirectory = (IDirectoryHandler)rootDirectoryHandler.CreateFile(
                "System",
                "directory",
                rootUser.Id);

            SystemDirectory.CreateFile(
                "SessionManager",
                "sessionmanager",
                rootUser.Id);

            // Allow logged in users to manage their sessions
            SystemDirectory.SetPermission(
                null,
                "SessionManager",
                FileHandlerFactoryLocator.UserFactory.AuthenticatedUsers.Id,
                FilePermissionEnum.Read,
                false,
                false);

            // Create the proxy
            SystemDirectory.CreateFile(
                "Proxy",
                "proxy",
                rootUser.Id);

            SystemDirectory.SetPermission(
                null,
                "Proxy",
                FileHandlerFactoryLocator.UserFactory.Everybody.Id,
                FilePermissionEnum.Read,
                false,
                false);
			
			// Create the log
			SystemDirectory.CreateFile(
				"Log",
				"log",
			    rootUser.Id);
			
			SystemDirectory.SetPermission(
				null,
			    "Log",
			    FileHandlerFactoryLocator.UserFactory.Administrators.Id,
			    FilePermissionEnum.Administer,
			    true,
			    false);

            DoUpgrades(rootDirectoryHandler);
        }

        public void Syncronize(IDirectoryHandler rootDirectoryHandler)
        {
            IFileHandler dir;

            dir = rootDirectoryHandler.OpenFile("Shell").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Shell");

            dir = rootDirectoryHandler.OpenFile("API").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "API");

            dir = rootDirectoryHandler.OpenFile("Templates").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Templates");

            dir = rootDirectoryHandler.OpenFile("Tests").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Tests");

            dir = rootDirectoryHandler.OpenFile("Pages").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Pages");

            dir = rootDirectoryHandler.OpenFile("Docs").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Docs");

            dir = rootDirectoryHandler.OpenFile("Classes").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Classes");

            // Do not syncronize the index file; this is for the user to update.  It's just a web component anyway
            //IFileHandler indexFile = rootDirectoryHandler.OpenFile("index.page").FileHandler;
            //indexFile.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "index.page");

            DoUpgrades(rootDirectoryHandler);
        }

        private void DoUpgrades(IDirectoryHandler rootDirectoryHandler)
        {
            IDirectoryHandler systemDirectory = rootDirectoryHandler.OpenFile("System").CastFileHandler<IDirectoryHandler>();

            if (!systemDirectory.IsFilePresent("BrowserInfo"))
            {
                systemDirectory.CreateFile(
                    "BrowserInfo",
                    "browserinfo",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                systemDirectory.SetPermission(
                    null,
                    "BrowserInfo",
                    FileHandlerFactoryLocator.UserFactory.Everybody.Id,
                    FilePermissionEnum.Read,
                    true,
                    false);
            }

            if (!systemDirectory.IsFilePresent("Comet"))
            {
                IDirectoryHandler cometDirectory = (IDirectoryHandler)systemDirectory.CreateFile(
                    "Comet",
                    "directory",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                systemDirectory.SetPermission(
                    null,
                    "Comet",
                    FileHandlerFactoryLocator.UserFactory.Everybody.Id,
                    FilePermissionEnum.Read,
                    true,
                    false);

                cometDirectory.CreateFile(
                    "Loopback",
                    "cometloopback",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                cometDirectory.SetPermission(
                    null,
                    "Loopback",
                    FileHandlerFactoryLocator.UserFactory.Everybody.Id,
                    FilePermissionEnum.Read,
                    true,
                    false);

                cometDirectory.CreateFile(
                    "Echo",
                    "cometecho",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                cometDirectory.SetPermission(
                    null,
                    "Echo",
                    FileHandlerFactoryLocator.UserFactory.Everybody.Id,
                    FilePermissionEnum.Read,
                    true,
                    false);

                cometDirectory.CreateFile(
                    "Multiplexer",
                    "cometmultiplex",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                cometDirectory.SetPermission(
                    null,
                    "Multiplexer",
                    FileHandlerFactoryLocator.UserFactory.Everybody.Id,
                    FilePermissionEnum.Read,
                    true,
                    false);
				
                cometDirectory.CreateFile(
                    "LoopbackQuality",
                    "cometloopbackqueuingreliable",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                cometDirectory.SetPermission(
                    null,
                    "LoopbackQuality",
                    FileHandlerFactoryLocator.UserFactory.Everybody.Id,
                    FilePermissionEnum.Read,
                    true,
                    false);
            }

            IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();
            string groupFileName = FileHandlerFactoryLocator.UserFactory.Administrators.Name.ToLower() + ".group";
            if (!usersDirectory.IsFilePresent(groupFileName))
            {
                IDatabaseHandler groupDB = usersDirectory.CreateFile(
                    groupFileName, 
                    "database",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id).FileContainer.CastFileHandler<IDatabaseHandler>();
                
                usersDirectory.SetPermission(
                    null, 
                    groupFileName, 
                    FileHandlerFactoryLocator.UserFactory.Administrators.Id, 
                    FilePermissionEnum.Read, 
                    true, 
                    true);

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
                    parameter.Value = FileHandlerFactoryLocator.UserFactory.Administrators.Id;
                    command.Parameters.Add(parameter);

                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// The default root password
        /// </summary>
        public string DefaultRootPassword
        {
            get 
            {
                if (null == _DefaultRootPassword)
                    throw new SecurityException("The default root password is unspecified");

                return _DefaultRootPassword; 
            }
            set { _DefaultRootPassword = value; }
        }
        private string _DefaultRootPassword;

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;
    }
}
