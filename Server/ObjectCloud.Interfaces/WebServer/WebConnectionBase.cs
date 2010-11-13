// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.Interfaces.WebServer.UserAgent;

namespace ObjectCloud.Interfaces.WebServer
{
    public abstract class WebConnectionBase : IWebConnection
    {
        private static ILog log = LogManager.GetLogger(typeof(WebConnectionBase));

        protected WebConnectionBase(IWebServer webServer, CallingFrom callingFrom, uint generation)
            : this(webServer, callingFrom)
        {
            // Prevent stack overflow.  TODO, make the maximum generation configurable
            if (generation > 250)
                throw new WebResultsOverrideException(
                    WebResults.From(Status._500_Internal_Server_Error, "Stack overflow, too many levels of shells"));

            _Generation = generation;
        }

        protected WebConnectionBase(IWebServer webServer, CallingFrom callingFrom)
        {
            _WebServer = webServer;
            _CallingFrom = callingFrom;
        }

        protected void ReadPropertiesFromHeader()
        {
            // Parse out cookies from client
            if (Headers.ContainsKey("COOKIE"))
                _CookiesFromBrowser = new CookiesFromBrowser(Headers["COOKIE"]);
            else
                _CookiesFromBrowser = new CookiesFromBrowser();

            if (_Headers.ContainsKey("IF-MODIFIED-SINCE"))
                _IfModifiedSince = DateTime.Parse(_Headers["IF-MODIFIED-SINCE"]);
            else
                _IfModifiedSince = null;

            if (_Headers.ContainsKey("REFERER"))
                _Referer = _Headers["REFERER"];
            else
                _Referer = null;

            if (_Headers.ContainsKey("CONTENT-TYPE"))
                _ContentType = _Headers["CONTENT-TYPE"];
            else
                _ContentType = null;

            if (_Headers.ContainsKey("HOST"))
                _RequestedHost = _Headers["HOST"];
            else
                _RequestedHost = null;
        }

        /// <summary>
        /// Loads a session by getting the session ID from a cookie, or by creating a new session
        /// </summary>
        protected void LoadSession()
        {
            // Try retrieving an existing session
            if (CookiesFromBrowser.ContainsKey("SESSION"))
            {
                string sessionGuidString = CookiesFromBrowser["SESSION"].Split(',')[0].Trim();

                Guid sessionGuid;
                if (GuidFunctions.TryParse(sessionGuidString, out sessionGuid))
                {
                    ID<ISession, Guid> sessionId = new ID<ISession, Guid>(sessionGuid);

                    _Session = WebServer.FileHandlerFactoryLocator.SessionManagerHandler[sessionId];

                    if (null != _Session)
                        if (log.IsInfoEnabled)
                            log.InfoFormat("Retrieved session: {0}", CookiesFromBrowser["SESSION"]);
                }
                else
                    _Session = null;
            }
            else
                // If no session was found, force creating one
                _Session = null;

            if (null == _Session)
            {
                _Session = WebServer.FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();

                if (log.IsInfoEnabled)
                    log.InfoFormat("Created session: {0}", _Session.SessionId.ToString());
            }

            ILoggerFactoryAdapter loggerFactoryAdapter = LogManager.Adapter;
            if (loggerFactoryAdapter is IObjectCloudLoggingFactoryAdapter)
                ((IObjectCloudLoggingFactoryAdapter)loggerFactoryAdapter).Session = _Session;
        }

        /// <summary>
        /// The date that the client's cached version of the requested object was created, or null if the client didn't
        /// send a date
        /// </summary>
        public DateTime? IfModifiedSince
        {
            get { return _IfModifiedSince; }
        }
        private DateTime? _IfModifiedSince;

        /// <summary>
        /// The refering page
        /// </summary>
        public string Referer
        {
            get { return _Referer; }
        }
        private string _Referer;

        /// <summary>
        /// The loaded session
        /// </summary>
        public ISession Session
        {
            get { return _Session; }
            set { _Session = value; }
        }
        protected ISession _Session;

