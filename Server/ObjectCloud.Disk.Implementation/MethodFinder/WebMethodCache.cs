// Copyright 2009 - 2012 Andrew Rondeau
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
    /// <summary>
    /// Cache of web methods associated with their name and handler
    /// </summary>
    public class WebMethodCache : Cache<MethodNameAndFileContainer, DelegateWrapper>, IWebMethodCache
    {
        public WebMethodCache()
            : base(CreateForCache) { }

        /// <summary>
        /// Cache of all method infos from types
        /// </summary>
        static MethodInfoCache MethodInfoCache = new MethodInfoCache();

        private static DelegateWrapper CreateForCache(MethodNameAndFileContainer key)
        {
            string methodName = key.MethodName;

            IDictionary<string, WebCallableMethod> methods = WebMethodCache.MethodInfoCache[key.WebHandlerPlugin.GetType()];

            if (!methods.ContainsKey(methodName))
                return null;

            WebCallableMethod methodInfo = methods[methodName];

            return new DelegateWrapper(methodInfo, key.WebHandlerPlugin);
        }

        WebDelegate IWebMethodCache.this[MethodNameAndFileContainer methodNameAndFileContainer]
        {
            get 
            {
                DelegateWrapper wrapper = this[methodNameAndFileContainer];

                if (null == wrapper)
                    return null;

                return wrapper.CallMethod;
            }
        }
    }
}
