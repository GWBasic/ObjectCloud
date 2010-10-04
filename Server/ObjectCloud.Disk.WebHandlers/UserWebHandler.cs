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
using ObjectCloud.Common.Threading;
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
            return WebResults.From(Status._200_OK, FileHandler.Name);
        }

        /// <summary>
        /// Returns the user's openID identity
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive)]
        public IWebResults GetOpenId(IWebConnection webConnection)
        {
            return WebResults.From(Status._200_OK, FileHandler.Identity);
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

            return WebResults.From(Status._202_Accepted, "Saved");
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

            return WebResults.From(Status._202_Accepted, "Saved");
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
                avatar = "/Shell/UserManagers/No Profile.jpg";

            string requestString = HTTPStringFunctions.AppendGetParameter(avatar, "Method", "GetScaled");
            foreach (KeyValuePair<string, string> getParameter in webConnection.GetParameters)
                if (getParameter.Key != "Method")
                    requestString = HTTPStringFunctions.AppendGetParameter(requestString, getParameter.Key, getParameter.Value);
			
            // Shell to get the avatar, but do the shell as if we're the user in question, instead of the currently-logged in user
            // This works around permissions issues
            return webConnection.ShellTo(requestString, FileContainer.Owner);
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

                            return WebResults.From(Status._200_OK, "openid_mode:id_res\nis_valid:" + associationHandleValid.ToString().ToLower());
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
                return WebResults.From(Status._401_Unauthorized, "Wrong password");
            }

            FileHandlerFactoryLocator.UserManagerHandler.SetPassword(user.Id, NewPassword);

            return WebResults.From(Status._202_Accepted, "Password changed");
        }

        static UserWebHandler()
        {
            NotificationColumnToParticleSpec = new Dictionary<NotificationColumn,string>();

            NotificationColumnToParticleSpec[NotificationColumn.NotificationId] = "notificationId";
            NotificationColumnToParticleSpec[NotificationColumn.Timestamp] = "timeStamp";
            NotificationColumnToParticleSpec[NotificationColumn.SenderIdentity] = "senderIdentity";
            NotificationColumnToParticleSpec[NotificationColumn.ObjectUrl] = "objectUrl";
            NotificationColumnToParticleSpec[NotificationColumn.SummaryView] = "summaryView";
            NotificationColumnToParticleSpec[NotificationColumn.DocumentType] = "documentType";
            NotificationColumnToParticleSpec[NotificationColumn.Verb] = "verb";
            NotificationColumnToParticleSpec[NotificationColumn.ChangeData] = "changeData";
            NotificationColumnToParticleSpec[NotificationColumn.LinkedSenderIdentity] = "linkedSenderIdentity";

            ParticleSpecColumnToNotificationColumn = new Dictionary<string, NotificationColumn>();
            foreach (KeyValuePair<NotificationColumn, string> notificationColumnPair in NotificationColumnToParticleSpec)
                ParticleSpecColumnToNotificationColumn[notificationColumnPair.Value] = notificationColumnPair.Key;
        }

        /// <summary>
        /// Maps the strongly-typed enum to the proper name as part of the particle spec
        /// </summary>
        private static readonly Dictionary<NotificationColumn, string> NotificationColumnToParticleSpec;

        /// <summary>
        /// Maps the particle spec column to the proper NotificationColumn
        /// </summary>
        private static readonly Dictionary<string, NotificationColumn> ParticleSpecColumnToNotificationColumn;

        /// <summary>
        /// 
        /// </summary>
        public IDirectoryHandler ParticleAvatarsDirectory
        {
            get
            {
                if (null == _ParticleAvatarsDirectory)
                    _ParticleAvatarsDirectory = FileContainer.ParentDirectoryHandler.OpenFile("ParticleAvatars").CastFileHandler<IDirectoryHandler>();

                return _ParticleAvatarsDirectory;
            }
        }
        private IDirectoryHandler _ParticleAvatarsDirectory = null;

        /// <summary>
        /// Returns notifications for the user in JSON format.
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="newestNotificationId"></param>
        /// <param name="oldestNotificationId"></param>
        /// <param name="maxNotifications"></param>
        /// <param name="objectUrls"></param>
        /// <param name="senderIdentities"></param>
        /// <param name="desiredValues"></param>
        /// <returns></returns>
        /// <returns>Notifications for the user in JSON format.  These are scrubbed and "eval-safe"</returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetNotifications(
            IWebConnection webConnection,
            long? newestNotificationId,
            long? oldestNotificationId,
            long? maxNotifications,
            string[] objectUrls,
            string[] senderIdentities,
            string[] desiredValues)
        {
            if (FileHandler == FileHandlerFactoryLocator.UserFactory.AnonymousUser.UserHandler)
                throw new WebResultsOverrideException(WebResults.From(
                    Status._403_Forbidden,
                    "The anonymous user can not recieve notifications"));

            Set<NotificationColumn> desiredValuesSet;

            bool includeAvatarUrl = false;

            if (null == desiredValues)
            {
                desiredValuesSet = new Set<NotificationColumn>(Enum<NotificationColumn>.Values);
                includeAvatarUrl = true;
            }
            else if (desiredValues.Length == 0)
            {
                desiredValuesSet = new Set<NotificationColumn>(Enum<NotificationColumn>.Values);
                includeAvatarUrl = true;
            }
            else
            {
                desiredValuesSet = new Set<NotificationColumn>();

                foreach (string desiredValue in desiredValues)
                {
                    NotificationColumn notificationColumn;

                    if ("senderAvatarUrl" == desiredValue)
                    {
                        includeAvatarUrl = true;

                        // Later code needs to look at the senderIdentity to find the avatar
                        desiredValuesSet.Add(NotificationColumn.SenderIdentity);
                    }
                    else if (ParticleSpecColumnToNotificationColumn.TryGetValue(desiredValue, out notificationColumn))
                        desiredValuesSet.Add(notificationColumn);
                }
            }

            List<Dictionary<string, object>> toReturn = new List<Dictionary<string, object>>();

            foreach (Dictionary<NotificationColumn, object> notificationFromDB in FileHandler.GetNotifications(
                newestNotificationId, oldestNotificationId, maxNotifications, objectUrls, senderIdentities, desiredValuesSet))
            {
                Dictionary<string, object> notification = ConvertNotificationFromDBToNotificationForWeb(includeAvatarUrl, notificationFromDB);
                toReturn.Add(notification);
            }

            // TODO:  sender avatar and ignored

            return WebResults.ToJson(toReturn);
        }

        private Dictionary<string, object> ConvertNotificationFromDBToNotificationForWeb(bool includeAvatarUrl, Dictionary<NotificationColumn, object> notificationFromDB)
        {
            Dictionary<string, object> notification = new Dictionary<string, object>();
            foreach (KeyValuePair<NotificationColumn, object> kvp in notificationFromDB)
                notification[NotificationColumnToParticleSpec[kvp.Key]] = kvp.Value;

            if (includeAvatarUrl)
            {
                try
                {
                    // TODO:  This can really slow down if the OpenID isn't in the DB and the remote servers respond slowly!
                    IUserOrGroup senderUserOrGroup = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupOrOpenId(
                        notificationFromDB[NotificationColumn.SenderIdentity].ToString(), true);

                    if (senderUserOrGroup is IUser)
                    {
                        IUser senderUser = (IUser)senderUserOrGroup;

                        if (senderUser.Local)
                            notification["senderAvatarUrl"] = senderUser.Identity + "?Method=GetAvatar";
                        else
                            notification["senderAvatarUrl"] = string.Format(
                                "{0}/{1}.jpg",
                                ParticleAvatarsDirectory.FileContainer.ObjectUrl,
                                senderUser.Id.ToString());
                    }
                    else
                        notification["senderAvatarUrl"] = "";
                }
                catch (Exception e)
                {
                    // This can happen if the server changes host name, or if an OpenID becomes invalid
                    log.Error("Error when trying to determine if an identity is local", e);
                    notification["senderAvatarUrl"] = "";
                }
            }

            notification["ignored"] = false;


            // handle change data
            notification.Remove("changeData");

            switch (notificationFromDB[NotificationColumn.Verb].ToString())
            {
                case ("share"):
                    {
                        notification["recipients"] = JsonReader.Deserialize(notificationFromDB[NotificationColumn.ChangeData].ToString());
                        break;
                    }

                case ("link"):
                    {
                        notification["link"] = JsonReader.Deserialize(notificationFromDB[NotificationColumn.ChangeData].ToString());
                        break;
                    }
            }

            return notification;
        }

        /// <summary>
        /// Sends new notifications as they arrive
        /// </summary>
        [ChannelEndpointMinimumPermission(FilePermissionEnum.Administer)]
        public IChannelEventWebAdaptor IncomingNotificationEvent
        {
            get
            {
                EnsureIncomingNotificationEventWired();
                return _IncomingNotificationEvent;
            }
        }
        private readonly ChannelEventWebAdaptor _IncomingNotificationEvent = new ChannelEventWebAdaptor();

        /// <summary>
        /// Sends new notifications as they arrive
        /// </summary>
        [ChannelEndpointMinimumPermission(FilePermissionEnum.Administer)]
        public IChannelEventWebAdaptor IncomingNotificationEventThroughTemplate
        {
            get
            {
                EnsureIncomingNotificationEventWired();
                return _IncomingNotificationEventThroughTemplate;
            }
        }

        private void EnsureIncomingNotificationEventWired()
        {
            if (!IncomingNotificationEventWired)
                using (TimedLock.Lock(_IncomingNotificationEventThroughTemplate))
                    if (!IncomingNotificationEventWired)
                    {
                        IncomingNotificationEventWired = true;
                        FileHandler.NotificationRecieved += new EventHandler<IUserHandler, EventArgs<Dictionary<NotificationColumn, object>>>(FileHandler_NotificationRecieved);
                    }
        }
        private readonly ChannelEventWebAdaptor _IncomingNotificationEventThroughTemplate = new ChannelEventWebAdaptor();

        private bool IncomingNotificationEventWired = false;

        void FileHandler_NotificationRecieved(IUserHandler sender, EventArgs<Dictionary<NotificationColumn, object>> e)
        {
            Dictionary<string, object> notification = ConvertNotificationFromDBToNotificationForWeb(true, e.Value);
            _IncomingNotificationEvent.SendAll(notification);

            string notificationEvalutatedThroughTemplate;
            ISession session = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();
            try
            {
                session.Login(FileContainer.Owner);

                BlockingShellWebConnection webConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    session,
                    "",
                    null,
                    null,
                    new CookiesFromBrowser(),
                    CallingFrom.Web,
                    WebMethod.GET);

                foreach (KeyValuePair<string, object> kvp in notification)
                    if (null != kvp.Value)
                        webConnection.GetParameters.Add(kvp.Key, kvp.Value.ToString());
                    else
                        webConnection.GetParameters.Add(kvp.Key, "");

                IWebResults webResults = TemplateEngine.EvaluateComponent(webConnection, "/DefaultTemplate/notification.occ");
                notificationEvalutatedThroughTemplate = webResults.ResultsAsString;
            }
            finally
            {
                FileHandlerFactoryLocator.SessionManagerHandler.EndSession(session.SessionId);
            }

            _IncomingNotificationEventThroughTemplate.SendAll(notificationEvalutatedThroughTemplate);
        }

        private TemplateEngine TemplateEngine
        {
            get 
            {
                if (null == _TemplateEngine)
                {
                    IFileContainer templateEngineContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/System/TemplateEngine");
                    _TemplateEngine = (TemplateEngine)templateEngineContainer.WebHandler;
                }

                return _TemplateEngine; 
            }
        }
        private TemplateEngine _TemplateEngine = null;
    }
}