        /// <summary>
        /// The web protocol in use
        /// </summary>
        public WebMethod Method
        {
            get { return _Method; }
        }
        protected WebMethod _Method;

        /// <summary>
        /// The POST parameters from the request
        /// </summary>
        public RequestParameters PostParameters
        {
            get { return _PostParameters; }
        }
        IDictionary<string, string> IWebConnection.PostParameters
        {
            get
            {
                if (null == _PostParameters)
                    return null;

                return _PostParameters.Clone();
            }
        }
        protected RequestParameters _PostParameters;

        /// <summary>
        /// Attempts to decode the POST parameters from the content
        /// </summary>
        protected void TryDecodePostParameters()
        {
            _PostParameters = null;
            if (WebMethod.POST == _Method)
                if (null != ContentType)
                    if (ContentType.ToLowerInvariant().StartsWith("application/x-www-form-urlencoded"))
                        ReadPostParameters();
                    else if (ContentType.ToLowerInvariant().StartsWith("multipart/form-data"))
                        ReadMimeMessage();
        }

        /// <summary>
        /// Reads the post parameters
        /// </summary>
        private void ReadPostParameters()
        {
            // decode bytes into a string and then create the post parameters
            string postString = _Content.AsString();
            _PostParameters = new RequestParameters(postString);
        }

        /// <summary>
        /// Reads the MIME parameters
        /// </summary>
        private void ReadMimeMessage()
        {
            string boundary = ContentType.Split(new string[] { "boundary=" }, StringSplitOptions.RemoveEmptyEntries)[1];

            _MimeReader = new MimeReader(boundary, Content.AsStream());
        }

        /// <summary>
        /// The GET parameters from the request
        /// </summary>
        public RequestParameters GetParameters
        {
            get { return _GetParameters; }
        }

        IDictionary<string, string> IWebConnection.GetParameters
        {
            get { return _GetParameters.Clone(); }
        }
        protected RequestParameters _GetParameters;

        public MimeReader MimeReader
        {
            get { return _MimeReader; }
        }
        protected MimeReader _MimeReader;

        /// <summary>
        /// The Content-Type
        /// </summary>
        public string ContentType
        {
            get { return _ContentType; }
        }
        protected string _ContentType;

        /// <summary>
        /// Returns the get argument with the given name, or throws an exception
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        public string GetArgumentOrException(string argumentName)
        {
            if (!_GetParameters.ContainsKey(argumentName))
            {
                throw new WebResultsOverrideException(
                    WebResults.From(Status._449_Retry_With, argumentName + " is missing"),
                    argumentName + " is missing");
            }

            return _GetParameters[argumentName];
        }

        /// <summary>
        /// Returns the post argument with the given name, or throws an exception
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        public string PostArgumentOrException(string argumentName)
        {
            if (null != _PostParameters)
                if (_PostParameters.ContainsKey(argumentName))
                    return _PostParameters[argumentName];


            throw new WebResultsOverrideException(
                WebResults.From(Status._449_Retry_With, argumentName + " is missing"),
                argumentName + " is missing");
        }

        /// <summary>
        /// Returns the cookie argument with the given name, or throws an exception
        /// </summary>
        /// <param name="cookieName"></param>
        /// <returns></returns>
        public string CookieOrException(string cookieName)
        {
            if (!CookiesFromBrowser.ContainsKey(cookieName))
            {
                throw new WebResultsOverrideException(
                    WebResults.From(Status._449_Retry_With, "Cookie " + cookieName + " is missing"),
                    "Cookie " + cookieName + " is missing");
            }

            return CookiesFromBrowser[cookieName];
        }

