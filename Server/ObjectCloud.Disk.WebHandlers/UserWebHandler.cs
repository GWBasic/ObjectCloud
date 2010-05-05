// Copyright 2009, 2010 Andrew Rondeau
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
    public class UserWebHandler : DatabaseWebHandler<IUserHandler, UserWebHandler>
    {
        private static ILog log = LogManager.GetLogger(typeof(UserWebHandler));

        /// <summary>
        /// Returns the user's name
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive)]
        public IWebResults GetName(IWebConnection webConnection)
        {
            return WebResults.FromString(Status._200_OK, FileHandler.Name);
        }

        /// <summary>
        /// Returns the user's openID identity
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive)]
        public IWebResults GetOpenId(IWebConnection webConnection)
        {
            return WebResults.FromString(Status._200_OK, FileHandler.Identity);
        }

		/// <summary>
		/// Gets all of the user's public metadata 
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
		[WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
		public IWebResults GetPublicData(IWebConnection webConnection)
		{
			Dictionary<string, object> toReturn = new Dictionary<string, object>();
			
			IEnumerable<string> publicMetadataItems = StringParser.ParseCommaSeperated(
				FileHandler["PublicMetadataItems"]);
			
			foreach (string publicMetadataItem in publicMetadataItems)
				toReturn[publicMetadataItem] = FileHandler[publicMetadataItem];
			
			return WebResults.ToJson(toReturn);
		}
		
        /// <summary>
        /// Gets all of the name-values, returns a JSON object with names and values as strings
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Administer)]
        public IWebResults GetAllData(IWebConnection webConnection)
        {
            Dictionary<string, string> toWrite = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> pair in FileHandler)
                toWrite.Add(pair.Key, pair.Value);

            return WebResults.ToJson(toWrite);
        }
		
        /// <summary>
        /// Sets all of the values based on the results of a POST query
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults SetAllData(IWebConnection webConnection)
        {
            // Decode the new pairs
            IDictionary<string, string> newPairs;

            newPairs = webConnection.PostParameters;

            FileHandler.WriteAll(webConnection.Session.User, newPairs, false);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }
		
        /// <summary>
        /// Sets all of the values based on the results of a POST query
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="pairs"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_JSON, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults SetAllDataJson(IWebConnection webConnection, JsonReader pairs)
        {
            // Decode the new pairs
            Dictionary<string, string> newPairs = new Dictionary<string, string>();

			foreach (KeyValuePair<string, object> kvp in (IEnumerable<KeyValuePair<string, object>>)pairs.Deserialize())
				if (kvp.Value is string)
					newPairs.Add(kvp.Key, kvp.Value.ToString());
				else
					newPairs.Add(kvp.Key, JsonWriter.Serialize(kvp.Value));

            FileHandler.WriteAll(webConnection.Session.User, newPairs, false);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }
		
		/// <summary>
		/// Returns the user's avatar image 
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
		[WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
		public IWebResults GetAvatar(IWebConnection webConnection)
		{
            string avatar;

            if (FileHandler.Contains("Avatar"))
                avatar = FileHandler["Avatar"];
            else
                avatar = "/Shell/UserManagers/No Profile.png";

            string requestString = HTTPStringFunctions.AppendGetParameter(avatar, "Method", "GetScaled");
            foreach (KeyValuePair<string, string> getParameter in webConnection.GetParameters)
                if (getParameter.Key != "Method")
                    requestString = HTTPStringFunctions.AppendGetParameter(requestString, getParameter.Key, getParameter.Value);
			
			return webConnection.ShellTo(requestString);
		}

        /// <summary>
        /// Returns the page that's used when a user from this server is logging into another server.  (TODO, verify)
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive)]
        public IWebResults DoOpenId(IWebConnection webConnection)
        {
            if (webConnection.GetParameters.ContainsKey("openid.mode"))
            {
                switch (webConnection.GetParameters["openid.mode"])
                {
                    case ("check_authentication"):
                        {
                            string associationHandle = webConnection.GetArgumentOrException("openid.assoc_handle");

                            IUser user = FileHandlerFactoryLocator.UserManagerHandler.GetUser(FileHandler.Name);
                            bool associationHandleValid = FileHandlerFactoryLocator.UserManagerHandler.VerifyAssociationHandle(
                                user.Id, associationHandle);

                            if (log.IsInfoEnabled)
                                log.InfoFormat("OpenID validation for {0}: {1}", FileHandler.Name, associationHandleValid);

                            return WebResults.FromString(Status._200_OK, "openid_mode:id_res\nis_valid:" + associationHandleValid.ToString().ToLower());
                        }

                    /*default:
                    {
                        return WebResults.FromString(Status._501_Not_Implemented, webConnection.GetParameters["openid.mode"] + " not supported");
                    }*/
                }
            }

            Dictionary<string, string> getParameters = new Dictionary<string, string>(webConnection.GetParameters);
            if (getParameters.ContainsKey("Method"))
                getParameters.Remove("Method");

            string getParametersAsJSON = JsonWriter.Serialize(getParameters);

            string shellUrl = HTTPStringFunctions.AppendGetParameter("/Shell/OpenID/OpenIDLandingPage.wchtml", "OriginalParameters", getParametersAsJSON);
            shellUrl = HTTPStringFunctions.AppendGetParameter(shellUrl, "openid.identity", webConnection.GetArgumentOrException("openid.identity"));

            return webConnection.ShellTo(shellUrl);
        }

        /// <summary>
        /// Changes the user's password
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="OldPassword"></param>
        /// <param name="NewPassword"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer)]
        public IWebResults SetPassword(IWebConnection webConnection, string OldPassword, string NewPassword)
        {
            IUser user;

            try
            {
                user = FileHandlerFactoryLocator.UserManagerHandler.GetUser(FileHandler.Name, OldPassword);
            }
            catch (WrongPasswordException)
            {
                return WebResults.FromString(Status._401_Unauthorized, "Wrong password");
            }

            FileHandlerFactoryLocator.UserManagerHandler.SetPassword(user.Id, NewPassword);

            return WebResults.FromString(Status._202_Accepted, "Password changed");
        }

        /// <summary>
        /// Gets the senderToken for the openId
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="openId"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Naked, FilePermissionEnum.Administer)]
        public IWebResults GetSenderToken(IWebConnection webConnection, string openId, bool? forceRefresh)
        {
            if (null == forceRefresh)
                forceRefresh = false;

            string senderToken = FileHandler.GetSenderToken(openId, forceRefresh.Value);

            return WebResults.FromString(Status._200_OK, senderToken);
        }

        /// <summary>
        /// Establishes trust with the sender using the sent token
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="sender"></param>
        /// <param name="token"></param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status)]
        public IWebResults EstablishTrust(IWebConnection webConnection, string sender, string token)
        {
            try
            {
                FileHandler.EstablishTrust(sender, token);
                return WebResults.FromStatus(Status._201_Created);
            }
            catch (ParticleException.CouldNotEstablishTrust cnet)
            {
                return WebResults.FromString(Status._401_Unauthorized, cnet.Message);
            }
        }

        /// <summary>
        /// Assists in responding when establishing trust
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="token"></param>
        /// <param name="senderToken"></param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status)]
        public IWebResults RespondTrust(IWebConnection webConnection, string token, string senderToken)
        {
            try
            {
                FileHandler.RespondTrust(token, senderToken);
                return WebResults.FromStatus(Status._202_Accepted);
            }
            catch (ParticleException.BadToken bt)
            {
                return WebResults.FromString(Status._401_Unauthorized, bt.Message);
            }
        }

        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="openId"></param>
        /// <param name="objectUrl"></param>
        /// <param name="title"></param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        /// <param name="changeData"></param>
        /// <param name="forceRefreshSenderToken"></param>
        /// <param name="forceRefreshEndpoints"></param>
        /// <param name="maxRetries"></param>
        /// <param name="transportErrorDelay"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults SendNotification(
            IWebConnection webConnection,
            string openId,
            string objectUrl,
            string title,
            string documentType,
            string messageSummary,
            string changeData,
            bool? forceRefreshSenderToken,
            bool? forceRefreshEndpoints,
            int? maxRetries,
            TimeSpan? transportErrorDelay)
        {
            if (null == forceRefreshSenderToken)
                forceRefreshSenderToken = false;

            if (null == forceRefreshEndpoints)
                forceRefreshEndpoints = false;

            if (null == maxRetries)
                maxRetries = 42;

            if (null == transportErrorDelay)
                transportErrorDelay = TimeSpan.FromMinutes(1);

            try
            {
                // TODO:  Deleted is not handled
                FileHandler.SendNotification(openId, objectUrl, title, documentType, messageSummary, changeData, forceRefreshSenderToken.Value, forceRefreshEndpoints.Value, maxRetries.Value, transportErrorDelay.Value);
                return WebResults.FromStatus(Status._202_Accepted);
            }
            catch (ParticleException pe)
            {
                return WebResults.FromString(Status._417_Expectation_Failed, pe.Message);
            }
        }

        /// <summary>
        /// Receives a notification
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="senderToken"></param>
        /// <param name="objectUrl"></param>
        /// <param name="title"></param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        /// <param name="changeData"></param>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status)]
        public IWebResults ReceiveNotification(
            IWebConnection webConnection,
            string senderToken,
            string objectUrl,
            string title,
            string documentType,
            string messageSummary,
            string changeData)
        {
            if (FileHandler == FileHandlerFactoryLocator.UserFactory.AnonymousUser.UserHandler)
                throw new WebResultsOverrideException(WebResults.FromString(Status._401_Unauthorized, "The anonymous user can not recieve notifications"));

            try
            {
                FileHandler.ReceiveNotification(
                    senderToken,
                    objectUrl,
                    title,
                    documentType,
                    messageSummary,
                    changeData);

                return WebResults.FromStatus(Status._202_Accepted);
            }
            catch (ParticleException.BadToken)
            {
                return WebResults.FromString(Status._412_Precondition_Failed, "senderToken");
            }
        }

        /// <summary>
        /// Returns notifications for the user in JSON format.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="newestNotificationId"></param>
        /// <param name="oldestNotificationId"></param>
        /// <param name="maxNotifications"></param>
        /// <param name="objectUrl"></param>
        /// <param name="sender"></param>
        /// <param name="desiredValues"></param>
        /// <returns>Notifications for the user in JSON format.  These are scrubbed and "eval-safe"</returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetNotifications(
            IWebConnection webConnection,
            long? newestNotificationId,
            long? oldestNotificationId,
            long? maxNotifications,
            string objectUrl,
            string sender,
            string desiredValues)
        {
            // Parse the desired values
            List<NotificationColumn> desiredValuesList = new List<NotificationColumn>();
            if (null == desiredValues)
                desiredValuesList.AddRange(Enum<NotificationColumn>.Values);
            else
                foreach (string notificationColumnString in desiredValues.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries))
                {
                    NotificationColumn? notificationColumn = Enum<NotificationColumn>.TryParse(notificationColumnString);

                    if (null != notificationColumn)
                        desiredValuesList.Add(notificationColumn.Value);
                    else
                        log.Warn(notificationColumnString + " is not a known column for notifications");
                }

            List<Dictionary<NotificationColumn, object>> notifications = new List<Dictionary<NotificationColumn,object>>(FileHandler.GetNotifications(
                newestNotificationId, oldestNotificationId, maxNotifications, objectUrl, sender, desiredValuesList));

            return WebResults.ToJson(notifications);
        }

        /// <summary>
        /// Sends new notifications as they arrive
        /// </summary>
        [ChannelEndpointMinimumPermission(FilePermissionEnum.Administer)]
        public IChannelEventWebAdaptor IncomingNotificationEvent
        {
            get
            {
                if (!IncomingNotificationEventWired)
                    using (TimedLock.Lock(_IncomingNotificationEvent))
                        if (!IncomingNotificationEventWired)
                        {
                            IncomingNotificationEventWired = true;
                            FileHandler.NotificationRecieved += new EventHandler<IUserHandler, EventArgs<Dictionary<NotificationColumn, object>>>(FileHandler_NotificationRecieved);
                        }

                return _IncomingNotificationEvent;
            }
        }
        private readonly ChannelEventWebAdaptor _IncomingNotificationEvent = new ChannelEventWebAdaptor();

        private bool IncomingNotificationEventWired = false;

        void FileHandler_NotificationRecieved(IUserHandler sender, EventArgs<Dictionary<NotificationColumn, object>> e)
        {
            _IncomingNotificationEvent.SendAll(e.Value);
        }
    }
}
