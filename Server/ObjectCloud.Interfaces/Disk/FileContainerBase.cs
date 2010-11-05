// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    public abstract class FileContainerBase : IFileContainer
    {
        private static ILog log = LogManager.GetLogger<FileContainerBase>();

        public FileContainerBase(
            IFileHandler fileHandler,
            IFileId fileId,
            string typeId,
            string filename,
            IDirectoryHandler parentDirectoryHandler,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            DateTime created)
            : this(fileId, typeId, filename, parentDirectoryHandler, fileHandlerFactoryLocator, created)
        {
            _FileHandler = fileHandler;
        }

        public FileContainerBase(
            IFileId fileId, 
            string typeId, 
            string filename, 
            IDirectoryHandler parentDirectoryHandler,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            DateTime created)
        {
            _FileId = fileId;
            _TypeId = typeId;
            _Filename = filename;
            _ParentDirectoryHandler = parentDirectoryHandler;
            _FileHandler = null;
            _FileHandlerFactory = null;
            _WebHandler = null;
            _FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            _Created = created;
        }

        /// <summary>
        /// The file handler
        /// </summary>
        public IFileHandler FileHandler
        {
            get 
            {
                if (null == _FileHandler)
				{
                    _FileHandler = FileHandlerFactoryLocator.FileSystemResolver.LoadFile(FileId, TypeId);
					_FileHandler.FileContainer = this;
				}
				
                return _FileHandler; 
            }
        }
        private IFileHandler _FileHandler;

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The FileId.  Use FileHandlerFactoryLocator.ParseId to parse an ID from a string.
        /// </summary>
        public IFileId FileId
        {
            get { return _FileId; }
        }
        private readonly IFileId _FileId;

        public string TypeId
        {
            get { return _TypeId; }
        }
        private readonly string _TypeId;

        /// <summary>
        /// The parent directory handler
        /// </summary>
        public IDirectoryHandler ParentDirectoryHandler
        {
            get { return _ParentDirectoryHandler; }
        }
        private readonly IDirectoryHandler _ParentDirectoryHandler;

        /// <summary>
        /// The filename
        /// </summary>
        public string Filename
        {
            get { return _Filename; }
        }
        private readonly string _Filename;

        /// <summary>
        /// Casts the file handler to the given type
        /// </summary>
        /// <typeparam name="TFileHandler"></typeparam>
        /// <exception cref="WrongFileType">Thrown if the file is of an unexpected type</exception>
        /// <returns></returns>
        public TFileHandler CastFileHandler<TFileHandler>()
            where TFileHandler : IFileHandler
        {
            if (FileHandler is TFileHandler)
                return (TFileHandler)FileHandler;

            throw new WrongFileType(
                "Wrong type when loading a file, expected a " + typeof(TFileHandler).ToString() +
                ", got a " + _FileHandler.GetType().ToString());
        }

        /// <summary>
        /// The file's owner, or null if the file has no owner
        /// </summary>
        public ID<IUserOrGroup, Guid>? OwnerId
        {
            get
            {
                // No one owns the root directory
                if (null == ParentDirectoryHandler)
                    return null;

                return ParentDirectoryHandler.GetOwnerId(_FileId);
            }
        }

        public IUser Owner
        {
            get
            {
                ID<IUserOrGroup, Guid>? ownerId = OwnerId;

                if (null != ownerId)
                    return FileHandlerFactoryLocator.UserManagerHandler.GetUserNoException(ownerId.Value);

                return null;
            }
        }

        /// <summary>
        /// Loads the user's permission for the file
        /// </summary>
        /// <returns>The user's permission for the file, or null if the user has no access to the file</returns>
        public FilePermissionEnum? LoadPermission(ID<IUserOrGroup, Guid> userId)
        {
            // The owner can always administer
            if (OwnerId == userId)
                return FilePermissionEnum.Administer;

            // The root user can always administer
            if (userId == FileHandlerFactoryLocator.UserManagerHandler.Root.Id)
                return FilePermissionEnum.Administer;

            // Everyone has read access to the root directory
            if (null == ParentDirectoryHandler)
                return FilePermissionEnum.Read;

            // When the above heuristics don't apply, load the user's permission from the parent folder
            return ParentDirectoryHandler.LoadPermission(Filename, userId);
        }

        public bool HasNamedPermissions(ID<IUserOrGroup, Guid> userId, params string[] namedPermissions)
        {
            // The owner can always administer
            if (OwnerId == userId)
                return true;

            // The root user can always administer
            if (userId == FileHandlerFactoryLocator.UserManagerHandler.Root.Id)
                return true;

            return ParentDirectoryHandler.HasNamedPermissions(FileId, namedPermissions, userId);
        }

        public static bool operator ==(FileContainerBase r, FileContainerBase l)
        {
            return r.FileHandler == l.FileHandler;
        }

        public static bool operator !=(FileContainerBase r, FileContainerBase l)
        {
            return r.FileHandler != l.FileHandler;
        }

        public override bool Equals(object obj)
        {
            if (obj is IFileContainer)
                return FileHandler.Equals(((IFileContainer)obj).FileHandler);

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return FileId.GetHashCode();
        }

		public IWebHandler WebHandler
		{
        	get
			{
				if (null == _WebHandler)
                    LoadWebHandlers();
				
        		return _WebHandler;
        	}
        }
		private IWebHandler _WebHandler;

        public IEnumerable<IWebHandlerPlugin> WebHandlerPlugins
        {
            get
            {
                if (null == _WebHandlerPlugins)
                    LoadWebHandlers();

                return _WebHandlerPlugins;
            }
        }
        private IEnumerable<IWebHandlerPlugin> _WebHandlerPlugins;

        private void LoadWebHandlers()
        {
            WebHandlers webHandlers = FileHandlerFactoryLocator.FileSystemResolver.LoadWebHandlers(this);

            _WebHandler = webHandlers.WebHandler;
            _WebHandlerPlugins = webHandlers.WebHandlersFromPlugins;
        }
 
		public IFileHandlerFactory FileHandlerFactory
		{
			get
			{
				if (null == _FileHandlerFactory)
        		    _FileHandlerFactory = FileHandlerFactoryLocator.FileSystemResolver.GetFactoryForFileType(TypeId);
				
        		return _FileHandlerFactory;
        	}
		}
		IFileHandlerFactory _FileHandlerFactory;

        /// <summary>
        /// Returns the full path of the file
        /// </summary>
        public string FullPath
        {
            get
            {
                // root
                if (null == ParentDirectoryHandler)
                    return "";

                return ParentDirectoryHandler.FileContainer.FullPath + "/" + Filename;
            }
        }

        /// <summary>
        /// The extension
        /// </summary>
        public string Extension
        {
            get
            {
                if (null == _Extension)
                {
                    int lastIndexOfDot = Filename.LastIndexOf('.');

                    if (-1 == lastIndexOfDot)
                        _Extension = null;
                    else
                        _Extension = Filename.Substring(lastIndexOfDot + 1);
                }

                return _Extension;
            }
        }
        private string _Extension = null;

        public string DocumentType
        {
            get 
            { 
                if (null != Extension)
                    return Extension;
                else
                    return TypeId;
            }
        }

        public string ObjectUrl
        {
            get { return "http://" + FileHandlerFactoryLocator.HostnameAndPort + FullPath; }
        }

        public DateTime Created
        {
            get { return _Created; }
        }
        private DateTime _Created;

        public abstract DateTime LastModified { get; }

        /// <summary>
        /// Returns a deserialized JSON named configuration file
        /// </summary>
        /// <returns></returns>
        public object[] GetNamedPermissionsConfiguration()
        {
            string filename = GetNamedPermissionsConfigurationFilename();

            if (!FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(filename))
                return new object[0];

            ITextHandler supportedNamedPermissionsHandler = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename).CastFileHandler<ITextHandler>();
            return JsonReader.Deserialize<object[]>(supportedNamedPermissionsHandler.ReadAll());
        }

        /// <summary>
        /// Returns the filename used for named permissions configuration
        /// </summary>
        /// <returns></returns>
        public string GetNamedPermissionsConfigurationFilename()
        {
            string filename = "/Actions/Security/";

            if (Filename.Contains("."))
                filename += "ByExtension/" + Extension;
            else
                filename += "ByType/" + TypeId;

            filename += ".json";
            return filename;
        }

        public string GenerateSummaryView()
        {
            string filename;

            if (null != Extension)
                filename = string.Format(
                    "/Shell/SummaryViews/ByExtension/{0}.oc",
                    Extension.ToLowerInvariant());
            else
                filename = string.Format(
                    "/Shell/SummaryViews/ByType/{0}.oc",
                    TypeId.ToLowerInvariant());

            Dictionary<string, object> getParameters = new Dictionary<string, object>();
            getParameters["filename"] = FullPath;
            getParameters["objectUrl"] = ObjectUrl;
            getParameters["HeaderFooterOverride"] = "/DefaultTemplate/summaryview.ochf";

            ISession session = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();
            session.Login(Owner);

            string summaryView;
            try
            {
                IWebConnection webConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    session,
                    FullPath,
                    null,
                    null,
                    new CookiesFromBrowser(),
                    CallingFrom.Web,
                    WebMethod.GET);

                summaryView = TemplateEngine.EvaluateComponentString(webConnection, filename, getParameters);
            }
            catch (Exception e)
            {
                log.Error("Exception when generating a summary view to send in a notification for " + FullPath, e);
                summaryView = string.Format("<a href=\"{0}\">{0}</a>", ObjectUrl);
            }
            finally
            {
                FileHandlerFactoryLocator.SessionManagerHandler.EndSession(session.SessionId);
            }

            return summaryView;
        }

        public ITemplateEngine TemplateEngine
        {
            get
            {
                if (null == _TemplateEngine)
                {
                    IFileContainer templateEngineFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/System/TemplateEngine");
                    _TemplateEngine = (ITemplateEngine)templateEngineFileContainer.WebHandler;
                }

                return _TemplateEngine;
            }
        }
        private ITemplateEngine _TemplateEngine = null;

        public IEnumerable<string> GetNotificationRecipientIdentities()
        {
            IEnumerable<IUser> notificationRecipients = GetNotificationRecipients();
            foreach (IUser user in notificationRecipients)
                yield return user.Identity;
        }

        public IEnumerable<IUser> GetNotificationRecipients()
        {
            // Load the recipients based on who has permission / owns the file
            IEnumerable<FilePermission> permissions = ParentDirectoryHandler.GetPermissions(Filename);

            HashSet<ID<IUserOrGroup, Guid>> userOrGroupIds = new HashSet<ID<IUserOrGroup, Guid>>();

            // send the notification to all users who have permission to this file...
            foreach (FilePermission filePermission in permissions)
                if (filePermission.SendNotifications)
                    if (filePermission.UserOrGroupId == FileHandlerFactoryLocator.UserFactory.Everybody.Id || filePermission.UserOrGroupId == FileHandlerFactoryLocator.UserFactory.LocalUsers.Id)
                        foreach (ID<IUserOrGroup, Guid> userOrGroupId in FileHandlerFactoryLocator.UserManagerHandler.GetAllLocalUserIds())
                            userOrGroupIds.Add(userOrGroupId);
                    else
                        userOrGroupIds.Add(filePermission.UserOrGroupId);

            // ... and the owner
            if (null != OwnerId)
                userOrGroupIds.Add(OwnerId.Value);

            IEnumerable<IUser> notificationRecipients = FileHandlerFactoryLocator.UserManagerHandler.GetUsersAndResolveGroupsToUsers(userOrGroupIds);
            return notificationRecipients;
        }
    }
}