        /// <summary>
        /// Returns the argument with the given name, or throws an exception.  Get arguments override Post arguments
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        public string EitherArgumentOrException(string argumentName)
        {
            if (_GetParameters.ContainsKey(argumentName))
                return _GetParameters[argumentName];

            if (null != _PostParameters)
                if (_PostParameters.ContainsKey(argumentName))
                    return _PostParameters[argumentName];

            if (CookiesFromBrowser.ContainsKey(argumentName))
                return CookiesFromBrowser[argumentName];

            throw new WebResultsOverrideException(
                WebResults.From(Status._449_Retry_With, argumentName + " is missing"),
                argumentName + " is missing");
        }

        public bool EitherArgumentContains(string argumentName)
        {
            if (_GetParameters.ContainsKey(argumentName))
                return true;

            if (null != _PostParameters)
                if (_PostParameters.ContainsKey(argumentName))
                    return true;

            if (CookiesFromBrowser.ContainsKey(argumentName))
                return true;

            return false;
        }

        /// <summary>
        /// Determines the requested file and the get arguments
        /// </summary>
        /// <param name="requestString"></param>
        protected void DetermineRequestedFileAndGetParameters(string requestString)
        {
            string[] requestedFileAndArguments = requestString.Split(new char[] { '?' }, 2);
            _RequestedFile = HTTPStringFunctions.DecodeRequestParametersFromBrowser(requestedFileAndArguments[0]);

            if (requestedFileAndArguments.Length > 1)
                _GetParameters = new RequestParameters(requestedFileAndArguments[1]);
            else
                _GetParameters = new RequestParameters();
        }

        /// <summary>
        /// The WebServer object initiating this connection
        /// </summary>
        public IWebServer WebServer
        {
            get { return _WebServer; }
        }
        protected readonly IWebServer _WebServer;

        /// <summary>
        /// The Content sent from the client
        /// </summary>
        public IWebConnectionContent Content
        {
            get { return _Content; }
        }
        protected IWebConnectionContent _Content;

        /// <summary>
        /// The cookies that arrived from the client
        /// </summary>
        public CookiesFromBrowser CookiesFromBrowser
        {
            get { return _CookiesFromBrowser; }
        }
        protected CookiesFromBrowser _CookiesFromBrowser;

        /// <summary>
        /// The cookies to send to the client
        /// </summary>
        public ICollection<CookieToSet> CookiesToSet
        {
            get { return _CookiesToSet; }
        }
        protected ICollection<CookieToSet> _CookiesToSet = new List<CookieToSet>();

        /// <summary>
        /// The HTTP version sent from the client
        /// </summary>
        public string HttpVersion
        {
            get { return _HttpVersion; }
        }
        protected string _HttpVersion;

        /// <summary>
        /// The requested file
        /// </summary>
        public string RequestedFile
        {
            get { return _RequestedFile; }
        }
        protected string _RequestedFile;

        /// <value>
        /// The host that the connection is working with 
        /// </value>
        public string RequestedHost
        {
            get { return _RequestedHost; }
        }
        protected string _RequestedHost;

        /// <summary>
        /// The header, indexed by the prefix of each line, in upper case
        /// </summary>
        public IDictionary<string, string> Headers
        {
            get { return _Headers; }
        }
        IDictionary<string, string> IWebConnection.Headers
        {
            get { return new Dictionary<string, string>(_Headers); }
        }
        protected Dictionary<string, string> _Headers = new Dictionary<string, string>();

