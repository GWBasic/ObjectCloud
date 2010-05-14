// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Wraps all functionality of a directory
    /// </summary>
    public class DirectoryWebHandler : DatabaseWebHandler<IDirectoryHandler, DirectoryWebHandler>
    {
        /// <summary>
        /// Synchronizes creating a file
        /// </summary>
        private object CreateFileLock = new object();

        /// <summary>
        /// Creates a file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="FileName">The file name.  Either the file name or extension need to be specified</param>
        /// <param name="extension">The file extension.  Either the extension or file name need to be specified</param>
        /// <param name="FileType">The file type</param>
        /// <param name="ErrorIfExists">True to return an error if the file already exists</param>
        /// <param name="fileNameSuggestion">A suggestion for creating a file name.  ObjectCloud will attempt to generate a real file name from this suggestion</param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Write)]
        public IWebResults CreateFile(
            IWebConnection webConnection,
            string FileName,
            string extension,
            string fileNameSuggestion,
            string FileType,
            bool? ErrorIfExists)
        {
            IFileHandler fileHandler = CreateFileHelper(webConnection, FileName, extension, fileNameSuggestion, FileType, ErrorIfExists);

            IWebResults toReturn = fileHandler.FileContainer.WebHandler.GetJSW(webConnection, null, null, false);
            toReturn.Status = Status._201_Created;
            return toReturn;
        }

        /// <summary>
        /// Helps in creating a file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="FileName"></param>
        /// <param name="extension"></param>
        /// <param name="fileNameSuggestion"></param>
        /// <param name="FileType"></param>
        /// <param name="ErrorIfExists"></param>
        /// <returns></returns>
        protected IFileHandler CreateFileHelper(
            IWebConnection webConnection,
            string FileName,
            string extension,
            string fileNameSuggestion,
            string FileType,
            bool? ErrorIfExists)
        {
            using (TimedLock.Lock(CreateFileLock))
            {
                // It's an error if both FileName and extension are null, or if both are set
                if (((null == FileName) && (null == extension)) || ((null != FileName) && (null != extension)))
                    throw new WebResultsOverrideException(
                        WebResults.FromString(Status._400_Bad_Request, "Either FileName or Extension must be specified"));

                else if (null == FileName)
                {
                    if (extension.Length < 1)
                        throw new WebResultsOverrideException(
                            WebResults.FromString(Status._400_Bad_Request, "The extension must be at least one character long"));

                    FileName = GenerateFilename(extension, fileNameSuggestion);
                }
                else
                {
                    int lastDot = FileName.LastIndexOf('.');
                    if (-1 != lastDot)
                        extension = FileName.Substring(lastDot + 1);
                }

                // This is a performance optimization when a file needs to be created if it doesn't exist
                if (null != ErrorIfExists)
                    if (!ErrorIfExists.Value)
                        if (FileHandler.IsFilePresent(FileName))
                            return FileHandler.OpenFile(FileName).FileHandler;

                // If the file type is not specified, try to load it from a server-side class
                if (null == FileType)
                {
                    FileType = DetermineFileType(extension);
                    if (null == FileType)
                        throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, "FileType must be specified for files with extension ." + extension));
                }

                try
                {
                    return FileHandler.CreateFile(FileName, FileType, webConnection.Session.User.Id);
                }
                catch (DuplicateFile)
                {
                    throw new WebResultsOverrideException(WebResults.FromString(Status._409_Conflict, FileName + " already exists"));
                }
                catch (BadFileName)
                {
                    throw new WebResultsOverrideException(WebResults.FromString(Status._409_Conflict, FileName + " is an invalid file name"));
                }
            }
        }

        /// <summary>
        /// Assists in determining the file type given the extension
        /// </summary>
        /// <param name="extension"></param>
        /// <returns></returns>
        protected string DetermineFileType(string extension)
        {
            IFileContainer javascriptClass = FindJavascriptContainer(extension, FileHandler);

            // If there is server-side Javascript, try to infer the file type
            if (null != javascriptClass)
                if (javascriptClass.FileHandler is ITextHandler)
                    foreach (string line in javascriptClass.CastFileHandler<ITextHandler>().ReadLines())
                        if (line.Trim().StartsWith("// FileType:"))
                            return line.Substring(12).Trim();

            return null;
        }

        /// <summary>
        /// Assists in generating a filename
        /// </summary>
        /// <param name="extension"></param>
        /// <param name="fileNameSuggestion"></param>
        /// <returns></returns>
        private string GenerateFilename(string extension, string fileNameSuggestion)
        {
            DateTime timestamp = DateTime.UtcNow;

            // If there's a suggestion, then follow it
            if (null != fileNameSuggestion)
            {
                // Limit potential filenames to a reasonable length
                if (fileNameSuggestion.Length > 25)
                    fileNameSuggestion = fileNameSuggestion.Substring(0, 25);

                // Get rid of forbidden characters
                foreach (char forbiddenChar in FileHandlerFactoryLocator.FileSystemResolver.FilenameForbiddenCharacters)
                    fileNameSuggestion = fileNameSuggestion.Replace(forbiddenChar, '_');

                // Try the suggestion plus an extension
                string fileName = string.Format(
                    "{0}.{1}",
                    fileNameSuggestion,
                    extension);

                // If that doesn't work, then just keep using the time to find something unique
                if (FileHandler.IsFilePresent(fileName))
                {
                    long ticks = timestamp.Ticks;

                    do
                    {
                        fileName = string.Format(
                            "{0}_{1}.{2}",
                            fileNameSuggestion,
                            ticks,
                            extension);

                        ticks++;
                    }
                    while (FileHandler.IsFilePresent(fileName));
                }

                return fileName;
            }
            else
                return string.Format(
                    "{0}x{1}-{2}-{3}___{4}-{5}-{6}_{7}.{8}",
                    SRandom.Next<byte>(),
                    timestamp.Year,
                    timestamp.Month,
                    timestamp.Day,
                    timestamp.Hour,
                    timestamp.Minute,
                    timestamp.Second,
                    timestamp.Millisecond,
                    extension);
        }

        /// <summary>
        /// Deletes a file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="FileName"></param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults DeleteFile(IWebConnection webConnection, string FileName)
        {
            try
            {
                FileHandler.DeleteFile(webConnection.Session.User, FileName);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._404_Not_Found, FileName + " does not exist"));
            }

            return WebResults.FromString(Status._202_Accepted, FileName + " deleted");
        }

        /// <summary>
        /// Returns a JavaScript object that can manipulate the given file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="FileName"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults Open(IWebConnection webConnection, string FileName)
        {
            IFileContainer fileContainer;

            try
            {
                fileContainer = FileHandler.OpenFile(FileName);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._404_Not_Found, FileName + " does not exist"));
            }
            catch (WrongFileType wft)
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._404_Not_Found, wft.Message));
            }
			
			FilePermissionEnum? permission = fileContainer.LoadPermission(webConnection.Session.User.Id);
			
			if (null != permission)
			{
		        IWebResults webResults = fileContainer.WebHandler.GetJSW(webConnection, null, null, false);
		
		        string minified = JavaScriptMinifier.Instance.Minify(webResults.ResultsAsString);

                IWebResults toReturn = WebResults.FromString(webResults.Status, minified);
                toReturn.ContentType = webResults.ContentType;

                return toReturn;
			}
			else
				return WebResults.FromString(Status._401_Unauthorized, "Permission Denied");
        }

        /// <summary>
        /// Lists all of the files in the directory as a JSON array
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults ListFiles(IWebConnection webConnection)
        {
            return ReturnFilesAsJSON(
                webConnection, 
                new List<IFileContainer>(FileHandler.Files));
        }

        /// <summary>
        /// Lists the newest files that the user has access to in descending order of creation
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="maxToReturn"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults ListNewestFiles(IWebConnection webConnection, long? maxToReturn)
        {
            if (null == maxToReturn)
                maxToReturn = long.MaxValue;

            return ReturnFilesAsJSON(
                webConnection,
                new List<IFileContainer>(FileHandler.GetNewestFiles(webConnection.Session.User.Id, maxToReturn.Value)));
        }

        /// <summary>
        /// Helper to return files as JSON
        /// </summary>
        /// <param name="files"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private IWebResults ReturnFilesAsJSON(IWebConnection webConnection, IEnumerable<IFileContainer> files)
        {
            IList<IDictionary<string, object>> toReturn = GetFilesForJSON(webConnection.Session, files);

            return WebResults.ToJson(toReturn);
        }

        /// <summary>
        /// Returns "true" if the file is present, "false" otherwise
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="FileName"></param>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults IsFilePresent(IWebConnection webConnection, string FileName)
        {
            bool result = FileHandler.IsFilePresent(FileName);

            return WebResults.ToJson(result);
        }

        /// <summary>
        /// Sets the user's permission for the given file.  Either the user or group ID or name are set
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="FileName">The filename</param>
        /// <param name="FilePermission">The permission, set to null to disable permissions to the file</param>
        /// <param name="Inherit">Set to true to allow permission inheritance.  For example, if this permission applies to a directory, it will be the default for files in the directory</param>
        /// <param name="UserOrGroup"></param>
        /// <param name="UserOrGroupId"></param>
        /// <param name="SendNotifications"></param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults SetFilePermission(IWebConnection webConnection, string UserOrGroupId, string UserOrGroup, string FileName, string FilePermission, bool? Inherit, bool? SendNotifications)
        {
            IFileContainer file;

            try
            {
                file = FileHandler.OpenFile(FileName);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._404_Not_Found, FileName + " doesn't exist"));
            }

            return file.WebHandler.SetPermission(webConnection, UserOrGroupId, UserOrGroup, FilePermission, Inherit, SendNotifications, null);
        }
		
		/// <summary>
		/// Returns all assigned permissions to the object
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
        /// <param name="FileName">The filename</param>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Administer)]
        public IWebResults GetFilePermissions(IWebConnection webConnection, string FileName)
		{
            IFileContainer file;

            try
            {
                file = FileHandler.OpenFile(FileName);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._404_Not_Found, FileName + " doesn't exist"));
            }

            return file.WebHandler.GetPermissions(webConnection);
        }
		
		/// <summary>
		/// Returns the current user's permission to the given filename
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
        /// <param name="FileName">The filename</param>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Administer)]
        public IWebResults GetUserFilePermission(IWebConnection webConnection, string FileName)
		{
			IFileContainer fileContainer = FileHandler.OpenFile(FileName);
			FilePermissionEnum? permission = fileContainer.LoadPermission(webConnection.Session.User.Id);
			
			if (null != permission)
				return WebResults.FromString(Status._200_OK, permission.ToString());
			else
				return WebResults.FromString(Status._200_OK, "None");
		}

        /// <summary>
        /// Renames a file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="NewFileName">The new file name</param>
        /// <param name="OldFileName">The old file name</param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults RenameFile(IWebConnection webConnection, string OldFileName, string NewFileName)
        {
            FileHandler.Rename(webConnection.Session.User, OldFileName, NewFileName);

            return WebResults.FromString(Status._202_Accepted, "File Renamed");
        }

        /// <summary>
        /// Copies a file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="DestinationFilename">The destination file</param>
        /// <param name="SourceFilename">The source file</param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Write)]
        public IWebResults CopyFile(IWebConnection webConnection, string SourceFilename, string DestinationFilename)
        {
            IFileContainer toCopy;

            try
            {
                if (SourceFilename.StartsWith("/"))
                    toCopy = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(SourceFilename);
                else
                    toCopy = FileHandler.OpenFile(SourceFilename);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(WebResults.FromString(
                    Status._404_Not_Found, SourceFilename + " not found"));
            }

            // Generate a filename, if needed
            bool generateFilename = false;

            if (null == DestinationFilename)
                generateFilename = true;
            else if (DestinationFilename.Length == 0)
                generateFilename = true;

            if (generateFilename)
                DestinationFilename = GenerateFilename(toCopy.Extension, toCopy.Filename);

            FilePermissionEnum? permissionToSource = toCopy.LoadPermission(webConnection.Session.User.Id);

            if (null != permissionToSource)
                if (permissionToSource.Value >= FilePermissionEnum.Read)
                {
                    try
                    {
                        FileHandler.CopyFile(webConnection.Session.User, toCopy, DestinationFilename, webConnection.Session.User.Id);
                    }
                    catch (DuplicateFile)
                    {
                        throw new WebResultsOverrideException(WebResults.FromString(
                            Status._404_Not_Found, DestinationFilename + " already exists"));
                    }
					catch (BadFileName)
					{
		                return WebResults.FromString(Status._409_Conflict, DestinationFilename + " is an invalid file name");
					}

                    // Return a Javascript wrapper to assist in the caller knowing more about the created file
                    IFileContainer newFile = FileHandler.OpenFile(DestinationFilename);
                    IWebResults wrapperResults = newFile.WebHandler.GetJSW(webConnection, null, null, false);
                    return WebResults.FromStream(Status._201_Created, wrapperResults.ResultsAsStream);
                }

            return WebResults.FromString(Status._401_Unauthorized, "Permission deined");
        }

        /// <summary>
        /// Handles when a user uploads a file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="File">The file as part of a MIME paramater</param>
        [WebCallable(WebCallingConvention.POST_multipart_form_data, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults Upload(IWebConnection webConnection, MimeReader.Part File)
        {
            if (null == File)
                return WebResults.FromString(Status._412_Precondition_Failed, "Expected MIME part File");

            string filename;
            string fileFactoryType = GetFileFactoryTypeFromUpload(File, out filename);

            if (FileHandler.IsFilePresent(filename))
                return WebResults.FromString(Status._409_Conflict, filename + " already exists");

            string tempFile = Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllBytes(tempFile, File.Contents);

                FileHandler.RestoreFile(filename, fileFactoryType, tempFile, webConnection.Session.User.Id);
            }
            catch (BadFileName bfn)
            {
                return WebResults.FromString(Status._403_Forbidden, bfn.Message);
            }
            finally
            {
                System.IO.File.Delete(tempFile);
            }

            string results = "Filename: " + filename + "<br/><br/>FileTypeId: " + fileFactoryType + "<br />";

            return WebResults.FromString(Status._201_Created, results);
        }

        /// <summary>
        /// Figures out the fileFactoryType from an upload
        /// </summary>
        /// <param name="File"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        protected string GetFileFactoryTypeFromUpload(MimeReader.Part File, out string filename)
        {
            // Figure out what kind of factory to use
            string contentType = File.ContentType;
            filename = File.ContentDisposition["FILENAME"];

            // the file.extension convention will be used
            string[] filenameSplitAtDot = filename.Split('.');
            string extension = filenameSplitAtDot[filenameSplitAtDot.Length - 1];

            IFileContainer extensionContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Shell/Upload/ByExtension");
            INameValuePairsHandler extensionHandler = extensionContainer.CastFileHandler<INameValuePairsHandler>();

            // If there's a specific file type given the extension, let it override the MIME type
            if (extensionHandler.Contains(extension))
                return extensionHandler[extension];
            else
            {
                string fileFactoryType = DetermineFileType(extension);

                if (null != fileFactoryType)
                    return fileFactoryType;

                else if (null != contentType)
                {
                    IFileContainer mimeContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Shell/Upload/ByMime");
                    INameValuePairsHandler mime = mimeContainer.CastFileHandler<INameValuePairsHandler>();

                    fileFactoryType = mime[contentType];

                    if (null == fileFactoryType)
                        fileFactoryType = "binary";

                    return fileFactoryType;
                }
                else
                    // If all else fails, just treat the file like its bimary
                    return "binary";
            }
        }

        /// <summary>
        /// If there is an index file set, this sets the implicit action to be something else
        /// </summary>
        public override string ImplicitAction
        {
            get
            {
                if (null != FileHandler.IndexFile)
                    return "ShellToIndexFile";

                return base.ImplicitAction;
            }
        }

        /// <summary>
        /// Sets the index file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="IndexFile"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Administer)]
        public IWebResults SetIndexFile(IWebConnection webConnection, string IndexFile)
        {
            if (null != IndexFile)
                if (IndexFile.Length > 0)
                {
                    FileHandler.IndexFile = IndexFile;
                    return WebResults.FromString(Status._202_Accepted, "Index file is now: " + FileHandler.IndexFile);
                }

            FileHandler.IndexFile = null;
            return WebResults.FromString(Status._202_Accepted, "Index file disabled");
        }

        /// <summary>
        /// Returns the name of the index file, or "No index file" if there is none
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults GetIndexFile(IWebConnection webConnection)
        {
            if (null != FileHandler.IndexFile)
                return WebResults.FromString(Status._200_OK, FileHandler.IndexFile);
            else
                return WebResults.FromString(Status._200_OK, "No index file");
        }

        /// <summary>
        /// Returns the results of shelling to the index file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Naked, FilePermissionEnum.Read)]
        public IWebResults ShellToIndexFile(IWebConnection webConnection)
        {
            string fullPathToIndexFile;

            if (null != FileHandler.IndexFile)
                if (FileHandler.IndexFile.StartsWith("/"))
                    fullPathToIndexFile = FileHandler.IndexFile;
                else
                    fullPathToIndexFile = string.Format(
                        "{0}/{1}",
                        FileContainer.FullPath,
                        FileHandler.IndexFile);
            else
                fullPathToIndexFile = FileContainer.FullPath;

            if (0 == webConnection.GetParameters.Count)
                return webConnection.ShellTo(fullPathToIndexFile);
            else
            {
                RequestParameters newGetParameters = new RequestParameters();
                foreach (KeyValuePair<string, string> getParameter in webConnection.GetParameters)
                    if (getParameter.Key != "Method")
                        newGetParameters.Add(getParameter);

                return webConnection.ShellTo(fullPathToIndexFile + "?" + newGetParameters);
            }
        }

        /// <summary>
        /// Sends a packet whenever the contents of the directory changes.  The packet contains the contents of the directory tailored to the user
        /// </summary>
        [ChannelEndpointMinimumPermission(FilePermissionEnum.Read)]
        public IChannelEventWebAdaptor ChangingEvent
        {
            get
            {
                if (!ChangingEventWired)
                    using (TimedLock.Lock(_ChangingEvent))
                        if (!ChangingEventWired)
                        {
                            ChangingEventWired = true;
                            FileHandler.DirectoryChanged += new EventHandler<IDirectoryHandler, EventArgs>(FileHandler_DirectoryChanged);
                        }

                return _ChangingEvent;
            }
        }
        private readonly ChannelEventWebAdaptor _ChangingEvent = new ChannelEventWebAdaptor();

        private bool ChangingEventWired = false;

        void FileHandler_DirectoryChanged(IDirectoryHandler sender, EventArgs e)
        {
            List<IFileContainer> files = new List<IFileContainer>(FileHandler.Files);

            foreach (IQueuingReliableCometTransport channel in _ChangingEvent.Channels)
                ThreadPool.QueueUserWorkItem(
                    delegate(object state)
                    {
                        UpdateConnectionFiles((IQueuingReliableCometTransport)state, files);
                    },
                    channel);
        }

        /// <summary>
        /// Helper to update the files on a connection
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="files"></param>
        private void UpdateConnectionFiles(IQueuingReliableCometTransport channel, IList<IFileContainer> files)
        {
            IList<IDictionary<string, object>> filesForJSON = GetFilesForJSON(channel.Session, files);

            Dictionary<string, object> toSend = new Dictionary<string, object>();
            toSend["Timestamp"] = DateTime.UtcNow;
            toSend["Files"] = filesForJSON;

            channel.Send(toSend, TimeSpan.Zero);
        }
    }
}
