using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

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
        /// <param name="exceptionMessageRegex"></param>
        /// <param name="messageRegex"></param>
        /// <param name="maxEvents"></param>
		[WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Administer, FilePermissionEnum.Administer)]
		public IWebResults ReadLog(
            IWebConnection webConnection,
			int? maxEvents,
           	string[] classnames,
            DateTime? maxTimeStamp,
            LoggingLevel[] loggingLevels,
            int[] threadIds,
            Guid[] sessionIds,
            string[] users,
		    string[] remoteEndpoints,
            string messageRegex,
            string[] exceptionClassnames,
            string exceptionMessageRegex)
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
			
			if (null == maxEvents)
				maxEvents = 300;
			else if (maxEvents > 2000)
				maxEvents = 2000;
			
			// Build a regular expression to filter remote endpoints
			// If the user puts in IPs, then it will just match the IP and any port,
			// Otherwise, the specific port is matched
			Regex remoteEndpointsRegex = null;
			if (null != remoteEndpoints)
			{
				var regexStrings = new List<string>(remoteEndpoints.Length);
				foreach (var remoteEndpoint in remoteEndpoints)
				{
					if (remoteEndpoint.Contains(":"))
						regexStrings.Add(Regex.Escape(remoteEndpoint));
					else
						regexStrings.Add(string.Format(
							"{0}{1}*",
							Regex.Escape(remoteEndpoint),
							Regex.Escape(":")));
				}
				
				remoteEndpointsRegex = new Regex(string.Join("|", regexStrings.ToArray()));
			}
			
            IEnumerable<LoggingEvent> events = FileHandler.ReadLog(
				maxEvents.Value,
                classnames.ToHashSet(),
                maxTimeStamp,
                loggingLevels.ToHashSet(),
                threadIds.ToHashSet(),
                convertedSessionIds.ToHashSet(),
                userIds.ToHashSet(),
                messageRegex != null ? new Regex(messageRegex) : null,
                exceptionClassnames.ToHashSet(),
                exceptionMessageRegex != null ? new Regex(exceptionMessageRegex) : null,
				remoteEndpointsRegex);

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
        /// <param name="maxEvents"></param>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read, FilePermissionEnum.Read)]
		public IWebResults ReadMyLog(
            IWebConnection webConnection,
			int? maxEvents,
            string[] classnames,
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
				maxEvents,
				classnames,
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
	}
}