        /// <summary>
        /// Generates the results that are returned to the client
        /// </summary>
        /// <returns></returns>
        public virtual IWebResults GenerateResultsForClient()
        {
            try
            {
                IWebResults webResults = RunMethodOrAction();

                if (null != webResults)
                {
                    Status status = webResults.Status;

                    // Certain GET parameters allow for manipulation of the results
                    if ((status >= Status._200_OK) && (status < Status._300_Multiple_Choices))
                    {
                        // If the BrowserCache GET argument is present, then it indicates that the results are immutable
                        // The convention is to use an MD5, version #, or timestamp as the value.  When the value changes,
                        // it instructs the browser to fetch a new version
                        // Note:  This is only done for status codes between 200 and 299...  Errors should not be cached!
                        if (_GetParameters.ContainsKey("BrowserCache"))
                        {
                            // Set to cache indefinately
                            webResults.Headers.Remove("Cache-Control");
                            webResults.Headers["Expires"] = DateTime.UtcNow.AddYears(30).ToString("r");
                        }

                        // Allow the requester to override the Mime type
                        if (_GetParameters.ContainsKey("MimeOverride"))
                            webResults.ContentType = _GetParameters["MimeOverride"];
                    }
                }

                return webResults;
            }
            catch (OutOfMemoryException oome)
            {
                // If there's an out-of-memory error, clear the cache and log it
                Cache.ReleaseAllCachedMemory();

                GC.Collect();

                log.Warn("Out-of-memory, trashed cache", oome);

                return WebResults.From(Status._500_Internal_Server_Error);
            }
            catch (JsonDeserializationException e)
            {
                log.Error(e);

                return WebResults.From(Status._400_Bad_Request, e.Message);
            }
            catch (NotImplementedException e)
            {
                log.Error("Hit some unimplemented functionality...", e);

                return WebResults.From(Status._501_Not_Implemented);
            }
            catch (StackOverflowException e)
            {
                log.Error(e);

                return WebResults.From(Status._500_Internal_Server_Error);
            }
            catch (Exception e)
            {
                log.Error(e);

                if (e is IHasWebResults)
                    return ((IHasWebResults)e).WebResults;
                else
                    return WebResults.From(Status._500_Internal_Server_Error, "An unhandled error occured");
            }
        }

