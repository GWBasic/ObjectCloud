// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Handles access to a file; provides an API for file access
    /// </summary>
    public interface IFileHandler
    {
        /// <summary>
        /// Dumps the file to the given path.  The FileHandler should be locked prior to calling this method
        /// </summary>
        /// <param name="path"></param>
        void Dump(string path, ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Called prior to the file being deleted; instructs the file to clean up any resources that won't
        /// be cleaned up when the file's directory is deleted
        /// </summary>
        /// <param name="changer">The user who initiated the change</param>
        void OnDelete(IUser changer);
		
		/// <value>
		/// The FileContainer used to wrap the file 
		/// </value>
		IFileContainer FileContainer { get; set; }

        /// <summary>
        /// Returns the last time that the file was modified
        /// </summary>
        DateTime LastModified { get; }

        /// <summary>
        /// Sends a notification to all of the recipients as originating from this object
        /// </summary>
        /// <param name="recipients"></param>
        /// <param name="title"></param>
        /// <param name="messageSummary"></param>
        /// <param name="changeData"></param>
        void SendNotification(IUser from, IEnumerable<IUser> recipients, string messageSummary, string changeData);

        /// <summary>
        /// Sends a notification to all of the users who have access to this object as originating from this object
        /// </summary>
        /// <param name="recipients"></param>
        /// <param name="title"></param>
        /// <param name="messageSummary"></param>
        /// <param name="changeData"></param>
        void SendNotification(IUser from, string messageSummary, string changeData);

        /// <summary>
        /// The file's title.  This can default to its URL
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Syncronizes this file from an authoritative version on the local disk if it's newer
        /// </summary>
        /// <param name="localDiskPath"></param>
        void SyncFromLocalDisk(string localDiskPath);
		
		/// <summary>
		/// Performs any needed maintanence operations, such as a SQLite vacuum
		/// </summary>
		void Vacuum();

        /// <summary>
        /// Occurs when the relationship is added
        /// </summary>
        event EventHandler<IFileHandler, RelationshipEventArgs> RelationshipAdded;

        /// <summary>
        /// Calls RelationshipAdded
        /// </summary>
        /// <param name="args"></param>
        void OnRelationshipAdded(RelationshipEventArgs args);

        /// <summary>
        /// Occurs when the relationship is deleted
        /// </summary>
        event EventHandler<IFileHandler, RelationshipEventArgs> RelationshipDeleted;

        /// <summary>
        /// Calls RelationshipDeleted
        /// </summary>
        /// <param name="args"></param>
        void OnRelationshipDeleted(RelationshipEventArgs args);
    }

    /// <summary>
    /// Encapsulates information about adding a relationship
    /// </summary>
    public class RelationshipEventArgs : EventArgs
    {
        public RelationshipEventArgs(IFileContainer relatedFile, string relationship)
        {
            _RelatedFile = relatedFile;
            _Relationship = relationship;
        }

        /// <summary>
        /// The named relationship
        /// </summary>
        public string Relationship
        {
            get { return _Relationship; }
        }
        private readonly string _Relationship;

        /// <summary>
        /// The related file
        /// </summary>
        public IFileContainer RelatedFile
        {
            get { return _RelatedFile; }
        }
        private readonly IFileContainer _RelatedFile;
    }
}