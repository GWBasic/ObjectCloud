// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Manages the file system, gets the correct handling object given a path
    /// </summary>
    public interface IFileSystemResolver
    {
		/// <summary>
		/// The FileHandlerFactoryLocator 
		/// </summary>
		FileHandlerFactoryLocator FileHandlerFactoryLocator { get;}
		
        /// <summary>
        /// The object that handles the root directory
        /// </summary>
        IDirectoryHandler RootDirectoryHandler { get;}

        /// <summary>
        /// The root directory's container
        /// </summary>
        IFileContainer RootDirectoryContainer { get;}

        /// <summary>
        /// Returns the correct factory for the given file type 
        /// </summary>
        /// <param name="fileType">This must be all lower-case</param>
        /// <returns></returns>
        IFileHandlerFactory GetFactoryForFileType(string fileType);

        /// <summary>
        /// Resolves the file
        /// </summary>
        /// <param name="fileName">the file to resolve</param>
        /// <exception cref="DiskException">Thrown if the file doesn't exist or some other unanticipated error occurs</exception>
        IFileContainer ResolveFile(string fileName);

        /// <summary>
        /// Returns true if the named file exists
        /// </summary>
        /// <param name="fileName">the file to resolve</param>
        /// <returns>True if the named file exists, false in all other conditions</returns>
        bool IsFilePresent(string fileName);

        /// <summary>
        /// Loads the file
        /// </summary>
        /// <param name="id">The file's ID</param>
        /// <param name="fileType">The file's type</param>
        /// <returns></returns>
        /// <exception cref="InvalidFileId">Bad file ID</exception>
        IFileHandler LoadFile(IFileId id, string fileType);

        /// <summary>
        /// Loads the web handler
        /// </summary>
        /// <param name="id">The file's ID</param>
        /// <param name="fileType">The file's type</param>
        /// <returns></returns>
        /// <exception cref="InvalidFileId">Bad file ID</exception>
        WebHandlers LoadWebHandlers(IFileContainer fileContainer);

        /// <summary>
        /// Deletes the file with the given FileID
        /// </summary>
        /// <param name="id"></param>
        void DeleteFile(IFileId id);

        /// <summary>
        /// Copies the file
        /// </summary>
        /// <param name="sourceFileName">File to copy</param>
        /// <param name="destinationFileName">Destination file name; the preceding path must exist</param>
        /// <exception cref="FileDoesNotExist">Thrown if the given path does not exist</exception>
        void CopyFile(string sourceFileName, string destinationFileName, ID<IUserOrGroup, Guid>? ownerID);

        /// <value>
        /// These delegates are used to start threads prior to using the filesystem.  They are stopped when the file system is stopped or disposed.
        /// </value>
        IDictionary<string, IRunnable> ServiceThreadStarts { get; set; }

        /// <summary>
        /// Starts threads that the filesystem needs; this would in turn start the embedded Google Wave server, if used
        /// </summary>
        void Start();

        /// <summary>
        /// Stops threads that the filesystem needs
        /// </summary>
        void Stop();
		
		/// <summary>
		/// Occurs before the file system starts 
		/// </summary>
		event EventHandler<IFileSystemResolver, EventArgs> Starting;
		
		/// <summary>
		/// Occurs after the file system starts 
		/// </summary>
		event EventHandler<IFileSystemResolver, EventArgs> Started;
		
		/// <summary>
		/// Occurs before the file system stops 
		/// </summary>
		event EventHandler<IFileSystemResolver, EventArgs> Stopping;
		
		/// <summary>
		/// Occurs after the file system stops 
		/// </summary>
		event EventHandler<IFileSystemResolver, EventArgs> Stopped;

        /// <summary>
        /// Returns true once the file system is started
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// The characters that are forbidden in filenames
        /// </summary>
        string FilenameForbiddenCharacters { get; set; }
		
		/// <summary>
		/// Throws an exception if the filename is invalid
		/// </summary>
		/// <param name="filename">
		/// A <see cref="System.String"/>
		/// </param>
		/// <exception cref="BadFileName">Thrown if the filename contains invalid characters</exception>
		void VerifyNoForbiddenChars(string filename);
    }

    /// <summary>
    /// Container for a file's webhandlers
    /// </summary>
    public class WebHandlers
    {
        public WebHandlers(
            IWebHandler webHandler,
            IEnumerable<IWebHandlerPlugin> webHandlersFromPlugins)
        {
            _WebHandler = webHandler;
            _WebHandlersFromPlugins = webHandlersFromPlugins;
        }

        /// <summary>
        /// The type-specific web handler
        /// </summary>
        public IWebHandler WebHandler
        {
            get { return _WebHandler; }
        }
        private readonly IWebHandler _WebHandler;

        /// <summary>
        /// The web handlers created from plugins
        /// </summary>
        public IEnumerable<IWebHandlerPlugin> WebHandlersFromPlugins
        {
            get { return _WebHandlersFromPlugins; }
        }
        private readonly IEnumerable<IWebHandlerPlugin> _WebHandlersFromPlugins;
    }
}