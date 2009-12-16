// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Common.Logging;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Generic web handler.  Other web handlers can inherit from this web handler, or the Non-Generic WebHandler can be used
    /// if the file doesn't support web access
    /// </summary>
    /// <typeparam name="TFileHandler">The type of file handler</typeparam>
    public partial class WebHandler<TFileHandler> : IWebHandler
        where TFileHandler : IFileHandler
    {
        private static ILog log = LogManager.GetLogger(typeof(WebHandler<>));

        /// <summary>
        /// Returns a delegate to handle the incoming request
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        public virtual WebDelegate GetMethod(IWebConnection webConnection)
        {
            IExecutionEnvironment executionEnvironment = GetOrCreateExecutionEnvironment();

            bool allowLocalMethods = true;

            if (!webConnection.BypassJavascript)
                if (null != executionEnvironment)
                {
                    WebDelegate toReturn = executionEnvironment.GetMethod(webConnection);

                    if (null != toReturn)
                        return toReturn;
                    else
                        allowLocalMethods = !executionEnvironment.IsBlockWebMethodsEnabled(webConnection);
                }

            string method = webConnection.GetArgumentOrException("Method");

            // When the call is local or there is no execution environment, then look for the base web method
            if (webConnection.CallingFrom == CallingFrom.Local || allowLocalMethods || AllowedBaseMethods.Contains(method))
                return FileHandlerFactoryLocator.WebMethodCache[MethodNameAndFileContainer.New(method, FileContainer)];

            // Throw an exception if no method is found
            throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, "method \"" + method + "\" does not exist"));
        }

        /// <summary>
        /// These methods are allowed even if the object is wrapped by a server-side javascript class
        /// </summary>
        private static Set<string> AllowedBaseMethods = new Set<string>();

        static WebHandler()
        {
            // Allow any base method to be called from the web
            foreach (MethodInfo method in typeof(WebHandler).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (typeof(IWebResults) == method.ReturnType)
                {
                    ParameterInfo[] parms = method.GetParameters();

                    if (parms.Length > 0)
                        if (typeof(IWebConnection) == parms[0].ParameterType)
                            AllowedBaseMethods.Add(method.Name);
                }
            }
        }

        /// <value>
        /// The FileHandler, pre-casted
        /// </value>
        public TFileHandler FileHandler
        {
            get { return _FileHandler; }
        }
        private TFileHandler _FileHandler;

        /// <value>
        /// The FileContainer 
        /// </value>
        public IFileContainer FileContainer
        {
            get { return _FileContainer; }
            set
            {
                _FileContainer = value;
                _FileHandler = value.CastFileHandler<TFileHandler>();
            }
        }
        private IFileContainer _FileContainer;

        /// <summary>
        /// The FileHandlerFactoryLocator
        /// </summary>
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The default action to run if no action or method is specified in the URL
        /// </summary>
        public virtual string ImplicitAction
        {
            get { return "View"; }
        }

        /// <summary>
        /// The Javascript wrappers for this object.  Set by GetJavascriptWrapper.  This can accumulate in memory because the WebHandlers are cached and collected as they fall out of use
        /// </summary>
        Dictionary<WrapperCallsThrough, string> JavascriptWrappers = new Dictionary<WrapperCallsThrough, string>();

        /// <summary>
        /// This should return a Javascript object that can perform all calls to all methods marked as WebCallable through AJAX.  This convention is depricated
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="assignToVariable">The variable to assign the wrapper object to</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetJavascriptWrapper(IWebConnection webConnection, string assignToVariable)
        {
            string javascriptToReturn = GetJavascriptWrapper(webConnection, assignToVariable, WrapperCallsThrough.AJAX);

            javascriptToReturn = "// Scripts: /API/Prototype.js\n" + javascriptToReturn;

            IWebResults toReturn = WebResults.FromString(Status._200_OK, javascriptToReturn);
            toReturn.ContentType = "application/javascript";
            return toReturn;
        }

        /// <summary>
        /// The cached in-browser JavaScript wrapper
        /// </summary>
        private string cachedInBrowserJSWrapper = null;

        /// <summary>
        /// This should return a Javascript object that can perform all calls to all methods marked as WebCallable through AJAX.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="assignToVariable">The variable to assign the wrapper object to</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetJSW(IWebConnection webConnection, string assignToVariable, string EncodeFor)
        {
            // Not worth syncronizing, nothing bad will happen if multiple threads enter this block at the same time
            if (null == cachedInBrowserJSWrapper)
            {
                string javascriptWrapper = StringGenerator.GenerateSeperatedList(
                    FileHandlerFactoryLocator.WebServer.JavascriptWebAccessCodeGenerator.GenerateWrapper(GetType()), ",\n");

                // Replace some key constants
                javascriptWrapper = javascriptWrapper.Replace("{0}", FileContainer.FullPath);
                javascriptWrapper = javascriptWrapper.Replace("{1}", FileContainer.Filename);
                cachedInBrowserJSWrapper = javascriptWrapper.Replace("{2}", "http://" + FileHandlerFactoryLocator.HostnameAndPort + FileContainer.FullPath);
            }

            string javascriptToReturn = cachedInBrowserJSWrapper;

            // Insert the user's permission to the file
            javascriptToReturn = javascriptToReturn.Replace("{3}", FileContainer.LoadPermission(webConnection.Session.User.Id).ToString());

            // Insert the server-side Javascript wrappers
            try
            {
                IExecutionEnvironment executionEnvironment = GetOrCreateExecutionEnvironment();
                if (null != executionEnvironment)
                {
                    string serversideJavscriptWrapper = StringGenerator.GenerateCommaSeperatedList(
                        executionEnvironment.GenerateJavascriptWrapper(webConnection));

                    serversideJavscriptWrapper = serversideJavscriptWrapper.Replace("{0}", FileContainer.FullPath);

                    javascriptToReturn = javascriptToReturn + ",\n" + serversideJavscriptWrapper;
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("Exception occured when trying to generate a Javascript wrapper for server-side Javascript", e);
            }

            // Enclose the functions with { .... }
            javascriptToReturn = "{\n" + javascriptToReturn + "\n}";

            if (null != assignToVariable)
                javascriptToReturn = string.Format("var {0} = {1};", assignToVariable, javascriptToReturn);

            javascriptToReturn = "// Scripts: /API/AJAX.js, /API/json2.js\n" + javascriptToReturn;

            if (EncodeFor == "JavaScript")
                if (FileHandlerFactoryLocator.WebServer.MinimizeJavascript)
                {
                    // The text will be "minimized" javascript to save space

                    JavaScriptMinifier javaScriptMinifier = new JavaScriptMinifier();

                    try
                    {
                        javascriptToReturn = javaScriptMinifier.Minify(javascriptToReturn);
                    }
                    catch (Exception e)
                    {
                        log.Error("Error when minimizing JavaScript", e);

                        return WebResults.FromString(Status._500_Internal_Server_Error, "Error when minimizing JavaScript: " + e.Message);
                    }
                }


            IWebResults toReturn = WebResults.FromString(
                Status._200_OK,
                javascriptToReturn);

            toReturn.ContentType = "application/javascript";
            return toReturn;
        }

        /// <summary>
        /// This should return a Javascript object that can perform all calls to all methods marked as WebCallable through server-side Javascript.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="assignToVariable">The variable to assign the wrapper object to</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetServersideJavascriptWrapper(IWebConnection webConnection, string assignToVariable)
        {
            string javascriptToReturn = GetJavascriptWrapper(webConnection, assignToVariable, WrapperCallsThrough.ServerSideShells);

            IWebResults toReturn = WebResults.FromString(Status._200_OK, javascriptToReturn);
            toReturn.ContentType = "application/javascript";
            return toReturn;
        }

        /// <summary>
        /// Used internally
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="assignToVariable"></param>
        /// <returns></returns>
        public string GetJavascriptWrapperForBase(IWebConnection webConnection, string assignToVariable)
        {
            return GetJavascriptWrapper(webConnection, assignToVariable, WrapperCallsThrough.ServerSideShells | WrapperCallsThrough.BypassServerSideJavascript);
        }

        /// <summary>
        /// This should return a Javascript object that can perform all calls to all methods marked as WebCallable through.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="assignToVariable">The variable to assign the wrapper object to</param>
        /// <param name="wrapperCallsThrough">Indicates either to generate server-side Javascript or AJAX calls</param>
        /// <returns></returns>
        private string GetJavascriptWrapper(IWebConnection webConnection, string assignToVariable, WrapperCallsThrough wrapperCallsThrough)
        {
            if (!JavascriptWrappers.ContainsKey(wrapperCallsThrough))
            {
                string javascriptWrapper = StringGenerator.GenerateSeperatedList(
                    FileHandlerFactoryLocator.WebServer.JavascriptWebAccessCodeGenerator.GenerateLegacyWrapper(GetType(), wrapperCallsThrough), ",\n");

                // Replace some key constants
                javascriptWrapper = javascriptWrapper.Replace("{0}", FileContainer.FullPath);
                javascriptWrapper = javascriptWrapper.Replace("{1}", FileContainer.Filename);
                JavascriptWrappers[wrapperCallsThrough] = javascriptWrapper.Replace("{2}", "http://" + FileHandlerFactoryLocator.HostnameAndPort + FileContainer.FullPath);
            }

            string javascriptToReturn = JavascriptWrappers[wrapperCallsThrough];

            // Insert the user's permission to the file
            javascriptToReturn = javascriptToReturn.Replace("{3}", FileContainer.LoadPermission(webConnection.Session.User.Id).ToString());

            if ((WrapperCallsThrough.BypassServerSideJavascript & wrapperCallsThrough) == 0)
                try
                {
                    IExecutionEnvironment executionEnvironment = GetOrCreateExecutionEnvironment();
                    if (null != executionEnvironment)
                    {
                        string serversideJavscriptWrapper = StringGenerator.GenerateCommaSeperatedList(
                            executionEnvironment.GenerateLegacyJavascriptWrapper(webConnection, wrapperCallsThrough));

                        serversideJavscriptWrapper = serversideJavscriptWrapper.Replace("{0}", FileContainer.FullPath);

                        javascriptToReturn = javascriptToReturn + ",\n" + serversideJavscriptWrapper;
                    }
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Exception occured when trying to generate a Javascript wrapper for server-side Javascript", e);
                }

            // Enclose the functions with { .... }
            javascriptToReturn = "{\n" + javascriptToReturn + "\n}";

            if (null != assignToVariable)
                javascriptToReturn = string.Format("var {0} = {1};", assignToVariable, javascriptToReturn);

            return javascriptToReturn;
        }

        /// <summary>
        /// Returns any syntax errors that occur as a result of trying to load server-side javascript
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive, FilePermissionEnum.Administer)]
        public IWebResults GetServersideJavascriptErrors(IWebConnection webConnection)
        {
            IExecutionEnvironment executionEnvironment = GetOrCreateExecutionEnvironment();
            return WebResults.FromString(Status._200_OK, executionEnvironment.ExecutionEnvironmentErrors != null ? executionEnvironment.ExecutionEnvironmentErrors : "no errors");
        }

        /// <summary>
        /// Sets the user's permission for the given file.  Either the user or group ID or name are set
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        /// <param name="FilePermission">The permission, set to null to disable permissions to the file</param>
        /// <param name="Inherit">Set to true to allow permission inheritance.  For example, if this permission applies to a directory, it will be the default for files in the directory</param>
        /// <param name="UserOrGroup"></param>
        /// <param name="UserOrGroupId"></param>
        /// <param name="SendNotifications"></param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Administer)]
        public IWebResults SetPermission(IWebConnection webConnection, string UserOrGroupId, string UserOrGroup, string FilePermission, bool? Inherit, bool? SendNotifications)
        {
            ID<IUserOrGroup, Guid> userOrGroupId;

            if (null != UserOrGroupId)
                userOrGroupId = new ID<IUserOrGroup, Guid>(new Guid(UserOrGroupId));
            else
                try
                {
                    userOrGroupId = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(UserOrGroup).Id;
                }
                catch (UnknownUser)
                {
                    return WebResults.FromString(Status._406_Not_Acceptable, UserOrGroup + " does not exist");
                }

            FilePermissionEnum? level = null;
            if (null != FilePermission)
                level = Enum<FilePermissionEnum>.TryParse(FilePermission);

            bool inherit = false;
            if (null != Inherit)
                inherit = Inherit.Value;

            bool sendNotifications = false;
            if (null != SendNotifications)
                sendNotifications = SendNotifications.Value;

            // Null permissions just remove the permission
            if (null != level)
            {
                FileHandler.FileContainer.ParentDirectoryHandler.SetPermission(webConnection.Session.User.Id, FileHandler.FileContainer.Filename, userOrGroupId, level.Value, inherit, sendNotifications);
                return WebResults.FromString(Status._202_Accepted, "Permission set to " + level.ToString());
            }
            else
            {
                FileHandler.FileContainer.ParentDirectoryHandler.RemovePermission(FileHandler.FileContainer.Filename, userOrGroupId);
                return WebResults.FromString(Status._202_Accepted, "Permission removed");
            }
        }

        /// <summary>
        /// Returns the currently logged un user's permission for this file.  If the user doesn't have an assigned permission, a 0-length string is returned.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive)]
        public IWebResults GetPermission(IWebConnection webConnection)
        {
            FilePermissionEnum? permission = FileContainer.LoadPermission(webConnection.Session.User.Id);

            if (null != permission)
                return WebResults.FromString(Status._200_OK, permission.ToString());
            else
                return WebResults.FromString(Status._200_OK, "");
        }

        /// <summary>
        /// Returns the currently logged in user's permission for this file as a Javascript object that can be queried.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON)]
        public IWebResults GetPermissionAsJSON(IWebConnection webConnection)
        {
            // Create an array of values to return
            Dictionary<string, object> toReturn = new Dictionary<string, object>();

            FilePermissionEnum? permissionNullable = FileContainer.LoadPermission(webConnection.Session.User.Id);

            if (null != permissionNullable)
            {
                FilePermissionEnum permission = permissionNullable.Value;

                foreach (FilePermissionEnum fpe in Enum<FilePermissionEnum>.Values)
                    toReturn["Can" + fpe.ToString()] = permission >= fpe;

                toReturn["Permission"] = permission.ToString();
            }
            else
                foreach (FilePermissionEnum fpe in Enum<FilePermissionEnum>.Values)
                    toReturn["Can" + fpe.ToString()] = false;

            return WebResults.ToJson(toReturn);
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
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON, FilePermissionEnum.Administer)]
        public IWebResults GetPermissions(IWebConnection webConnection)
        {
            IUserManagerHandler userManagerHandler = FileHandlerFactoryLocator.UserManagerHandler;

            List<object> permissionsList = new List<object>();

            Dictionary<ID<IUserOrGroup, Guid>, FilePermission> permissionsById = new Dictionary<ID<IUserOrGroup, Guid>, FilePermission>();

            foreach (FilePermission filePermission in FileHandler.FileContainer.ParentDirectoryHandler.GetPermissions(FileHandler.FileContainer.Filename))
                permissionsById[filePermission.UserOrGroupId] = filePermission;

            foreach (IUserOrGroup userOrGroup in userManagerHandler.GetUsersAndGroups(permissionsById.Keys))
            {
                Dictionary<string, object> permission = new Dictionary<string, object>();
                FilePermission filePermission = permissionsById[userOrGroup.Id];

                permission["Id"] = userOrGroup.Id.Value;
                permission["Permission"] = filePermission.FilePermissionEnum;
                permission["Name"] = userOrGroup.Name;
                permission["Inherit"] = filePermission.Inherit;
                permission["SendNotifications"] = filePermission.SendNotifications;

                permissionsList.Add(permission);
            }

            return WebResults.ToJson(permissionsList);
        }

        /// <summary>
        /// Sets a named permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="usernameOrGroup"></param>
        /// <param name="namedPermission"></param>
        /// <param name="inherit"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults SetNamedPermission(IWebConnection webConnection, string usernameOrGroup, string namedPermission, bool inherit)
        {
            IUserOrGroup userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(usernameOrGroup);

            FileContainer.ParentDirectoryHandler.SetNamedPermission(
                FileContainer.FileId,
                namedPermission,
                userOrGroup.Id,
                inherit);

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Removes the named permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="usernameOrGroup"></param>
        /// <param name="namedPermission"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults RemoveNamedPermission(IWebConnection webConnection, string usernameOrGroup, string namedPermission)
        {
            IUserOrGroup userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(usernameOrGroup);

            FileContainer.ParentDirectoryHandler.RemoveNamedPermission(
                FileContainer.FileId,
                namedPermission,
                userOrGroup.Id);

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Returns all of the users and groups that have the named permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="namedPermission"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Administer)]
        public IWebResults GetNamedPermissions(IWebConnection webConnection, string namedPermission)
        {
            List<object> toReturn = new List<object>();

            foreach (NamedPermission np in FileContainer.ParentDirectoryHandler.GetNamedPermissions(
                FileContainer.FileId, namedPermission))
            {
                Dictionary<string, object> toJSON = new Dictionary<string, object>();
                toJSON["UserOrGroupId"] = np.UserOrGroupId;
                toJSON["UserOrGroup"] = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(np.UserOrGroupId).Name;
                toJSON["Inherit"] = np.Inherit;

                toReturn.Add(toJSON);
            }

            return WebResults.ToJson(toReturn);
        }

        /// <summary>
        /// Returns true if the user has the named permission, false otherwise
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="namedPermission"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults HasNamedPermission(IWebConnection webConnection, string namedPermission)
        {
            if (null == FileContainer.ParentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, "Permissions do not apply to the root directory"));

            bool hasPermission = FileContainer.ParentDirectoryHandler.HasNamedPermissions(
                FileContainer.FileId, new string[] { namedPermission }, webConnection.Session.User.Id);

            return WebResults.ToJson(hasPermission);
        }

        /// <summary>
        /// Performs any needed cleanup and optimization operations needed on the file 
        /// </summary>
        /// <param name="webConnection">
        /// A <see cref="IWebConnection"/>
        /// </param>
        /// <returns>
        /// A <see cref="IWebResults"/>
        /// </returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Administer)]
        public IWebResults Vacuum(IWebConnection webConnection)
        {
            FileHandler.Vacuum();

            return WebResults.FromStatus(Status._200_OK);
        }

        #region Common bus methods

        [ChannelEndpointMinimumPermission(FilePermissionEnum.Read)]
        public IChannelEventWebAdaptor Bus
        {
            get
            {
                if (null == _Bus)
                {
                    _Bus = new ChannelEventWebAdaptor();
                    _Bus.ClientConnected += new EventHandler<IChannelEventWebAdaptor, EventArgs<IQueuingReliableCometTransport>>(Bus_ClientConnected);
                    _Bus.ClientDisconnected += new EventHandler<IChannelEventWebAdaptor, EventArgs<IQueuingReliableCometTransport>>(Bus_ClientDisconnected);
                    _Bus.DataReceived += new EventHandler<IChannelEventWebAdaptor, ChannelEventWebAdaptor.DataReceivedEventArgs>(Bus_DataReceived);
                }

                return _Bus;
            }
        }
        private ChannelEventWebAdaptor _Bus = null;

        /// <summary>
        /// Helper to create a JSON object for a user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private Dictionary<string, object> CreateUserObjectForJSON(IUser user)
        {
            Dictionary<string, object> toReturn = new Dictionary<string, object>();

            toReturn["User"] = user.Name;
            toReturn["UserId"] = user.Id.ToString();
            toReturn["UserIdentity"] = user.Identity;

            return toReturn;
        }

        void Bus_DataReceived(IChannelEventWebAdaptor sender, ChannelEventWebAdaptor.DataReceivedEventArgs e)
        {
            PostBus(e.User, e.Data, "Bus");
        }

        void Bus_ClientDisconnected(IChannelEventWebAdaptor sender, EventArgs<IQueuingReliableCometTransport> e)
        {
            Dictionary<string, object> toSend = CreateUserObjectForJSON(e.Value.Session.User);

            toSend["Disconnected"] = true;

            toSend["Timestamp"] = DateTime.UtcNow;

            Bus.SendAll(toSend);
        }

        void Bus_ClientConnected(IChannelEventWebAdaptor sender, EventArgs<IQueuingReliableCometTransport> e)
        {
            Dictionary<string, object> toSend = CreateUserObjectForJSON(e.Value.Session.User);

            toSend["Connected"] = true;

            toSend["Timestamp"] = DateTime.UtcNow;

            Bus.SendAll(toSend);
        }

        /// <summary>
        /// Posts a message to the bus as coming from someone with read permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults PostBusAsRead(IWebConnection webConnection, string incoming)
        {
            object fromClient = JsonReader.Deserialize(incoming);

            PostBus(webConnection.Session.User, fromClient, "Read");
            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Posts a message to the bus as coming from someone with write permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults PostBusAsWrite(IWebConnection webConnection, string incoming)
        {
            object fromClient = JsonReader.Deserialize(incoming);

            PostBus(webConnection.Session.User, fromClient, "Write");
            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Posts a message to the bus as coming from someone with administer permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults PostBusAsAdminister(IWebConnection webConnection, string incoming)
        {
            object fromClient = JsonReader.Deserialize(incoming);

            PostBus(webConnection.Session.User, fromClient, "Administer");
            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Posts some data to the bus
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="data"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        private void PostBus(IUser user, object data, string source)
        {
            Dictionary<string, object> toSend = CreateUserObjectForJSON(user);

            toSend["Source"] = source;
            toSend["Data"] = data;

            toSend["Timestamp"] = DateTime.UtcNow;

            Bus.SendAll(toSend);
        }

        /// <summary>
        /// Returns all of the users connected to the object
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject)]
        public IWebResults GetConnectedUsers(IWebConnection webConnection)
        {
            Dictionary<IUser, Dictionary<string, object>> usersByUser = new Dictionary<IUser, Dictionary<string, object>>();

            foreach (ISession session in Bus.ConnectedSessions)
            {
                Dictionary<string, object> userToSend = null;

                if (usersByUser.TryGetValue(session.User, out userToSend))
                    userToSend["NumWindows"] = ((int)userToSend["NumWindows"]) + 1;
                else
                {
                    userToSend = CreateUserObjectForJSON(session.User);
                    userToSend["NumWindows"] = 1;

                    usersByUser[session.User] = userToSend;
                }
            }

            return WebResults.ToJson(usersByUser.Values);
        }

        #endregion

        #region Comet handlers

        /// <summary>
        /// Called as part of the loop of a transport (level 1) comet session
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.Naked, WebReturnConvention.JavaScriptObject)]
        public IWebResults PostComet(IWebConnection webConnection)
        {
            Dictionary<string, object> fromClient = JsonReader.Deserialize<Dictionary<string, object>>(webConnection.Content.AsString());

            object transportIdObject = null;
            if (!fromClient.TryGetValue("tid", out transportIdObject))
                return WebResults.FromString(Status._400_Bad_Request, "Transport id (tid) missing.");

            long transportId = default(long);
            try
            {
                transportId = Convert.ToInt64(transportIdObject);
            }
            catch
            {
                log.Error("Invalid transport ID: " + transportIdObject.ToString());
                return WebResults.FromString(Status._400_Bad_Request, "Transport id is invalid.  It must be an integer");
            }

            object timeoutObject = null;
            if (!fromClient.TryGetValue("lp", out timeoutObject))
                return WebResults.FromString(Status._400_Bad_Request, "Long poll (lp) missing.");

            double timeoutDouble = default(double);
            try
            {
                timeoutDouble = Convert.ToDouble(timeoutObject);
            }
            catch
            {
                log.Error("Invalid long poll: " + transportIdObject.ToString());
                return WebResults.FromString(Status._400_Bad_Request, "Long-poll is invalid.  It must be an number");
            }

            TimeSpan longPoll = TimeSpan.FromMilliseconds(timeoutDouble);

            ICometTransport cometTransport;
            if (fromClient.ContainsKey("isNew"))
                cometTransport = CreateNewCometTransport(webConnection.Session, webConnection.GetParameters, transportId);
            else
                cometTransport = GetCometTransport(webConnection.Session, transportId);

            if (fromClient.ContainsKey("d"))
                cometTransport.HandleIncomingData(fromClient["d"]);

            // If there's data to send, return it NOW
            if (PendingCometListeners.ContainsKey(cometTransport))
            {
                // This will cause the old long poll to send current pending results
                PendingCometListeners[cometTransport].TimeoutHandler(cometTransport);
                PendingCometListeners.Remove(cometTransport);
            }
            else
            {
                object data = cometTransport.GetDataToSend();
                if (null != data)
                    return WebResults.ToJson(data);
            }

            // if long-poll is disabled, return NOW without a long-poll
            if (0 == longPoll.TotalMilliseconds)
                webConnection.SendResults(WebResults.FromStatus(Status._200_OK));

            // If any pending data was returned on an old long poll, or there's no pending data, go to long-poll mode and wait for data
            MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>.Listener listener =
                default(MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>.Listener);

            listener = new MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>.Listener(
                longPoll,
                delegate(ICometTransport cometTransportT, EventArgs<TimeSpan> e)
                {
                    // TODO:  Honor the send delay
                    cometTransport.StartSend.RemoveListener(listener);
                    PendingCometListeners.Remove(cometTransport);

                    object data = cometTransport.GetDataToSend();

                    // Only return data if there was actually data to send
                    if (null != data)
                        webConnection.SendResults(WebResults.ToJson(data));
                },
                delegate(ICometTransport cometTransportT)
                {
                    cometTransport.StartSend.RemoveListener(listener);
                    PendingCometListeners.Remove(cometTransport);

                    object data = cometTransport.GetDataToSend();

                    if (null != data)
                        webConnection.SendResults(WebResults.ToJson(data));
                    else
                        webConnection.SendResults(WebResults.FromStatus(Status._200_OK));
                });

            cometTransport.StartSend.AddListener(listener);
            PendingCometListeners[cometTransport] = listener;

            return null;
        }

        /// <summary>
        /// Pending comet listeners
        /// </summary>
        private Dictionary<ICometTransport, MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>.Listener> PendingCometListeners =
            new Dictionary<ICometTransport, MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>.Listener>();

        /// <summary>
        /// Used to uniquely identify a comet session
        /// </summary>
        private struct CometSessionId
        {
            public CometSessionId(ID<ISession, Guid> sessionId, long transportId)
            {
                SessionId = sessionId;
                TransportId = transportId;
                _Hash = default(int?);
            }

            public ID<ISession, Guid> SessionId;
            public long TransportId;

            public override bool Equals(object obj)
            {
                if (!(obj is CometSessionId))
                    return false;

                CometSessionId csId = (CometSessionId)obj;
                return (SessionId == csId.SessionId) && (TransportId == csId.TransportId);
            }

            static MD5 md5 = new MD5CryptoServiceProvider();

            public override int GetHashCode()
            {
                if (default(int?) == _Hash)
                {
                    List<byte> toHash = new List<byte>();
                    toHash.AddRange(SessionId.Value.ToByteArray());
                    toHash.AddRange(BitConverter.GetBytes(TransportId));

                    byte[] hash = md5.ComputeHash(toHash.ToArray());
                    _Hash = BitConverter.ToInt32(hash, 0);
                }

                return _Hash.Value;
            }
            private int? _Hash;

            public override string ToString()
            {
                return SessionId.ToString() + "-" + TransportId.ToString();
            }
        }

        /// <summary>
        /// Used to track if a comet session is going idle
        /// </summary>
        private class CometSessionTracker
        {
            public ICometTransport CometTransport;
            public DateTime LastUsed;
        }

        /// <summary>
        /// All of the comet transports that are currently active
        /// </summary>
        private Dictionary<CometSessionId, CometSessionTracker> ActiveCometTransports = new Dictionary<CometSessionId, CometSessionTracker>();

        /// <summary>
        /// Creates a new comet transport as part of a transport (level 1) or multiplexed (level 2) comet session
        /// </summary>
        /// <param name="session"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        public ICometTransport CreateNewCometTransport(ISession session, IDictionary<string, string> getArguments, long transportId)
        {
            CometSessionId id = new CometSessionId(session.SessionId, transportId);

            if (ActiveCometTransports.ContainsKey(id))
                throw new WebResultsOverrideException(WebResults.FromStatus(Status._409_Conflict));

            CometSessionTracker toReturn = new CometSessionTracker();
            toReturn.CometTransport = ConstructCometTransport(session, getArguments, transportId);

            using (TimedLock.Lock(ActiveCometTransports))
                ActiveCometTransports[id] = toReturn;

            toReturn.LastUsed = DateTime.UtcNow;

            using (TimedLock.Lock(PurgeOldCometSessionsKey))
                if (null == PurgeOldCometSessionsTimer)
                {
                    // This no-op is to work around a weird mono compiler bug...  Get rid of it once the mono compiler is updated!
                    // https://bugzilla.novell.com/show_bug.cgi?id=554715
                    if (noop < 0) noop = SRandom.Next<int>();

                    PurgeOldCometSessionsTimer = new Timer(
                        CleanOldTransports,
                        null,
                        TimeSpan.FromMilliseconds(0),
                        TimeSpan.FromSeconds(FileHandlerFactoryLocator.WebServer.CheckDeadConnectionsFrequencySeconds));
                }

            return toReturn.CometTransport;
        }

        private static int noop = 0;

        /// <summary>
        /// Holds all properties and permissions by Type for channel endpoints
        /// </summary>
        private static Dictionary<Type, Dictionary<string, PropertyAndPermission>> ChannelEndpointPropertiesAndPermissionsByType =
            new Dictionary<Type, Dictionary<string, PropertyAndPermission>>();

        /// <summary>
        /// Holds property and permission
        /// </summary>
        private struct PropertyAndPermission
        {
            internal PropertyInfo Property;
            internal FilePermissionEnum MinimumPermission;
        }

        /// <summary>
        /// Constructs a comet transport for transport (level 1) or multiplexed (level 2) comet
        /// </summary>
        /// <param name="session"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        public virtual ICometTransport ConstructCometTransport(ISession session, IDictionary<string, string> getArguments, long transportId)
        {
            string channelEndpointName = null;
            if (getArguments.TryGetValue("ChannelEndpoint", out channelEndpointName))
            {
                Type myType = GetType();

                // Reflect on the class if needed to figure out what channel endpoints are present
                if (!ChannelEndpointPropertiesAndPermissionsByType.ContainsKey(myType))
                    using (TimedLock.Lock(ChannelEndpointPropertiesAndPermissionsByType))
                        if (!ChannelEndpointPropertiesAndPermissionsByType.ContainsKey(myType))
                        {
                            Dictionary<string, PropertyAndPermission> channelEndpointPropertiesAndPermissions = new Dictionary<string, PropertyAndPermission>();

                            foreach (PropertyInfo pi in myType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public))
                                if (typeof(IChannelEventWebAdaptor) == pi.PropertyType)
                                    foreach (ChannelEndpointMinimumPermissionAttribute cempa in
                                        pi.GetCustomAttributes(typeof(ChannelEndpointMinimumPermissionAttribute), true))
                                    {
                                        PropertyAndPermission pap = new PropertyAndPermission();
                                        pap.Property = pi;
                                        pap.MinimumPermission = cempa.MinimumPermission;

                                        channelEndpointPropertiesAndPermissions[pi.Name] = pap;
                                    }

                            ChannelEndpointPropertiesAndPermissionsByType[myType] = channelEndpointPropertiesAndPermissions;
                        }

                // Load and return the channel, if present
                PropertyAndPermission propertyAndPermission = default(PropertyAndPermission);
                if (ChannelEndpointPropertiesAndPermissionsByType[myType].TryGetValue(channelEndpointName, out propertyAndPermission))
                {
                    if (FileContainer.LoadPermission(session.User.Id) < propertyAndPermission.MinimumPermission)
                        throw new WebResultsOverrideException(WebResults.FromString(Status._401_Unauthorized, "Permission denied"));

                    QueuingReliableCometTransport toReturn =
                        new QueuingReliableCometTransport(FileContainer.FullPath + "?ChannelEndpoint=" + channelEndpointName, session);

                    IChannelEventWebAdaptor channelEventWebAdaptor = (IChannelEventWebAdaptor)propertyAndPermission.Property.GetValue(this, null);
                    channelEventWebAdaptor.AddChannel(toReturn);

                    return toReturn;
                }
            }

            throw new WebResultsOverrideException(WebResults.FromStatus(Status._404_Not_Found));
        }

        /// <summary>
        /// Syncronizes access to PurgeOldCometSessionsTimer
        /// </summary>
        private object PurgeOldCometSessionsKey = new object();

        /// <summary>
        /// Purges dead comet sessions
        /// </summary>
        private Timer PurgeOldCometSessionsTimer = null;

        /// <summary>
        /// Retrieves a pre-exising comet transport as part of a transport (level 1) or multiplexed (level 2) comet session
        /// </summary>
        /// <param name="session"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        public ICometTransport GetCometTransport(ISession session, long transportId)
        {
            CometSessionId id = new CometSessionId(session.SessionId, transportId);

            CometSessionTracker toReturn = default(CometSessionTracker);
            if (!ActiveCometTransports.TryGetValue(id, out toReturn))
                throw new WebResultsOverrideException(WebResults.FromStatus(Status._410_Gone));

            toReturn.LastUsed = DateTime.UtcNow;

            Console.Write("");

            return toReturn.CometTransport;
        }

        /// <summary>
        /// Cleans up old and dead transports
        /// </summary>
        private void CleanOldTransports(object state)
        {
            List<KeyValuePair<CometSessionId, CometSessionTracker>> activeCometTransports;
            using (TimedLock.Lock(ActiveCometTransports))
                activeCometTransports = new List<KeyValuePair<CometSessionId, CometSessionTracker>>(ActiveCometTransports);

            foreach (KeyValuePair<CometSessionId, CometSessionTracker> cst in activeCometTransports)
            {
                DateTime lastUsed = cst.Value.LastUsed;
                DateTime expiresAt = lastUsed.AddSeconds(FileHandlerFactoryLocator.WebServer.MaxConnectionIdleSeconds);

                if (expiresAt < DateTime.UtcNow)
                {
                    using (TimedLock.Lock(ActiveCometTransports))
                        ActiveCometTransports.Remove(cst.Key);

                    cst.Value.CometTransport.Dispose();
                }
            }

            bool killTimer;
            using (TimedLock.Lock(ActiveCometTransports))
                killTimer = (0 == ActiveCometTransports.Count);

            if (killTimer)
                using (TimedLock.Lock(PurgeOldCometSessionsKey))
                    if (null != PurgeOldCometSessionsTimer)
                    {
                        PurgeOldCometSessionsTimer.Dispose();
                        PurgeOldCometSessionsTimer = null;
                    }
        }

        #endregion

        /// <summary>
        /// Adds a file as being related to this file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <param name="relationship"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults AddRelatedFile(IWebConnection webConnection, string filename, string relationship)
        {
            if (null == FileContainer.ParentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._406_Not_Acceptable, "The root directory can not have relationships"));

            // Get the full path if it's not present
            if (!filename.StartsWith("/"))
                filename = FileContainer.ParentDirectoryHandler.FileContainer.FullPath + "/" + filename;

            IFileContainer relatedContainer;
            try
            {
                relatedContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(WebResults.FromString(Status._404_Not_Found, filename + " does not exist"));
            }

            FileContainer.ParentDirectoryHandler.AddRelationship(
                FileContainer, relatedContainer, relationship);

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Removes a file from being related to this file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <param name="relationship"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults DeleteRelatedFile(IWebConnection webConnection, string filename, string relationship)
        {
            if (null == FileContainer.ParentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._406_Not_Acceptable, "The root directory can not have relationships"));

            // Get the full path if it's not present
            if (!filename.StartsWith("/"))
                filename = FileContainer.ParentDirectoryHandler.FileContainer.FullPath + "/" + filename;

            IFileContainer relatedContainer;
            try
            {
                relatedContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(WebResults.FromString(Status._404_Not_Found, filename + " does not exist"));
            }

            FileContainer.ParentDirectoryHandler.DeleteRelationship(
                FileContainer, relatedContainer, relationship);

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Returns information about each file that is related to this file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="relationships">A JSON array of potential relationships, or null to match all relationships</param>
        /// <param name="extensions">A JSON array of potential extentions, or null to match all extensions</param>
        /// <param name="newest"></param>
        /// <param name="maxToReturn"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetRelatedFiles(
            IWebConnection webConnection,
            string relationships,
            string extensions,
            DateTime? newest,
            DateTime? oldest,
            uint? maxToReturn)
        {
            List<string> relationshipsAsList = null;

            // Convert relationships
            if (null != relationships)
            {
                object relationshipsAsObject = JsonReader.Deserialize(relationships);
                relationshipsAsList = new List<string>();

                if (relationshipsAsObject is string)
                    relationshipsAsList.Add(relationshipsAsObject.ToString());
                else if (relationshipsAsObject is IEnumerable<object>)
                    foreach (object relationshipObj in (IEnumerable<object>)relationshipsAsObject)
                        relationshipsAsList.Add(relationshipObj.ToString());
                else
                    throw new WebResultsOverrideException(
                        WebResults.FromString(Status._406_Not_Acceptable, relationships + " is invalid, must be either a JSON string or JSON array"));
            }

            List<string> extensionsAsList = null;

            // Convert extensions
            if (null != extensions)
            {
                object extensionsAsObject = JsonReader.Deserialize(extensions);
                extensionsAsList = new List<string>();

                if (extensionsAsObject is string)
                    extensionsAsList.Add(extensionsAsObject.ToString());
                else if (extensionsAsObject is IEnumerable<object>)
                    foreach (object extensionObj in (IEnumerable<object>)extensionsAsObject)
                        extensionsAsList.Add(extensionObj.ToString());
                else
                    throw new WebResultsOverrideException(
                        WebResults.FromString(Status._406_Not_Acceptable, extensions + " is invalid, must be either a JSON string or JSON array"));
            }

            // Run the query
            IEnumerable<IFileContainer> relatedFiles = FileContainer.ParentDirectoryHandler.GetRelatedFiles(
                webConnection.Session.User.Id,
                FileContainer.FileId,
                relationshipsAsList,
                extensionsAsList,
                newest,
                oldest,
                maxToReturn);

            IList<IDictionary<string, object>> toReturn = GetFilesForJSON(webConnection.Session, relatedFiles);

            return WebResults.ToJson(toReturn);

        }

        /// <summary>
        /// Returns all files as JSON-able dictionaries
        /// </summary>
        /// <param name="session"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        protected IList<IDictionary<string, object>> GetFilesForJSON(ISession session, IEnumerable<IFileContainer> files)
        {
            IList<IDictionary<string, object>> toReturn = new List<IDictionary<string, object>>();

            foreach (IFileContainer file in files)
                toReturn.Add(GetFileForJSON(session, file));

            return toReturn;
        }

        /// <summary>
        /// Returns the file as JSON
        /// </summary>
        /// <param name="session"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        protected IDictionary<string, object> GetFileForJSON(ISession session, IFileContainer file)
        {
            IDictionary<string, object> toReturn = new Dictionary<string, object>();

            toReturn["Filename"] = file.Filename;
            toReturn["FullPath"] = file.FullPath;
            toReturn["FileId"] = file.FileId.Value.ToString(CultureInfo.InvariantCulture);
            toReturn["TypeId"] = file.TypeId;
            toReturn["LastModified"] = file.FileHandler.LastModified;
            toReturn["Created"] = file.Created;
            toReturn["Permission"] = file.LoadPermission(session.User.Id);

            // Load the owner
            bool ownerKnown = null != file.Owner;

            if (ownerKnown)
            {
                toReturn["OwnerId"] = file.OwnerId.Value.Value.ToString("D", CultureInfo.InvariantCulture);
                toReturn["Owner"] = file.Owner.Name;
                toReturn["OwnerIdentity"] = file.Owner.Identity;
            }
            else
            {
                toReturn["OwnerId"] = null;
                toReturn["Owner"] = null;
                toReturn["OwnerIdentity"] = null;
            }

            return toReturn;
        }

        /// <summary>
        /// Changes this file's owner
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="newOwnerId">The new owner's user ID</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults Chown(IWebConnection webConnection, Guid? newOwnerId)
        {
            if (null == FileContainer.ParentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._406_Not_Acceptable, "The root directory can not have ownership"));

            ID<IUserOrGroup, Guid>? ownerId = null;
            if (null != newOwnerId)
                ownerId = new ID<IUserOrGroup, Guid>(newOwnerId.Value);

            FileContainer.ParentDirectoryHandler.Chown(
                webConnection.Session.User, FileContainer.FileId, ownerId);

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Sends a packet whenever a relationship is added
        /// </summary>
        [ChannelEndpointMinimumPermission(FilePermissionEnum.Read)]
        public IChannelEventWebAdaptor RelationshipEvent
        {
            get
            {
                if (!RelationshipEventWired)
                    using (TimedLock.Lock(_RelationshipEvent))
                        if (!RelationshipEventWired)
                        {
                            RelationshipEventWired = true;
                            FileHandler.RelationshipAdded += new EventHandler<IFileHandler, RelationshipEventArgs>(FileHandler_RelationshipAdded);
                        }

                return _RelationshipEvent;
            }
        }
        private readonly ChannelEventWebAdaptor _RelationshipEvent = new ChannelEventWebAdaptor();

        private bool RelationshipEventWired = false;

        void FileHandler_RelationshipAdded(IFileHandler sender, RelationshipEventArgs args)
        {
            foreach (IQueuingReliableCometTransport channel in _RelationshipEvent.Channels)
                ThreadPool.QueueUserWorkItem(
                    delegate(object state)
                    {
                        try
                        {
                            SendRelationship((IQueuingReliableCometTransport)state, args);
                        }
                            // Sometimes a channel can become disposed.  It's okay, we'll just ignore it!
                        catch (ObjectDisposedException) { }
                    },
                    channel);
        }

        /// <summary>
        /// Helper to update the files on a connection
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="files"></param>
        private void SendRelationship(IQueuingReliableCometTransport channel, RelationshipEventArgs args)
        {
            Dictionary<string, object> toSend = new Dictionary<string, object>();
            toSend["Timestamp"] = DateTime.UtcNow;
            toSend["File"] = GetFileForJSON(channel.Session, args.RelatedFile);
            toSend["Relationship"] = args.Relationship;
            
            try
            {
                IWebConnection webConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    channel.Session,
                    args.RelatedFile.FullPath + "?Action=Preview",
                    null,
                    null,
                    null,
                    CallingFrom.Web,
                    default(WebMethod));

                IWebResults webResults = webConnection.GenerateResultsForClient();

                Dictionary<string, object> toJSON = new Dictionary<string, object>();
                toJSON["Status"] = (int)webResults.Status;
                toJSON["Content"] = webResults.ResultsAsString;
                toJSON["Headers"] = webResults.Headers;

                toSend["View"] = toJSON;
            }
            catch
            {
                toSend["View"] = null;
            }

            channel.Send(toSend, TimeSpan.Zero);
        }

        #region Execution Environment Handling logic... TODO: This needs to move somewhere else


        /// <summary>
        /// Where Javascript is executed
        /// </summary>
        public IExecutionEnvironment GetOrCreateExecutionEnvironment()
        {
            // Files without an extension can not have a local execution environment
            string extension = FileContainer.Extension;
            if (null == extension)
                return null;

            // Try to find the javascript file

            IDirectoryHandler parentDirectoryHandler = FileContainer.ParentDirectoryHandler;
            IFileContainer javascriptContainer = null;

            // Keep looking up the directory tree for a Classes folder...
            while (null != parentDirectoryHandler && null == javascriptContainer)
            {
                if (parentDirectoryHandler.IsFilePresent("Classes"))
                {
                    IFileContainer classesDirectoryContainer = parentDirectoryHandler.OpenFile("Classes");

                    // ...if a parent folder has a directory named Classes
                    if (classesDirectoryContainer.FileHandler is IDirectoryHandler)
                    {
                        IDirectoryHandler classesDirectoryHandler = classesDirectoryContainer.CastFileHandler<IDirectoryHandler>();

                        if (classesDirectoryHandler.IsFilePresent(extension))
                        {
                            IFileContainer potentialClassFileContainer = classesDirectoryHandler.OpenFile(extension);

                            // Files with no owner are considered owned by an administrator
                            bool ownedByAdministrator;
                            if (null != potentialClassFileContainer.OwnerId)
                                ownedByAdministrator = FileHandlerFactoryLocator.UserManagerHandler.IsUserInGroup(
                                potentialClassFileContainer.OwnerId.Value,
                                FileHandlerFactoryLocator.UserFactory.Administrators.Id);
                            else
                                ownedByAdministrator = true;

                            // And it has a text file with the same name as the extention, then it means that the javascript handler is found!
                            if (ownedByAdministrator)
                                if (potentialClassFileContainer.FileHandler is ITextHandler)
                                    javascriptContainer = potentialClassFileContainer;
                        }
                    }
                }

                // move to the parent directory
                if (null != parentDirectoryHandler.FileContainer)
                    parentDirectoryHandler = parentDirectoryHandler.FileContainer.ParentDirectoryHandler;
                else
                    parentDirectoryHandler = null;
            }

            // If a javascript container was found, make sure there's an up-to-date ExecutionEnvironment
            if (null != javascriptContainer)
            {
                using (TimedLock.Lock(ExecutionEnvironmentLock))
                {
                    IExecutionEnvironmentFactory factory = FileHandlerFactoryLocator.ExecutionEnvironmentFactory;

                    if (null == _ExecutionEnvironment)
                        _ExecutionEnvironment = factory.Create(FileHandlerFactoryLocator, FileContainer, javascriptContainer);
                    else if (_ExecutionEnvironment.JavascriptContainer != javascriptContainer)
                        _ExecutionEnvironment = factory.Create(FileHandlerFactoryLocator, FileContainer, javascriptContainer);
                    else if (javascriptContainer.FileHandler.LastModified > _ExecutionEnvironment.JavascriptLastModified)
                        _ExecutionEnvironment = factory.Create(FileHandlerFactoryLocator, FileContainer, javascriptContainer);

                    // using a local version of the object outside of the lock avoids a potential null reference issue if the file is deleted while its javascript is run
                    return _ExecutionEnvironment;
                }
            }
            else
            {
                // If there is no javascript container, make sure that the execution environment is explicitly disabled!
                using (TimedLock.Lock(ExecutionEnvironmentLock))
                    _ExecutionEnvironment = null;

                return null;
            }
        }

        /// <summary>
        /// The cached ExecutionEnvironment.  Call GetOrCreateExecutionEnvironment to ensure that the value is fresh
        /// </summary>
        private IExecutionEnvironment _ExecutionEnvironment = null;

        /// <summary>
        /// Lock for when determining if the ExecutionEnvironment should be updated
        /// </summary>
        protected object ExecutionEnvironmentLock = new object();

        #endregion
    }

    /// <summary>
    /// WebHandler for files that don't have any web access
    /// </summary>
    public class WebHandler : WebHandler<IFileHandler> { }
}
