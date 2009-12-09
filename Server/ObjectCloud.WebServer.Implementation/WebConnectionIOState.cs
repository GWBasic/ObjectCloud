using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.WebServer.Implementation
{
    /// <summary>
    /// The state of the web connection's I/O
    /// </summary>
    internal enum WebConnectionIOState
    {
        /// <summary>
        /// The socket is currently transmitting the header
        /// </summary>
        ReadingHeader,

        /// <summary>
        /// The socket reader is parsing the header
        /// </summary>
        ParsingHeader,

        /// <summary>
        /// The socket is currently transmitting POST content
        /// </summary>
        ReadingContent,

        /// <summary>
        /// The socket is idle while a request is being performed
        /// </summary>
        PerformingRequest,

        /// <summary>
        /// The socket is disconnected
        /// </summary>
        Disconnected,

        /// <summary>
        /// The socket is idle, per HTTP it is waiting for the initial parts of the header to come
        /// </summary>
        Idle
    }
}