        /// <summary>
        /// Runs either the requested method or action on the specified object
        /// </summary>
        /// <returns></returns>
        private IWebResults RunMethodOrAction()
        {
            // Allow the URL to specify who the user must be.  If that user isn't logged in, then guide the user to log in
            if (GetParameters.ContainsKey("AsUser"))
            {
                string requestedUser = GetParameters["AsUser"];
                IUser user = Session.User;

                // If passed in an open id that's local to this server, switch to regular user name
                if (requestedUser.StartsWith("http://" + WebServer.FileHandlerFactoryLocator.HostnameAndPort + "/Users/") &&
                    requestedUser.EndsWith(".user"))
                {
                    requestedUser = requestedUser.Substring(((string)("http://" + WebServer.FileHandlerFactoryLocator.HostnameAndPort + "/Users/")).Length);
                    requestedUser = requestedUser.Substring(0, requestedUser.Length - 5);
                }

                if (user.Name != requestedUser)
                    if (requestedUser.StartsWith("http://"))
                    {
                        // Prompt user to login with OpenID

                        _PostParameters = new RequestParameters();
                        _PostParameters["openid_url"] = requestedUser;
                        _PostParameters["redirect"] = "http://" + WebServer.FileHandlerFactoryLocator.HostnameAndPort + RequestedFile + "?" +
                            GetParameters.ToURLEncodedString();

                        _Method = WebMethod.POST;

                        WebDelegate webDelegate = WebServer.FileHandlerFactoryLocator.WebMethodCache[MethodNameAndFileContainer.New(
                            "OpenIDLogin",
                            WebServer.FileHandlerFactoryLocator.UserManagerHandler.FileContainer)];

                        return webDelegate(this, CallingFrom);
                    }
                    else
                        // Shell to the login form
                        try
                        {
                            IWebResults webResults = ShellTo("/Shell/UserManagers/Login.wchtml");

                            // Even though we're shelling to another file, the permission still needs to be unauthorized
                            webResults.Status = Status._401_Unauthorized;

                            return webResults;
                        }
                        catch (Exception e)
                        {
                            // Errors when shelling to the permission denied page are logged and swallowed.
                            // The system defaults to a simple message in this case
                            log.Error("Error when shelling to /Shell/UserManagers/Login.wchtml for a login form", e);
                            return WebResults.From(Status._401_Unauthorized, "Permission Denied");
                        }
            }

            // if the user is an administrator, allow the user to change CallingFrom
            if (GetParameters.ContainsKey("CallingFrom"))
                if (WebServer.FileHandlerFactoryLocator.UserManagerHandler.IsUserInGroup(
                    Session.User.Id,
                    WebServer.FileHandlerFactoryLocator.UserFactory.Administrators.Id))
                {
                    Enum<CallingFrom>.TryParse(GetParameters["CallingFrom"], out _CallingFrom);
                }

            string requestedFile = ResolveUserVariables(_RequestedFile);

            IFileContainer fileContainer;
            try
            {
                fileContainer = WebServer.FileSystemResolver.ResolveFile(requestedFile);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(
                    WebResults.From(Status._404_Not_Found, requestedFile + " does not exist"),
                    requestedFile + " does not exist");
            }

            // Track what files are touched for a given web request.  This helps with caching results on the client
            TouchedFiles.Add(fileContainer);

            _UserPermission = fileContainer.LoadPermission(Session.User.Id);

            // Make sure that directories always have a trailing /
            // This makes handling directories much easier, as the names aren't ambiguous
            // We skip this for directories that have an extension
            if (fileContainer.FileHandler is IDirectoryHandler)
                if (!requestedFile.EndsWith("/") && !fileContainer.Filename.Contains("."))
                    requestedFile = requestedFile + "/";

            // Handling the request can go in one of four possible directions:
            // - Request contains Method argument:  The requested object handles the request
            // - Request contains Action argument:  The shell defers the request to another object, which uses this object as an argument
            // - Request contains no argument:      The shell uses an implied "Action=..." argument.  The web handler specifies what the action is, although by convention it is usually View
            // - Infinate recursion of appending "Action=View"...  Not sure how to handle, not sure what the risk is yet.

            if (GetParameters.ContainsKey("Method"))
            {
                // First, see if any of the plugins will intercept the call
                foreach (IWebHandlerPlugin webHandlerPlugin in fileContainer.WebHandlerPlugins)
                {
                    WebDelegate pluginWebDelegate = webHandlerPlugin.GetMethod(this);

                    if (null != pluginWebDelegate)
                        return pluginWebDelegate(this, CallingFrom);
                }

                // Next, directly poll the object / target

                IWebHandler webHandler = fileContainer.WebHandler;

                WebDelegate webDelegate = webHandler.GetMethod(this);

                if (null != webDelegate)
                    return webDelegate(this, CallingFrom);
            }

            string action;

            // If no action or method is specified, then the file's web handler will state what the action will be
            if (!(GetParameters.ContainsKey("Action")))
                action = fileContainer.WebHandler.ImplicitAction;
            else
                action = GetParameters["Action"];

            // Make sure that the user has appropriate permissions for the action

            // Note that this isn't TRUE security, it's merely to prevent a user from doing something like opening
            // an editor, making changes, and then learning that the changes weren't allowed.

            // If the shell isn't aware of an action, then permission enforcement is disabled

            bool bypassActionPermission = false;
            if (GetParameters.ContainsKey("BypassActionPermission"))
                bypassActionPermission = bool.TryParse(GetParameters["BypassActionPermission"], out bypassActionPermission);

            if (!bypassActionPermission)
            {
                FilePermissionEnum? minimumPermissionForAction = LoadMinimumPermissionForAction(action);

                if (null != minimumPermissionForAction)
                {
                    FilePermissionEnum? userFilePermission = fileContainer.LoadPermission(Session.User.Id);

                    bool userHasPermission = false;
                    if (null != userFilePermission)
                        userHasPermission = userFilePermission >= minimumPermissionForAction;

                    // If the user doesn't have permission, give the user a chance to log in
                    if (!userHasPermission)
                    {
                        try
                        {
                            IWebResults webResults = ShellTo("/DefaultTemplate/permissiondenied.oc");

                            // Even though we're shelling to another file, the permission still needs to be unauthorized
                            webResults.Status = Status._401_Unauthorized;

                            return webResults;
                        }
                        catch (Exception e)
                        {
                            // Errors when shelling to the permission denied page are logged and swallowed.
                            // The system defaults to a simple message in this case
                            log.Error("Error when shelling to /DefaultTemplate/permissiondenied.oc for a permission denied message", e);
                            return WebResults.From(Status._401_Unauthorized, "Permission Denied");
                        }
                    }
                }
            }

            // The file has an extension (/folder/file.extension) if the last index of '.' is greater then '/'
            bool hasExtension = false;

            int lastIndexOfDot = default(int);

            if (requestedFile.Contains("."))
            {
                lastIndexOfDot = requestedFile.LastIndexOf('.');
                hasExtension = lastIndexOfDot > requestedFile.LastIndexOf('/');
            }

            // The shell file has the URLs that implement the actions
            string shellFile;
            string extension;

            if (hasExtension)
            {
                extension = requestedFile.Substring(lastIndexOfDot + 1);
                shellFile = "/Actions/ByExtension/" + extension;
            }
            else
            {
                shellFile = "/Actions/ByType/" + fileContainer.TypeId;
                extension = fileContainer.TypeId;
            }

            IFileContainer shellFileContainer;
            try
            {
                shellFileContainer = WebServer.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(shellFile);
            }
            catch (FileDoesNotExist)
            {
                return WebResults.From(Status._500_Internal_Server_Error, "ObjectCloud is not configured to handle files of type " + extension);
            }

            INameValuePairsHandler shellFileHandler = shellFileContainer.CastFileHandler<INameValuePairsHandler>();

            if (!shellFileHandler.Contains(action))
                return WebResults.From(Status._500_Internal_Server_Error, "ObjectCloud does not support the action \"" + action + "\" for files of type \"" + extension + "\"");

            string actionInstructions = shellFileHandler[action];

            // The action can be /r/t/e.wchtml?File=[target], text/html
            string[] actionURLAndMime = actionInstructions.Split(new char[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries);

            // Decode the internal URL to interpret the action
            // IE
            // http://blah/foo.abc -> http://blah/foo.abc?Action=View -> http://blah/Viewers/AbcViewer.wchtml?FileName=/blah/foo.abc
            string actionURL = actionURLAndMime[0].Trim().Replace("[Target]", requestedFile);
            actionURL = actionURL.Replace("[Host]", RequestedHost);

            string mimeOverride;
            if (actionURLAndMime.Length > 1)
                mimeOverride = actionURLAndMime[1].Trim();
            else
                mimeOverride = null;

            // Pass through GET Arguments
            List<string> encodedArgumentsList = new List<string>();
            foreach (KeyValuePair<string, string> kvp in GetParameters)
                encodedArgumentsList.Add(string.Format(
                    "{0}={1}",
                    kvp.Key,
                    HTTPStringFunctions.EncodeRequestParametersForBrowser(kvp.Value)));

            string encodedArguments = StringGenerator.GenerateSeperatedList(encodedArgumentsList, "&");

            actionURL = actionURL.Replace("[Arguments]", encodedArguments);

            ShellWebConnection shellWebConnection = new NonBlockingShellWebConnection(
                actionURL,
                this,
                PostParameters,
                _CookiesFromBrowser);

            IWebResults toReturn = shellWebConnection.GenerateResultsForClient();

            // If the MimeType is overridden, then set it
            if (null != mimeOverride)
            {
                if (null == toReturn)
                    throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, "Can not override mime type when calling asyncronous web methods"));

                toReturn.ContentType = mimeOverride;
            }

