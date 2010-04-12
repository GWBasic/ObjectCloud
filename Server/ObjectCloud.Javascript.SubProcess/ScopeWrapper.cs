// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Wraps a Javascript scope on a per-user basis
    /// </summary>
    public class ScopeWrapper : IDisposable
    {
        private static ILog log = LogManager.GetLogger<ScopeWrapper>();
		
		private static Wrapped<int> IdCtr = int.MinValue;

        public int ScopeId
        {
            get { return _ScopeId; }
        }
        private readonly int _ScopeId;

        private ISubProcessFactory SubProcessFactory;
        private IFileContainer JavascriptContainer;

        public SubProcess SubProcess
        {
            get { return _SubProcess; }
        }
        private SubProcess _SubProcess = null;

        /// <summary>
        /// Pointers to cache IDs
        /// </summary>
        private Dictionary<object, object> CacheIDsByKey;

        /// <summary>
        /// Returns a new CacheID
        /// </summary>
        /// <returns></returns>
        public object GenerateCacheID(object key)
        {
            using (TimedLock.Lock(CacheIDsByKey))
            {
                object toReturn;

                do
                    toReturn = SRandom.Next();
                while (CacheIDsByKey.ContainsKey(toReturn));

                CacheIDsByKey[key] = toReturn;

                return toReturn;
            }
        }

        /// <summary>
        /// Gets the cache id for the object, or returns false if it's missing
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool GetCacheID(object key, out object cacheId)
        {
            return CacheIDsByKey.TryGetValue(key, out cacheId);
        }

        /// <summary>
        /// Creates a scope wrapper
        /// </summary>
        /// <param name="fileHandlerFactoryLocator"></param>
        /// <param name="webConnection"></param>
        /// <param name="javascript"></param>
        /// <param name="fileContainer"></param>
        public ScopeWrapper(
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            IFileContainer javascriptContainer,
            ISubProcessFactory subProcessFactory,
            IFileContainer fileContainer)
        {
            _FileContainer = fileContainer;
            SubProcessFactory = subProcessFactory;
            JavascriptContainer = javascriptContainer;

            using (TimedLock.Lock(IdCtr))
            {
                _ScopeId = IdCtr.Value;

                if (int.MaxValue == IdCtr.Value)
                    IdCtr.Value = int.MinValue;
                else
                    IdCtr.Value++;
            }

            _FileHandlerFactoryLocator = fileHandlerFactoryLocator;

            ConstructScope();
        }

        private void ConstructScope()
        {
            log.Info("Constructing Javascript scope for " + FileContainer.FullPath);

            CacheIDsByKey = new Dictionary<object, object>();

            SubProcess subProcess = SubProcessFactory.GetOrCreateSubProcess(JavascriptContainer);
            _SubProcess.RegisterParentFunctionDelegate(ScopeId, CallParentFunction);
            FunctionCallers = new Dictionary<string, FunctionCaller>();

            ISession ownerSession = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();
            SubProcess.CreateScopeResults data;

            try
            {
                ownerSession.User = FileContainer.Owner;

                IWebConnection ownerWebConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    ownerSession,
                    FileContainer.FullPath,
                    null,
                    null,
                    new CookiesFromBrowser(),
                    CallingFrom.Web,
                    WebMethod.GET);

                data = FunctionCaller.UseTemporaryCaller<SubProcess.CreateScopeResults>(
                    this, FileContainer, ownerWebConnection, delegate()
                    {
                        return subProcess.CreateScope(
                            ScopeId,
                            Thread.CurrentThread.ManagedThreadId,
                            CreateMetadata());
                    });
            }
            finally
            {
                FileHandlerFactoryLocator.SessionManagerHandler.EndSession(ownerSession.SessionId);
            }

            // Initialize each function caller
            foreach (KeyValuePair<string, SubProcess.EvalScopeFunctionInfo> functionKVP in data.Functions)
            {
                string functionName = functionKVP.Key;
                SubProcess.EvalScopeFunctionInfo functionInfo = functionKVP.Value;
                Dictionary<string, object> properties = functionInfo.Properties;

                // ... and it's marked as webCallable, create a FunctionCaller
                if (properties.ContainsKey("webCallable"))
                {
                    FunctionCaller functionCaller = new FunctionCaller(this, FileContainer, functionName, functionInfo);

                    // ... and if the function caller supports the calling convention, then cache it!

                    if (null != functionCaller.WebDelegate)
                        FunctionCallers[functionName] = functionCaller;
                }
            }

            // Get options
            if (data.Result is Dictionary<string, object>)
                ParseOptions((Dictionary<string, object>)data.Result);

            // Don't switch over to the new sub process until everything is constructed
            _SubProcess = subProcess;
        }

        ~ScopeWrapper()
        {
            try
            {
                Dispose();
            }
            catch (Exception e)
            {
                try
                {
                    log.Error("Error finalizing: ", e);
                }
                catch { }
            }
        }

        internal bool Disposed = false;

        public void Dispose()
        {
            Disposed = true;
            _SubProcess.DisposeScope(ScopeId, Thread.CurrentThread.ManagedThreadId);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Generates runtime metadata for the server-side javascript
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="context"></param>
        private Dictionary<string, object> CreateMetadata()
        {
            Dictionary<string, object> toReturn = new Dictionary<string, object>();

            //Ability to know the following from within Javascript:  File name, file path, owner name, owner ID, connected user name, connected user id
            Dictionary<string, object> fileMetadata = new Dictionary<string, object>();
            fileMetadata["filename"] = FileContainer.Filename;
            fileMetadata["fullpath"] = FileContainer.FullPath;
            fileMetadata["url"] = FileContainer.ObjectUrl;

            if (null != FileContainer.OwnerId)
            {
                fileMetadata["ownerId"] = FileContainer.OwnerId.Value;

                IUserOrGroup owner = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupNoException(FileContainer.OwnerId.Value);
                if (null != owner)
                {
                    fileMetadata["owner"] = owner.Name;

                    if (owner is IUser)
                        fileMetadata["ownerIdentity"] = ((IUser)owner).Identity;
                }
            }

            toReturn["fileMetadata"] = fileMetadata;

            Dictionary<string, object> hostMetadata = new Dictionary<string, object>();
            hostMetadata["host"] = FileHandlerFactoryLocator.HostnameAndPort;
            hostMetadata["justHost"] = FileHandlerFactoryLocator.Hostname;
            hostMetadata["port"] = FileHandlerFactoryLocator.WebServer.Port;

            toReturn["hostMetadata"] = hostMetadata;

            return toReturn;
        }
		
        /// <summary>
        /// The FileHandlerFactoryLocator
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
        }
        private readonly FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The Function callers, indexed by function name
        /// </summary>
        private Dictionary<string, FunctionCaller> FunctionCallers;

        /// <summary>
        /// The wrapped object
        /// </summary>
        public IFileContainer FileContainer
        {
            get { return _FileContainer; }
        }
        private readonly IFileContainer _FileContainer;

        /// <summary>
        /// Returns the appropriate delegate for the named method, or null if it doesn't exist
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public WebDelegate GetMethod(string method)
        {
            if (FunctionCallers.ContainsKey(method))
                return FunctionCallers[method].WebDelegate;
            else
                return null;
        }

        /// <summary>
        /// Calls a function in the sub-process
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the sub process was disposed through normal execution.</exception>
        /// <exception cref="AbortedException">Thrown if the sub process aborted anormally.  Callers should recover from this error condition</exception>
        public object CallFunction(IWebConnection webConnection, string functionName, IEnumerable arguments)
        {
            int tryCtr = 0;

            while (true)
                try
                {
                    return _SubProcess.CallFunctionInScope(ScopeId, Thread.CurrentThread.ManagedThreadId, functionName, arguments);
                }
                catch (SubProcess.AbortedException)
                {
                    // Allow a max of 3 tries
                    if (tryCtr >= 3)
                        throw;

                    tryCtr++;
                    ConstructScope();
                }
        }

        /// <summary>
        /// Generates a Javscript wrapper for the browser that calls functions in this javascript
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GenerateJavascriptWrapper()
        {
            foreach (string function in FunctionCallers.Keys)
                foreach (string wrapperFunction in FunctionCallers[function].GenerateWrapper())
                    yield return wrapperFunction;
        }

        /// <summary>
        /// Callback for when Javascript calls back into C#
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="threadId"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private object CallParentFunction(string functionName, object threadId, object[] arguments)
        {
            try
            {
                MethodInfo toInvoke = SubProcess.FunctionsInScope[functionName];
                ParameterInfo[] parameters = toInvoke.GetParameters();

                object[] allArguments = new object[parameters.Length];

                for (int argCtr = 0; argCtr < allArguments.Length; argCtr++)
                {
                    ParameterInfo parameter = parameters[argCtr];
                    Type parameterType = parameter.ParameterType;

                    // If this is a parameter that's passed in from javascript
                    if (argCtr < arguments.Length)
                    {
                        object argument = arguments[argCtr];

                        // First see if either the JsInstance or it's value can be directly accepted without converstion
                        if (parameterType.IsInstanceOfType(argument))
                            allArguments[argCtr] = argument;
                        else
                            allArguments[argCtr] = Convert.ChangeType(argument, parameterType);
                    }
                    else
                        allArguments[argCtr] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
                }

                return SubProcess.FunctionsInScope[functionName].Invoke(null, allArguments);
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        /// <summary>
        /// Loads options from the javascript
        /// </summary>
        private void ParseOptions(Dictionary<string, object> options)
        {
            object blockWebMethods = null;
            if (options.TryGetValue("BlockWebMethods", out blockWebMethods))
                try
                {
                    _BlockWebMethods = Convert.ToBoolean(blockWebMethods);
                }
                catch (Exception e)
                {
                    log.Error("Error when parsing options.BlockWebMethods", e);
                }
        }

        /// <summary>
        /// Returns true if underlying web methods are blocked, false otherwise
        /// </summary>
        public bool BlockWebMethods
        {
            get { return _BlockWebMethods; }
        }
        private bool _BlockWebMethods = true;

        /// <summary>
        /// When the loaded libraries were last modified
        /// </summary>
        Dictionary<string, DateTime> LoadedLibrariesLastModified = new Dictionary<string, DateTime>();

        /// <summary>
        /// Used to create an ID to uniquely identify the parent directory wrapper once its created
        /// </summary>
        private static readonly object parentDirectoryWrapperKey = new object();

        /// <summary>
        /// Gets the parent directory wrapper
        /// </summary>
        /// <returns></returns>
        public object GetParentDirectoryWrapper(IWebConnection webConnection)
        {
            object cacheID;

            if (GetCacheID(parentDirectoryWrapperKey, out cacheID))
                return new SubProcess.CachedObjectId(cacheID);

            IDirectoryHandler parentDirectoryHandler = FileContainer.ParentDirectoryHandler;

            if (null == parentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, "The root directory has no parent directory"));

            IWebResults webResults = parentDirectoryHandler.FileContainer.WebHandler.GetJSW(webConnection, null, null, false);
            string webResultsAsString = webResults.ResultsAsString;

            return new SubProcess.StringToEval(
                "(" + webResultsAsString + ")",
                GenerateCacheID(parentDirectoryWrapperKey));
        }

        /// <summary>
        /// The times that objects were last modified
        /// </summary>
        Dictionary<string, DateTime> UseLastModified = new Dictionary<string, DateTime>();

        /// <summary>
        /// Loads the given Javascript library into the scope, if it is not yet loaded
        /// </summary>
        /// <param name="toLoad"></param>
        public object Use(IWebConnection webConnection, string toLoad)
        {
            // If the library isn't loaded, then the script needs to be loaded and executed
            IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(toLoad);

            // Just return if the user doesn't have permission to the file
            if (null == fileContainer.LoadPermission(webConnection.Session.User.Id))
                return false;

            ITextHandler textHandler;
            try
            {
                textHandler = fileContainer.CastFileHandler<ITextHandler>();
                LoadedLibrariesLastModified[toLoad] = fileContainer.LastModified;
            }
            catch (Exception e)
            {
                log.Warn("An attempt was made to load a Javascript library that is not a text file.", e);
                return false;
            }

            object cacheId;
            string cacheIdKey = "Use:::::::" + toLoad;

            // If the library is already loaded, then the return value is cached.
            if (GetCacheID(cacheIdKey, out cacheId))
            {
                if (fileContainer.LastModified == UseLastModified[toLoad])
                    return new SubProcess.CachedObjectId(cacheId);
            }
            else
                cacheId = GenerateCacheID(cacheIdKey);

            UseLastModified[toLoad] = fileContainer.LastModified;
            return new SubProcess.StringToEval(
                textHandler.ReadAll(),
                cacheId);
        }

        /// <summary>
        /// The times that objects were last modified
        /// </summary>
        Dictionary<string, DateTime> OpenLastModified = new Dictionary<string, DateTime>();

        /// <summary>
        /// Returns a wrapper to use the specified object
        /// </summary>
        /// <param name="toOpen"></param>
        /// <returns></returns>
        public object Open(IWebConnection webConnection, string toOpen)
        {
            object cacheId;
            string cacheIdKey = "Open-+-+-+-+" + toOpen;

            IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(toOpen);

            // If the library is already loaded, then the return value is cached.
            if (GetCacheID(cacheIdKey, out cacheId))
            {
                if (fileContainer.LastModified == OpenLastModified[toOpen])
                    return new SubProcess.CachedObjectId(cacheId);
            }
            else
                cacheId = GenerateCacheID(cacheIdKey);

            string wrapper = fileContainer.WebHandler.GetJSW(webConnection, null, null, false).ResultsAsString;

            OpenLastModified[toOpen] = fileContainer.LastModified;
            return new SubProcess.StringToEval(
                "(" + wrapper + ")",
                cacheId);
        }
    }
}
