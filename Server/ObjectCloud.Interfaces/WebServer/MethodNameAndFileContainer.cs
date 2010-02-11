// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Interfaces.WebServer
{
    public struct MethodNameAndFileContainer
    {
        /// <summary>
        /// The method name
        /// </summary>
        public string MethodName
        {
            get { return _MethodName; }
        }
        private string _MethodName;

        /// <summary>
        /// The type
        /// </summary>
        public IFileContainer FileContainer
        {
            get { return _FileContainer; }
        }
        private IFileContainer _FileContainer;

        /// <summary>
        /// The WebHandler that this applies to
        /// </summary>
        public IWebHandlerPlugin WebHandlerPlugin
        {
            get { return _WebHandlerPlugin; }
        }
        private IWebHandlerPlugin _WebHandlerPlugin;

        /// <summary>
        /// Creates a new MethodNameAndContainer for the default web handler
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="fileContainer"></param>
        /// <returns></returns>
        public static MethodNameAndFileContainer New(string methodName, IFileContainer fileContainer)
        {
            return New(methodName, fileContainer, fileContainer.WebHandler);
        }

        /// <summary>
        /// Creates a new MethodNameAndContainer for a specfic plugin
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="fileContainer"></param>
        /// <param name="webHandler"></param>
        /// <returns></returns>
        public static MethodNameAndFileContainer New(string methodName, IFileContainer fileContainer, IWebHandlerPlugin webHandlerPlugin)
        {
            MethodNameAndFileContainer me = new MethodNameAndFileContainer();
            me._MethodName = methodName;
            me._FileContainer = fileContainer;
            me._WebHandlerPlugin = webHandlerPlugin;

            return me;
        }

        public static bool operator ==(MethodNameAndFileContainer a, MethodNameAndFileContainer b)
        {
            return a._FileContainer == b._FileContainer
                && a._MethodName == b._MethodName
                && a.WebHandlerPlugin == b.WebHandlerPlugin;
        }

        public static bool operator !=(MethodNameAndFileContainer a, MethodNameAndFileContainer b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj is MethodNameAndFileContainer)
            {
                MethodNameAndFileContainer mnah = (MethodNameAndFileContainer)obj;

                return _MethodName.Equals(mnah._MethodName) && _FileContainer.Equals(mnah._FileContainer) && _WebHandlerPlugin.Equals(mnah._WebHandlerPlugin);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int methodNameMask = 0xff;
            int fileContainerMask = 0xff00;
            int webHandlerMask = 0xff0000;

            return (methodNameMask & _MethodName.GetHashCode())
                | (fileContainerMask & _FileContainer.GetHashCode())
                | (webHandlerMask & _WebHandlerPlugin.GetHashCode());
        }
    }
}
