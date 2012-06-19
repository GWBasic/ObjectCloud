// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Implementation
{
    public class RootDirectoryCreator : IRootDirectoryCreator
    {
        private static ILog log = LogManager.GetLogger<RootDirectoryCreator>();

        public virtual void CreateRootDirectoryHandler(IFileContainer rootDirectoryContainer)
        {
            // Construct the root directory on disk
            FileHandlerFactoryLocator.DirectoryFactory.CreateFile(FileHandlerFactoryLocator.FileSystem.RootDirectoryId);

            IDirectoryHandler rootDirectoryHandler = rootDirectoryContainer.CastFileHandler<IDirectoryHandler>();

            // Create users folder
            IDirectoryHandler usersDirectory = (IDirectoryHandler)rootDirectoryHandler.CreateFile("Users", "directory", null);

            IUserManagerHandler userManager = usersDirectory.CreateSystemFile<IUserManagerHandler>("UserDB", "usermanager", null);
			
			userManager.Dispose();
			userManager = FileHandlerFactoryLocator.UserManagerHandler;
			
            IUserFactory userFactory = FileHandlerFactoryLocator.UserFactory;

            IUser anonymousUser = userManager.CreateUser(
                "anonymous",
                "",
                "anonymous",
                userFactory.AnonymousUser.Id,
                true);

            usersDirectory.DeleteFile(null, "anonymous");
			usersDirectory.RestoreFile("anonymous avatar.jpg", "image", "anonymous avatar.jpg", userFactory.RootUser.Id);
			usersDirectory.SetPermission(null, "anonymous avatar.jpg", new ID<IUserOrGroup, Guid>[] {userFactory.Everybody.Id}, FilePermissionEnum.Read, false, false);
			anonymousUser.UserHandler.Set(null, "Avatar", "/Users/anonymous avatar.jpg");

            IUser rootUser = userManager.CreateUser(
                "root",
                DefaultRootPassword,
                "root",
                userFactory.RootUser.Id,
                true);
			
			rootUser.UserHandler.Set(rootUser, "Avatar", "/DefaultTemplate/root.jpg");

			// Let the root user see information about the anonymous user
			IFileContainer anonymousUserFileContainer = usersDirectory.OpenFile("anonymous.user");
			usersDirectory.Chown(null, anonymousUserFileContainer.FileId, rootUser.Id);
			usersDirectory.RemovePermission("anonymous.user", new ID<IUserOrGroup, Guid>[] { anonymousUser.Id });
			
            // Create groups
            IGroup everybody = userManager.CreateGroup(userFactory.Everybody.Name, userFactory.Everybody.Name, null, userFactory.Everybody.Id, true, true, GroupType.Private);
            userManager.CreateGroup(userFactory.AuthenticatedUsers.Name, userFactory.AuthenticatedUsers.Name, null, userFactory.AuthenticatedUsers.Id, true, true, GroupType.Private);
            userManager.CreateGroup(userFactory.LocalUsers.Name, userFactory.LocalUsers.Name, null, userFactory.LocalUsers.Id, true, true, GroupType.Private);
            IGroup administrators = userManager.CreateGroup(userFactory.Administrators.Name, userFactory.Administrators.Name, rootUser.Id, userFactory.Administrators.Id, true, false, GroupType.Private);

            // Add root user to administrators
            userManager.AddUserToGroup(rootUser.Id, administrators.Id);

            // Allow people who aren't logged in to read the user database
            usersDirectory.SetPermission(
                null,
                "UserDB",
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
                ObjectCloud.Interfaces.Security.FilePermissionEnum.Read,
                false,
                false);

            // Allow administrators to administer the user database
            usersDirectory.SetPermission(
                null,
                "UserDB",
                new ID<IUserOrGroup, Guid>[] { administrators.Id },
                ObjectCloud.Interfaces.Security.FilePermissionEnum.Administer,
                false,
                false);

            // Create shell directory
            rootDirectoryHandler.RestoreFile(
                "Shell",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Shell",
                rootUser.Id);

            // Create shell directory
            rootDirectoryHandler.RestoreFile(
                "Config",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Config",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "Config",
                new ID<IUserOrGroup, Guid>[] { everybody.Id },
                ObjectCloud.Interfaces.Security.FilePermissionEnum.Read,
                false,
                false);

            rootDirectoryHandler.SetPermission(
                null,
                "Config",
                new ID<IUserOrGroup, Guid>[] { administrators.Id },
                ObjectCloud.Interfaces.Security.FilePermissionEnum.Administer,
                false,
                false);

            rootDirectoryHandler.SetPermission(
                null,
                "Shell",
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
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
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
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
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
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
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
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
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
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
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
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
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
                FilePermissionEnum.Read,
                true,
                false);

            // Create DefaultTemplate directory
            rootDirectoryHandler.RestoreFile(
                "DefaultTemplate",
                "directory",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "DefaultTemplate",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "DefaultTemplate",
                new ID<IUserOrGroup, Guid>[] {everybody.Id},
                FilePermissionEnum.Read,
                true,
                false);

            // Create index.oc
            rootDirectoryHandler.RestoreFile(
                "index.oc",
                "text",
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "index.oc",
                rootUser.Id);

            rootDirectoryHandler.SetPermission(
                null,
                "index.oc",
                new ID<IUserOrGroup, Guid>[] { everybody.Id },
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
                new ID<IUserOrGroup, Guid>[] { everybody.Id },
                FilePermissionEnum.Read,
                true,
                false);

            rootDirectoryHandler.IndexFile = "index.oc";

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
                new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.AuthenticatedUsers.Id },
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
                new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
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
                new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Administrators.Id },
                FilePermissionEnum.Administer,
                true,
                false);

            DoUpgrades(rootDirectoryHandler);
        }

        public virtual void Syncronize(IDirectoryHandler rootDirectoryHandler)
        {
            IFileHandler dir;

            if (rootDirectoryHandler.IsFilePresent("Actions"))
            {
                dir = rootDirectoryHandler.OpenFile("Actions").FileHandler;
                dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Actions", false, DateTime.MinValue);
            }

            dir = rootDirectoryHandler.OpenFile("Shell").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Shell", false, DateTime.MinValue);

            dir = rootDirectoryHandler.OpenFile("API").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "API", false, DateTime.MinValue);

            dir = rootDirectoryHandler.OpenFile("Templates").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Templates", false, DateTime.MinValue);

            dir = rootDirectoryHandler.OpenFile("Tests").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Tests", false, DateTime.MinValue);

            dir = rootDirectoryHandler.OpenFile("Pages").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Pages", false, DateTime.MinValue);

            dir = rootDirectoryHandler.OpenFile("Docs").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Docs", false, DateTime.MinValue);

            dir = rootDirectoryHandler.OpenFile("Classes").FileHandler;
            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Classes", false, DateTime.MinValue);

            if (!rootDirectoryHandler.IsFilePresent("Config"))
            {
                dir = (IDirectoryHandler)rootDirectoryHandler.CreateFile(
                    "Config", "directory", FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                rootDirectoryHandler.SetPermission(
                    null,
                    "Config",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
                    ObjectCloud.Interfaces.Security.FilePermissionEnum.Read,
                    false,
                    false);

                rootDirectoryHandler.SetPermission(
                    null,
                    "Config",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Administrators.Id },
                    ObjectCloud.Interfaces.Security.FilePermissionEnum.Administer,
                    false,
                    false);
            }
            else
                dir = rootDirectoryHandler.OpenFile("Config").FileHandler;

            dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Config", false, DateTime.MinValue);

            if (!rootDirectoryHandler.IsFilePresent("DefaultTemplate"))
            {
                dir = rootDirectoryHandler.CreateFile(
                    "DefaultTemplate",
                    "directory",
                    null);

                rootDirectoryHandler.SetPermission(
                    null,
                    "DefaultTemplate",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
                    FilePermissionEnum.Read,
                    true,
                    false);
            }
            else
				dir = rootDirectoryHandler.OpenFile("DefaultTemplate").FileHandler;

            /*// Only sync if the DefaultTemplate is empty
            List<IFileContainer> defaultTemplateFiles = new List<IFileContainer>(
                dir.FileContainer.CastFileHandler<IDirectoryHandler>().Files);

            if (defaultTemplateFiles.Count == 0)
				// Because most installations will modify the DefaultTemplate folder, syncing only happens if the folder is
				// missing.  This will prevent minor system upgrades from overwriting custom look and feel.
            	dir.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "DefaultTemplate", false);*/

            dir.FileContainer.CastFileHandler<IDirectoryHandler>().SyncFromLocalDisk(
                "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "DefaultTemplate",
                false,
                true);

            // Do not syncronize the index file; this is for the user to update.  It's just a web component anyway
            //IFileHandler indexFile = rootDirectoryHandler.OpenFile("index.oc").FileHandler;
            //indexFile.SyncFromLocalDisk("." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "index.oc");

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
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
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
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
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
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
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
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
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
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
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
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
                    FilePermissionEnum.Read,
                    true,
                    false);
            }

            if (!systemDirectory.IsFilePresent("TemplateEngine"))
            {
                systemDirectory.CreateFile(
                    "TemplateEngine",
                    "templateengine",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                systemDirectory.SetPermission(
                    null,
                    "TemplateEngine",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
                    FilePermissionEnum.Read,
                    true,
                    false);
            }

            if (!systemDirectory.IsFilePresent("JavascriptInterpreter"))
            {
                systemDirectory.CreateFile(
                    "JavascriptInterpreter",
                    "javascriptinterpreter",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                systemDirectory.SetPermission(
                    null,
                    "JavascriptInterpreter",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
                    FilePermissionEnum.Read,
                    true,
                    false);
            }

            IDirectoryHandler usersDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users").CastFileHandler<IDirectoryHandler>();

            if (!systemDirectory.IsFilePresent("Documentation"))
            {
                systemDirectory.CreateFile(
                    "Documentation",
                    "documentation",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                systemDirectory.SetPermission(
                    null,
                    "Documentation",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
                    FilePermissionEnum.Read,
                    true,
                    false);
            }

            if (!rootDirectoryHandler.IsFilePresent("Actions"))
            {
                // Create actions directory
                rootDirectoryHandler.RestoreFile(
                    "Actions",
                    "directory",
                    "." + Path.DirectorySeparatorChar + "DefaultFiles" + Path.DirectorySeparatorChar + "Actions",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                rootDirectoryHandler.SetPermission(
                    null,
                    "Actions",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.Everybody.Id },
                    FilePermissionEnum.Read,
                    true,
                    false);
            }

			// Let the root user see information about the anonymous user
			IFileContainer anonymousUserFileContainer = usersDirectory.OpenFile("anonymous.user");
			usersDirectory.Chown(null, anonymousUserFileContainer.FileId, FileHandlerFactoryLocator.UserFactory.RootUser.Id);
            usersDirectory.RemovePermission("anonymous.user", new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id });

            if (!usersDirectory.IsFilePresent("ParticleAvatars"))
            {
                usersDirectory.CreateFile("ParticleAvatars", "directory", null);
                usersDirectory.SetPermission(
                    null, 
                    "ParticleAvatars",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.LocalUsers.Id }, 
                    FilePermissionEnum.Read, 
                    true, 
                    false);
            }

            // Make sure every user has a friends group
            // TODO: At some point this needs to go away
            foreach (ID<IUserOrGroup, Guid> userId in FileHandlerFactoryLocator.UserManagerHandler.GetAllLocalUserIds())
            {
                IUser user = FileHandlerFactoryLocator.UserManagerHandler.GetUser(userId);

                if (!user.BuiltIn)
                    if (FileHandlerFactoryLocator.LocalIdentityProvider == user.IdentityProvider)
                    {
                        bool hasFriendsGroup = false;

                        foreach (IGroupAndAlias group in FileHandlerFactoryLocator.UserManagerHandler.GetGroupsThatUserOwns(userId))
                        {
                            if (group.Alias == "friends")
                                hasFriendsGroup = true;
                        }

                        if (!hasFriendsGroup)
                            try
                            {
                                FileHandlerFactoryLocator.UserManagerHandler.CreateGroup("friends", user.DisplayName + "'s friends", user.Id, GroupType.Personal);
                            }
                            catch (Exception e)
                            {
                                log.Warn("Exception creating friends group for user " + user.Name, e);
                            }
                    }
            }

            /*if (!rootDirectoryHandler.IsFilePresent("Config"))
            {
                IDirectoryHandler configDirectory = (IDirectoryHandler)rootDirectoryHandler.CreateFile(
                    "Config", "directory", FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                CreateConfigDirectory(configDirectory, "ByExtension");
                CreateConfigDirectory(configDirectory, "ByType");
            }*/
		}

        /*private void CreateConfigDirectory(IDirectoryHandler configDirectory, string fileType)
        {
            configDirectory = (IDirectoryHandler)configDirectory.CreateFile(
                fileType, "directory", FileHandlerFactoryLocator.UserFactory.RootUser.Id);

            Dictionary<string, Dictionary<string, object>> configObjectsByType = new Dictionary<string, Dictionary<string, object>>();

            IDirectoryHandler securityDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(
                "/Actions/Security/" + fileType).CastFileHandler<IDirectoryHandler>();

            foreach (IFileContainer securityFile in securityDirectory.Files)
            {
                string typeName = securityFile.Filename.Split('.')[0];
                string contents = securityFile.CastFileHandler<ITextHandler>().ReadAll();

                Dictionary<string, object> configObject = new Dictionary<string, object>();
                configObject["ViewComponents"] = JsonReader.Deserialize(contents);

                configObjectsByType[typeName] = configObject;
            }

            IDirectoryHandler actionsDirectory = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(
                "/Actions/" + fileType).CastFileHandler<IDirectoryHandler>();

            foreach (IFileContainer actionsFile in actionsDirectory.Files)
            {
                string typeName = actionsFile.Filename;

                Dictionary<string, object> configObject;
                if (!configObjectsByType.TryGetValue(typeName, out configObject))
                {
                    configObject = new Dictionary<string, object>();
                    configObjectsByType[typeName] = configObject;
                }

                Dictionary<string, object> actions = new Dictionary<string, object>();
                configObject["Actions"] = actions;

                foreach (KeyValuePair<string, string> action in actionsFile.CastFileHandler<INameValuePairsHandler>())
                    actions[action.Key] = action.Value;
            }

            foreach (KeyValuePair<string, Dictionary<string, object>> configObject in configObjectsByType)
            {
                ITextHandler configObjectHandler = (ITextHandler)configDirectory.CreateFile(
                    configObject.Key + ".json", "text", FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                configObjectHandler.WriteAll(null, JsonWriter.Serialize(configObject.Value));
            }
        }*/

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
