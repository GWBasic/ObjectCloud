using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Represents an event that was logged into ObjectCloud
    /// </summary>
    [Serializable]
	public class LoggingEvent : IHasTimeStamp
    {
        public string Classname { get; set; }
        public DateTime TimeStamp { get; set; }
        public LoggingLevel Level { get; set; }
        public int ThreadId { get; set; }
        public ID<ISession, Guid>? SessionId { get; set; }
        public ID<IUserOrGroup, Guid>? UserId { get; set; }
        public string Message { get; set; }
        public string ExceptionClassname { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
        public string RemoteEndPoint { get; set; }
    }
}
