// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    public abstract partial class WebCallableMethod
    {
        public class GET_UrlEncoded : UrlEncoded
        {
            public GET_UrlEncoded(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
                : base(methodInfo, webCallableAttribute, ObjectCloud.Interfaces.WebServer.WebMethod.GET) { }

            public override IWebResults CallMethod(IWebConnection webConnection, IWebHandlerPlugin webHandlerPlugin)
            {
                return base.CallMethod(webConnection, webHandlerPlugin, webConnection.GetParameters);
            }
        }
    }
}