// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.DataAccess.Log;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

using ILog = Common.Logging.ILog;

namespace ObjectCloud.Disk.FileHandlers
{
	public class LogHandler : FileHandler, IObjectCloudLogHandler
	{
        public LogHandler(PersistedObjectSequence<LoggingEvent> sequence, FileHandlerFactoryLocator fileHandlerFactoryLocator, bool writeToConsole, DelegateQueue delegateQueue)
            : base(fileHandlerFactoryLocator) 
		{
			this.sequence = sequence;
            this.delegateQueue = delegateQueue;
            this.writeToConsole = writeToConsole;
			
			this.sequence.ReadSequence(DateTime.MaxValue, 1, loggingEvent =>
			{
				if (null != loggingEvent.Classname)
					this.classNames.Add(loggingEvent.Classname);
				
				if (null != loggingEvent.ExceptionClassname)
					this.classNames.Add(loggingEvent.ExceptionClassname);
				
				return false;
			});
		}
		
		private readonly PersistedObjectSequence<LoggingEvent> sequence;
				
		public override void OnDelete (ObjectCloud.Interfaces.Security.IUser changer)
		{
			new ObjectCloud.Disk.Factories.FileSystem().RecursiveDelete(this.sequence.DirectoryName);
		}

        public bool WriteToConsole
        {
            get { return writeToConsole; }
        }
        private bool writeToConsole;

        public override string Title 
		{
        	get { return "Log"; }
        }
		
        public override void Dump (string path, ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId)
        {
        	throw new System.NotImplementedException();
        }

        /// <summary>
        /// Asyncronously runs delegates on a queue where they can't block each other
        /// </summary>
        private DelegateQueue delegateQueue;

		public void WriteLog(
			string className, 
			LoggingLevel logLevel, 
			ISession session, 
			EndPoint remoteEndPoint,
			string message, 
			Exception exception)
        {
			Thread callingThread = Thread.CurrentThread;

			// Always write to the log on a separate thread so it doesn't block the caller
            // But drop logging items if it's going to impede performance
            if (delegateQueue.QueuedDelegatesCount < delegateQueue.BusyThreshold - 5)
    			delegateQueue.QueueUserWorkItem(state => WriteLog(callingThread, className, logLevel, session, remoteEndPoint, message, exception));
        }

		/// <summary>
		/// Inserts into the log.  This is intended to be called asyncronously from the threadpool
		/// </summary>
		/// <param name="timestamp">
		/// A <see cref="DateTime"/>
		/// </param>
		/// <param name="callingThread">
		/// A <see cref="Thread"/>
		/// </param>
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
		private void WriteLog (
			Thread callingThread, 
			string className, 
			LoggingLevel logLevel, 
			ISession session, 
			EndPoint remoteEndPoint,
			string message, 
			Exception exception)
		{
			try
			{
				var loggingEvent = new LoggingEvent()
				{
					TimeStamp = DateTime.UtcNow,
					Classname = className,
					ExceptionClassname = null != exception ? exception.GetType().FullName : null,
					ExceptionMessage = null != exception ? exception.Message : null,
					ExceptionStackTrace = null != exception ? exception.StackTrace : null,
					Level = logLevel,
					Message = message,
					SessionId = null != session ? (ID<ISession, Guid>?)session.SessionId : (ID<ISession, Guid>?)null,
					ThreadId = callingThread.ManagedThreadId,
					UserId = null != session ? (ID<IUserOrGroup, Guid>?)session.User.Id : (ID<IUserOrGroup, Guid>?)null,
					RemoteEndPoint = null != remoteEndPoint ? remoteEndPoint.ToString() : null
				};
				
				// Update the classnames in a thread-safe manner
				if (!this.classNames.Contains(className))
				{
					var classNames = new HashSet<string>(this.classNames);
					classNames.Add(className);
					
					this.classNames = classNames;
				}
				
				if (null != loggingEvent.ExceptionClassname)
					if (!this.classNames.Contains(loggingEvent.ExceptionClassname))
					{
						var classNames = new HashSet<string>(this.classNames);
						classNames.Add(loggingEvent.ExceptionClassname);
						
						this.classNames = classNames;
					}
				
				this.sequence.Append(loggingEvent);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Exception when writing to the log:\n" + e.Message);
			}
		}

        public IEnumerable<LoggingEvent> ReadLog(
			int maxEvents,
			HashSet<string> classnames,
			DateTime? maxTimeStamp,
			HashSet<LoggingLevel> loggingLevels,
			HashSet<int> threadIds,
			HashSet<ID<ISession, Guid>> sessionIds,
			HashSet<ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid>> userIds,
            Regex messageRegex,
            HashSet<string> exceptionClassnames,
            Regex exceptionMessageRegex,
			Regex remoteEndpointsRegex)
        {
			return this.sequence.ReadSequence(
				maxTimeStamp != null ? maxTimeStamp.Value : DateTime.MaxValue,
				maxEvents,
				loggingEvent => 
			{
				if (null != classnames)
					if (!classnames.Contains(loggingEvent.Classname))
						return false;
				
				if (null != loggingLevels)
					if (!loggingLevels.Contains(loggingEvent.Level))
						return false;
				
				if (null != threadIds)
					if (!threadIds.Contains(loggingEvent.ThreadId))
						return false;
				
				if (null != sessionIds)
				{
					if (null == loggingEvent.SessionId)
						return false;
					
					if (!sessionIds.Contains(loggingEvent.SessionId.Value))
						return false;
				}
				
				if (null != userIds)
				{
					if (null == loggingEvent.UserId)
						return false;
					
					if (!userIds.Contains(loggingEvent.UserId.Value))
						return false;
				}
				
	            if (null != messageRegex)
				{
					if (null == loggingEvent.Message)
						return false;
					
					if (!messageRegex.IsMatch(loggingEvent.Message))
						return false;
				}
				
				if (null != exceptionClassnames)
					if (!exceptionClassnames.Contains(loggingEvent.ExceptionClassname))
						return false;
	
	            if (null != exceptionMessageRegex)
				{
					if (null == loggingEvent.ExceptionMessage)
						return false;
					
					if (!exceptionMessageRegex.IsMatch(loggingEvent.ExceptionMessage))
						return false;
				}
				
				if (null != remoteEndpointsRegex)
				{
					if (null == loggingEvent.RemoteEndPoint)
						return false;
					
					if (!remoteEndpointsRegex.IsMatch(loggingEvent.RemoteEndPoint))
						return false;
				}
				
				return true;
			});
        }

        public IEnumerable<string> ClassNames
		{
			get 
			{
				foreach (var className in this.classNames)
					yield return className;
			}
        }
		private HashSet<string> classNames = new HashSet<string>();
    }
}
