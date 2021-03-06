// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using JsonFx.Json;

using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    public abstract partial class WebCallableMethod
    {
        public class POST_JSON : POST
        {
            public POST_JSON(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
                : base(methodInfo, webCallableAttribute) { }

            protected override object GetSecondArgument(IWebConnectionContent webConnectionContent)
            {
                JsonReader jsonReader = new JsonReader(webConnectionContent.AsStream());
                return jsonReader;
            }
        }
    }
}