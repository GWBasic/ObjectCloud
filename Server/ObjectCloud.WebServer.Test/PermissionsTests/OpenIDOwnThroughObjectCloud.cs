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
using ObjectCloud.WebServer.Test;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.WebServer.Test.PermissionsTests
{
    [TestFixture]
    public class OpenIDOwnThroughObjectCloud : HasSecondContext
    {
		protected override void DoAdditionalSetup ()
		{
            base.DoAdditionalSetup();

            SecondWebServer.StartServer();
        }

		protected override void DoAdditionalTearDown ()
		{
            SecondWebServer.Dispose();

            base.DoAdditionalTearDown();
        }

        protected override IUserLogoner Owner
        {
            get
            {
                if (null == _Owner)
                    _Owner = new OpenIDLogonerThroughObjectCloud("owner" + SRandom.Next().ToString(), SRandom.Next<long>().ToString(), WebServer, SecondWebServer);

                return _Owner;
            }
        }
        private IUserLogoner _Owner = null;

        protected override IUserLogoner Accessor
        {
            get
            {
                if (null == _Accessor)
                    _Accessor = new OpenIDLogonerThroughObjectCloud("accessor" + SRandom.Next().ToString(), SRandom.Next<long>().ToString(), SecondWebServer);

                return _Accessor;
            }
        }
        private IUserLogoner _Accessor = null;
        
        [Test]
        public new void TestRead()
        {
        	base.TestRead();
        }
        
        [Test]
        public new void TestWrite()
        {
        	base.TestWrite();
        }
    }
}
