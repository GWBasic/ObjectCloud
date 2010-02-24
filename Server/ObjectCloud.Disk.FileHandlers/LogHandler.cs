using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;

using ObjectCloud.DataAccess.Log;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

using ILog = Common.Logging.ILog;

namespace ObjectCloud.Disk.FileHandlers
{
	public class LogHandler : HasDatabaseFileHandler<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction>, IObjectCloudLogHandler
	{
        private static ILog log = LogManager.GetLogger<LogHandler>();

        public LogHandler(IDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator, bool writeToConsole)
            : base(databaseConnector, fileHandlerFactoryLocator) 
		{
			foreach (IClasses_Readable classNameAndId in DatabaseConnection.Classes.Select())
			{
				ClassNameIds[classNameAndId.Name] = classNameAndId.ClassId;
				ClassNamesById[classNameAndId.ClassId] = classNameAndId.Name;
			}

            DelegateQueue.QueueUserWorkItem(DeleteOldLogEntries);

            _WriteToConsole = writeToConsole;
		}

        public bool WriteToConsole
        {
            get { return _WriteToConsole; }
        }
        private bool _WriteToConsole;

        public override string Title 
		{
        	get { return "Log"; }
        }

		/// <summary>
		/// Association of class names and IDs
		/// </summary>
		/// <param name="path">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="userId">
		/// A <see cref="ObjectCloud.Common.ID"/>
		/// </param>
		private Dictionary<string, long> ClassNameIds = new Dictionary<string, long>();
		
		/// <summary>
		/// Association that goes from ClassNameId back to the name
		/// </summary>
		/// <param name="className">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.Int64"/>
		/// </returns>
		private Dictionary<long, string> ClassNamesById = new Dictionary<long, string>();
		
		/// <summary>
		/// Returns the appropriate ID for the class name, inserting a row in the foregn key table if needed
		/// </summary>
		/// <param name="className">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.Int32"/>
		/// </returns>
		private long GetClassNameId(string className)
		{
			long toReturn = long.MinValue;
			if (ClassNameIds.TryGetValue(className, out toReturn))
				return toReturn;
			
			toReturn = DatabaseConnection.Classes.InsertAndReturnPK<long>(delegate(IClasses_Writable classesWritable)
			{
				classesWritable.Name = className;
			});
			
			ClassNameIds[className] = toReturn;
			ClassNamesById[toReturn] = className;
			return toReturn;
		}
		
		/// <summary>
		/// Returns all of the ids for the passed in class names
		/// </summary>
		/// <param name="classNames">
		/// A <see cref="IEnumerable"/>
		/// </param>
		/// <returns>
		/// A <see cref="IEnumerable"/>
		/// </returns>
		private IEnumerable<long> GetClassNameIds(IEnumerable<string> classNames)
		{
			foreach (string className in classNames)
				yield return GetClassNameId(className);
		}
		
        public override void Dump (string path, ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId)
        {
        	throw new System.NotImplementedException();
        }

        /// <summary>
        /// Asyncronously runs delegates on a queue where they can't block each other
        /// </summary>
        private DelegateQueue DelegateQueue = new DelegateQueue();

		public void WriteLog(
			string className, 
			LoggingLevel logLevel, 
			ISession session, 
			EndPoint remoteEndPoint,
			string message, 
			Exception exception)
        {
			Thread callingThread = Thread.CurrentThread;
			DateTime timestamp = DateTime.UtcNow;

			// Always write to the log on the threadpool so it doesn't block the caller
			DelegateQueue.QueueUserWorkItem(delegate(object state)
			{
				WriteLog(timestamp, callingThread, className, logLevel, session, remoteEndPoint, message, exception);
			});
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
			DateTime timestamp, 
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
				long classId = GetClassNameId(className);
				long? exceptionClassId = null;
				string exceptionMessage = null;
                string exceptionStackTrace = null;
				
				if (null != exception)
				{
					exceptionClassId = GetClassNameId(exception.GetType().FullName);
					exceptionMessage = exception.Message;
                    exceptionStackTrace = exception.StackTrace;
				}
				
				DatabaseConnection.Log.Insert(delegate(ILog_Writable logWritable)
				{
					logWritable.ClassId = classId;
					logWritable.ExceptionClassId = exceptionClassId;
					logWritable.ExceptionMessage = exceptionMessage;
                    logWritable.ExceptionStackTrace = exceptionStackTrace;
					logWritable.Level = logLevel;
					logWritable.Message = message;
					
					if (null != session)
					{
						logWritable.SessionId = session.SessionId;
						logWritable.UserId = session.User.Id;
					}
					
					if (null != remoteEndPoint)
						logWritable.RemoteEndPoint = remoteEndPoint.ToString();
					
					logWritable.ThreadId = callingThread.ManagedThreadId;
					logWritable.TimeStamp = timestamp;
				});

                if (DateTime.UtcNow > NextTimeToDeleteOldLogEntries)
                    DeleteOldLogEntries(null);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Exception when writing to the log:\n" + e.Message);
			}
		}

