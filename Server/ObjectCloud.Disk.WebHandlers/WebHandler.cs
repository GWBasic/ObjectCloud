// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
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
            if (!webConnection.BypassJavascript)
            {
                IExecutionEnvironment executionEnvironment = GetOrCreateExecutionEnvironment();
                if (null != executionEnvironment)
                {
                    WebDelegate toReturn = executionEnvironment.GetMethod(webConnection);

                    if (null != toReturn)
                        return toReturn;
                }
            }

            bool allowLocalMethods = true;
            if (!webConnection.BypassJavascript)
                allowLocalMethods = !FileContainer.FileConfigurationManager.BlockWebMethods;

            string method = webConnection.GetArgumentOrException("Method");

            // When the call is local or there is no execution environment, then look for the base web method
            if (webConnection.CallingFrom == CallingFrom.Local || allowLocalMethods || AllowedBaseMethods.Contains(method))
            {
                WebDelegate toReturn = FileHandlerFactoryLocator.WebMethodCache[MethodNameAndFileContainer.New(method, FileContainer)];
                if (null != toReturn)
                    return toReturn;
            }

            // Throw an exception if no method is found
            throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "method \"" + method + "\" does not exist"));
        }

        /// <summary>
        /// These methods are allowed even if the object is wrapped by a server-side javascript class
        /// </summary>
        private static HashSet<string> AllowedBaseMethods = new HashSet<string>();

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
        /// The cached in-browser JavaScript wrapper
        /// </summary>
        private string CachedInBrowserJSWrapper = null;

        /// <summary>
        /// The web handler types
        /// </summary>
        public HashSet<Type> WebHandlerTypes
        {
            get 
            {
                if (null == _WebHandlerTypes)
                {
                    HashSet<Type> webHandlerTypes = new HashSet<Type>();
                    webHandlerTypes.Add(GetType());

                    foreach (IWebHandlerPlugin webHandlerPlugin in FileContainer.WebHandlerPlugins)
                        webHandlerTypes.Add(webHandlerPlugin.GetType());

                    _WebHandlerTypes = webHandlerTypes;
                }

                return _WebHandlerTypes; 
            }
        }
        private HashSet<Type> _WebHandlerTypes = null;

        /// <summary>
        /// Returns a Javascript object that can perform all calls to all methods marked as WebCallable through AJAX.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="assignToVariable">The variable to assign the wrapper object to</param>
        /// <param name="EncodeFor">If set to "JavaScript", the generated JavaScript will be minimized</param>
        /// <param name="bypassJavascript">true to bypass server-side Javascript</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetJSW(IWebConnection webConnection, string assignToVariable, string EncodeFor, bool bypassJavascript)
        {
            // Not worth syncronizing, nothing bad will happen if multiple threads enter this block at the same time
            if (null == CachedInBrowserJSWrapper)
            {
                List<string> javascriptMethods = new List<string>(
                    FileHandlerFactoryLocator.WebServer.JavascriptWebAccessCodeGenerator.GenerateWrapper(WebHandlerTypes));
                
                javascriptMethods.Add(
                    "\"Url\": \"" + "http://" + FileHandlerFactoryLocator.HostnameAndPort + FileContainer.FullPath + "\"");

                // Insert the user's permission to the file
                javascriptMethods.Add("\"Permission\": \"{3}\"");

                string javascriptWrapper = StringGenerator.GenerateSeperatedList(javascriptMethods, ",\n");

                // Replace some key constants
                javascriptWrapper = javascriptWrapper.Replace("{0}", FileContainer.FullPath);
                javascriptWrapper = javascriptWrapper.Replace("{1}", FileContainer.Filename);
                javascriptWrapper = javascriptWrapper.Replace("{2}", FileContainer.TypeId);

                if (null != FileContainer.Owner)
                    javascriptWrapper = javascriptWrapper.Replace("{5}", JsonWriter.Serialize(FileContainer.Owner.Name));
                else
                    javascriptWrapper = javascriptWrapper.Replace("{5}", JsonWriter.Serialize(null));

                CachedInBrowserJSWrapper = javascriptWrapper;
            }

            string javascriptToReturn = CachedInBrowserJSWrapper;

            // Insert the user's permission to the file
            javascriptToReturn = javascriptToReturn.Replace("{3}", FileContainer.LoadPermission(webConnection.Session.User.Id).ToString());

            // Insert the server-side Javascript wrappers
            if (!bypassJavascript)
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

            javascriptToReturn = javascriptToReturn.Replace("{4}", bypassJavascript.ToString().ToLower());

            // Enclose the functions with { .... }
            javascriptToReturn = "{\n" + javascriptToReturn + "\n}";

            if (null != assignToVariable)
                javascriptToReturn = string.Format("var {0} = {1};", assignToVariable, javascriptToReturn);

            javascriptToReturn = "// Scripts: /API/AJAX.js, /API/json2.js\n" + javascriptToReturn;

            if (EncodeFor == "JavaScript")
            //if (FileHandlerFactoryLocator.WebServer.MinimizeJavascript)
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

                    return WebResults.From(Status._500_Internal_Server_Error, "Error when minimizing JavaScript: " + e.Message);
                }
            }


            IWebResults toReturn = WebResults.From(
                Status._200_OK,
                javascriptToReturn);

            toReturn.ContentType = "application/javascript";
            return toReturn;
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
            return WebResults.From(Status._200_OK, executionEnvironment.ExecutionEnvironmentErrors != null ? executionEnvironment.ExecutionEnvironmentErrors : "no errors");
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
        /// <param name="namedPermissions"></param>
        /// <param name="UserOrGroupIds"></param>
        /// <param name="UserOrGroups"></param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Administer)]
        public IWebResults SetPermission(
            IWebConnection webConnection, 
            string UserOrGroupId, 
            string UserOrGroup,
            string[] UserOrGroups,
            string[] UserOrGroupIds,
            string FilePermission, 
            bool? Inherit, 
            bool? SendNotifications, 
            string[] namedPermissions)
        {
            HashSet<ID<IUserOrGroup, Guid>> userOrGroupIds = new HashSet<ID<IUserOrGroup,Guid>>();

            // Build list of IDs to check
            HashSet<ID<IUserOrGroup, Guid>> userOrGroupIdsToCheck = new HashSet<ID<IUserOrGroup, Guid>>();
            if (null != UserOrGroupIds)
                foreach (string userOrGroupIdString in UserOrGroupIds)
                    userOrGroupIdsToCheck.Add(new ID<IUserOrGroup, Guid>(new Guid(userOrGroupIdString)));
            if (null != UserOrGroupId)
                userOrGroupIdsToCheck.Add(new ID<IUserOrGroup, Guid>(new Guid(UserOrGroupId)));

            // Build list of usernames to check
            HashSet<string> userOrGroupsToCheck = new HashSet<string>();
            if (null != UserOrGroups)
                foreach (string userOrGroupName in UserOrGroups)
                    userOrGroupsToCheck.Add(userOrGroupName);
            if (null != UserOrGroup)
                userOrGroupsToCheck.Add(UserOrGroup);

            // Build set of user and group IDs while verifying that they exist
            object errorObject = "";
            try
            {
                foreach (ID<IUserOrGroup, Guid> userOrGroupId in userOrGroupIdsToCheck)
                {
                    errorObject = userOrGroupId;

                    // Verify that user exists
                    IUserOrGroup toVerify =
                        FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(userOrGroupId);

                    userOrGroupIds.Add(toVerify.Id);
                }

                foreach (string userOrGroupName in userOrGroupsToCheck)
                {
                    errorObject = userOrGroupName;

                    // Verify that user exists
                    IUserOrGroup toVerify =
                        FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(userOrGroupName.Trim());

                    userOrGroupIds.Add(toVerify.Id);
                }
            }
            catch (UnknownUser)
            {
                return WebResults.From(Status._406_Not_Acceptable, errorObject.ToString() + " does not exist");
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
                FileHandler.FileContainer.ParentDirectoryHandler.SetPermission(
                   webConnection.Session.User.Id,
                   FileHandler.FileContainer.Filename,
                   userOrGroupIds,
                   level.Value,
                   inherit,
                   sendNotifications);
				
				if (null != namedPermissions)
					foreach (string namedPermission in namedPermissions)
						FileHandler.FileContainer.ParentDirectoryHandler.SetNamedPermission(
	                        FileContainer.FileId,
	                        namedPermission,
	                        userOrGroupIds,
	                        inherit);
				
                return WebResults.From(Status._202_Accepted, "Permission set to " + level.ToString());
            }
            else
            {
                FileHandler.FileContainer.ParentDirectoryHandler.RemovePermission(FileHandler.FileContainer.Filename, userOrGroupIds);
                return WebResults.From(Status._202_Accepted, "Permission removed");
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
                return WebResults.From(Status._200_OK, permission.ToString());
            else
                return WebResults.From(Status._200_OK, "");
        }

        /// <summary>
        /// Returns the currently logged in user's permission for this file as a Javascript object that can be queried.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON)]
        public IWebResults GetPermissionAsJSON(IWebConnection webConnection)
        {
            ID<IUserOrGroup, Guid> userId = webConnection.Session.User.Id;
            Dictionary<string, object> toReturn = GetPermissionAsJSON(userId);

            return WebResults.ToJson(toReturn);
        }

        private Dictionary<string, object> GetPermissionAsJSON(ID<IUserOrGroup, Guid> userId)
        {
            // Create an array of values to return
            Dictionary<string, object> toReturn = new Dictionary<string, object>();

            FilePermissionEnum? permissionNullable = FileContainer.LoadPermission(userId);

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

            foreach (Dictionary<string, object> supportedNamedPermission in FileContainer.FileConfigurationManager.ViewComponents)
                toReturn["Supports" + supportedNamedPermission["NamedPermission"].ToString()] = true;

            return toReturn;
        }

        /// <summary>
        /// Returns the currently logged in user's permission for this file as a Javascript object that can be queried, the file name, extension, full path, create date, and modification date.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON)]
        public IWebResults GetInfoAndPermission(IWebConnection webConnection)
        {
            ID<IUserOrGroup, Guid> userId = webConnection.Session.User.Id;
            Dictionary<string, object> toReturn = GetPermissionAsJSON(userId);

            toReturn["Created"] = FileContainer.Created;
            toReturn["LastModified"] = FileContainer.LastModified;
            toReturn["Extension"] = FileContainer.Extension;
            toReturn["FileId"] = FileContainer.FileId.ToString();
            toReturn["Name"] = FileContainer.Filename;
            toReturn["Url"] = FileContainer.ObjectUrl;

            if (null != FileContainer.OwnerId)
            {
                IUser owner = FileHandlerFactoryLocator.UserManagerHandler.GetUser(FileContainer.OwnerId.Value);

                toReturn["OwnerId"] = owner.Id.ToString();
                toReturn["Owner"] = owner.Name;
                toReturn["OwnerIdentity"] = owner.Identity;
				toReturn["OwnerUrl"] = owner.Url;
				toReturn["OwnerAvatarUrl"] = owner.AvatarUrl;
				toReturn["OwnerDisplayName"] = owner.DisplayName;
				toReturn["HasOwner"] = true;
            }
            else
            {
                toReturn["OwnerId"] = null;
                toReturn["Owner"] = null;
                toReturn["OwnerIdentity"] = null;
				toReturn["OwnerUrl"] = null;
				toReturn["OwnerAvatarUrl"] = null;
				toReturn["OwnerDisplayName"] = null;
			}
			
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
                permission["DisplayName"] = userOrGroup.DisplayName;
                permission["Identity"] = userOrGroup.Identity;
                permission["Url"] = userOrGroup.Url;
                permission["AvatarUrl"] = userOrGroup.AvatarUrl;
                permission["Inherit"] = filePermission.Inherit;
                permission["SendNotifications"] = filePermission.SendNotifications;
				
				Dictionary<string, object> namedPermissions = new Dictionary<string, object>();
				permission["NamedPermissions"] = namedPermissions;
				
				foreach (KeyValuePair<string, bool> namedPermission in filePermission.NamedPermissions)
					namedPermissions[namedPermission.Key] = namedPermission.Value ? "NoInherit" : "Inherit";

                permissionsList.Add(permission);
            }

            return WebResults.ToJson(permissionsList);
        }

        /// <summary>
        /// Sets a named permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="UserOrGroupId"></param>
        /// <param name="usernameOrGroup"></param>
        /// <param name="namedPermission"></param>
        /// <param name="inherit"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults SetNamedPermission(IWebConnection webConnection, Guid? UserOrGroupId, string usernameOrGroup, string namedPermission, bool inherit)
        {
			IUserOrGroup userOrGroup;
			
			if (null != UserOrGroupId)
				userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(new ID<IUserOrGroup, Guid>(UserOrGroupId.Value));
			else
                userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(usernameOrGroup);

            FileContainer.ParentDirectoryHandler.SetNamedPermission(
                FileContainer.FileId,
                namedPermission,
                new ID<IUserOrGroup, Guid>[] { userOrGroup.Id },
                inherit);

            return WebResults.From(Status._202_Accepted);
        }

        /// <summary>
        /// Removes the named permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="UserOrGroupId"></param>
        /// <param name="usernameOrGroup"></param>
        /// <param name="namedPermission"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults RemoveNamedPermission(IWebConnection webConnection, Guid? UserOrGroupId, string usernameOrGroup, string namedPermission)
        {
			IUserOrGroup userOrGroup;
			
			if (null != UserOrGroupId)
				userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroup(new ID<IUserOrGroup, Guid>(UserOrGroupId.Value));
			else
            	userOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(usernameOrGroup);

            FileContainer.ParentDirectoryHandler.RemoveNamedPermission(
                FileContainer.FileId,
                namedPermission,
                new ID<IUserOrGroup, Guid>[] { userOrGroup.Id });

            return WebResults.From(Status._202_Accepted);
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
                toJSON["UserOrGroupId"] = np.UserOrGroupId.Value.ToString();
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
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "Permissions do not apply to the root directory"));

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

            return WebResults.From(Status._200_OK);
        }

        #region Common bus methods

        /// <summary>
        /// The object's bus.  Messages can be written to the bus without having to open a Comet session; any user with read permission to the object can open a comet session and see all messages on the bus
        /// </summary>
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
        /// <param name="incoming">The message to post to the bus</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults PostBusAsRead(IWebConnection webConnection, string incoming)
        {
            object fromClient = JsonReader.Deserialize(incoming);

            PostBus(webConnection.Session.User, fromClient, "Read");
            return WebResults.From(Status._202_Accepted);
        }

        /// <summary>
        /// Posts a message to the bus as coming from someone with write permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="incoming">The message to post to the bus</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults PostBusAsWrite(IWebConnection webConnection, string incoming)
        {
            object fromClient = JsonReader.Deserialize(incoming);

            PostBus(webConnection.Session.User, fromClient, "Write");
            return WebResults.From(Status._202_Accepted);
        }

        /// <summary>
        /// Posts a message to the bus as coming from someone with administer permission
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="incoming"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults PostBusAsAdminister(IWebConnection webConnection, string incoming)
        {
            object fromClient = JsonReader.Deserialize(incoming);

            PostBus(webConnection.Session.User, fromClient, "Administer");
            return WebResults.From(Status._202_Accepted);
        }

        /// <summary>
        /// Posts some data to the bus
        /// </summary>
        /// <param name="data"></param>
        /// <param name="source"></param>
        /// <param name="user"></param>
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
                return WebResults.From(Status._400_Bad_Request, "Transport id (tid) missing.");

            long transportId = default(long);
            try
            {
                transportId = Convert.ToInt64(transportIdObject);
            }
            catch
            {
                log.Error("Invalid transport ID: " + transportIdObject.ToString());
                return WebResults.From(Status._400_Bad_Request, "Transport id is invalid.  It must be an integer");
            }

            object timeoutObject = null;
            if (!fromClient.TryGetValue("lp", out timeoutObject))
                return WebResults.From(Status._400_Bad_Request, "Long poll (lp) missing.");

            double timeoutDouble = default(double);
            try
            {
                timeoutDouble = Convert.ToDouble(timeoutObject);
            }
            catch
            {
                log.Error("Invalid long poll: " + transportIdObject.ToString());
                return WebResults.From(Status._400_Bad_Request, "Long-poll is invalid.  It must be an number");
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
                webConnection.SendResults(WebResults.From(Status._200_OK));

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
                        webConnection.SendResults(WebResults.From(Status._200_OK));
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
        /// <param name="getArguments"></param>
        /// <returns></returns>
        public ICometTransport CreateNewCometTransport(ISession session, IDictionary<string, string> getArguments, long transportId)
        {
            CometSessionId id = new CometSessionId(session.SessionId, transportId);

            if (ActiveCometTransports.ContainsKey(id))
                throw new WebResultsOverrideException(WebResults.From(Status._409_Conflict));

            CometSessionTracker toReturn = new CometSessionTracker();
            toReturn.CometTransport = ConstructCometTransport(session, getArguments, transportId);

            using (TimedLock.Lock(ActiveCometTransports))
                ActiveCometTransports[id] = toReturn;
			
			session.RegisterCometTransport(toReturn.CometTransport);

            toReturn.LastUsed = DateTime.UtcNow;

            using (TimedLock.Lock(PurgeOldCometSessionsKey))
                if (null == PurgeOldCometSessionsTimer)
                    PurgeOldCometSessionsTimer = new Timer(
                        CleanOldTransports,
                        null,
                        TimeSpan.FromMilliseconds(0),
                        TimeSpan.FromSeconds(FileHandlerFactoryLocator.WebServer.CheckDeadConnectionsFrequencySeconds));

            return toReturn.CometTransport;
        }

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
        /// <param name="getArguments"></param>
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
                        throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "Permission denied"));

                    QueuingReliableCometTransport toReturn =
                        new QueuingReliableCometTransport(FileContainer.FullPath + "?ChannelEndpoint=" + channelEndpointName, session);

                    IChannelEventWebAdaptor channelEventWebAdaptor = (IChannelEventWebAdaptor)propertyAndPermission.Property.GetValue(this, null);
                    channelEventWebAdaptor.AddChannel(toReturn);

                    return toReturn;
                }
            }

            throw new WebResultsOverrideException(WebResults.From(Status._404_Not_Found));
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
                throw new WebResultsOverrideException(WebResults.From(Status._410_Gone));

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
        /// <param name="inheritPermission">Set to true if the related file should inherit READ permissions from the parent file.  That is, anyone who has at least READ permission to the parent file will be able to read the related file.  In order for this to work, the user must have administer permissions to the related file or an error will occur</param>
        /// <param name="chownRelatedFileTo">Identity of user to chown the related file to</param>
        /// <returns>JSON object with two properties:  confirmLinkPage, the endpoint to POST the user to in order to confirm that the user posted the link.  args:  The arguments that must be URLencoded in the post request to confirmLinkPage.  Note, you must add redirectUrl based on the implemented workflow.</returns>
        [WebCallable(
            WebCallingConvention.POST_application_x_www_form_urlencoded,
            WebReturnConvention.JSON,
            FilePermissionEnum.Administer)]
        public IWebResults AddRelatedFile(
			IWebConnection webConnection,
		    string filename,
		    string relationship,
		    bool? inheritPermission,
		    string chownRelatedFileTo)
        {
            if (null == FileContainer.ParentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.From(Status._406_Not_Acceptable, "The root directory can not have relationships"));

            // Get the full path if it's not present
            if (!filename.StartsWith("/"))
                filename = FileContainer.ParentDirectoryHandler.FileContainer.FullPath + "/" + filename;
			
			// The default relationship is "link"
			if (null == relationship)
				relationship = "link";

            IFileContainer relatedContainer;
            try
            {
                relatedContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
            }
            catch (FileDoesNotExist)
            {
                throw new WebResultsOverrideException(WebResults.From(Status._404_Not_Found, filename + " does not exist"));
            }

            bool inheritPermissionValue = false;
            if (null != inheritPermission)
                inheritPermissionValue = inheritPermission.Value;
			
            if (inheritPermissionValue || (null != chownRelatedFileTo))
                if (FilePermissionEnum.Administer > relatedContainer.LoadPermission(webConnection.Session.User.Id))
                    throw new WebResultsOverrideException(WebResults.From(
                        Status._401_Unauthorized,
                        "You must have administer permission to " + relatedContainer.FullPath + 
                        " in order for it to inherit permissions from the parent file or chown it"));
			
			if (null != chownRelatedFileTo)
			{
				IUserOrGroup newOwner = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(chownRelatedFileTo, true);
				
				if (newOwner is IUser)
					relatedContainer.ParentDirectoryHandler.Chown(webConnection.Session.User, relatedContainer.FileId, newOwner.Id);
			}

            LinkNotificationInformation linkNotificationInformation = FileContainer.ParentDirectoryHandler.AddRelationship(
                FileContainer, relatedContainer, relationship, inheritPermissionValue);
			
			// Figure out who should get notifications
			// If permission isn't inherited, then only send notifications to people who would get notifications for the
			// linked object and this object
			HashSet<string> notificationRecipientIdentities = new HashSet<string>(
				FileContainer.GetNotificationRecipientIdentities());
			
			if (!inheritPermissionValue)
			{
				IEnumerable<string> relatedRecipients = relatedContainer.GetNotificationRecipientIdentities();
				notificationRecipientIdentities.IntersectWith(relatedRecipients);
			}

			// TODO: Need a better approach to handle when the linked file doesn't have an owner
			// For now, just sending as root
			IUser relatedOwner;
			if (null != relatedContainer.Owner)
				relatedOwner = relatedContainer.Owner;
			else
				relatedOwner = FileHandlerFactoryLocator.UserFactory.RootUser;

			Dictionary<string, object> clpArgs = new Dictionary<string, object>();
            clpArgs["objectUrl"] = FileContainer.ObjectUrl;
            clpArgs["ownerIdentity"] = relatedOwner.Identity;
            clpArgs["linkSummaryView"] = linkNotificationInformation.linkSummaryView;
            clpArgs["linkUrl"] = relatedContainer.ObjectUrl;
            clpArgs["linkDocumentType"] = relatedContainer.DocumentType;
            clpArgs["recipients"] = new List<object>(Enumerable<object>.Cast(notificationRecipientIdentities)).ToArray();
            clpArgs["linkID"] = linkNotificationInformation.linkID;

            // This is done because this function is called from server-side Javascript
            // Server-side Javascript only supports syncronous calls
            object key = new object();
            IWebResults toReturn = null;

            lock (key)
            {
                FileHandlerFactoryLocator.UserManagerHandler.GetEndpoints(
                    relatedOwner.Identity,
                    delegate(IEndpoints endpoints)
                    {
						if (null != endpoints)
						{
        	                Dictionary<string, object> linkResults = new Dictionary<string, object>();
            	            linkResults["confirmLinkPage"] = endpoints[ParticleEndpoint.ConfirmLinkPage];
                	        linkResults["args"] = clpArgs;

                    	    toReturn = WebResults.ToJson(linkResults);
						}
						else
							toReturn = WebResults.ToJson(null);

                        lock (key)
                            Monitor.Pulse(key);
                    },
                    delegate(Exception e)
                    {
                        toReturn = WebResults.From(
                            Status._412_Precondition_Failed, "Could not find particle.confirmLinkPage endpoint");

                        lock (key)
                            Monitor.Pulse(key);
                    });

                if (null == toReturn)
                    Monitor.Wait(key);
            }

            return toReturn;
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
                throw new WebResultsOverrideException(WebResults.From(Status._406_Not_Acceptable, "The root directory can not have relationships"));

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
                throw new WebResultsOverrideException(WebResults.From(Status._404_Not_Found, filename + " does not exist"));
            }

            FileContainer.ParentDirectoryHandler.DeleteRelationship(
                FileContainer, relatedContainer, relationship);

            return WebResults.From(Status._202_Accepted);
        }

        /// <summary>
        /// Returns information about each file that is related to this file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="relationships">A JSON array of potential relationships, or null to match all relationships</param>
        /// <param name="extensions">A JSON array of potential extentions, or null to match all extensions</param>
        /// <param name="newest"></param>
        /// <param name="oldest"></param>
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
                        WebResults.From(Status._406_Not_Acceptable, relationships + " is invalid, must be either a JSON string or JSON array"));
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
                        WebResults.From(Status._406_Not_Acceptable, extensions + " is invalid, must be either a JSON string or JSON array"));
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
            toReturn["FileId"] = file.FileId.ToString();
            toReturn["TypeId"] = file.TypeId;
            toReturn["LastModified"] = file.LastModified;
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
        /// <param name="newOwner">The new owner's name</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults Chown(IWebConnection webConnection, Guid? newOwnerId, string newOwner)
        {
            if (null == FileContainer.ParentDirectoryHandler)
                throw new WebResultsOverrideException(WebResults.From(Status._406_Not_Acceptable, "The root directory can not have ownership"));

            ID<IUserOrGroup, Guid>? ownerId = null;
            IUser newOwnerUser = null;

            if (null != newOwnerId)
            {
                ownerId = new ID<IUserOrGroup, Guid>(newOwnerId.Value);
                newOwnerUser = FileHandlerFactoryLocator.UserManagerHandler.GetUser(ownerId.Value);
            }
            else if (null != newOwner)
                if (newOwner.Length > 0)
                {
                    newOwnerUser = FileHandlerFactoryLocator.UserManagerHandler.GetUser(newOwner);
                    ownerId = newOwnerUser.Id;
                }

            FileContainer.ParentDirectoryHandler.Chown(
                webConnection.Session.User, FileContainer.FileId, ownerId);

            if (null == newOwnerUser)
                return WebResults.From(Status._202_Accepted, "Owner removed");
            else
                return WebResults.From(Status._202_Accepted, "Owner changed to " + newOwnerUser.Name);
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
        /// <param name="args"></param>
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
        /// Rebuilds the Javascript execution environment
        /// </summary>
        public void ResetExecutionEnvironment()
		{
            using (TimedLock.Lock(ExecutionEnvironmentLock))
            {
                _ExecutionEnvironment = null;
                CachedInBrowserJSWrapper = null;
            }
		}

        /// <summary>
        /// Prevents recurion when calling GetOrCreateExecutionEnvironment(), as this will almost always result in a stack overflow exception
        /// </summary>
        bool CreatingExecutionEnvironment = false;
		
		/// <summary>
        /// Where Javascript is executed
        /// </summary>
        public IExecutionEnvironment GetOrCreateExecutionEnvironment()
        {
            IExecutionEnvironment executionEnvironment = _ExecutionEnvironment;
            if (null != executionEnvironment)
                return executionEnvironment;

            string javascriptFile = FileContainer.FileConfigurationManager.JavascriptFile;
            if (null == javascriptFile)
                return null;

            // Try to find the javascript file
            IFileContainer javascriptContainer =
                FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(javascriptFile);

            // Note: a large timeout is used in case a thread is constructing the scope.  Constructing the scope can be time consuming
            // TODO:  Try to do a lot of checking without a lock, or using a read/write lock
            using (TimedLock.Lock(ExecutionEnvironmentLock, TimeSpan.FromSeconds(15)))
            {
                if (null != _ExecutionEnvironment)
                    return _ExecutionEnvironment;

                if (CreatingExecutionEnvironment)
                {
                    log.Error("An attempt was made to call GetOrCreateExecutionEnvironment() while it's being created.  " +
                        "This usually occurs when server-side javascript, while creating a scope calls a function that depends on " +
                        "GetOrCreateExecutionEnvironment() being complete.  As GetOrCreateExecutionEnvrionment() isn't complete, it " +
                        "would attempt to create a new one which will result in a stack overflow and server crash.  " +
                        "To resolve this problem, do not use operations while creating a Javascript scope that depend on the completed " +
                        "scope, such as calling open() against yourself.  " + FileContainer.ObjectUrl);
                    throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error));
                }

                CreatingExecutionEnvironment = true;

                try
                {
                    IExecutionEnvironmentFactory factory = FileHandlerFactoryLocator.ExecutionEnvironmentFactory;
                    _ExecutionEnvironment = factory.Create(FileContainer, javascriptContainer);
                    return _ExecutionEnvironment;
                }
                finally
                {
                    CreatingExecutionEnvironment = false;
                }
            }
        }

        /// <summary>
        /// Creates an execution environment if no other thread is creating one
        /// </summary>
        public void CreateExecutionEnvironmentIfNoOtherThreadCreating()
        {
            if (null != _ExecutionEnvironment)
                if (Monitor.TryEnter(ExecutionEnvironmentLock))
                    try
                    {
                        GetOrCreateExecutionEnvironment();
                    }
                    finally
                    {
                        Monitor.Exit(ExecutionEnvironmentLock);
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

		/// <summary>
		/// Returns the named permissions that apply to this file 
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON)]
		public IWebResults GetAssignableNamedPermissions(IWebConnection webConnection)
		{
            return WebResults.ToJson(FileContainer.FileConfigurationManager.ViewComponents);
		}

        /// <summary>
        /// The unix epoch
        /// </summary>
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Verifies that the incoming call has a proper securityTimestamp
        /// </summary>
        /// <param name="webConnection"></param>
        public static void VerifySecurityTimestamp(IWebConnection webConnection)
        {
            string timestampString;

            if (webConnection.PostParameters.TryGetValue("securityTimestamp", out timestampString))
            {
                double timestampDays;
                if (double.TryParse(timestampString, out timestampDays))
                {
                    DateTime securityTimestamp = UnixEpoch + TimeSpan.FromDays(timestampDays);

                    if (securityTimestamp >= DateTime.UtcNow.AddMinutes(-5))
                        if (securityTimestamp <= DateTime.UtcNow.AddMinutes(5))
                            return;
                }
            }

            throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "securityTimestamp"));
        }

        /// <summary>
        /// Returns a security timestamp
        /// </summary>
        /// <returns></returns>
        public static KeyValuePair<string, string> GenerateSecurityTimestamp()
        {
            string securityTimestamp = (DateTime.UtcNow - UnixEpoch).TotalDays.ToString("R");
            return new KeyValuePair<string, string>("securityTimestamp", securityTimestamp);
        }
    }

    /// <summary>
    /// WebHandler for files that don't have any web access
    /// </summary>
    public class WebHandler : WebHandler<IFileHandler> { }
}
