// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
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
            IFileContainer fileContainer = key.FileContainer;

            IDictionary<string, WebCallableMethod> methods = WebMethodCache.MethodInfoCache[fileContainer.WebHandler.GetType()];

            if (!methods.ContainsKey(methodName))
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, "method does not exist"), "method does not exist");

            WebCallableMethod methodInfo = methods[methodName];

            return new DelegateWrapper(methodInfo, fileContainer);
        }

        WebDelegate IWebMethodCache.this[MethodNameAndFileContainer methodNameAndFileContainer]
        {
            get 
            {
                DelegateWrapper wrapper = this[methodNameAndFileContainer];
                return wrapper.CallMethod;
            }
        }
    }
}
