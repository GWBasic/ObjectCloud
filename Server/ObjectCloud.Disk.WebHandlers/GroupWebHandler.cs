// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Allows manipulation and querying of local users.
    /// </summary>
    public class GroupWebHandler : NameValuePairsWebHandler
    {
        private IGroup Group
        {
            get 
            {
                if (null == _Group)
                {
                    ID<IUserOrGroup, Guid> groupId = new ID<IUserOrGroup, Guid>(new Guid(FileHandler["GroupId"]));
                    _Group = FileHandlerFactoryLocator.UserManagerHandler.GetGroup(groupId);
                }

                return _Group; 
            }
        }
        private IGroup _Group = null;

        private UserManagerWebHandler UserManagerWebHandler
        {
            get
            {
                if (null == _UserManagerWebHandler)
                    _UserManagerWebHandler = (UserManagerWebHandler)FileHandlerFactoryLocator.UserManagerHandler.FileContainer.WebHandler;

                return _UserManagerWebHandler; 
            }
        }
        private UserManagerWebHandler _UserManagerWebHandler = null;

        /// <summary>
        /// Returns all of the members of a group
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults getMembers(IWebConnection webConnection)
        {
            return UserManagerWebHandler.GetUsersInGroup(webConnection, null, Group.Id.ToString());
        }

        /// <summary>
        /// Returns the group and its alias
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults getGroup(IWebConnection webConnection)
        {
            return UserManagerWebHandler.GetGroupAndAlias(webConnection, Group.Id.Value);
        }

        /// <summary>
        /// Allows a user to join the group
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults joinGroup(IWebConnection webConnection)
        {
            // Only let people join if the group is public
            if (Group.Type != GroupType.Public)
                throw new WebResultsOverrideException(WebResults.From(
                    Status._403_Forbidden, "This group is not public.  Contact the owner to join."));

            if (FileHandlerFactoryLocator.UserFactory.AnonymousUser == webConnection.Session.User)
                throw new WebResultsOverrideException(WebResults.From(
                    Status._403_Forbidden, "You must be logged in to join a group"));

            ID<IUserOrGroup, Guid> ownerId = 
                Group.OwnerId != null ? Group.OwnerId.Value : FileHandlerFactoryLocator.UserFactory.RootUser.Id;
            
            IUser groupOwner = FileHandlerFactoryLocator.UserManagerHandler.GetUser(ownerId);

            IWebConnection shellWebConnection = webConnection.CreateShellConnection(groupOwner);

            return UserManagerWebHandler.AddUserToGroup(
                shellWebConnection,
                null,
                Group.Id.ToString(),
                null,
                webConnection.Session.User.Id.ToString());
        }

        /// <summary>
        /// Allows a user to leave a group
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults leaveGroup(IWebConnection webConnection)
        {
            // Only let people join if the group is public
            if (Group.Type != GroupType.Public)
                throw new WebResultsOverrideException(WebResults.From(
                    Status._403_Forbidden, "This group is not public.  Contact the owner to leave."));

            ID<IUserOrGroup, Guid> ownerId =
                Group.OwnerId != null ? Group.OwnerId.Value : FileHandlerFactoryLocator.UserFactory.RootUser.Id;

            IUser owner = FileHandlerFactoryLocator.UserManagerHandler.GetUser(ownerId);

            IWebConnection shellWebConnection = webConnection.CreateShellConnection(owner);

            return UserManagerWebHandler.RemoveUserFromGroup(
                shellWebConnection, null, Group.Id.ToString(), null, webConnection.Session.User.Id.ToString());
        }

        /// <summary>
        /// Returns true if the current user is a member of the group, false otherwise
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject)]
        public IWebResults isMember(IWebConnection webConnection)
        {
            return WebResults.ToJson(
                FileHandlerFactoryLocator.UserManagerHandler.IsUserInGroup(webConnection.Session.User.Id, Group.Id));
        }
    }
}
