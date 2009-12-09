// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Spring.Context;
using Spring.Context.Support;

using ObjectCloud.Spring.Config;

namespace ObjectCloud.ORM.DataAccess.SQLite.Test
{
    public abstract class TestBase : ObjectCloud.UnitTestHelpers.UnitTestBase
    {
        public TestBase() : base ("file://Database.xml") {}
    }
}
