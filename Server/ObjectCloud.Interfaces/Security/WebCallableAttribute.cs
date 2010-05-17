// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Attribute that declares that a method is to be exposed to the web
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WebCallableAttribute : Attribute
    {
        /// <summary>
        /// Declares that a method is to be exposed to the web
        /// </summary>
        /// <param name="webCallingConvention">The kind of calling convention supported.  This instructs ObjectCloud on how to map the HTTP request to the C# method's parameters</param>
        /// <param name="webReturnConvention">The kind of return convention supported.  This instructs the JavaScript code generator on how handle the transaction object</param>
        /// <param name="minimumPermission">The minimum permission needed to call this method from both the web and other trusted sources</param>
        public WebCallableAttribute(
            WebCallingConvention webCallingConvention,
            WebReturnConvention webReturnConvention,
            FilePermissionEnum minimumPermission)
        {
            _WebCallingConvention = webCallingConvention;
            _WebReturnConvention = webReturnConvention;
            _MinimumPermissionForWeb = minimumPermission;
            _MinimumPermissionForTrusted = minimumPermission;
        }

        /// <summary>
        /// Declares that a method is to be exposed to the web.  When no permission is declared, the method is callable by anyone
        /// </summary>
        /// <param name="webCallingConvention">The kind of calling convention supported.  This instructs ObjectCloud on how to map the HTTP request to the C# method's parameters</param>
        /// <param name="webReturnConvention">The kind of return convention supported.  This instructs the JavaScript code generator on how handle the transaction object</param>
        public WebCallableAttribute(
            WebCallingConvention webCallingConvention,
            WebReturnConvention webReturnConvention)
        {
            _WebCallingConvention = webCallingConvention;
            _WebReturnConvention = webReturnConvention;
            _MinimumPermissionForWeb = null;
            _MinimumPermissionForTrusted = null;
        }

        /// <summary>
        /// Declares that a method is to be exposed to the web
        /// </summary>
        /// <param name="webCallingConvention">The kind of calling convention supported.  This instructs ObjectCloud on how to map the HTTP request to the C# method's parameters</param>
        /// <param name="webReturnConvention">The kind of return convention supported.  This instructs the JavaScript code generator on how handle the transaction object</param>
        /// <param name="minimumPermissionForTrusted">The minimum permission needed to call this method from the web</param>
        /// <param name="minimumPermissionForWeb">The minimum permission needed to call this method from trusted sources</param>
        public WebCallableAttribute(
            WebCallingConvention webCallingConvention,
            WebReturnConvention webReturnConvention,
            FilePermissionEnum minimumPermissionForWeb,
            FilePermissionEnum minimumPermissionForTrusted)
        {
            _WebCallingConvention = webCallingConvention;
            _WebReturnConvention = webReturnConvention;
            _MinimumPermissionForWeb = minimumPermissionForWeb;
            _MinimumPermissionForTrusted = minimumPermissionForTrusted;
        }

        /// <summary>
        /// The Method's calling convention
        /// </summary>
        public WebCallingConvention WebCallingConvention
        {
            get { return _WebCallingConvention; }
        }
        private readonly WebCallingConvention _WebCallingConvention;

        /// <summary>
        /// The convention used for returning
        /// </summary>
        public WebReturnConvention WebReturnConvention
        {
            get { return _WebReturnConvention; }
        }
        private readonly WebReturnConvention _WebReturnConvention;

        /// <summary>
        /// The minimum permission needed to call the method for requests originating from the public internet and other untrusted sources
        /// </summary>
        public FilePermissionEnum? MinimumPermissionForWeb
        {
            get { return _MinimumPermissionForWeb; }
        }
        private readonly FilePermissionEnum? _MinimumPermissionForWeb;

        /// <summary>
        /// The minimum permission needed to call the method for requests originating from the local server that are trusted to be non-malevolent
        /// </summary>
        public FilePermissionEnum? MinimumPermissionForTrusted
        {
            get { return _MinimumPermissionForTrusted; }
        }
        private readonly FilePermissionEnum? _MinimumPermissionForTrusted;
    }
}
