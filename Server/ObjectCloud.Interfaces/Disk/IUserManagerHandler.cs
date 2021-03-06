// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    public interface IUserManagerHandler : IFileHandler
    {
        /// <summary>
        /// Creates a user with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password">The password</param>
        /// <returns></returns>
        /// <exception cref="UserAlreadyExistsException">Thrown if the user already exists</exception>
        /// <exception cref="MaximumUsersExceeded">Thrown when the maximum number of users will be exceeded if the user is created</exception>
        IUser CreateUser(string name, string password, string displayName);

        /// <summary>
        /// Creates a user with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password">The password</param>
        /// <returns></returns>
        /// <exception cref="UserAlreadyExistsException">Thrown if the user already exists</exception>
        /// <exception cref="MaximumUsersExceeded">Thrown when the maximum number of users will be exceeded if the user is created</exception>
        IUser CreateUser(string name, string displayName, string identityProviderArgs, IIdentityProvider identityProvider);

        /// <summary>
        /// Creates a user with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password">The password</param>
        /// <param name="userId">The user's ID.  This is for special IDs, like root and anyonymous</param>
        /// <param name="builtIn">Implies that the user is managed by the system and can not be deleted</param>
        /// <returns></returns>
        /// <exception cref="UserAlreadyExistsException">Thrown if the user already exists</exception>
        /// <exception cref="MaximumUsersExceeded">Thrown when the maximum number of users will be exceeded if the user is created</exception>
        IUser CreateUser(string name, string password, string displayName, ID<IUserOrGroup, Guid> userId, bool builtIn);

        /// <summary>
        /// Creates a group with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ownerId">The user that owns the group</param>
        /// <returns></returns>
        /// <exception cref="UserAlreadyExistsException">Thrown if the group already exists</exception>
        IGroup CreateGroup(string name, string displayName, ID<IUserOrGroup, Guid>? ownerId, GroupType groupType);

		/// <summary>
        /// Creates a group with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ownerId">The user that owns the group</param>
        /// <param name="groupId">The group's ID.  This is for special groups like "all users," "everyone," "super users"</param>
        /// <param name="builtIn">Implies that the group is managed by the system and can not be deleted</param>
        /// <param name="automatic">Implies that membership in the group is automatically determined at runtime and can not be modified.</param>
        /// <returns></returns>
        /// <exception cref="UserAlreadyExistsException">Thrown if the group already exists</exception>
        IGroup CreateGroup(string name, string displayName, ID<IUserOrGroup, Guid>? ownerId, ID<IUserOrGroup, Guid> groupId, bool builtIn, bool automatic, GroupType groupType);

        /// <summary>
        /// Gets the user with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="UnknownUser">Thrown if the user does not exist</exception>
        IUser GetUser(string name);

        /// <summary>
        /// Gets the user, group, or openId
        /// </summary>
        /// <param name="nameOrGroupOrIdentity">The user name, group name, or openId</param>
        /// <returns></returns>
        /// <exception cref="UnknownUser">Thrown if the user, group, or openId does not exist</exception>
        IUserOrGroup GetUserOrGroupOrOpenId(string nameOrGroupOrIdentity);

        /// <summary>
        /// Gets the user, group, or openId
        /// </summary>
        /// <param name="nameOrGroupOrIdentity">The user name, group name, or openId</param>
        /// <param name="onlyInLocalDB">true to only return information about users that are in the local DB</param>
        /// <returns></returns>
        /// <exception cref="UnknownUser">Thrown if the user, group, or openId does not exist</exception>
        IUserOrGroup GetUserOrGroupOrOpenId(string nameOrGroupOrIdentity, bool onlyInLocalDB);

        /// <summary>
        /// Gets the user, or group by ID
        /// </summary>
        /// <param name="nameOrGroupOrIdentity">The user name, group name, or openId</param>
        /// <returns></returns>
        /// <exception cref="UnknownUser">Thrown if the user, group, or openId does not exist</exception>
        IUserOrGroup GetUserOrGroup(ID<IUserOrGroup, Guid> userOrGroupId);

        /// <summary>
        /// Gets the user, or group by ID.  Returns null if the user or group doesn't exist
        /// </summary>
        /// <param name="nameOrGroupOrIdentity">The user name, group name, or openId</param>
        /// <returns></returns>
        IUserOrGroup GetUserOrGroupNoException(ID<IUserOrGroup, Guid> userOrGroupId);

        /// <summary>
        /// Gets the user with the given name and password
        /// </summary>
        /// <param name="name"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        /// <exception cref="WrongPasswordException">Thrown if the password is incorrect</exception>
        /// <exception cref="UnknownUser">Thrown if the user does not exist</exception>
        IUser GetUser(string name, string password);

        /// <summary>
        /// Gets the user with the corresponding ID 
        /// </summary>
        /// <exception cref="UnknownUser">Thrown if the user does not exist</exception>
        IUser GetUser(ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Gets the user with the corresponding ID, returns null if the user doesn't exist
        /// </summary>
        IUser GetUserNoException(ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Gets the user with the corresponding name or identity, returns null if the user isn't found
        /// </summary>
        IUser GetUserNoException(string nameOrIdentity);

        /// <summary>
        /// Gets the group with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="UnknownUser">Thrown if the group does not exist</exception>
        IGroup GetGroup(string name);
		
		/// <summary>
		/// Gets the group with the corresponding ID
		/// </summary>
        /// <exception cref="UnknownUser">Thrown if the group does not exist</exception>
		IGroup GetGroup(ID<IUserOrGroup, Guid> groupId);

        /// <summary>
        /// Gets the users and groups with corresponding IDs 
        /// </summary>
        /// <param name="userOrGroupIds">
        /// A <see cref="IEnumerable"/>
        /// </param>
        /// <returns>
        /// A <see cref="IEnumerable"/>
        /// </returns>
        IEnumerable<IUserOrGroup> GetUsersAndGroups(IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds);

        /// <summary>
        /// Gets the users and groups with corresponding names
        /// </summary>
        /// <param name="userOrGroupIds">
        /// A <see cref="IEnumerable"/>
        /// </param>
        /// <returns>
        /// A <see cref="IEnumerable"/>
        /// </returns>
        IEnumerable<IUserOrGroup> GetUsersAndGroups(IEnumerable<string> names);

        /// <summary>
        /// Gets all the users from the given IDs.  If any of the IDs are groupIds, then the users in the groups are returned
        /// </summary>
        /// <param name="userOrGroupIds">
        /// A <see cref="IEnumerable"/>
        /// </param>
        /// <returns>
        /// A <see cref="IEnumerable"/>
        /// </returns>
        IEnumerable<IUser> GetUsersAndResolveGroupsToUsers(IEnumerable<ID<IUserOrGroup, Guid>> userOrGroupIds);

        /// <summary>
        /// Deletes the user
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="UnknownUser">Thrown if the user does not exist</exception>
        /// <exception cref="CanNotDeleteBuiltInUserOrGroup">Thrown if the user is built-in and can not be deleted</exception>
        void DeleteUser(string name);

        /// <summary>
        /// Deletes the group
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="UnknownUser">Thrown if the group does not exist</exception>
        /// <exception cref="CanNotDeleteBuiltInUserOrGroup">Thrown if the group is built-in and can not be deleted</exception>
        void DeleteGroup(string name);

        /// <summary>
        /// The root user
        /// </summary>
        IUser Root { get; }
		
		/// <summary>
		///Restores the user from XML 
		/// </summary>
		/// <param name="xmlReader">
		/// A <see cref="XmlReader"/>
		/// </param>
		/// <param name="userId">
		/// A <see cref="ID"/>
		/// </param>
		void Restore(XmlReader xmlReader, ID<IUserOrGroup, Guid> userId);
		
		/// <summary>
		/// Creates an association handle to return to as part of OpenID authentication
		/// </summary>
		/// <returns>
		/// A <see cref="System.String"/>
		/// </returns>
		string CreateAssociationHandle(ID<IUserOrGroup, Guid> userId);
		
		/// <summary>
		/// Verifies that the passed in association handle is valid
		/// </summary>
		/// <param name="associationHandle">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.Boolean"/>
		/// </returns>
        bool VerifyAssociationHandle(ID<IUserOrGroup, Guid> userId, string associationHandle);
		
		/// <summary>
		/// Adds a user to a group.  If the user is already in the group, no action will occur
		/// </summary>
		/// <param name="userId">
		/// A <see cref="ID"/>
		/// </param>
		/// <param name="groupId">
		/// A <see cref="ID"/>
		/// </param>
		/// <exception cref="CanNotEditMembershipOfSystemGroup">Thrown if the group is an automatic group, which can not be edited</exception>
		void AddUserToGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId);
		
		/// <summary>
		/// Removes a user from a group.  If the user is not already in the group, no action will occur
		/// </summary>
		/// <param name="userId">
		/// A <see cref="ID"/>
		/// </param>
		/// <param name="groupId">
		/// A <see cref="ID"/>
		/// </param>
		/// <exception cref="CanNotEditMembershipOfSystemGroup">Thrown if the group is an automatic group, which can not be edited</exception>
		void RemoveUserFromGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId);
		
		/// <summary>
		/// Returns all of the group IDs of groups that the user is in 
		/// </summary>
		/// <param name="userId">
		/// A <see cref="ID"/>
		/// </param>
		/// <returns>
		/// A <see cref="IEnumerable"/>
		/// </returns>
		IEnumerable<ID<IUserOrGroup, Guid>> GetGroupIdsThatUserIsIn(ID<IUserOrGroup, Guid> userId);
		
		/// <summary>
		/// Returns all of the groups that the user is in 
		/// </summary>
		/// <param name="userId">
		/// A <see cref="ID"/>
		/// </param>
		/// <returns>
		/// A <see cref="IEnumerable"/>
		/// </returns>
        IEnumerable<IGroupAndAlias> GetGroupsThatUserIsIn(ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Searches for users and groups that match the query.  This behavior is implementation dependant
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        IEnumerable<IUserOrGroup> SearchUsersAndGroups(string query, int max);

        /// <summary>
        /// Returns all groups in the system 
        /// </summary>
        /// <returns>
        /// A <see cref="IEnumerable"/>
        /// </returns>
        IEnumerable<IGroup> GetAllGroups();

        /// <summary>
        /// Returns all groups in the system 
        /// </summary>
        /// <returns>
        /// A <see cref="IEnumerable"/>
        /// </returns>
        IEnumerable<IGroupAndAlias> GetAllGroups(ID<IUserOrGroup, Guid> userId);

        /// <summary>
        /// Returns all local user IDs
        /// </summary>
        /// <returns></returns>
        IEnumerable<ID<IUserOrGroup, Guid>> GetAllLocalUserIds();
		
		/// <summary>
		/// Returns all groups that a user owns 
		/// </summary>
		/// <param name="userId">
		/// A <see cref="ID"/>
		/// </param>
		/// <returns>
		/// A <see cref="IEnumerable"/>
		/// </returns>
        IEnumerable<IGroupAndAlias> GetGroupsThatUserOwns(ID<IUserOrGroup, Guid> userId);
		
		/// <summary>
		/// Returns all of the users in a group 
		/// </summary>
		/// <param name="groupId">
		/// A <see cref="ID"/>
		/// </param>
		/// <returns>
		/// A <see cref="IEnumerable"/>
		/// </returns>
		IEnumerable<IUser> GetUsersInGroup(ID<IUserOrGroup, Guid> groupId);

        /// <summary>
        /// Returns true if the user is in the group
        /// </summary>
        /// <param name="userId">UserId</param>
        /// <param name="groupId">GroupId</param>
        /// <returns></returns>
        bool IsUserInGroup(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId);

        /// <summary>
        /// Sets the user's password
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="password"></param>
        void SetPassword(ID<IUserOrGroup, Guid> userId, string password);

        /// <summary>
        /// Sets the user's alias for the group.  If alias is null, deletes the alias.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="groupId"></param>
        /// <param name="alias"></param>
        void SetGroupAlias(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId, string alias);

        /// <summary>
        /// Gets the user's alias for the group, or returns the group name if no alias is set
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="groupId"></param>
        /// <param name="alias"></param>
        IGroupAndAlias GetGroupAndAlias(ID<IUserOrGroup, Guid> userId, ID<IUserOrGroup, Guid> groupId);

        /// <summary>
        /// Gets information about recipients for sending a notification
        /// </summary>
        /// <param name="openIdOrWebFinger"></param>
        /// <param name="forceRefresh"></param>
        /// <param name="callback">Called when recipient information is known</param>
        /// <param name="errorCallback">Called when an error occurs establishing recipient information</param>
        /// <param name="exceptionCallback">Callback for unhandled exceptions</param>
        /// <returns></returns>
        void GetEndpointInfos(
            IUserOrGroup sender, 
            bool forceRefresh, 
            IEnumerable<string> recipientIdentities,
            ParticleEndpoint particleEndpoint,
            Action<EndpointInfo> callback,
            Action<IEnumerable<string>> errorCallback,
            Action<Exception> exceptionCallback);

        /// <summary>
        /// Used when responding to a request to establish trust
        /// </summary>
        /// <param name="token"></param>
        /// <param name="senderToken"></param>
        /// <exception cref="DiskException">Thrown if the token is invalid</exception>
        void RespondTrust(string token, string senderToken);

        /// <summary>
        /// Writes information about established trust into the database
        /// </summary>
        /// <param name="senderIdentity"></param>
        /// <param name="token"></param>
        /// <param name="loginUrl"></param>
        /// <param name="loginUrlOpenID"></param>
        /// <param name="loginUrlWebFinger"></param>
        /// <param name="loginUrlRedirect"></param>
        /// <returns></returns>
        void EstablishTrust(
            string senderIdentity, 
            string senderToken, 
            string loginUrl, 
            string loginUrlOpenID, 
            string loginUrlWebFinger, 
            string loginUrlRedirect);

        /// <summary>
        /// Deletes all trust that the user has established, requiring that trust is re-established when future notifications are sent
        /// </summary>
        /// <param name="userOrGroup"></param>
        void DeleteAllEstablishedTrust(IUserOrGroup userOrGroup);

        /// <summary>
        /// Responds with the endpoint needed to respond trust
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="callback"></param>
        void GetEndpoints(string identity, Action<IEndpoints> callback, Action<Exception> errorCallback);

        /// <summary>
        /// Gets the sender token ID for the given sender token, or returns false if the sender token is unknown
        /// </summary>
        /// <param name="senderToken"></param>
        /// <param name="senderIdendity">The sender's ideneity</param>
        /// <returns></returns>
        bool TryGetSenderIdentity(string senderToken, out string senderIdendity);

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="recipientIdentities"></param>
        /// <param name="fileContainer"></param>
        /// <param name="summaryView"></param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        void SendNotification(
            IUser sender,
            bool forceRefresh,
            IEnumerable<IUser> recipients,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData,
            int maxRetries,
            TimeSpan transportErrorDelay);

        /// <summary>
        /// Returns information needed to speed up an OpenID login when viewing an object from a sender
        /// </summary>
        /// <param name="senderIdentity"></param>
        /// <returns></returns>
        RapidLoginInfo GetRapidLoginInfo(string senderIdentity);

        /// <summary>
        /// Returns the total number of local users
        /// </summary>
        /// <returns></returns>
        int GetTotalLocalUsers();

        /// <summary>
        /// The maximum number of local users allowed
        /// </summary>
        int? MaxLocalUsers { get; set; }
    }

    /// <summary>
    /// Encapsulates information about a recipient when sending a notification
    /// </summary>
    public struct EndpointInfo
    {
        /// <summary>
        /// The sender token that's used to identify the sender
        /// </summary>
        public string SenderToken;

        /// <summary>
        /// The requested endpoint
        /// </summary>
        public string Endpoint;

        /// <summary>
        /// The OpenIDs or WebFinders that are valid for this endpoint
        /// </summary>
        public List<string> RecipientIdentities;
    }

    /// <summary>
    /// Encapsulates information needed to facilitate quickly logging in through OpenID
    /// </summary>
    public struct RapidLoginInfo
    {
        public string LoginUrl;
        public string LoginUrlOpenID;
        public string LoginUrlWebFinger;
        public string LoginUrlRedirect;
    }
}
