// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using NUnit.Framework;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.WebServer.Test.PermissionsTests
{
    public abstract class HasSecondContext : PermissionTest
    {
        /// <summary>
        /// The second web server object for loopback OpenID tests
        /// </summary>
        public IWebServer SecondWebServer
        {
            get { return SecondFileHandlerFactoryLocator.WebServer; }
        }

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
    }
}
