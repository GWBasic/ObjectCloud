// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Spring.Context;

namespace ObjectCloud.WebServer.Test.PermissionsTests
{
    public abstract class HasSecondContext : PermissionTest
    {
        private static IApplicationContext _SecondContext = null;

        /// <summary>
        /// The second context, statically cached
        /// </summary>
        public IApplicationContext SecondContext
        {
            get
            {
                if (null == _SecondContext)
                    _SecondContext = this.LoadContext("Test.SecondWebServer.ObjectCloudConfig.xml");

                return HasSecondContext._SecondContext;
            }
        }
    }
}
