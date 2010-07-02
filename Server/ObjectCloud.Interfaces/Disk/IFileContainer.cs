// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    public interface IFileContainer
    {
        /// <summary>
        /// The file handler
        /// </summary>
        IFileHandler FileHandler { get; }

        /// <summary>
        /// Casts the file handler to the given type
        /// </summary>
        /// <typeparam name="TFileHandler"></typeparam>
        /// <exception cref="WrongFileType">Thrown if the file is of an unexpected type</exception>
        /// <returns></returns>
        TFileHandler CastFileHandler<TFileHandler>() where TFileHandler : IFileHandler;
		
        /// <summary>
        /// The object that handles web requests
        /// </summary>
        IWebHandler WebHandler { get; }

        /// <summary>
        /// All of the WebHandlerPlugins
        /// </summary>
        IEnumerable<IWebHandlerPlugin> WebHandlerPlugins { get; }
		
		/// <value>
		/// The factory used to construct the file 
		/// </value>
		IFileHandlerFactory FileHandlerFactory { get; }

        /// <summary>
        /// The file's owner, or null if the file has no owner
        /// </summary>
        ID<IUserOrGroup, Guid>? OwnerId { get; }

        /// <summary>
        /// The file's owner, or null if the file has no owner
        /// </summary>
        IUser Owner { get; }

        /// <summary>
        /// Loads the user's permission for the file
        /// </summary>
        /// <returns>The user's permission for the file, or null if the user has no access to the file</returns>
        FilePermissionEnum? LoadPermission(ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Returns true if the user has the named permission
        /// </summary>
        /// <param name="namedPermission"></param>
        /// <returns></returns>
        bool HasNamedPermissions(ID<IUserOrGroup, Guid> userId, params string[] namedPermissions);

        /// <summary>
        /// The FileId.  Use FileHandlerFactoryLocator.ParseId to parse an ID from a string.
        /// </summary>
        IFileId FileId { get; }

        /// <summary>
        /// The filename
        /// </summary>
        string Filename { get; }

        /// <summary>
        /// The file's type ID
        /// </summary>
        string TypeId { get; }

        /// <summary>
        /// The parent directory handler
        /// </summary>
        IDirectoryHandler ParentDirectoryHandler { get; }

        /// <summary>
        /// Returns the full path of the file, including the file name
        /// </summary>
        string FullPath { get; }

        /// <summary>
        /// Shortcut to get the file's extension
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// The file's URL
        /// </summary>
        string ObjectUrl { get; }

        /// <summary>
        /// When the file was created
        /// </summary>
        DateTime Created { get; }

        /// <summary>
        /// Returns the last time that the file was modified
        /// </summary>
        DateTime LastModified { get; }

        /// <summary>
        /// The FileHandlerFactoryLocator
        /// </summary>
        FileHandlerFactoryLocator FileHandlerFactoryLocator { get; }
    }
}
