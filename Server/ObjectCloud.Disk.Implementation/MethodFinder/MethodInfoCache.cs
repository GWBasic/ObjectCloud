// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    internal class MethodInfoCache : Cache<Type, IDictionary<string, WebCallableMethod>>
    {
        internal MethodInfoCache()
            : base(CreateForCache) { }

        private static IDictionary<string, WebCallableMethod> CreateForCache(Type type)
        {
            Dictionary<string, WebCallableMethod> toReturn = new Dictionary<string, WebCallableMethod>();

            IEnumerable<MethodInfo> allMethods =
                type.GetMethods(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public);

            foreach (MethodInfo methodInfo in allMethods)
            {
                // Only method that have a return type of IWebResults...
                if (methodInfo.ReturnType == typeof(IWebResults))
                {
                    ParameterInfo[] parameterInfos = methodInfo.GetParameters();

                    // and at least 1 parameters
                    if (parameterInfos.Length > 0)

                        // and take an argument of type IWebConnection as the first argument
                        if (typeof(IWebConnection) == parameterInfos[0].ParameterType)
                        {
                            WebCallableAttribute webCallableAttribute = null;

                            foreach (WebCallableAttribute wca in methodInfo.GetCustomAttributes(typeof(WebCallableAttribute), true))
                                webCallableAttribute = wca;

                            // and have a [WebCallable... attribute
                            if (null != webCallableAttribute)
                            {
                                switch (webCallableAttribute.WebCallingConvention)
                                {
                                    case WebCallingConvention.GET:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.GET(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.GET_application_x_www_form_urlencoded:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.GET_UrlEncoded(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.POST_application_x_www_form_urlencoded:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_UrlEncoded(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.POST_bytes:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_bytes(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.POST_JSON:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_JSON(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.POST_multipart_form_data:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_Multipart(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.POST_stream:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_stream(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.POST_string:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.POST_string(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.other:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.Other(methodInfo, webCallableAttribute);
                                        break;

                                    case WebCallingConvention.Naked:
                                        toReturn[methodInfo.Name] = new WebCallableMethod.Naked(methodInfo, webCallableAttribute);
                                        break;

                                    default:
                                        throw new WebServerException("Unknown calling convention: " + webCallableAttribute.ToString());
                                }
                            }
                        }
                }
            }

            return toReturn;

        }
    }
}
