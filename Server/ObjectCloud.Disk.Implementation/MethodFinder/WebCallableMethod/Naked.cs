// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    public abstract partial class WebCallableMethod
    {
        public class Naked : WebCallableMethod
        {
            public Naked(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
                : base(methodInfo, webCallableAttribute, null) { }

            public override IWebResults CallMethod(IWebConnection webConnection, IWebHandlerPlugin webHandlerPlugin)
            {
                object[] arguments = new object[]
                {
                    webConnection,
                };

                object toReturn = MethodInfo.Invoke(webHandlerPlugin, arguments);
                return (IWebResults)toReturn;
            }
        }
    }
}