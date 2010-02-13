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
        public class Naked : WebCallableMethod
        {
            public Naked(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
                : base(methodInfo, webCallableAttribute, null) { }

            public override IWebResults CallMethod(IWebConnection webConnection, IWebHandlerPlugin webHandlerPlugin)
            {
                object toReturn;

                try
                {
                    toReturn = MethodInfo.Invoke(webHandlerPlugin, new object[] { webConnection });
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