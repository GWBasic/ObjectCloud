// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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
    public class LocalAccessThroughGroup : PermissionTest
    {
        protected override IUserLogoner Owner
        {
            get
            {
                if (null == _Owner)
                    _Owner = new LocalUserLogoner("owner" + SRandom.Next().ToString(), SRandom.Next<long>().ToString(), WebServer);

                return _Owner;
            }
        }
        private LocalUserLogoner _Owner = null;

        protected override IUserLogoner Accessor
        {
            get
            {
                if (null == _Accessor)
                    _Accessor = new LocalUserLogoner("accessor" + SRandom.Next().ToString(), SRandom.Next<long>().ToString(), WebServer);

                return _Accessor;
            }
        }
        private LocalUserLogoner _Accessor = null;
        
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
