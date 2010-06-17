// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;

using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Locates all of the different kinds of factories for files
    /// </summary>
    public class FileHandlerFactoryLocator
    {
        /// <summary>
        /// The file handler factories
        /// </summary>
        public Dictionary<string, IFileHandlerFactory> FileHandlerFactories
        {
            get { return _FileHandlerFactories; }
            set { _FileHandlerFactories = value; }
        }
        private Dictionary<string, IFileHandlerFactory> _FileHandlerFactories;

        /// <summary>
        /// The Web Handler classes.  These must support initialization without a constructor
        /// </summary>
        public Dictionary<string, Type> WebHandlerClasses
        {
            get { return _WebHandlerClasses; }
            set { _WebHandlerClasses = value; }
        }
        private Dictionary<string, Type> _WebHandlerClasses;

        /// <summary>
        /// Plugins for "WebHandlers" that are tacked on to all objects to add global behaviors to all objects
        /// </summary>
        public List<Type> WebHandlerPlugins
        {
            get { return _WebHandlerPlugins; }
            set { _WebHandlerPlugins = value; }
        }
        private List<Type> _WebHandlerPlugins = new List<Type>();

        /// <summary>
        /// Returns the name of the file handler factory
        /// </summary>
        /// <param name="fileHandlerFactory"></param>
        /// <returns></returns>
        public string GetFactoryName(IFileHandlerFactory fileHandlerFactory)
        {
            foreach (string factoryName in FileHandlerFactories.Keys)
                if (fileHandlerFactory == FileHandlerFactories[factoryName])
                    return factoryName;

            throw new UnknownFileHandlerFactory();
        }

        /// <summary>
        /// Thrown if an attempt is made to get the name of an unregistered FileHandlerFactory
        /// </summary>
        public class UnknownFileHandlerFactory : DiskException
        {
            internal UnknownFileHandlerFactory() : base("An unknown IFileHandlerFactory was used to find its name") { }
        }

        /// <summary>
        /// Provides information about the file system
        /// </summary>
        public IFileSystem FileSystem
        {
            get { return _FileSystem; }
            set { _FileSystem = value; }
        }
        private IFileSystem _FileSystem;

        /// <summary>
        /// Resolves different files in the file system
        /// </summary>
        public IFileSystemResolver FileSystemResolver
        {
            get { return _FileSystemResolver; }
            set { _FileSystemResolver = value; }
        }
        private IFileSystemResolver _FileSystemResolver;

        /// <summary>
        /// Creates the root directory when it does not exist
        /// </summary>
        public IRootDirectoryCreator RootDirectoryCreator
        {
            get { return _RootDirectoryCreator; }
            set { _RootDirectoryCreator = value; }
        }
        private IRootDirectoryCreator _RootDirectoryCreator;

        /// <summary>
        /// Used to access directory objects
        /// </summary>
        public IFileHandlerFactory<IDirectoryHandler> DirectoryFactory
        {
            get { return (IFileHandlerFactory<IDirectoryHandler>)FileHandlerFactories["directory"]; }
        }

        /// <summary>
        /// Creates a user handler
        /// </summary>
        public ISystemFileHandlerFactory UserHandlerFactory
        {
            get { return (ISystemFileHandlerFactory)FileHandlerFactories["user"]; }
        }

        /// <summary>
        /// Creates the user manager
        /// </summary>
        public ISystemFileHandlerFactory UserManagerHandlerFactory
        {
            get { return (ISystemFileHandlerFactory)FileHandlerFactories["usermanager"]; }
        }

        /// <summary>
        /// The user manager
        /// </summary>
        public IUserManagerHandler UserManagerHandler
        {
            get 
            {
                if (null == _UserManagerHandler)
                    _UserManagerHandler = (IUserManagerHandler)FileSystemResolver.ResolveFile("/Users/UserDB").FileHandler;

                return _UserManagerHandler; 
            }
        }
        private IUserManagerHandler _UserManagerHandler = null;

        /// <summary>
        /// The user factory
        /// </summary>
        public IUserFactory UserFactory
        {
            get { return _UserFactory; }
            set { _UserFactory = value; }
        }
        private IUserFactory _UserFactory;

        /// <summary>
        /// The session manager handler
        /// </summary>
        public ISessionManagerHandler SessionManagerHandler
        {
            get 
            {
                if (null == _SessionManagerHandler)
                    _SessionManagerHandler = FileSystemResolver.ResolveFile("/System/SessionManager").CastFileHandler<ISessionManagerHandler>();

                return _SessionManagerHandler;
            }
        }
        private ISessionManagerHandler _SessionManagerHandler = null;

        /// <summary>
        /// The web server
        /// </summary>
        public IWebServer WebServer
        {
            get { return _WebServer; }
            set { _WebServer = value; }
        }
        private IWebServer _WebServer;

        /// <summary>
        /// Factory for creating server-side Javascript execution environments
        /// </summary>
        public IExecutionEnvironmentFactory ExecutionEnvironmentFactory
        {
            get { return _ExecutionEnvironmentFactory; }
            set { _ExecutionEnvironmentFactory = value; }
        }
        private IExecutionEnvironmentFactory _ExecutionEnvironmentFactory;

        /// <summary>
        /// The hostname that this FileSystem is for.  Someday the server will need to support multiple hosts using appdomains.
        /// </summary>
        public string Hostname
        {
            get { return _Hostname; }
            set { _Hostname = value; }
        }
        private string _Hostname;

        /// <summary>
        /// Returns the hostname with the port number, if applicable
        /// </summary>
        public string HostnameAndPort
        {
            get
            {
                if (null == WebServer)
                    return Hostname;
                else if (80 == WebServer.Port)
                    return Hostname;
                else
                    return string.Format("{0}:{1}", Hostname, WebServer.Port);
            }
        }

        /// <summary>
        /// Cache of web methods
        /// </summary>
        public IWebMethodCache WebMethodCache
        {
            get { return _WebMethodCache; }
            set { _WebMethodCache = value; }
        }
        private IWebMethodCache _WebMethodCache;

        /// <summary>
        /// The plugins; ObjectCloud initializes these prior to starting the file system
        /// </summary>
        public List<Plugin> Plugins
        {
            get { return _Plugins; }
            set { _Plugins = value; }
        }
        private List<Plugin> _Plugins = new List<Plugin>();
    }
}