            return toReturn;
        }

        /// <summary>
        /// Returns the minimum permission needed to perform an action, if such a permission is unknown, then the default is Administer
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        private FilePermissionEnum? LoadMinimumPermissionForAction(string action)
        {
            IFileContainer actionPermissionsFileContainer = WebServer.FileSystemResolver.ResolveFile("/Actions/ActionPermissions");
            INameValuePairsHandler actionPermissions = actionPermissionsFileContainer.CastFileHandler<INameValuePairsHandler>();

            if (actionPermissions.Contains(action))
                return Enum<FilePermissionEnum>.Parse(actionPermissions[action]);

            return null;
        }

        /// <summary>
        /// Replaces [blahblah] with the named user variable
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string ResolveUserVariables(string url)
        {
            string[] splitAtOpenBrace = url.Split('[');

            if (splitAtOpenBrace.Length == 1)
                return url;

            StringBuilder toReturn = new StringBuilder(splitAtOpenBrace[0]);

            for (int ctr = 1; ctr < splitAtOpenBrace.Length; ctr++)
            {
                string[] splitAtCloseBrace = splitAtOpenBrace[ctr].Split(new char[] { ']' }, 2);

                if (splitAtCloseBrace.Length != 2)
                {
                    throw new WebResultsOverrideException(
                        WebResults.From(Status._400_Bad_Request, "Invalid URL"),
                        "Invalid URL");
                }

                string resolvedValue;
                try
                {
                    resolvedValue = Session.User.UserHandler[splitAtCloseBrace[0]];
                }
                catch (Exception e)
                {
                    throw new WebResultsOverrideException(
                        WebResults.From(Status._400_Bad_Request, "Invalid variable: " + splitAtOpenBrace[0]),
                        "Invalid variable: " + splitAtOpenBrace[0],
                        e);
                }

                if (null == resolvedValue)
                {
                    throw new WebResultsOverrideException(
                        WebResults.From(Status._400_Bad_Request, "Invalid variable: " + splitAtOpenBrace[0]),
                        "Invalid variable: " + splitAtOpenBrace[0]);
                }

                toReturn.Append(resolvedValue);
                toReturn.Append(splitAtCloseBrace[1]);
            }

            return toReturn.ToString();
        }

