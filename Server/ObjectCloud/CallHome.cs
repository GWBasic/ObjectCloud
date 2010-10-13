// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;

using SignalHandller;

using Common.Logging;
using Spring.Context;
using Spring.Context.Support;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.Spring.Config;

namespace ObjectCloud
{
    public static class CallHome
    {
        private static ILog log = LogManager.GetLogger(typeof(CallHome));

        public static void StartCallHome(FileHandlerFactoryLocator fileHandlerFactoryLocator)
        {
            if (null == fileHandlerFactoryLocator.CallHomeEndpoint)
                return;

            // Only call home when running on port 80
            if (80 != fileHandlerFactoryLocator.WebServer.Port)
                return;

            FileHandlerFactoryLocator = fileHandlerFactoryLocator;

            // Call home every hour
            Timer = new Timer(DoCallHome, null, 0, 3600000);
        }

        private static FileHandlerFactoryLocator FileHandlerFactoryLocator;

        private static Timer Timer;

        private static void DoCallHome(object state)
        {
            HttpWebClient client = new HttpWebClient();

            log.Info("Calling home to " + FileHandlerFactoryLocator.CallHomeEndpoint);

            client.BeginPost(
                FileHandlerFactoryLocator.CallHomeEndpoint,
                delegate(HttpResponseHandler response)
                {
                    log.Info("Successfully called home to " + FileHandlerFactoryLocator.CallHomeEndpoint);
                },
                delegate(Exception e)
                {
                    log.Error("Exception when calling home to " + FileHandlerFactoryLocator.CallHomeEndpoint, e);
				
					// no-op for strict compiler
					if (null == Timer)
					{}
                },
                new KeyValuePair<string, string>("host", FileHandlerFactoryLocator.Hostname));
        }
    }
}
