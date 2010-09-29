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
    public abstract class HasSecondServer : WebServerTestBase
    {
        public FileHandlerFactoryLocator SecondFileHandlerFactoryLocator
        {
            get
            {
                if (null == _SecondFileHandlerFactoryLocator)
                    _SecondFileHandlerFactoryLocator =
                        ContextLoader.GetFileHandlerFactoryLocatorForConfigurationFile("Test.SecondWebServer.ObjectCloudConfig.xml");

                return _SecondFileHandlerFactoryLocator;
            }
        }
        private FileHandlerFactoryLocator _SecondFileHandlerFactoryLocator = null;

        /// <summary>
        /// The second web server object for loopback OpenID tests
        /// </summary>
        public IWebServer SecondWebServer
        {
            get { return SecondFileHandlerFactoryLocator.WebServer; }
        }

        protected override void DoAdditionalSetup()
        {
            base.DoAdditionalSetup();

            SecondWebServer.StartServer();
        }

        protected override void DoAdditionalTearDown()
        {
            SecondWebServer.Dispose();

            base.DoAdditionalTearDown();
        }
    }
}