        /// <summary>
        /// The next time that old log entries should be deleted
        /// </summary>
        private DateTime NextTimeToDeleteOldLogEntries = DateTime.MaxValue;

        public IDictionary<LoggingLevel, TimeSpan> GetLoggingTimespans()
        {
            Dictionary<LoggingLevel, TimeSpan> toReturn = new Dictionary<LoggingLevel, TimeSpan>();

            foreach (ILifespan_Readable lifespan in DatabaseConnection.Lifespan.Select())
                toReturn[lifespan.Level] = lifespan.Timespan;

            return toReturn;
        }

        public void UpdateLoggingTimespans(IDictionary<LoggingLevel, TimeSpan> loggingTimeSpans)
        {
            foreach (LoggingLevel level in loggingTimeSpans.Keys)
                DatabaseConnection.Lifespan.Update(
                    Lifespan_Table.Level == level,
                    delegate(ILifespan_Writable lifespan)
                    {
                        lifespan.Timespan = loggingTimeSpans[level];
                    });
        }

        /// <summary>
        /// Deletes old log entries.  Intended to be called from a timer
        /// </summary>
        /// <param name="state"></param>
        void DeleteOldLogEntries(object state)
        {
            try
            {
                IDictionary<LoggingLevel, TimeSpan> loggingTimespans = GetLoggingTimespans();

                foreach (LoggingLevel level in loggingTimespans.Keys)
                {
                    DateTime threashold = DateTime.UtcNow - loggingTimespans[level];

                    DateTime start = DateTime.UtcNow;

                    DatabaseConnection.Log.Delete(
                        (Log_Table.Level == level) & (Log_Table.TimeStamp < threashold));

                    TimeSpan deleteTime = DateTime.UtcNow - start;

                    log.InfoFormat("Deleting log entries for {0} took {1}", level, deleteTime.ToString());
                }

                NextTimeToDeleteOldLogEntries = DateTime.UtcNow.AddMinutes(5);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error when cleaning the logs\n" + e.ToString());
            }
        }

        public IEnumerable<LoggingEvent> ReadLog(
			IEnumerable<string> classnames,
			DateTime? minTimeStamp,
			DateTime? maxTimeStamp,
			IEnumerable<LoggingLevel> loggingLevels,
			IEnumerable<int> threadIds,
			IEnumerable<ID<ISession, Guid>> sessionIds,
			IEnumerable<ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid>> userIds,
            string messageLike,
            IEnumerable<string> exceptionClassnames,
            string exceptionMessageLike)
        {
			List<ComparisonCondition> comparisonConditions = new List<ComparisonCondition>();
			
			if (null != classnames)
				comparisonConditions.Add(Log_Table.ClassId.In(GetClassNameIds(classnames)));
			
			if (null != minTimeStamp)
				comparisonConditions.Add(Log_Table.TimeStamp >= minTimeStamp.Value);
			
			if (null != maxTimeStamp)
				comparisonConditions.Add(Log_Table.TimeStamp <= maxTimeStamp.Value);
			
			if (null != loggingLevels)
				comparisonConditions.Add(Log_Table.Level.In(loggingLevels));
			
			if (null != threadIds)
				comparisonConditions.Add(Log_Table.ThreadId.In(threadIds));
			
			if (null != sessionIds)
				comparisonConditions.Add(Log_Table.SessionId.In(sessionIds));
			
			if (null != userIds)
				comparisonConditions.Add(Log_Table.UserId.In(userIds));

            if (null != messageLike)
                comparisonConditions.Add(Log_Table.Message.Like(messageLike));
			
			if (null != exceptionClassnames)
				comparisonConditions.Add(Log_Table.ExceptionClassId.In(GetClassNameIds(exceptionClassnames)));

            if (null != exceptionMessageLike)
                comparisonConditions.Add(Log_Table.ExceptionMessage.Like(exceptionMessageLike));

            foreach (ILog_Readable logReadable in DatabaseConnection.Log.Select(
                ComparisonCondition.Condense(comparisonConditions), null, ObjectCloud.ORM.DataAccess.OrderBy.Desc, Log_Table.TimeStamp))
			{
				LoggingEvent toYeild = new LoggingEvent();
				
				toYeild.Classname = ClassNamesById[logReadable.ClassId];
				
				if (null != logReadable.ExceptionClassId)
					toYeild.ExceptionClassname = ClassNamesById[logReadable.ExceptionClassId.Value];
				
				toYeild.ExceptionMessage = logReadable.ExceptionMessage;
                toYeild.ExceptionStackTrace = logReadable.ExceptionStackTrace;
				toYeild.Level = logReadable.Level;
				toYeild.Message = logReadable.Message;
				toYeild.SessionId = logReadable.SessionId;
				toYeild.ThreadId = logReadable.ThreadId;
				toYeild.TimeStamp = logReadable.TimeStamp;
				toYeild.UserId = logReadable.UserId;

                toYeild.RemoteEndPoint = logReadable.RemoteEndPoint;
				
				yield return toYeild;
			}
        }

        public IEnumerable<string> ClassNames
		{
			get { return ClassNameIds.Keys; }
        }
    }
}
