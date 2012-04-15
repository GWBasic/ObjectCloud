using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
	/// <summary>
	/// Interface for file handlers that can act as a log
	/// </summary>
	public interface IObjectCloudLogHandler : IFileHandler
	{
		/// <summary>
		/// Writes an entry into the log
		/// </summary>
		/// <param name="className">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="logLevel">
		/// A <see cref="LogLevel"/>
		/// </param>
		/// <param name="session">
		/// A <see cref="ISession"/>
		/// </param>
		/// <param name="message">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="exception">
		/// A <see cref="Exception"/>
		/// </param>
		void WriteLog(
			string className,
		    LoggingLevel logLevel,
		    ISession session,
		    EndPoint remoteEndPoint,
		    string message,
		    Exception exception);

        /// <summary>
        /// Indicates that the logger should write to the console.  This should default to false as the console can really slow down the server under load
        /// </summary>
        bool WriteToConsole { get; }

        /// <summary>
        /// Reads the log and returns events that occured
        /// </summary>
        /// <param name="classnames"></param>
        /// <param name="minTimeStamp"></param>
        /// <param name="maxTimeStamp"></param>
        /// <param name="loggingLevels"></param>
        /// <param name="threadIds"></param>
        /// <param name="sessionIds"></param>
        /// <param name="userIds"></param>
        /// <param name="exceptionClassnames"></param>
        /// <returns></returns>
        IEnumerable<LoggingEvent> ReadLog(
			int maxEvents,
            HashSet<string> classnames,
            DateTime? maxTimeStamp,
            HashSet<LoggingLevel> loggingLevels,
            HashSet<int> threadIds,
            HashSet<ID<ISession, Guid>> sessionIds,
            HashSet<ID<IUserOrGroup, Guid>> userIds,
            Regex messageRegex,
            HashSet<string> exceptionClassnames,
            Regex exceptionMessageRegex);

		/// <summary>
		/// Returns the class names that are currently used
		/// </summary>
		IEnumerable<string> ClassNames { get; }
	}
}
