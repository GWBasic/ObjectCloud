// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    public abstract class WebServerBase : IWebServer
    {
        private ILog log = LogManager.GetLogger(typeof(WebServerBase));
                
        public WebServerBase(int port)
        {
            _Port = port;
        }

        public virtual void StartServer()
        {
            // Don't start if the server is already running
            if (Running)
                return;

            AcceptingSockets = false;

			// Register an event handler to preload default ObjectCloud objects
			EventHandler<IFileSystemResolver, EventArgs> preloadObjects = delegate(IFileSystemResolver sender, EventArgs e)
			{
				StartExecutionEnvironments();
			};
			FileHandlerFactoryLocator.FileSystemResolver.Started += preloadObjects;
			
			EventHandler<IFileSystemResolver, EventArgs> abortInError = delegate(IFileSystemResolver sender, EventArgs e)
			{
                using (TimedLock.Lock(AcceptingSocketsSignal))
				    Monitor.PulseAll(AcceptingSocketsSignal);
			};

            _RequestDelegateQueue = new DelegateQueue("Request handler", NumConcurrentRequests);
			RequestDelegateQueue.BusyThreshold = BusyThreshold;
			
			try
			{
	            Thread thread = new Thread(new ThreadStart(RunServer));
	
	            thread.IsBackground = true;
	            thread.Name = GetType().FullName;
	
				FileHandlerFactoryLocator.FileSystemResolver.Stopped += abortInError;
				
				try
				{
		            thread.Start();

					// Wait for the web server to start
					Thread.BeginCriticalRegion();
					try
					{
			            if (!AcceptingSockets && (null == TerminatingException))
							using (TimedLock.Lock(AcceptingSocketsSignal))
				                Monitor.Wait(AcceptingSocketsSignal);
					}
					finally
					{
						Thread.EndCriticalRegion();
					}
				}
				finally
				{
					FileHandlerFactoryLocator.FileSystemResolver.Stopped -= abortInError;
				}
			}
			finally
			{
				FileHandlerFactoryLocator.FileSystemResolver.Started -= preloadObjects;
			}
	
            // If the web server couldn't start due to an exception, then re-throw the exception
            if (null != TerminatingException)
                throw TerminatingException;
        }

        public virtual void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Flag to indicate that StartServer() can return.  It should not return until sockets can be accepted 
        /// </summary>
        protected bool AcceptingSockets
		{
			get { return _AcceptingSockets; }
			set
			{
				_AcceptingSockets = value;
				
				if (value)
					using (TimedLock.Lock(AcceptingSocketsSignal))
						Monitor.PulseAll(AcceptingSocketsSignal);
			}
		}
        private volatile bool _AcceptingSockets = false;
		private object AcceptingSocketsSignal = new object();

        /// <summary>
        /// The port to accept connections on
        /// </summary>
        public int Port
        {
            get { return _Port; }
            set
            {
                if (Running)
                    throw new WebServerException("Can not change the port while the web server is running!");

                _Port = value;
            }
        }
        int _Port;

        /// <summary>
        /// Set to false and ping the port to stop
        /// </summary>
        public bool Running
        {
            get { return _Running; }
        }
        protected volatile bool _Running = false;

        /// <summary>
        /// The server's thread
        /// </summary>
        public Thread ServerThread
        {
            get { return _ServerThread; }
        }
        protected Thread _ServerThread;

        /// <summary>
        /// The file system resolver used with this particular web server
        /// </summary>
        public IFileSystemResolver FileSystemResolver
        {
            get { return fileSystemResolver; }
            set { fileSystemResolver = value; }
        }
        private IFileSystemResolver fileSystemResolver;

        /// <summary>
        /// The type of web server that this is
        /// </summary>
        public string ServerType
        {
            get { return _ServerType; }
            set { _ServerType = value; }
        }
        private string _ServerType;

        /// <summary>
        /// The amount of time that the server waits before aborting the thread that handles incoming sockets
        /// </summary>
        public TimeSpan TimeToWaitForServerThreadToComplete
        {
            get { return _TimeToWaitForServerThreadToComplete; }
            set { _TimeToWaitForServerThreadToComplete = value; }
        }
        private TimeSpan _TimeToWaitForServerThreadToComplete = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The service locator.  This should be set in Spring so that these assemblies aren't dependant on Spring
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get
            {
                if (null == _FileHandlerFactoryLocator)
                    log.Warn("FileHandlerFactoryLocator is not set.  This must be set in Spring");

                return _FileHandlerFactoryLocator;
            }
            set
            {
                _FileHandlerFactoryLocator = value;
                value.WebServer = this;
            }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The object that resolves web components
        /// </summary>
        public IWebComponentResolver WebComponentResolver
        {
            get { return _WebComponentResolver; }
            set { _WebComponentResolver = value; }
        }
        private IWebComponentResolver _WebComponentResolver;

        /// <summary>
        /// Occurs when the web server is terminated
        /// </summary>
        public event EventHandler<EventArgs> WebServerTerminated;

        /// <summary>
        /// Calls WebServerTerminated
        /// </summary>
        /// <param name="e"></param>
        protected void OnWebServerTerminated(EventArgs e)
        {
            if (null != WebServerTerminated)
                WebServerTerminated(this, e);
        }

        /// <summary>
        /// Occurs when the web server is terminating, but before it is terminating
        /// </summary>
        public event EventHandler<EventArgs> WebServerTerminating;

        /// <summary>
        /// Calls WebConnectionStarting
        /// </summary>
        /// <param name="e"></param>
        protected void OnWebServerTerminating(EventArgs e)
        {
            if (null != WebServerTerminating)
                WebServerTerminating(this, e);
        }

        public uint MaxInMemoryContentSize
        {
            get { return _MaxInMemoryContentSize; }
            set { _MaxInMemoryContentSize = value; }
        }
        private uint _MaxInMemoryContentSize = 1024 * 1024;

        public uint MaxContentSize
        {
            get { return _MaxContentSize; }
            set { _MaxContentSize = value; }
        }
        private uint _MaxContentSize = 150 * 1024 * 1024;

        public bool KeepAlive
        {
            get { return _KeepAlive; }
            set { _KeepAlive = value; }
        }
        private bool _KeepAlive = true;

        /// <summary>
        /// The object that generates code for javascript access to a WebHandler
        /// </summary>
        public IWebAccessCodeGenerator JavascriptWebAccessCodeGenerator
        {
            get { return _JavascriptWebAccessCodeGenerator; }
            set { _JavascriptWebAccessCodeGenerator = value; }
        }
        private IWebAccessCodeGenerator _JavascriptWebAccessCodeGenerator;

        /*public IWebResults ShellTo(IUser user, WebMethod method, string url, byte[] content, string contentType, CallingFrom callingFrom, bool bypassJavascript)
        {
            ISession session = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();

            if (null != user)
                session.User = user;

            ShellWebConnection shellWebConnection = new BlockingShellWebConnection(
                this,
                session,
                url,
                content,
                contentType,
                new CookiesFromBrowser(),
                callingFrom,
                method);

            return shellWebConnection.GenerateResultsForClient();
        }*/

        public bool RedirectIfRequestedHostIsDifferent
        {
            get { return _RedirectIfRequestedHostIsDifferent; }
            set { _RedirectIfRequestedHostIsDifferent = value; }
        }
        private bool _RedirectIfRequestedHostIsDifferent = false;

        public bool MinimizeJavascript
        {
            get { return _MinimizeJavascript; }
            set { _MinimizeJavascript = value; }
        }
        private bool _MinimizeJavascript = true;

        /// <summary>
        /// The size of the header to buffer when reading headers.  Any headers sent that are longer then this buffer size will be aborted.
        /// </summary>
        public int HeaderSize
        {
            get { return _HeaderSize; }
            set { _HeaderSize = value; }
        }
        private int _HeaderSize;

        /// <summary>
        /// The maxiumum amount of time that can elapse when reading a header
        /// </summary>
        public TimeSpan HeaderTimeout
        {
            get { return _HeaderTimeout; }
            set { _HeaderTimeout = value; }
        }
        private TimeSpan _HeaderTimeout;

        /// <summary>
        /// The maximum time that can elapse when reading content
        /// </summary>
        public TimeSpan ContentTimeout
        {
            get { return _ContentTimeout; }
            set { _ContentTimeout = value; }
        }
        private TimeSpan _ContentTimeout;

        /// <summary>
        /// The size of buffer to use when sending
        /// </summary>
        public int SendBufferSize
        {
            get { return _SendBufferSize; }
            set { _SendBufferSize = value; }
        }
        private int _SendBufferSize;

        public abstract void RunServer();

        public void Stop()
        {
            try
            {
                StopImpl();
            }
            finally
            {
                ThreadPool.QueueUserWorkItem(
                    delegate(object state)
                    {
                        try
                        {
                            ((DelegateQueue)state).Stop();
                        }
                        catch (Exception e)
                        {
                            log.Error("Exception shutting down request handling threads", e);
                        }
                    },
                    RequestDelegateQueue);

                _RequestDelegateQueue = null;
            }
        }

        protected abstract void StopImpl();

        public bool CachingEnabled
        {
            get { return _CachingEnabled; }
            set { _CachingEnabled = value; }
        }
        private bool _CachingEnabled = true;

        public double CheckDeadConnectionsFrequencySeconds
        {
            get { return _CheckDeadConnectionsFrequencySeconds; }
            set { _CheckDeadConnectionsFrequencySeconds = value; }
        }
        private double _CheckDeadConnectionsFrequencySeconds = 60;

        public double MaxConnectionIdleSeconds
        {
            get { return _MaxConnectionIdleSeconds; }
            set { _MaxConnectionIdleSeconds = value; }
        }
        private double _MaxConnectionIdleSeconds = 300;

        public string CacheRAMThreashold
        {
            get
            {
                IEnumerable<long> memorySizeLimits = Cache.MemorySizeLimits;
                if (null == memorySizeLimits)
                    return null;

                return StringGenerator.GenerateCommaSeperatedList(memorySizeLimits);
            }
            set
            {
                if (null == value)
                    Cache.MemorySizeLimits = null;
                else
                {
                    IEnumerable<string> values = StringParser.ParseCommaSeperated(value);

                    List<long> toSet = new List<long>();

                    foreach (string toParse in values)
                    {
                        long parsed = default(long);
                        if (long.TryParse(toParse, out parsed))
                            toSet.Add(parsed);
                    }

                    Cache.MemorySizeLimits = toSet;
                }
            }
        }

        public long? CacheRAMMaxMemory
        {
            get
            {
                return Cache.MaxMemory;
            }
            set
            {
                Cache.MaxMemory = value;
            }
        }

        public int CacheRAMMinReferences
        {
            get
            {
                return Cache.MinCacheReferences;
            }
            set
            {
                Cache.MinCacheReferences = value;
            }
        }

        public long CacheRAMMaxReferences
        {
            get
            {
                return Cache.MaxCacheReferences;
            }
            set
            {
                Cache.MaxCacheReferences = value;
            }
        }

        public double CachePercentOfMaxWorkingSet
        {
            get
            {
                return Cache.PercentOfMaxWorkingSet;
            }
            set
            {
                Cache.PercentOfMaxWorkingSet = value;
            }
        }

        public Exception TerminatingException
        {
            get { return _TerminatingException; }
            set 
            {
                _TerminatingException = value;
                using (TimedLock.Lock(AcceptingSocketsSignal))
                    Monitor.PulseAll(AcceptingSocketsSignal);
            }
        }
        private Exception _TerminatingException = null;
		
		/// <summary>
		/// Comma-seperated list of all of the objects to pre-load.  Directories will be traversed 
		/// </summary>
		public string PreloadedObjects
		{
			get { return _PreloadedObjects; }
			set { _PreloadedObjects = value; }
		}
		private string _PreloadedObjects = null;
		
        /// <summary>
        /// Execution environments are started prior to making the web server available.  This is because loading them on-demand will cause a poor response time
        /// </summary>
		private void StartExecutionEnvironments()
		{
            ThreadPool.QueueUserWorkItem(PreloadObjects);
        }

        /// <summary>
        /// Objects are loaded asyncronously on another thread because we really don't need to wait for them in order to have snappy performance
        /// </summary>
        /// <param name="state"></param>
        private void PreloadObjects(object state)
        {
            log.Debug("Pre-loading system objects");
            DateTime start = DateTime.UtcNow;

			try
			{
				if (null == PreloadedObjects)
					return;
				
				IEnumerable<string> preloadedObjects = StringParser.ParseCommaSeperated(PreloadedObjects);
				IEnumerable<IFileContainer> toPreLoad = GetObjectsAndTraverseDirectories(preloadedObjects);
				
				Enumerable<IFileContainer>.MultithreadedEach(
					1,
					toPreLoad,
					delegate(IFileContainer fileContainer)
					{
						fileContainer.WebHandler.GetOrCreateExecutionEnvironment();
					});

                TimeSpan loadTime = DateTime.UtcNow - start;
                log.Info("Pre-loading system objects took " + loadTime.TotalSeconds.ToString() + " seconds");
			}
			catch (Exception e)
			{
				log.Error("Error pre-loading objects", e);
			}
		}
		
		private IEnumerable<IFileContainer> GetObjectsAndTraverseDirectories(IEnumerable<string> objectNames)
		{
			foreach (string objectName in objectNames)
			{
				IFileContainer toYield = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(objectName);
				yield return toYield;
				
				if (toYield.FileHandler is IDirectoryHandler)
					foreach (IFileContainer subToYield in GetObjectsAndTraverseDirectories((IDirectoryHandler)toYield.FileHandler))
						yield return subToYield;
			}
		}
		
		private IEnumerable<IFileContainer> GetObjectsAndTraverseDirectories(IDirectoryHandler directory)
		{
			foreach (IFileContainer toYield in directory.Files)
			{
				yield return toYield;
				
				if (toYield.FileHandler is IDirectoryHandler)
					foreach (IFileContainer subToYield in GetObjectsAndTraverseDirectories((IDirectoryHandler)toYield.FileHandler))
						yield return subToYield;
			}
		}

        /// <summary>
        /// The number of concurrent requests that the web server will handle.  Defaults to 2 per core 
        /// </summary>
        public int NumConcurrentRequests
        {
            get { return this._NumConcurrentRequests; }
            set { _NumConcurrentRequests = value; }
        }
        private int _NumConcurrentRequests = 2 * Environment.ProcessorCount;

        /// <summary>
        /// The delegate queue that handles requests
        /// </summary>
        public DelegateQueue RequestDelegateQueue
        {
            get { return _RequestDelegateQueue; }
        }
        private DelegateQueue _RequestDelegateQueue;

		/// <summary>
		/// The busy threshold for queued web requests.  Defaults to 6 times the processor count.  When this limit is hit, the server will stop accepting incoming requests 
		/// </summary>
		public int BusyThreshold
		{
			get { return this._BusyThreshold; }
			set { _BusyThreshold = value; }
		}
		private int _BusyThreshold = 6 * Environment.ProcessorCount;
	}
}
