// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Common.Logging;
using Common.Logging.Simple;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Logger
{
    public class Log : AbstractSimpleLogger
    {
		private static Dictionary<LogLevel, LoggingLevel> LogLevelMap = new Dictionary<LogLevel, LoggingLevel>();
		
		static Log()
		{
			LogLevelMap[LogLevel.Debug] = LoggingLevel.Debug;
			LogLevelMap[LogLevel.Error] = LoggingLevel.Error;
			LogLevelMap[LogLevel.Fatal] = LoggingLevel.Fatal;
			LogLevelMap[LogLevel.Info] = LoggingLevel.Info;
			LogLevelMap[LogLevel.Trace] = LoggingLevel.Trace;
			LogLevelMap[LogLevel.Warn] = LoggingLevel.Warn;
			
			// In case of weird situations:
			LogLevelMap[LogLevel.All] = LoggingLevel.Error;
			LogLevelMap[LogLevel.Off] = LoggingLevel.Error;
		}
		
        /// <summary>
        /// Creates and initializes a logger that writes messages to <see cref="Console.Out" />.
        /// </summary>
        /// <param name="logName">The name, usually type name of the calling class, of the logger.</param>
        /// <param name="logLevel">The current logging threshold. Messages recieved that are beneath this threshold will not be logged.</param>
        /// <param name="showLevel">Include the current log level in the log message.</param>
        /// <param name="showDateTime">Include the current time in the log message.</param>
        /// <param name="showLogName">Include the instance name in the log message.</param>
        /// <param name="dateTimeFormat">The date and time format to use in the log message.</param>
        public Log(string logName, LogLevel logLevel, bool showLevel, bool showDateTime, bool showLogName, string dateTimeFormat, LoggerFactoryAdapter loggerFactoryAdapter)
            : base(logName, logLevel, showLevel, showDateTime, showLogName, dateTimeFormat)
        {
			this.LoggerFactoryAdapter = loggerFactoryAdapter;
		}
		
		/// <summary>
		/// The LoggerFactoryAdapter 
		/// </summary>
		private readonly LoggerFactoryAdapter LoggerFactoryAdapter;

        /// <summary>
        /// The logging levels will be sent to stderr
        /// </summary>
        private static List<LogLevel> LevelsForSTDerror = new List<LogLevel>(new LogLevel[] {LogLevel.Error, LogLevel.Warn, LogLevel.Fatal});

        /// <summary>
        /// Do the actual logging by constructing the log message using a <see cref="StringBuilder" /> then
        /// sending the output to <see cref="Console.Out" />.
        /// </summary>
        /// <param name="level">The <see cref="LogLevel" /> of the message.</param>
        /// <param name="message">The log message.</param>
        /// <param name="e">An optional <see cref="Exception" /> associated with the message.</param>
        protected override void WriteInternal(LogLevel level, object message, Exception e)
        {
            IObjectCloudLogHandler logHandler = LoggerFactoryAdapter.ObjectCloudLogHandler;

            bool writeToConsole = true;

            if (null != logHandler)
            {
                writeToConsole = logHandler.WriteToConsole;
                logHandler.WriteLog(Name, LogLevelMap[level], LoggerFactoryAdapter.Session, LoggerFactoryAdapter.RemoteEndPoint, message.ToString(), e);
            }

            if (writeToConsole)
            {
                // Use a StringBuilder for better performance
                StringBuilder sb = new StringBuilder();
                FormatOutput(sb, level, message, e);

                // Print to the appropriate destination
                if (LevelsForSTDerror.Contains(level))
                    Console.Error.WriteLine(sb.ToString());
                else
                    NonBlockingConsoleWriter.Print(sb.ToString() + "\n");
            }
        }
    }
}
