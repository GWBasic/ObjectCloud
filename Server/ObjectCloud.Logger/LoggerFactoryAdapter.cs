// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;

using Common.Logging;
using Common.Logging.Simple;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

using LogLevel = Common.Logging.LogLevel;

namespace ObjectCloud.Logger
{
    public class LoggerFactoryAdapter : AbstractSimpleLoggerFactoryAdapter, IObjectCloudLoggingFactoryAdapter
    {
        public LoggerFactoryAdapter(NameValueCollection properties)
            : base(properties)
        { }

        public LoggerFactoryAdapter()
            : base(null)
        { }

        /// <summary>
        /// Creates a new <see cref="Log"/> instance.
        /// </summary>
        protected override ILog CreateLogger(string name, LogLevel level, bool showLevel, bool showDateTime, bool showLogName, string dateTimeFormat)
        {
            ILog log = new Log(name, level, showLevel, showDateTime, showLogName, dateTimeFormat, this);
            return log;
		}

		/// <value>
      	/// The log handler
      	/// </value>
		public IObjectCloudLogHandler ObjectCloudLogHandler
		{
			get { return _ObjectCloudLogHandler; }
			set { _ObjectCloudLogHandler = value; }
		}
		private IObjectCloudLogHandler _ObjectCloudLogHandler = null;

		/// <value>
		/// The current session 
		/// </value>
	    public ISession Session 
		{
        	get { return _Session; }
        	set { _Session = value; }
        }
		[ThreadStatic]
	    private ISession _Session = null;
		
		/// <value>
		/// The current thread's IP 
		/// </value>
	    public EndPoint RemoteEndPoint
		{
        	get { return _RemoteEndPoint; }
        	set { _RemoteEndPoint = value; }
        }
		[ThreadStatic]
		private EndPoint _RemoteEndPoint = null;
    }
}
