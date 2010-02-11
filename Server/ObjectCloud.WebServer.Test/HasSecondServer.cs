// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Spring.Context;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Test;

namespace ObjectCloud.WebServer.Test
{
    public abstract class HasSecondServer : WebServerTestBase
    {
        /// <summary>
        /// The second context, statically cached
        /// </summary>
        public IApplicationContext SecondContext
        {
            get
            {
                if (null == _SecondContext)
                    _SecondContext = this.LoadContext("Test.SecondWebServer.ObjectCloudConfig.xml");

                return _SecondContext;
            }
        }
        private static IApplicationContext _SecondContext = null;

        /// <summary>
        /// The second web server object for loopback OpenID tests
        /// </summary>
        public IWebServer SecondWebServer
        {
            get { return _SecondWebServer; }
            set { _SecondWebServer = value; }
        }
        private IWebServer _SecondWebServer;

        protected override void DoAdditionalSetup()
        {
            base.DoAdditionalSetup();

            SecondWebServer = (IWebServer)SecondContext.GetObject("WebServer");
            SecondWebServer.StartServer();
        }

        protected override void DoAdditionalTearDown()
        {
            SecondWebServer.Dispose();

            base.DoAdditionalTearDown();
        }

        public FileHandlerFactoryLocator SecondFileHandlerFactoryLocator
        {
            get
            {
                if (null == _SecondFileHandlerFactoryLocator)
                    _SecondFileHandlerFactoryLocator = (FileHandlerFactoryLocator)SecondContext["FileHandlerFactoryLocator"];

                return _SecondFileHandlerFactoryLocator;
            }
            set { _SecondFileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _SecondFileHandlerFactoryLocator = null;
    }
}
