// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    public class FileContainer : IFileContainer
    {
        public FileContainer(
            IFileHandler fileHandler,
            ID<IFileContainer, long> fileId,
            string typeId,
            string filename,
            IDirectoryHandler parentDirectoryHandler,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            DateTime created)
            : this(fileId, typeId, filename, parentDirectoryHandler, fileHandlerFactoryLocator, created)
        {
            _FileHandler = fileHandler;
        }

        public FileContainer(
            ID<IFileContainer, long> fileId, 
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

        public ID<IFileContainer, long> FileId
        {
            get { return _FileId; }
        }
        private readonly ID<IFileContainer, long> _FileId;

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

        public bool HasNamedPermissions(ID<IUserOrGroup, Guid> userId, IEnumerable<string> namedPermissions)
        {
            // The owner can always administer
            if (OwnerId == userId)
                return true;

            // The root user can always administer
            if (userId == FileHandlerFactoryLocator.UserManagerHandler.Root.Id)
                return true;

            return ParentDirectoryHandler.HasNamedPermissions(FileId, namedPermissions, userId);
        }

        public static bool operator ==(FileContainer r, FileContainer l)
        {
            return r.FileHandler == l.FileHandler;
        }

        public static bool operator !=(FileContainer r, FileContainer l)
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
				{
                    _WebHandler = FileHandlerFactoryLocator.FileSystemResolver.LoadWebHandler(FileId, TypeId);
					_WebHandler.FileContainer = this;
                    _WebHandler.FileHandlerFactoryLocator = FileHandlerFactory.FileHandlerFactoryLocator;
                }
				
        		return _WebHandler;
        	}
        }
		private IWebHandler _WebHandler;

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

        public string ObjectUrl
        {
            get { return "http://" + FileHandlerFactoryLocator.HostnameAndPort + FullPath; }
        }

        public DateTime Created
        {
            get { return _Created; }
        }
        private DateTime _Created;
    }
}
