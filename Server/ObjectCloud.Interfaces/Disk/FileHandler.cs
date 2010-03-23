// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

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
#if DEBUG
                if (ExistingFileHandlers.ContainsKey(value.FileId))
                    if (ExistingFileHandlers[value.FileId].IsAlive)
                        if (this != ExistingFileHandlers[value.FileId].Target)
                            if (System.Diagnostics.Debugger.IsAttached)

                                // If you hit this break, it means that there are two open FileHandlers!!!
                                System.Diagnostics.Debugger.Break();

                ExistingFileHandlers[value.FileId] = new WeakReference(this);
#endif
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

        public void SendNotification(IUser from, string messageSummary, string changeData)
        {
        	// Do not send notifications if the object is incomplete
            if (null == FileContainer)
                return;
            if (null == FileContainer.ParentDirectoryHandler)
                return;
            if (null == FileHandlerFactoryLocator)
                return;

            // Load the recipients based on who has permission / owns the file
            IEnumerable<FilePermission> permissions = FileContainer.ParentDirectoryHandler.GetPermissions(FileContainer.Filename);

            List<ID<IUserOrGroup, Guid>> userOrGroupIds = new List<ID<IUserOrGroup, Guid>>();

            // send the notification to all users who have permission to this file...
            foreach (FilePermission filePermission in permissions)
                if (filePermission.SendNotifications)
                    userOrGroupIds.Add(filePermission.UserOrGroupId);

            // ... and the owner
            if (null != FileContainer.OwnerId)
                userOrGroupIds.Add(FileContainer.OwnerId.Value);

            IEnumerable<IUser> recipients = FileHandlerFactoryLocator.UserManagerHandler.GetUsersAndResolveGroupsToUsers(userOrGroupIds);

            SendNotification(from, recipients, messageSummary, changeData);
        }

        public void SendNotification(IUser from, IEnumerable<IUser> recipients, string messageSummary, string changeData)
        {
            // Do not send notifications while the system is starting up
            if (!FileHandlerFactoryLocator.FileSystemResolver.IsStarted)
                return;

        	// Do not send notifications if the object is incomplete
            if (null == FileContainer)
                return;
            if (null == FileContainer.ParentDirectoryHandler)
                return;

            if (null == from)
                if (null != FileContainer.OwnerId)
                    try
                    {
                        from = FileHandlerFactoryLocator.UserManagerHandler.GetUser(FileContainer.OwnerId.Value);
                    }
                    catch (UnknownUser u)
                    {
                        log.Warn("Unknown UserID set as owning a file.", u);
                        return;
                    }
                else return;

            // TODO:  It is unknown how to handle when from is a user that's an OpenId from another server.  Perhaps OAuth?
            // For now, in these cases, from is re-assigned to be the owner, or the message isn't sent if there is no owner

            else if (!from.Identity.StartsWith("http://" + FileHandlerFactoryLocator.HostnameAndPort + "/Users/"))
                if (null != FileContainer.OwnerId)
                {
                    from = FileHandlerFactoryLocator.UserManagerHandler.GetUser(FileContainer.OwnerId.Value);

                    if (!from.Identity.StartsWith("http://" + FileHandlerFactoryLocator.HostnameAndPort + "/Users/"))
                        return;
                }
                else return;

            IUserHandler fromHandler = from.UserHandler;

            // In some rare cases, the fromHandler can be null when constructing users
            if (null == fromHandler)
                return;

            string documentType = FileContainer.TypeId;
            string extension = FileContainer.Extension;
            if (null != extension)
                if (extension.Length > 0)
                    documentType = extension;


            foreach (IUser targetUserI in recipients)
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    try
                    {
                        IUser targetUser = (IUser)state;

                        fromHandler.SendNotification(
                            targetUser.Identity,
                            FileContainer.ObjectUrl,
                            Title,
                            documentType,
                            messageSummary,
                            changeData);
                    }
                    catch (Exception e)
                    {
                        log.Error("Error when sending a notification to " + state.ToString(), e);
                    }
                }, targetUserI);
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
