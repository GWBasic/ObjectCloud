// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    public abstract partial class WebCallableMethod
    {
        public class POST_Multipart : WebCallableMethod
        {
            public POST_Multipart(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
                : base(methodInfo, webCallableAttribute, ObjectCloud.Interfaces.WebServer.WebMethod.POST) { }

            public override IWebResults CallMethod(IWebConnection webConnection, IWebHandlerPlugin webHandlerPlugin)
            {
                object[] arguments = new object[NumParameters];

                // Decode the arguments
                foreach (MimeReader.Part mimePart in webConnection.MimeReader)
                    if (ParameterIndexes.ContainsKey(mimePart.Name))
                    {
                        uint parameterIndex = ParameterIndexes[mimePart.Name];
                        arguments[parameterIndex] = mimePart;
                    }

                // The first argument is always the web connection
                arguments[0] = webConnection;

                object toReturn = MethodInfo.Invoke(webHandlerPlugin, arguments);
                return (IWebResults)toReturn;
            }
        }
    }
}