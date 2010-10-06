// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Provides rudimentary IFileHandler functionality
    /// </summary>
    public abstract class FileHandler : IFileHandler
    {
        private static readonly ILog log = LogManager.GetLogger<FileHandler>();

        public FileHandler(FileHandlerFactoryLocator fileHandlerFactoryLocator)
        {
            _FileHandlerFactoryLocator = fileHandlerFactoryLocator;
        }

#if DEBUG
        internal static Dictionary<IFileId, WeakReference> ExistingFileHandlers = new Dictionary<IFileId, WeakReference>();
#endif

        public IFileContainer FileContainer
        {
            get { return _FileContainer; }
            set 
            {
/*#if DEBUG
				using (TimedLock.Lock(ExistingFileHandlers))
				{
	                if (ExistingFileHandlers.ContainsKey(value.FileId))
	                    if (ExistingFileHandlers[value.FileId].IsAlive)
	                        if (this != ExistingFileHandlers[value.FileId].Target)
	                            if (System.Diagnostics.Debugger.IsAttached)
	
	                                // If you hit this break, it means that there are two open FileHandlers!!!
	                                System.Diagnostics.Debugger.Break();
	
	                ExistingFileHandlers[value.FileId] = new WeakReference(this);
				}
#endif*/
                _FileContainer = value;
            }
        }
        private IFileContainer _FileContainer;

        public abstract void Dump(string path, ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId);

        public virtual void OnDelete(IUser changer) { }

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
        }
        private readonly FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The default title is the objectUrl, but it can be overridden
        /// </summary>
        public virtual string Title
        {
            get { return FileContainer.Filename; }
        }

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="recipientIdentities"></param>
        /// <param name="fileContainer"></param>
        /// <param name="summaryViewParent">The summary view will be all child nodes</param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        public void SendUpdateNotificationFrom(IUser sender)
        {
            SendNotification(sender, "update", null);
        }

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="recipientIdentities"></param>
        /// <param name="fileContainer"></param>
        /// <param name="summaryViewParent">The summary view will be all child nodes</param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        public void SendDeleteNotificationFrom(IUser sender)
        {
            SendNotification(sender, "delete", null);
        }

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="recipientIdentities"></param>
        /// <param name="fileContainer"></param>
        /// <param name="summaryViewParent">The summary view will be all child nodes</param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        public void SendShareNotificationFrom(IUser sender)
        {
            List<string> recipientIdentities = new List<string>(GetNotificationRecipientIdentities());
            SendNotification(sender, "share", recipientIdentities.ToArray());
        }

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="recipientIdentities"></param>
        /// <param name="fileContainer"></param>
        /// <param name="summaryViewParent">The summary view will be all child nodes</param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        public void SendLinkNotificationFrom(
            IUser sender,
            IFileContainer linkedFileContainer)
        {
            Dictionary<string, object> changeData = new Dictionary<string,object>();
            changeData["URL"] = linkedFileContainer.ObjectUrl;
            changeData["summaryView"] = GenerateSummaryView(linkedFileContainer);
            changeData["owner"] = linkedFileContainer.Owner.Identity;

            SendNotification(sender, "link", changeData);
        }

        private void SendNotification(
            IUser sender,
            string verb,
            object changeData)
        {
            // Do not send a notification if this is a file type that shouldn't have notifications sent
            if (null != FileContainer.Extension)
                if (FileHandlerFactoryLocator.NoParticle.Contains(FileContainer.Extension))
                    return;

            // Do not send notifications while the system is starting up
            if (!FileHandlerFactoryLocator.FileSystemResolver.IsStarted)
                return;

            // Do not send notifications if the object is incomplete
            if (null == FileContainer)
                return;
            if (null == FileContainer.ParentDirectoryHandler)
                return;
            if (null == FileHandlerFactoryLocator)
                return;

            if (null == sender)
                return;

            // Do not send notifications if the owner is an OpenID
            // TODO:  Sometime later this might be supported, but it introduces a security concern
            if (!sender.Local)
                return;

            Set<IUser> recipients = new Set<IUser>(GetNotificationRecipients());

            // Attempt to get a summary view
            string summaryView = GenerateSummaryView(FileContainer);

            FileHandlerFactoryLocator.UserManagerHandler.SendNotification(
                sender,
                false,
                recipients,
                FileContainer.ObjectUrl,
                summaryView,
                null != FileContainer.Extension ? FileContainer.Extension : FileContainer.TypeId,
                verb,
                null != changeData ? JsonWriter.Serialize(changeData) : null,
                30,
                TimeSpan.FromMinutes(5));
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

        private string GenerateSummaryView(IFileContainer fileContainer)
        {
            string filename;

            if (null != fileContainer.Extension)
                filename = string.Format(
                    "/Shell/SummaryViews/ByExtension/{0}.oc", 
                    fileContainer.Extension.ToLowerInvariant());
            else
                filename = string.Format(
                    "/Shell/SummaryViews/ByType/{0}.oc",
                    fileContainer.TypeId.ToLowerInvariant());

            Dictionary<string, object> getParameters = new Dictionary<string, object>();
            getParameters["filename"] = fileContainer.FullPath;
            getParameters["objectUrl"] = fileContainer.ObjectUrl;
            getParameters["HeaderFooterOverride"] = "/DefaultTemplate/summaryview.ochf";

            ISession session = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();
            session.Login(fileContainer.Owner);

            string summaryView;
            try
            {
                IWebConnection webConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    session,
                    fileContainer.FullPath,
                    null,
                    null,
                    new CookiesFromBrowser(),
                    CallingFrom.Web,
                    WebMethod.GET);

                summaryView = TemplateEngine.EvaluateComponent(webConnection, filename, getParameters);
            }
            catch (Exception e)
            {
                log.Error("Exception when generating a summary view to send in a notification for " + fileContainer.FullPath, e);
                summaryView = string.Format("<a href=\"{0}\">{0}</a>", FileContainer.ObjectUrl);
            }
            finally
            {
                FileHandlerFactoryLocator.SessionManagerHandler.EndSession(session.SessionId);
            }
            return summaryView;
        }

        private IEnumerable<string> GetNotificationRecipientIdentities()
        {
            IEnumerable<IUser> notificationRecipients = GetNotificationRecipients();
            foreach (IUser user in notificationRecipients)
                yield return user.Identity;
        }

        private IEnumerable<IUser> GetNotificationRecipients()
        {
            // Load the recipients based on who has permission / owns the file
            IEnumerable<FilePermission> permissions = FileContainer.ParentDirectoryHandler.GetPermissions(FileContainer.Filename);

            Set<ID<IUserOrGroup, Guid>> userOrGroupIds = new Set<ID<IUserOrGroup, Guid>>();

            // send the notification to all users who have permission to this file...
            foreach (FilePermission filePermission in permissions)
                if (filePermission.SendNotifications)
                    if (filePermission.UserOrGroupId == FileHandlerFactoryLocator.UserFactory.Everybody.Id || filePermission.UserOrGroupId == FileHandlerFactoryLocator.UserFactory.LocalUsers.Id)
                        userOrGroupIds.AddRange(FileHandlerFactoryLocator.UserManagerHandler.GetAllLocalUserIds());
                    else
                        userOrGroupIds.Add(filePermission.UserOrGroupId);

            // ... and the owner
            userOrGroupIds.Add(FileContainer.OwnerId.Value);

            IEnumerable<IUser> notificationRecipients = FileHandlerFactoryLocator.UserManagerHandler.GetUsersAndResolveGroupsToUsers(userOrGroupIds);
            return notificationRecipients;
        }

        public virtual void SyncFromLocalDisk(string localDiskPath, bool force)
        {
            throw new NotImplementedException();
        }

        public virtual void Dispose()
        {
#if DEBUG
            // Yeah, this is ugly, but that's not the point.  A lot of these file handlers are sitting in memory
            // after the FileSystem is stopped, but the FileSystem loses its WeakReferences to some of them.  This
            // Ensures that they don't flag the debugger when the objects are re-loaded for different instances of the database
			using (TimedLock.Lock(ExistingFileHandlers))
            		ExistingFileHandlers.Clear();
#endif
        }
		
		/// <summary>
		/// The default vacuum does nothing; although subclasses may add their own implementations 
		/// </summary>
		public virtual void Vacuum()
		{
		}

        /// <summary>
        /// Occurs when the relationship is added
        /// </summary>
        public event EventHandler<IFileHandler, RelationshipEventArgs> RelationshipAdded;

        /// <summary>
        /// Occurs when the relationship is deleted
        /// </summary>
        public event EventHandler<IFileHandler, RelationshipEventArgs> RelationshipDeleted;

        public void OnRelationshipAdded(RelationshipEventArgs args)
        {
            if (null != RelationshipAdded)
                RelationshipAdded(this, args);
        }

        public void OnRelationshipDeleted(RelationshipEventArgs args)
        {
            if (null != RelationshipDeleted)
                RelationshipDeleted(this, args);
        }
    }
}
