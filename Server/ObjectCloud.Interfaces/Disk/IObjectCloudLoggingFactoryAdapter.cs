using System;
using System.Net;

using Common.Logging;

using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
	/// <summary>
	/// Interface for ObjectCloud-aware loggers that can log into ObjectCloud's file system
	/// </summary>
	public interface IObjectCloudLoggingFactoryAdapter : ILoggerFactoryAdapter
	{
		/// <value>
		/// ObjectCloud's log file handler
		/// </value>
		IObjectCloudLogHandler ObjectCloudLogHandler { get; set; }
		
		/// <value>
		/// Threadstatic value that indicates the current session.  Set this value when the session is known, set back to null when the session isn't known.
		/// </value>
		ISession Session { get; set; }
		
		/// <value>
		/// Threadstatic value that indicates that current IP.  Set this when the remote IP is known, and set back to null when it isn't known.
		/// </value>
		EndPoint RemoteEndPoint { get; set; }
	}
}
