using System;
using System.Collections.Generic;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Reads ObjectCloud's system log
    /// </summary>
	public class LogWebHandler : DatabaseWebHandler<IObjectCloudLogHandler, LogWebHandler>
	{
		/// <summary>
		/// Returns all of the class names that are present in the log
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
		[WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read, FilePermissionEnum.Read)]
		public IWebResults GetClassnames(IWebConnection webConnection)
		{
			List<string> toReturn = new List<string>(FileHandler.ClassNames);
			
			return WebResults.ToJson(toReturn.ToArray());
		}
		
		/// <summary>
		/// Reads the log
		/// </summary>
		/// <param name="classnames">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="minTimeStamp">
		/// A <see cref="System.Nullable"/>
		/// </param>
		/// <param name="maxTimeStamp">
		/// A <see cref="System.Nullable"/>
		/// </param>
		/// <param name="loggingLevels">
		/// A <see cref="LoggingLevel"/>
		/// </param>
		/// <param name="threadIds">
		/// A <see cref="System.Int32"/>
		/// </param>
		/// <param name="sessionIds">
		/// A <see cref="Guid"/>
		/// </param>
		/// <param name="users">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="exceptionClassnames">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
        /// <param name="remoteEndpoints"></param>
        /// <param name="webConnection"></param>
        /// <param name="exceptionMessageLike"></param>
        /// <param name="messageLike"></param>
		[WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Administer, FilePermissionEnum.Administer)]
		public IWebResults ReadLog(
            IWebConnection webConnection,
           	string[] classnames,
            DateTime? minTimeStamp,
            DateTime? maxTimeStamp,
            LoggingLevel[] loggingLevels,
            int[] threadIds,
            Guid[] sessionIds,
            string[] users,
		    string[] remoteEndpoints,
            string messageLike,
            string[] exceptionClassnames,
            string exceptionMessageLike)
        {
            // Convert the session Ids
            List<ID<ISession, Guid>> convertedSessionIds = null;
            if (null != sessionIds)
            {
                convertedSessionIds = new List<ID<ISession,Guid>>();
                foreach (Guid sessionId in sessionIds)
                    convertedSessionIds.Add(new ID<ISession,Guid>(sessionId));
            }

            // Get the user IDs
            List<ID<IUserOrGroup, Guid>> userIds = null;
            if (null != users)
            {
                userIds = new List<ID<IUserOrGroup, Guid>>();

                foreach (string usernameOrId in users)
                    if (GuidFunctions.IsGuid(usernameOrId))
                        userIds.Add(new ID<IUserOrGroup, Guid>(new Guid(usernameOrId)));
                    else
                    {
                        IUser user = FileHandlerFactoryLocator.UserManagerHandler.GetUser(usernameOrId);
                        userIds.Add(user.Id);
                    }
            }

            IEnumerable<LoggingEvent> events = FileHandler.ReadLog(
                classnames,
                minTimeStamp,
                maxTimeStamp,
                loggingLevels,
                threadIds,
                convertedSessionIds,
                userIds,
                messageLike,
                exceptionClassnames,
                exceptionMessageLike);

            Dictionary<ID<IUserOrGroup, Guid>, IUserOrGroup> usersById = new Dictionary<ID<IUserOrGroup, Guid>, IUserOrGroup>();

            List<Dictionary<string, object>> toReturn = new List<Dictionary<string, object>>();
            foreach (LoggingEvent loggingEvent in events)
            {
                Dictionary<string, object> lEvent = new Dictionary<string, object>();

                lEvent["classname"] = loggingEvent.Classname;
                lEvent["timestamp"] = loggingEvent.TimeStamp;

                // Load the owner
                bool userKnown = false;
                if (null != loggingEvent.UserId)
                {
                    if (usersById.ContainsKey(loggingEvent.UserId.Value))
                        userKnown = true;
                    else
                    {
                        IUserOrGroup owner = FileHandlerFactoryLocator.UserManagerHandler.GetUserOrGroupNoException(loggingEvent.UserId.Value);

                        if (null != owner)
                        {
                            usersById[loggingEvent.UserId.Value] = owner;
                            userKnown = true;
                        }
                    }

                    lEvent["userId"] = loggingEvent.UserId.ToString();
                }

                if (userKnown)
                    lEvent["user"] = usersById[loggingEvent.UserId.Value].Name;

                lEvent["remoteEndPoint"] = loggingEvent.RemoteEndPoint;
                lEvent["message"] = loggingEvent.Message;
                lEvent["exceptionClassname"] = loggingEvent.ExceptionClassname;
                lEvent["exceptionMessage"] = loggingEvent.ExceptionMessage;
                lEvent["exceptionStackTrace"] = loggingEvent.ExceptionStackTrace;
                lEvent["level"] = loggingEvent.Level;
                lEvent["remoteEndPoint"] = loggingEvent.RemoteEndPoint;

                if (null != loggingEvent.SessionId)
                    lEvent["sessionId"] = loggingEvent.SessionId.ToString();
                
                lEvent["threadId"] = loggingEvent.ThreadId;

                toReturn.Add(lEvent);
            }

            return WebResults.ToJson(toReturn);
		}
		
		/// <summary>
		/// Lets a user read the log entries that he or she created
		/// </summary>
        /// <param name="remoteEndpoints"></param>
        /// <param name="webConnection"></param>
        /// <param name="classnames">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="minTimeStamp">
		/// A <see cref="System.Nullable"/>
		/// </param>
		/// <param name="maxTimeStamp">
		/// A <see cref="System.Nullable"/>
		/// </param>
		/// <param name="loggingLevels">
		/// A <see cref="LoggingLevel"/>
		/// </param>
		/// <param name="threadIds">
		/// A <see cref="System.Int32"/>
		/// </param>
		/// <param name="sessionIds">
		/// A <see cref="Guid"/>
		/// </param>
		/// <param name="exceptionClassnames">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
        /// <param name="exceptionMessageLike"></param>
        /// <param name="messageLike"></param>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read, FilePermissionEnum.Read)]
		public IWebResults ReadMyLog(
            IWebConnection webConnection,
            string[] classnames,
            DateTime? minTimeStamp,
            DateTime? maxTimeStamp,
            LoggingLevel[] loggingLevels,
            int[] threadIds,
            Guid[] sessionIds,
		    string[] remoteEndpoints,
            string messageLike,
            string[] exceptionClassnames,
            string exceptionMessageLike)
		{
			return ReadLog(
			    webConnection,
				classnames,
				minTimeStamp,
				maxTimeStamp,
				loggingLevels,
				threadIds,
				sessionIds,
				new string[] { webConnection.Session.User.Id.Value.ToString() },
			    remoteEndpoints,
                messageLike,
				exceptionClassnames,
                exceptionMessageLike);
		}
		
		/// <summary>
		/// Updates when specific kinds of logging information can be deleted
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
		[WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Administer, FilePermissionEnum.Administer)]
		public IWebResults GetDeleteTimespans(IWebConnection webConnection)
		{
			IDictionary<LoggingLevel, TimeSpan> deleteTimespans = FileHandler.GetLoggingTimespans();
			
			Dictionary<string, double> toReturn = new Dictionary<string, double>();
			
			foreach (LoggingLevel loggingLevel in deleteTimespans.Keys)
				toReturn[loggingLevel.ToString()] = deleteTimespans[loggingLevel].TotalDays;
			
			return WebResults.ToJson(toReturn);
		}
		
		/// <summary>
		/// Updates when specific kinds of logging information can be deleted
		/// </summary>
		/// <param name="webConnection">
		/// A <see cref="IWebConnection"/>
		/// </param>
        /// <param name="deleteTimespans"></param>
		/// <returns>
		/// A <see cref="IWebResults"/>
		/// </returns>
		[WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Administer, FilePermissionEnum.Administer)]
		public IWebResults SetDeleteTimespans(IWebConnection webConnection, Dictionary<string, double> deleteTimespans)
		{
			Dictionary<LoggingLevel, TimeSpan> toSet = new Dictionary<LoggingLevel, TimeSpan>();
			
			foreach(KeyValuePair<string, double> levelAndDays in deleteTimespans)
			{
				LoggingLevel loggingLevel;
				try
				{
					loggingLevel = Enum<LoggingLevel>.Parse(levelAndDays.Key);
				}
				catch
				{
					throw new WebResultsOverrideException(WebResults.From(
						Status._400_Bad_Request, levelAndDays.Key + " is not a valid logging level"));
				}
				
				toSet[loggingLevel] = TimeSpan.FromDays(levelAndDays.Value);
			}
			
			FileHandler.UpdateLoggingTimespans(toSet);
			
			return WebResults.From(Status._202_Accepted);
		}
	}
}
