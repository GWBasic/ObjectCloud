// Copyright 2009, 2010 Andrew Rondeau
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
        public abstract class POST : WebCallableMethod
        {
            public POST(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
                : base(methodInfo, webCallableAttribute, ObjectCloud.Interfaces.WebServer.WebMethod.POST) { }

            /// <summary>
            /// Returns the correct value from the webConnectionContent
            /// </summary>
            /// <param name="webConnectionContent"></param>
            /// <returns></returns>
            protected abstract object GetSecondArgument(IWebConnectionContent webConnectionContent);

            public override IWebResults CallMethod(IWebConnection webConnection, IWebHandlerPlugin webHandlerPlugin)
            {
                if (null == webConnection.Content)
                    return WebResults.FromString(Status._400_Bad_Request, "No data sent");

                object toReturn;

                try
                {
                    toReturn = MethodInfo.Invoke(webHandlerPlugin, new object[] { webConnection, GetSecondArgument(webConnection.Content) });
                }
                catch (TargetInvocationException e)
                {
                    // Invoke wraps exceptions
                    throw e.InnerException;
                }

                return (IWebResults)toReturn;
            }
        }
    }
}