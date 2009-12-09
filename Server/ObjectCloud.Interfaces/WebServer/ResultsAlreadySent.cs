using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Thrown when an attempt is made to send duplicate results over a web connection
    /// </summary>
    public class ResultsAlreadySent : WebServerException
    {
        public ResultsAlreadySent(string message) : base(message) { }
    }
}
