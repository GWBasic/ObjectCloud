// Copyright 2009 - 2012 Andrew Rondeau
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
            get 
			{
				// For debugging, other parts of code perform null checks during initialization
				//if (null == _FileContainer)
				//	throw new NullReferenceException("FileContainer hasn't been set yet");
				
				return _FileContainer; 
			}
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

        public abstract void OnDelete(IUser changer);

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
            List<string> recipientIdentities = new List<string>(FileContainer.GetNotificationRecipientIdentities());
            SendNotification(sender, "share", recipientIdentities.ToArray());
        }

        /// <summary>
        /// The next linkId to use in SendLinkNotificationFrom
        /// </summary>
        private long NextLinkID = SRandom.Next<long>();

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="recipientIdentities"></param>
        /// <param name="fileContainer"></param>
        /// <param name="summaryViewParent">The summary view will be all child nodes</param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        public LinkNotificationInformation SendLinkNotificationFrom(
            IUser sender,
            IFileContainer linkedFileContainer)
        {
			// TODO: Need a better approach to handle when the linked file doesn't have an owner
			// For now, just sending as root
			IUser owner;
			if (null != linkedFileContainer.Owner)
				owner = linkedFileContainer.Owner;
			else
				owner = FileHandlerFactoryLocator.UserFactory.RootUser;
			
            string linkSummaryView = linkedFileContainer.GenerateSummaryView();
            string linkID = Interlocked.Increment(ref NextLinkID).ToString();

            Dictionary<string, object> changeData = new Dictionary<string,object>();
            changeData["linkUrl"] = linkedFileContainer.ObjectUrl;
            changeData["linkSummaryView"] = linkSummaryView;
            changeData["linkDocumentType"] = linkedFileContainer.DocumentType;
            changeData["ownerIdentity"] = owner.Identity;
            changeData["linkID"] = linkID;

            SendNotification(sender, "link", changeData);

            LinkNotificationInformation toReturn = new LinkNotificationInformation();
            toReturn.linkID = linkID;
            toReturn.linkSummaryView = linkSummaryView;

            return toReturn;
        }

        private void SendNotification(
            IUser sender,
            string verb,
            object changeData)
        {
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

            // Do not send a notification if this is a file type that shouldn't have notifications sent
            if (null != FileContainer.Extension)
                if (FileHandlerFactoryLocator.NoParticle.Contains(FileContainer.Extension))
                    return;

            if (null == sender)
                return;

            // Do not send a notification if this is a file type that shouldn't have notifications sent
            if (null != FileContainer.Extension)
                if (FileHandlerFactoryLocator.NoParticle.Contains(FileContainer.Extension))
                    return;

            // Do not send notifications if the owner is an OpenID
            // TODO:  Sometime later this might be supported, but it introduces a security concern
            if (!sender.Local)
                return;

			ThreadPool.QueueUserWorkItem(delegate(object state)
			{
				try
				{
		            HashSet<IUser> recipients = new HashSet<IUser>(FileContainer.GetNotificationRecipients());
		
		            // Attempt to get a summary view
		            string summaryView = FileContainer.GenerateSummaryView();
		
		            FileHandlerFactoryLocator.UserManagerHandler.SendNotification(
		                sender,
		                false,
		                recipients,
		                FileContainer.ObjectUrl,
		                summaryView,
		                FileContainer.DocumentType,
		                verb,
		                null != changeData ? JsonWriter.Serialize(changeData) : null,
		                30,
		                TimeSpan.FromMinutes(5));
				}
				catch (Exception e)
				{
					log.Error("Exception when sending a notification", e);
				}
			});
        }

        public virtual void SyncFromLocalDisk(string localDiskPath, bool force, DateTime lastModified)
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
