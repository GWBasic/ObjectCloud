using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Represents an event that was logged into ObjectCloud
    /// </summary>
    public struct LoggingEvent
    {
        public string Classname;
        public DateTime TimeStamp;
        public LoggingLevel Level;
        public int ThreadId;
        public ID<ISession, Guid>? SessionId;
        public ID<IUserOrGroup, Guid>? UserId;
        public string Message;
        public string ExceptionClassname;
        public string ExceptionMessage;
        public string ExceptionStackTrace;
        public string RemoteEndPoint;
    }
}
