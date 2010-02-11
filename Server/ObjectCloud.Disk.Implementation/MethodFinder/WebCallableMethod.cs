// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    /// <summary>
    /// Wraps a MethodInfo object with metadata needed to call the method via the web
    /// </summary>
    public abstract partial class WebCallableMethod
    {
        protected WebCallableMethod(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute, WebMethod? webMethod)
        {
            _MethodInfo = methodInfo;
            _WebCallableAttribute = webCallableAttribute;
            _WebMethod = webMethod;

            _Parameters = _MethodInfo.GetParameters();

            for (uint parameterCtr = 0; parameterCtr < _Parameters.Length; parameterCtr++)
                ParameterIndexes[_Parameters[parameterCtr].Name] = parameterCtr;

            foreach (NamedPermissionAttribute npa in methodInfo.GetCustomAttributes(typeof(NamedPermissionAttribute), true))
                _NamedPermissions.Add(npa.NamedPermission);
        }

        /// <summary>
        /// The actual wrapped MethodInfo
        /// </summary>
        public MethodInfo MethodInfo
        {
            get { return _MethodInfo; }
        }
        private readonly MethodInfo _MethodInfo;

        /// <summary>
        /// The HTTP verb, either GET, POST, or other.  Null if any verb is allowed
        /// </summary>
        public WebMethod? WebMethod
        {
            get { return _WebMethod; }
        }
        private readonly WebMethod? _WebMethod;

        /// <summary>
        /// The supported calling convention
        /// </summary>
        public WebCallableAttribute WebCallableAttribute
        {
            get { return _WebCallableAttribute; }
        }
        private readonly WebCallableAttribute _WebCallableAttribute;

        /// <summary>
        /// Calls the method.  The webHandler is the target
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="webHandler"></param>
        /// <returns></returns>
        public abstract IWebResults CallMethod(IWebConnection webConnection, IWebHandlerPlugin webHandlerPlugin);

        /// <summary>
        /// Each named parameter, and the numerical location in its call
        /// </summary>
        public Dictionary<string, uint> ParameterIndexes
        {
            get { return _ParameterIndexes; }
        }
        private readonly Dictionary<string, uint> _ParameterIndexes = new Dictionary<string,uint>();

        /// <summary>
        /// The number of parameters to allocate when calling the method
        /// </summary>
        public int NumParameters
        {
            get { return _ParameterIndexes.Count; }
        }

        /// <summary>
        /// Cached array of parameters
        /// </summary>
        public ParameterInfo[] Parameters
        {
            get { return _Parameters; }
        }
        private readonly ParameterInfo[] _Parameters;

        /// <summary>
        /// All of the named permissions
        /// </summary>
        public IEnumerable<string> NamedPermissions
        {
            get { return _NamedPermissions; }
        }
        private readonly List<string> _NamedPermissions = new List<string>();
    }
}
