// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

using Common.Logging;
using ExtremeSwank.OpenId;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Web wrapper for the user database
    /// </summary>
    public partial class UserManagerWebHandler : DatabaseWebHandler<IUserManagerHandler, UserManagerWebHandler>
    {
		private static ILog log = LogManager.GetLogger(typeof(UserManagerWebHandler));
		
        /// <summary>
        /// Logs in
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status)]
        public IWebResults Login(IWebConnection webConnection, string username, string password)
        {
            try
            {
                IUser user = FileHandler.GetUser(username, password);
                webConnection.Session.Login(user);

                // success
                return WebResults.From(Status._202_Accepted, user.Name + " logged in");
            }
            catch (WrongPasswordException)
            {
                return WebResults.From(Status._401_Unauthorized, "Bad Password");
            }
            catch (UnknownUser)
            {
                return WebResults.From(Status._404_Not_Found, "Unknown user");
            }
        }

        /// <summary>
        /// Logs out
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status)]
        public IWebResults Logout(IWebConnection webConnection)
        {
            FileHandlerFactoryLocator.SessionManagerHandler.EndSession(webConnection.Session.SessionId);
            webConnection.Session = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();

            return WebResults.From(Status._202_Accepted, "logged out");
        }

        /// <summary>
        /// Creates the user
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Write)]
        [NamedPermission("CreateUser")]
        public IWebResults CreateUser(IWebConnection webConnection, string username, string password)
        {
			bool assignSession = false;
			if (webConnection.PostParameters.ContainsKey("assignSession"))
				bool.TryParse(webConnection.PostParameters["assignSession"], out assignSession);

            try
            {
                IUser user = FileHandler.CreateUser(username, password);
				
				if (assignSession)
                	webConnection.Session.Login(user);

                return WebResults.From(Status._201_Created, user.Name + " created");
            }
            catch (UserAlreadyExistsException)
            {
                return WebResults.From(Status._409_Conflict, "Duplicate user");
            }
        }

        /// <summary>
        /// Creates the group
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupname"></param>
        /// <param name="username">The group's owner, or null if the current user is the owner</param>
        /// <param name="grouptype"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults CreateGroup(IWebConnection webConnection, string groupname, string username, string grouptype)
        {
            // TODO:  If it's a personal group, uniqueify the name.  Personal groups shouldn't have global names

            if (!webConnection.Session.User.Local)
                throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "Sorry, only local users can create groups"));

            if (webConnection.Session.User.Id == FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id)
                throw new WebResultsOverrideException(WebResults.From(Status._403_Forbidden, "You must be logged in to create a group"));

            GroupType groupType;
            if (!Enum<GroupType>.TryParse(grouptype, out groupType))
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, grouptype + " is not a valid group type"));

            // Write permission is needed to add non-personal groups
            if (groupType > GroupType.Personal)
                if ((FileContainer.LoadPermission(webConnection.Session.User.Id) < FilePermissionEnum.Write)
                    && (!FileContainer.HasNamedPermissions(webConnection.Session.User.Id, "CreateGroup")))
                {
                    throw new WebResultsOverrideException(WebResults.From(
                        Status._401_Unauthorized, "You must have write or \"CreateGroup\" permission to /Users/UserDB create non-personal groups"));
                }

            try
            {
                IUser user = webConnection.Session.User;
				
				// Allow users with administrative privilages to create groups owned by other people
				if (null != username)
                    if (username.Length > 0)
                    {
                        if (FilePermissionEnum.Administer == FileHandler.FileContainer.LoadPermission(
                            webConnection.Session.User.Id))
                            user = FileHandler.GetUser(username);
                        else
                            return WebResults.From(Status._401_Unauthorized, "You do not have permission to create groups owned by other people");
                    }
				
				IGroup group = FileHandler.CreateGroup(groupname, user.Id, groupType);

                return WebResults.From(Status._201_Created, group.Name + " created");
            }
            catch (UserAlreadyExistsException)
            {
                return WebResults.From(Status._409_Conflict, "Duplicate group");
            }
        }
		
        /// <summary>
        /// Deletes the group
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupname"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults DeleteGroup(IWebConnection webConnection, string groupname, Guid? groupId)
        {
            try
            {
                IUserOrGroup groupObj;

                try
                {
                    if (null != groupId)
                    {
                        ID<IUserOrGroup, Guid> groupIdTyped = new ID<IUserOrGroup, Guid>(groupId.Value);
                        groupObj = FileHandler.GetUserOrGroup(groupIdTyped);
                    }
                    else
                        groupObj = FileHandler.GetUserOrGroupOrOpenId(groupname);
                }
                catch (UnknownUser)
                {
                    throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "Group does not exist"));
                }

				if (!(groupObj is IGroup))
					throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "Specified object is a user"));
				
				IGroup group = (IGroup)groupObj;

                // Make sure the user has permission to delete the group
                FilePermissionEnum? permissionToGroupEditor = FileContainer.LoadPermission(webConnection.Session.User.Id);
                if (null == group.OwnerId)
                    if (FilePermissionEnum.Administer != permissionToGroupEditor)
                        throw new WebResultsOverrideException(WebResults.From(Status._403_Forbidden, "You do not have permission to delete groups"));
                else if (group.OwnerId.Value != webConnection.Session.User.Id)
                    if (FilePermissionEnum.Administer != permissionToGroupEditor)
                        throw new WebResultsOverrideException(WebResults.From(Status._403_Forbidden, "You do not have permission to delete groups"));

				
				// Determine if the user has permission to delete this group
				// The user must either own the group, or have administrative privilages in order to delete a group
				bool userHasPermission = false;
				
				if (group.OwnerId == webConnection.Session.User.Id)
					userHasPermission = true;
				else if (FilePermissionEnum.Administer == FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id))
					userHasPermission = true;
				
				if (!userHasPermission)
					return WebResults.From(Status._401_Unauthorized, "You do not have permission to delete this group");
				
				FileHandler.DeleteGroup(group.Name);
				
				return WebResults.From(Status._200_OK, group.Name + " deleted");
	        }
            catch (UnknownUser)
            {
				return WebResults.From(Status._400_Bad_Request, groupname + " does not exist");
            }
        }

        /// <summary>
        /// Returns the group. Either groupname OR groupid must be specified
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupname"></param>
        /// <param name="groupid"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON)]
        public IWebResults GetGroup(IWebConnection webConnection, string groupname, string groupid)
        {
            IGroup group = GetGroupInt(webConnection, groupname, groupid);

            // Determine if the user has permission to delete this group
            // The user must either own the group, or have administrative privilages in order to delete a group
            bool userHasPermission = false;

            if (group.OwnerId == webConnection.Session.User.Id)
                userHasPermission = true;
            else if (FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id) >= FilePermissionEnum.Read)
                userHasPermission = true;

            if (!userHasPermission)
                return WebResults.From(Status._401_Unauthorized, "You do not have permission to view this group");

            return WebResults.ToJson(CreateJSONDictionary(group));
        }

        /// <summary>
        /// Returns the user's alias for the group
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupid"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON)]
        public IWebResults GetGroupAndAlias(IWebConnection webConnection, Guid groupid)
        {
            IGroupAndAlias groupAndAlias = FileHandler.GetGroupAndAlias(
                webConnection.Session.User.Id,
                new ID<IUserOrGroup, Guid>(groupid));

            // Determine if the user has permission to delete this group
            // The user must either own the group, or have administrative privilages in order to delete a group
            bool userHasPermission = false;

            if (groupAndAlias.OwnerId == webConnection.Session.User.Id)
                userHasPermission = true;
            else if (FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id) >= FilePermissionEnum.Read)
                userHasPermission = true;

            if (!userHasPermission)
                return WebResults.From(Status._401_Unauthorized, "You do not have permission to view this group");

            return WebResults.ToJson(CreateJSONDictionary(groupAndAlias));
        }

		/// <summary>
		/// Gets the group specified in the web connection 
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IGroup"/>
		/// </returns>
        /// <param name="groupid"></param>
        /// <param name="groupname"></param>
		private IGroup GetGroupInt(IWebConnection webConnection, string groupname, string groupid)
		{
            if (null != groupname)
                return FileHandler.GetGroup(groupname);
            else if (null != groupid)
            {
                ID<IUserOrGroup, Guid> groupId = new ID<IUserOrGroup, Guid>(new Guid(groupid));
                return FileHandler.GetGroup(groupId);
            }
            else
                throw new WebResultsOverrideException(
                    WebResults.From(Status._400_Bad_Request, "groupname or groupid must be provided"));
		}
		
		/// <summary>
		/// Gets the user specified in the web connection 
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IUser"/>
		/// </returns>
        /// <param name="userid"></param>
        /// <param name="username"></param>
		private IUser GetUser(IWebConnection webConnection, string username, string userid)
		{
			if (null != username)
				return FileHandler.GetUser(username);
			else if (null != userid)
			{
				ID<IUserOrGroup, Guid> userId = new ID<IUserOrGroup, Guid>(new Guid(userid));
				return FileHandler.GetUser(userId);
			}
            else
                throw new WebResultsOverrideException(
                    WebResults.From(Status._400_Bad_Request, "username or userid missing"));
        }

        /// <summary>
        /// Returns the user name of the currently-logged in user.  This is preferable to querying the user's object due
        /// to OpenID issues
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults GetUsername(IWebConnection webConnection)
        {
            return WebResults.From(Status._200_OK, webConnection.Session.User.Name);
        }

        /// <summary>
        /// Returns a JSON object with information about the current user
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults GetCurrentUser(IWebConnection webConnection)
        {
            return WebResults.ToJson(this.CreateJSONDictionary(webConnection.Session.User));
        }

        /// <summary>
        /// Returns the user open id of the currently-logged in user.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults GetIdentity(IWebConnection webConnection)
        {
            return WebResults.From(Status._200_OK, webConnection.Session.User.Identity);
        }

        /// <summary>
        /// Adds the user to the group.  Either groupname or groupid must be specified, AND, username or userid must be specified
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupname"></param>
        /// <param name="groupid"></param>
        /// <param name="username"></param>
        /// <param name="userid"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults AddUserToGroup(IWebConnection webConnection, string groupname, string groupid, string username, string userid)
        {
            IGroup group = GetGroupInt(webConnection, groupname, groupid);

            IUser user;

            try
            {
                user = GetUser(webConnection, username, userid);
            }
            catch (UnknownUser)
            {
                if (null != username)
                    throw new WebResultsOverrideException(WebResults.From(Status._404_Not_Found, username + " doesn't exist"));
                else
                    throw new WebResultsOverrideException(WebResults.From(Status._404_Not_Found, "user doesn't exist"));
            }
			
			// Determine if the user has permission to administer this group
			// The user must either own the group, or have administrative privilages in order to delete a group
			bool userHasPermission = false;
			
			if (group.OwnerId == webConnection.Session.User.Id)
				userHasPermission = true;
			else if (FilePermissionEnum.Administer == FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id))
				userHasPermission = true;
			
			if (!userHasPermission)
				return WebResults.From(Status._401_Unauthorized, "You do not have permission to add users to this group");

			FileHandler.AddUserToGroup(user.Id, group.Id);
			
			return WebResults.From(Status._200_OK, user.Name + " added to " + group.Name);
		}

        /// <summary>
        /// Removes the user from the group.  Either groupname or groupid must be specified, OR, username or userid must be specified
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupname"></param>
        /// <param name="groupid"></param>
        /// <param name="username"></param>
        /// <param name="userid"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults RemoveUserFromGroup(IWebConnection webConnection, string groupname, string groupid, string username, string userid)
        {
            IGroup group = GetGroupInt(webConnection, groupname, groupid);
			IUser user = GetUser(webConnection, username, userid);
			
			// Determine if the user has permission to administer this group
			// The user must either own the group, or have administrative privilages in order to delete a group
			bool userHasPermission = false;
			
			if (group.OwnerId == webConnection.Session.User.Id)
				userHasPermission = true;
			else if (FilePermissionEnum.Administer == FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id))
				userHasPermission = true;
			
			if (!userHasPermission)
				return WebResults.From(Status._401_Unauthorized, "You do not have permission to add users to this group");

			FileHandler.RemoveUserFromGroup(user.Id, group.Id);
			
			return WebResults.From(Status._200_OK, user.Name + " removed from " + group.Name);
		}
		
        /// <summary>
        /// Returns all of the groups that the user is in.  The caller must either be the user queried, be an administrator, or have administrative permission for the user
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="username"></param>
        /// <param name="userid"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON)]
        public IWebResults GetUsersGroups(IWebConnection webConnection, string username, string userid)
		{
			IUser user = GetUser(webConnection, username, userid);
				
			// Determine if the user has permission to view all of the groups that the requested user is a member of
			// The user must either be the user in question, or have administrative privilages in order to delete a group
			bool userHasPermission = false;
			
			if (user.Id == webConnection.Session.User.Id)
				userHasPermission = true;
			else if (FilePermissionEnum.Administer == FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id))
				userHasPermission = true;
			
			if (!userHasPermission)
				return WebResults.From(Status._401_Unauthorized, "You do not have permission to view the groups that this user is a member of");
			
			IEnumerable<IGroupAndAlias> groups = FileHandler.GetGroupsThatUserIsIn(user.Id);
			return ReturnAsJSON(groups);
		}

        /// <summary>
        /// Sets the user's alias for the group
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupId"></param>
        /// <param name="alias"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults SetGroupAlias(IWebConnection webConnection, Guid groupId, string alias)
        {
            if (webConnection.Session.User == FileHandlerFactoryLocator.UserFactory.AnonymousUser)
                throw new WebResultsOverrideException(WebResults.From(Status._403_Forbidden, "You must be logged in to set an alias"));

            if (0 == alias.Length)
                alias = null;

            try
            {
                FileHandler.SetGroupAlias(webConnection.Session.User.Id, new ID<IUserOrGroup, Guid>(groupId), alias);
            }
            catch (SecurityException)
            {
                throw new WebResultsOverrideException(WebResults.From(Status._403_Forbidden, "Permission Denied"));
            }

            return WebResults.From(Status._202_Accepted);
        }

        /// <summary>
        /// Returns all of the users in the group.  Either groupname or groupid must be specified
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupname"></param>
        /// <param name="groupid"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults GetUsersInGroup(IWebConnection webConnection, string groupname, string groupid)
		{
			IGroup group = GetGroupInt(webConnection, groupname, groupid);
				
			// Determine if the user has permission to administer this group
			// The user must either own the group, or have read privilages in order to delete a group
			bool userHasPermission = false;
			
			if (group.OwnerId == webConnection.Session.User.Id)
				userHasPermission = true;
			else if (FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id) >= FilePermissionEnum.Read)
				userHasPermission = true;
			
			if (!userHasPermission)
				return WebResults.From(Status._401_Unauthorized, "You do not have permission to view the users in a group");
			
			IEnumerable<IUser> users = FileHandler.GetUsersInGroup(group.Id);
			return ReturnAsJSON(users);
		}

        private IDictionary<string, object> CreateJSONDictionary(IUserOrGroup userOrGroup)
        {
            IDictionary<string, object> toReturn = new Dictionary<string, object>();

            toReturn["Name"] = userOrGroup.Name;
            toReturn["Id"] = userOrGroup.Id.Value;
            toReturn["BuiltIn"] = userOrGroup.BuiltIn;

            return toReturn;
        }

        private IDictionary<string, object> CreateJSONDictionary(IUser user)
        {
            IDictionary<string, object> toReturn = CreateJSONDictionary(user as IUserOrGroup);

            toReturn["Identity"] = user.Identity;
            toReturn["UserOrGroup"] = "User";

            return toReturn;
        }

        private IDictionary<string, object> CreateJSONDictionary(IGroupAndAlias group)
        {
            IDictionary<string, object> toReturn = CreateJSONDictionary(group as IGroup);

            toReturn["Alias"] = group.Alias;
            toReturn["NameOrAlias"] = group.Alias != null ? group.Alias : group.Name;

            return toReturn;
        }

        private IDictionary<string, object> CreateJSONDictionary(IGroup group)
        {
            IDictionary<string, object> toReturn = CreateJSONDictionary(group as IUserOrGroup);

            toReturn["OwnerId"] = null != group.OwnerId ? (object)group.OwnerId.Value : (object)null;
            toReturn["Owner"] = null != group.OwnerId ? FileHandler.GetUser(group.OwnerId.Value).Name : (object)null;
            toReturn["OwnerIdentity"] = null != group.OwnerId ? FileHandler.GetUser(group.OwnerId.Value).Identity : (object)null;
            toReturn["Automatic"] = group.Automatic;
            toReturn["Type"] = group.Type.ToString();
            toReturn["UserOrGroup"] = "Group";

            return toReturn;
        }

        private IWebResults ReturnAsJSON(IEnumerable<IGroupAndAlias> groups)
        {
            ArrayList groupsAL = new ArrayList();
            foreach (IGroupAndAlias group in groups)
            {
                IDictionary<string, object> groupDictionary = CreateJSONDictionary(group);
                groupsAL.Add(groupDictionary);
            }

            return WebResults.ToJson(groupsAL.ToArray());
        }

		private IWebResults ReturnAsJSON(IEnumerable<IUser> users)
		{
			ArrayList usersAL = new ArrayList();
			foreach (IUser user in users)
			{
				IDictionary<string, object> userDictionary = CreateJSONDictionary(user);
				usersAL.Add(userDictionary);
			}
			
			return WebResults.ToJson(usersAL.ToArray());
		}

        /// <summary>
        /// Gets all of the groups that the current user can administer
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON)]
        public IWebResults GetGroupsThatCanBeAdministered(IWebConnection webConnection)
        {
            IEnumerable<IGroupAndAlias> groupAndAliases;

            if (FilePermissionEnum.Administer == FileHandler.FileContainer.LoadPermission(webConnection.Session.User.Id))
                groupAndAliases = FileHandler.GetAllGroups(webConnection.Session.User.Id);

            else
                groupAndAliases = FileHandler.GetGroupsThatUserOwns(webConnection.Session.User.Id);

            return ReturnAsJSON(groupAndAliases);
        }

        /// <summary>
        /// Returns all public groups.  On systems with large amounts of groups; this should be somehow disabled, or 
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="max"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON)]
        public IWebResults SearchUsersAndGroups(IWebConnection webConnection, string query, uint? max)
        {
            List<object> toReturn = new List<object>();

            // Prevent too large maxes unless the user has admin privileges
            bool fixMax = false;
            if (null == max)
                fixMax = true;
            else if (max.Value < 50)
                fixMax = true;

            if (fixMax)
                if (FilePermissionEnum.Administer != FileContainer.LoadPermission(webConnection.Session.User.Id))
                    max = 50;

            foreach (IUserOrGroup userOrGroup in FileHandler.SearchUsersAndGroups(query, max))
            {
                if (userOrGroup is IUser)
                    toReturn.Add(CreateJSONDictionary(userOrGroup as IUser));

                else if (userOrGroup is IGroupAndAlias)
                    toReturn.Add(CreateJSONDictionary(userOrGroup as IGroupAndAlias));

                else if (userOrGroup is IGroup)
                    toReturn.Add(CreateJSONDictionary(userOrGroup as IGroup));

                else // if userOrGroup is something else, make a best effort and keep going
                    toReturn.Add(userOrGroup);
            }

            return WebResults.ToJson(toReturn);
        }

        /// <summary>
        /// Gets all of the groups that the current user is in
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JSON)]
        public IWebResults GetGroupsThatUserIsIn(IWebConnection webConnection)
        {
            IEnumerable<IGroupAndAlias> groupAndAliases = FileHandler.GetGroupsThatUserIsIn(webConnection.Session.User.Id);
            return ReturnAsJSON(groupAndAliases);
        }

        /// <summary>
        /// Returns true if the user is in the specified group, false otherwise
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupname">The group name to match.</param>
        /// <param name="groupid">The group ID.  Either this or groupname must be set</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON)]
        	public IWebResults IsUserInGroup(IWebConnection webConnection, string groupname, string groupid)
		{
			IGroup group = GetGroupInt(webConnection, groupname, groupid);
			return WebResults.ToJson(FileHandler.IsUserInGroup(webConnection.Session.User.Id, group.Id));
        }

        /// <summary>
        /// Returns true if the user is in the specified groups or the username is specified
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="groupAndUserNames">The group and user names.  Returns true if the user is a member of any of these groups, or if the user name is specified</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON)]
        	public IWebResults IsUserInGroupsOrMatch(IWebConnection webConnection, string[] groupAndUserNames)
		{
			Set<string> namesSet = new Set<string>(groupAndUserNames);
			
			if (namesSet.Contains(webConnection.Session.User.Name))
				return WebResults.ToJson(true);
			
			foreach (IGroup group in FileHandler.GetGroupsThatUserIsIn(webConnection.Session.User.Id))
				if (namesSet.Contains(group.Name))
					return WebResults.ToJson(true);
			
			return WebResults.ToJson(false);
        }
		
        /// <summary>
        /// Starts the process of logging into this server using an OpenId.  The result is that the user will be rediected to a new page
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Naked)]
		public IWebResults OpenIDLogin(IWebConnection webConnection)
		{
			string openIdIdentity = webConnection.PostArgumentOrException("openid_url");
			
			NameValueCollection openIdClientArgs = new NameValueCollection();
			
			OpenIdClient openIdClient = new OpenIdClient(openIdClientArgs);
			openIdClient.Identity = openIdIdentity;
			openIdClient.TrustRoot = null;
			
			// TODO:  Don't hardcode path when this object is able to know its own path
            string returnUrl = "http://" + FileHandlerFactoryLocator.HostnameAndPort + "/Users/UserDB?Method=CompleteOpenIdLogin";

            if (webConnection.PostParameters.ContainsKey("redirect"))
                returnUrl = HTTPStringFunctions.AppendGetParameter(returnUrl, "redirect", webConnection.PostParameters["redirect"]);

            openIdClient.ReturnUrl = new Uri(returnUrl);

			Uri requestUri = openIdClient.CreateRequest(false, false);
			
			if (openIdClient.ErrorState == ErrorCondition.NoErrors)
				return WebResults.Redirect(requestUri);
			else
			{
				return WebResults.From(
					Status._417_Expectation_Failed,
				    "Error when logging in with OpenID: \"" + openIdIdentity + ":\" " + openIdClient.ErrorState.ToString());
			}
		}
		
        /// <summary>
        /// Completes the process of a user logging into this web server with openId
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
		[WebCallable(WebCallingConvention.Naked, WebReturnConvention.Naked)]
		public IWebResults CompleteOpenIdLogin(IWebConnection webConnection)
		{
			NameValueCollection openIdClientArgs = DictionaryFunctions.ToNameValueCollection(webConnection.GetParameters);
			OpenIdClient openIdClient = new OpenIdClient(openIdClientArgs);
			
			OpenIdUser openIdUser = openIdClient.RetrieveUser();
			
			if (null == openIdUser)
				throw new WebResultsOverrideException(
					WebResults.From(Status._417_Expectation_Failed, "Could not get an OpenIdUser"));
			
			bool validResponse = openIdClient.ValidateResponse();
			if (!validResponse)
				throw new WebResultsOverrideException(
					WebResults.From(Status._401_Unauthorized, "Invalid response"));
			
			string identity = webConnection.EitherArgumentOrException("openid.identity");

			try
			{
				IUser user = FileHandler.GetOpenIdUser(identity);
				webConnection.Session.Login(user);

                // success
                if (webConnection.GetParameters.ContainsKey("redirect"))
                    return WebResults.Redirect(webConnection.GetParameters["redirect"]);
                else
                    return WebResults.From(Status._202_Accepted, user.Name + " logged in");
            }
            catch (WrongPasswordException)
            {
                return WebResults.From(Status._401_Unauthorized, "Bad Password");
            }
            catch (UnknownUser)
            {
                return WebResults.From(Status._404_Not_Found, "Unknown user");
            }
		}

        /// <summary>
        /// Accepts the user's password when logging into another site with OpenID
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.Naked, WebReturnConvention.Naked)]
		public IWebResults ProvideOpenID(IWebConnection webConnection)
		{
			string requestedIdentity = webConnection.PostArgumentOrException("openid.identity");
			
			// Make sure the user isn't trying to authenticate against the anonymous user
			if (FileHandlerFactoryLocator.UserFactory.AnonymousUser.Identity == requestedIdentity)
		        return WebResults.From(Status._403_Forbidden, "I'm not that stupid, you can't use the anonymous user as an OpenID identity.");
			
			IUser user;
			
			// if the user is trying to authenticate as a different user then what the current user is; (or the user
			// isn't logged on,) then verify the password
			if (webConnection.Session.User.Identity != requestedIdentity)
			{
                string name = GetLocalUserNameFromOpenID(requestedIdentity);
				
				if (log.IsInfoEnabled)
					log.Info("Provided an OpenID identity for " + name);
				
				// Get the password
				string password = webConnection.PostArgumentOrException("password");
				
				// Load the user and verify the password
				try
				{
					user = FileHandler.GetUser(name, password);
	            }
	            catch (WrongPasswordException)
	            {
	                return WebResults.From(Status._401_Unauthorized, "Bad Password");
	            }
	            catch (UnknownUser)
	            {
	                return WebResults.From(Status._404_Not_Found, "Unknown user");
	            }
			}
			else
				user = webConnection.Session.User;
			
			IUserHandler userHandler = user.UserHandler;

			// Extract out originating GET parameters that need to be passed through to the destination web site
			Dictionary<string, string> getParametersToPass = new Dictionary<string, string>();
			foreach (KeyValuePair<string, string> getParameter in webConnection.PostParameters)
				if (!getParameter.Key.StartsWith("openid."))
					if ("password" != getParameter.Key)
						getParametersToPass.Add(getParameter.Key, getParameter.Value);
			
			// Delegate specific logic to the correct method based on openid.mode
			string openIdMode = webConnection.PostArgumentOrException("openid.mode");
			switch (openIdMode)
			{
				case("checkid_setup"):
				{
					return CheckID_Setup(webConnection, user.Id, userHandler, getParametersToPass);
				}
				default:
				{
			        return WebResults.From(Status._501_Not_Implemented, "openid.mode " + openIdMode + " Not Implemented");
				}
			}
		}

        private string GetLocalUserNameFromOpenID(string requestedIdentity)
        {
            // First, the user name needs to be derrived from the open ID
            string openIdPrefix = string.Format("http://{0}/Users/", FileHandlerFactoryLocator.HostnameAndPort);

            // Make sure the identiy is in a valid form
            if (!(requestedIdentity.StartsWith(openIdPrefix)) && requestedIdentity.EndsWith(".user"))
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, requestedIdentity + "is not a valid identity"));

            string nameDotUser = requestedIdentity.Substring(openIdPrefix.Length);
            string name = nameDotUser.Substring(0, nameDotUser.LastIndexOf('.'));
            return name;
        }
		
		private IWebResults CheckID_Setup(IWebConnection webConnection, ID<IUserOrGroup, Guid> userId, IUserHandler userHandler, IDictionary<string, string> getParametersToPass)
		{
			StringBuilder additionalGetParameters = new StringBuilder();
			foreach (KeyValuePair<string, string> getParameter in getParametersToPass)
				additionalGetParameters.AppendFormat(
					"{0}={1}&",
				    HTTPStringFunctions.EncodeRequestParametersForBrowser(getParameter.Key),
				    HTTPStringFunctions.EncodeRequestParametersForBrowser(getParameter.Value));
			
			additionalGetParameters.AppendFormat(
				"openid.signed=&openid.identity={0}&openid.return_to={1}&openid.assoc_handle={2}",
			  	HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.PostArgumentOrException("openid.identity")),
			  	HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.PostArgumentOrException("openid.return_to")),
			  	HTTPStringFunctions.EncodeRequestParametersForBrowser(FileHandler.CreateAssociationHandle(userId)));

			string redirectURL = webConnection.PostArgumentOrException("openid.return_to");
			string toReturnTo;
			if (redirectURL.Contains("?"))
				toReturnTo = string.Format("{0}&{1}", redirectURL, additionalGetParameters.ToString());
			else
				toReturnTo = string.Format("{0}?{1}", redirectURL, additionalGetParameters.ToString());
			
			return WebResults.Redirect(toReturnTo);
			
			/*    4.3.2.2. Sent on Positive Assertion

    * openid.identity

          Value: Verified Identifier

    * openid.assoc_handle

          Value: Opaque association handle being used to fine the HMAC key for the signature.

    * openid.return_to

          Value: Verbatim copy of the return_to URL parameter sent in the request, before the Provider modified it.

    * openid.signed

          Value: Comma-seperated list of signed fields.

          Note: Fields without the "openid." prefix that the signature covers. For example, "mode,identity,return_to".

    * openid.sig

          Value: base64(HMAC(secret(assoc_handle), token_contents)

          Note: Where token_contents is a key-value format string of all the signed keys and values in this response. They MUST be in the same order as listed in the openid.signed field. Consumer SHALL recreate the token_contents string prior to checking the signature. See Appendix D (Limits).

    * openid.invalidate_handle

          Value: Optional; The association handle sent in the request if the Provider did not accept or recognize it.


 TOC 
4.3.3. Extra Notes

    * In the response, the Identity Provider's signature MUST cover openid.identity and openid.return_to.
    * In a lot of cases, the Consumer won't get a cancel mode; the End User will just quit or press back within their User-Agent. But if it is returned, the Consumer SHOULD return to what it was doing. In the case of a cancel mode, the rest of the response parameters will be absent.

*/
			
			
	        //return WebResults.FromString(Status._501_Not_Implemented, "Not Implemented");
		}
	}
}