// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.Spring.Config;
using ObjectCloud.WebServer.Test;

namespace ObjectCloud.WebServer.Test
{
    public abstract class HasThirdServer : HasSecondServer
    {
        public FileHandlerFactoryLocator ThirdFileHandlerFactoryLocator
        {
            get
            {
                if (null == _ThirdFileHandlerFactoryLocator)
                    _ThirdFileHandlerFactoryLocator =
                        ContextLoader.GetFileHandlerFactoryLocatorForConfigurationFile("Test.ThirdWebServer.ObjectCloudConfig.xml");

                return _ThirdFileHandlerFactoryLocator;
            }
        }
        private FileHandlerFactoryLocator _ThirdFileHandlerFactoryLocator = null;

        /// <summary>
        /// The third web server object for loopback OpenID tests
        /// </summary>
        public IWebServer ThirdWebServer
        {
            get { return ThirdFileHandlerFactoryLocator.WebServer; }
        }

        protected override void DoAdditionalSetup()
        {
            base.DoAdditionalSetup();

            ThirdWebServer.StartServer();
        }

        protected override void DoAdditionalTearDown()
        {
            ThirdWebServer.Dispose();

            base.DoAdditionalTearDown();
        }
    }
}