        public string ResolveWebComponents(string toResolve)
        {
            return WebServer.WebComponentResolver.ResolveWebComponents(toResolve, this);
        }

        public string DoWebComponent(string url)
        {
            IWebResults webResults = ShellTo(url);

            return webResults.ResultsAsString;
        }

        public IWebConnection CreateShellConnection(IUser user)
        {
            ISession shellSession = new ShellSession(Session, user);

            ShellWebConnection shellWebConnection = new BlockingShellWebConnection(
                this,
                shellSession,
                RequestedFile,
                GetParameters,
                new byte[0],
                ContentType,
                CookiesFromBrowser,
                CallingFrom);

            shellWebConnection._BypassJavascript = BypassJavascript;

            return shellWebConnection;
        }

        public IWebResults ShellTo(string url)
        {
            return ShellTo(url, CallingFrom, false);
        }

        public IWebResults ShellTo(string url, IUser user)
        {
            ISession shellSession = new ShellSession(Session, user);

            ShellWebConnection shellWebConnection = new BlockingShellWebConnection(
                WebServer,
                shellSession,
                url,
                new byte[0],
                ContentType,
                CookiesFromBrowser,
                CallingFrom,
                WebMethod.GET);

            shellWebConnection._BypassJavascript = BypassJavascript;

            return shellWebConnection.GenerateResultsForClient();
        }

        public IWebResults ShellTo(string url, CallingFrom callingFrom, bool bypassJavascript)
        {
            ShellWebConnection shellWebConnection = new BlockingShellWebConnection(
                url,
                this,
                PostParameters,
                _CookiesFromBrowser,
                callingFrom);

            shellWebConnection._BypassJavascript = bypassJavascript;

            return shellWebConnection.GenerateResultsForClient();
        }

