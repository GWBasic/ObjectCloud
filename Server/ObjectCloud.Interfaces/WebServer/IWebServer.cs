// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// The web server
    /// </summary>
    public interface IWebServer : IDisposable
    {
        /// <summary>
        /// Starts the web server.  This method blocks until the server is disposed; thus it should 
        /// run on its own thread
        /// </summary>
        void RunServer();

        /// <summary>
        /// Starts the web server on a seperate thread, blocks until the server is ready to accept incoming connections
        /// </summary>
        void StartServer();

        /// <summary>
        /// The server's thread
        /// </summary>
        Thread ServerThread { get; }

        /// <summary>
        /// The FileSystemResolver that the web server works with
        /// </summary>
        IFileSystemResolver FileSystemResolver { get; set; }

        /// <summary>
        /// The ServerType that is returned to the browser
        /// </summary>
        string ServerType { get; set; }

        /// <summary>
        /// Changes to false when the web server terminates
        /// </summary>
        bool Running { get;}

        /// <summary>
        /// Occurs when the web server is terminated
        /// </summary>
        event EventHandler<EventArgs> WebServerTerminated;

        /// <summary>
        /// Occurs when the web server is terminating, before it is termianted
        /// </summary>
        event EventHandler<EventArgs> WebServerTerminating;
		
        /// <summary>
        /// Stops the server and releases all resources
        /// </summary>
        void Stop();

        /// <summary>
        /// The service locator
        /// </summary>
        FileHandlerFactoryLocator FileHandlerFactoryLocator { get; set; }

        /// <summary>
        /// The object that resolves web components
        /// </summary>
        IWebComponentResolver WebComponentResolver { get; set;}

        /// <summary>
        /// The web server's port.  This can not be changed while the web server is running
        /// </summary>
        /// <exception cref="WebServerException">Thrown if the web server is running while the port is set</exception>
        int Port { get; set; }

        /// <summary>
        /// The maximum content-length to hold in memory, in bytes.  Content larger then this size will be held on disk
        /// </summary>
        uint MaxInMemoryContentSize { get; set;}

        /// <summary>
        /// The maximum content-length to accept, in bytes.  Content larger then this size will cause the connection to be closed and aborted
        /// </summary>
        uint MaxContentSize { get; set;}

        /// <summary>
        /// Setting for using HTTP KeepAlive, which keeps the socket open after a request has gone through.  Defaults to true
        /// </summary>
        bool KeepAlive { get; set; }

        /// <summary>
        /// The object that generates code for javascript access to a WebHandler
        /// </summary>
        IWebAccessCodeGenerator JavascriptWebAccessCodeGenerator { get; set; }

        /*// <summary>
        /// Shells to the URL as the given user, with the given postBody, HTTP method, and calling security level
        /// </summary>
        /// <param name="user"></param>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <param name="postBody"></param>
        /// <param name="httpVersion"></param>
        /// <param name="callingFrom"></param>
        /// <param name="bypassJavascript">Set to true to bypass server-side javascript</param>
        /// <returns></returns>
        IWebResults ShellTo(
		    IUser user,
            WebMethod method,
            string url,
            byte[] content,
            string contentType,
            CallingFrom callingFrom,
            bool bypassJavascript);*/

        /// <summary>
        /// When set to true, if the incoming HTTP request specifies a host that isn't exactly what the configured hostname is, then the
        /// request will be redirected.  Defaults to false.
        /// </summary>
        bool RedirectIfRequestedHostIsDifferent { get; set; }

        /*// <summary>
        /// Set to false to disable minimizing Javascript.  Defaults to true
        /// </summary>
        bool MinimizeJavascript { get; set; }*/

        /// <summary>
        /// The size of the header to buffer when reading headers.  Any headers sent that are longer then this buffer size will be aborted.
        /// </summary>
        int HeaderSize { get; set; }

        /// <summary>
        /// The maxiumum amount of time that can elapse when reading a header
        /// </summary>
        TimeSpan HeaderTimeout { get; set; }

        /// <summary>
        /// The maximum time that can elapse when reading content
        /// </summary>
        TimeSpan ContentTimeout { get; set; }

        /// <summary>
        /// Defaults to true, set to false to disable caching
        /// </summary>
        bool CachingEnabled { get; set; }

        /// <summary>
        /// How often the web server checks for dead connections
        /// </summary>
        double CheckDeadConnectionsFrequencySeconds { get; set; }

        /// <summary>
        /// How long a connection can remain idle until it's purged
        /// </summary>
        double MaxConnectionIdleSeconds { get; set; }

        /// <summary>
        /// The percentage of the working set that ObjectCloud should attempt to occupy
        /// </summary>
        double CachePercentOfMaxWorkingSet { get; set; }

        /// <summary>
        /// Comma-seperated list of values that are passed to Cache.MemorySizeLimts
        /// </summary>
        string CacheRAMThreashold { get; set; }

        /// <summary>
        /// The maximum memory to hold in the cache
        /// </summary>
        long? CacheRAMMaxMemory { get; set; }

        /// <summary>
        /// The minimum number of references to hold in the cache
        /// </summary>
        int CacheRAMMinReferences { get; set; }

        /// <summary>
        /// The maximum number of references to hold in the cache
        /// </summary>
        long CacheRAMMaxReferences { get; set; }

        /// <summary>
        /// The exception that terminated the web server
        /// </summary>
        Exception TerminatingException { get; }
    }
}
