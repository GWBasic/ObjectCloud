// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
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
        /// The object that handles the root directory
        /// </summary>
        IDirectoryHandler RootDirectoryHandler { get;}

        /// <summary>
        /// The root directory's container
        /// </summary>
        IFileContainer RootDirectoryContainer { get;}

        /// <summary>
        /// Creates a file Id
        /// </summary>
        /// <param name="fileCreator">This object must create the corresponding file</param>
        /// <param name="fileType"></param>
        /// <returns></returns>
        IFileHandler CreateFile(FileCreatorDelegate fileCreator, string fileType);

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
        IFileHandler LoadFile(ID<IFileContainer, long> id, string fileType);

        /// <summary>
        /// Loads the web handler
        /// </summary>
        /// <param name="id">The file's ID</param>
        /// <param name="fileType">The file's type</param>
        /// <returns></returns>
        /// <exception cref="InvalidFileId">Bad file ID</exception>
        IWebHandler LoadWebHandler(ID<IFileContainer, long> id, string fileType);

        /// <summary>
        /// Deletes the file with the given FileID
        /// </summary>
        /// <param name="id"></param>
        void DeleteFile(ID<IFileContainer, long> id);

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
        /// Returns true once the file system is started
        /// </summary>
        bool IsStarted { get; }
        
        /// <summary>
        /// The ID of the root object
        /// </summary>
        long RootDirectoryId { get; set; }

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
}