        public IWebResults ShellTo(WebMethod method, string url, byte[] content, string contentType, CallingFrom callingFrom, bool bypassJavascript)
        {
            ShellWebConnection shellWebConnection = new BlockingShellWebConnection(
                this,
                method,
                url,
                content,
                contentType,
                _CookiesFromBrowser,
                callingFrom);

            shellWebConnection._BypassJavascript = bypassJavascript;

            return shellWebConnection.GenerateResultsForClient();
        }

        public FilePermissionEnum? UserPermission
        {
            get
            {
                return _UserPermission;
            }
        }
        protected FilePermissionEnum? _UserPermission;

        public abstract bool Connected { get; }

        public uint Generation
        {
            get { return _Generation; }
        }
        private readonly uint _Generation = 0;

        public CallingFrom CallingFrom
        {
            get { return _CallingFrom; }
        }
        private CallingFrom _CallingFrom;

        public bool BypassJavascript
        {
            get { return _BypassJavascript; }
        }
        protected bool _BypassJavascript = false;

        /*public void TemporaryChangeSession(ISession tempSession, GenericVoid del)
        {
            ISession realSession = _Session;
            _Session = tempSession;

            try
            {
                del();
            }
            finally
            {
                _Session = realSession;
            }
        }*/

        public void ChangeCallingFrom(CallingFrom newCallingFrom, GenericVoid toCall)
        {
            CallingFrom oldCallingFrom = _CallingFrom;
            _CallingFrom = newCallingFrom;

            try
            {
                toCall();
            }
            finally
            {
                _CallingFrom = oldCallingFrom;
            }
        }

        public abstract EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Sends the results to the client.  Be careful, calling this multiple times can have undesired results!
        /// </summary>
        public abstract void SendResults(IWebResults webResults);

        public abstract HashSet<IFileContainer> TouchedFiles { get; }

        public virtual HashSet<string> Scripts
        {
            get { return _Scripts; }
        }
        private readonly HashSet<string> _Scripts = new HashSet<string>();

        /// <summary>
        /// Precalculated browser cache urls
        /// </summary>
        private Dictionary<string, string> BrowserCacheUrls = new Dictionary<string, string>();

        /// <summary>
        /// Returns a url that contains the correct browser cache ID for the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public virtual string GetBrowserCacheUrl(string url)
        {
            // So, this function is somewhat CPU intense; but it's much faster then many calls coming in through the web

            // In some cases, full urls might make it in.  This might happen when getting avatars, ect
            if (url.StartsWith("http://" + WebServer.FileHandlerFactoryLocator.HostnameAndPort))
                url = url.Substring(7 + WebServer.FileHandlerFactoryLocator.HostnameAndPort.Length);
            else if (url.StartsWith("http://") || url.StartsWith("https://"))
                return url;
                
            string toReturn;
            if (BrowserCacheUrls.TryGetValue(url, out toReturn))
                return toReturn;

            if (url.Contains("BrowserCache="))
                return url;

            IWebResults urlResults = ShellTo(url);

            byte[] scriptBytes = new byte[urlResults.ResultsAsStream.Length];
            urlResults.ResultsAsStream.Read(scriptBytes, 0, scriptBytes.Length);

            // Get a free hash calculator
            MD5CryptoServiceProvider hashAlgorithm = StaticRecycler<MD5CryptoServiceProvider>.Get();

            byte[] scriptHash;
            try
            {
                scriptHash = hashAlgorithm.ComputeHash(scriptBytes);
            }
            finally
            {
                // Save the hash calculator for reuse
                StaticRecycler<MD5CryptoServiceProvider>.Recycle(hashAlgorithm);
            }

            string hash = Convert.ToBase64String(scriptHash);

            toReturn = HTTPStringFunctions.AppendGetParameter(url, "BrowserCache", hash);
            BrowserCacheUrls[url] = toReturn;

            return toReturn;
        }

        public virtual IBrowser UserAgent
        {
            get
            {
                if (null == _UserAgent)
                    _UserAgent = Browser.GetBrowser(Headers["USER-AGENT"]);

                return _UserAgent;
            }
        }
        private IBrowser _UserAgent = null;
    }
}
