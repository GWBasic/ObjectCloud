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
        public class GET_UrlEncoded : UrlEncoded
        {
            public GET_UrlEncoded(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
                : base(methodInfo, webCallableAttribute, ObjectCloud.Interfaces.WebServer.WebMethod.GET) { }

            public override IWebResults CallMethod(IWebConnection webConnection, IWebHandler webHandler)
            {
                return base.CallMethod(webConnection, webHandler, webConnection.GetParameters);
            }
        }
    }
}