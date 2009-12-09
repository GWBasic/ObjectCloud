// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    public class BlockingWebConnection : WebConnection
    {
		private static ILog log = LogManager.GetLogger(typeof(BlockingWebConnection));

        /// <summary>
        /// Initializes the WebConnection
        /// </summary>
        /// <param name="s"></param>
        /// <param name="webServer"></param>
        public BlockingWebConnection(IWebServer webServer, Socket socket, NetworkStream networkStream)
            : base(webServer, socket)
        {
            NetworkStream = networkStream;
        }

        /// <summary>
        /// Helper object to read from the socket
        /// </summary>
        internal NetworkStream NetworkStream;

    }
}
