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

        public static MethodNameAndFileContainer New(string methodName, IFileContainer fileContainer)
        {
            MethodNameAndFileContainer me = new MethodNameAndFileContainer();
            me._MethodName = methodName;
            me._FileContainer = fileContainer;

            return me;
        }

        public static bool operator ==(MethodNameAndFileContainer a, MethodNameAndFileContainer b)
        {
            return a._FileContainer == b._FileContainer
                && a._MethodName == b._MethodName;
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

                return _MethodName.Equals(mnah._MethodName) && _FileContainer.Equals(mnah._FileContainer);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int methodNameMask = ~0xff;
            int webHandlerMask = 0xff;

            return (methodNameMask & _MethodName.GetHashCode())
                | (webHandlerMask & _FileContainer.GetHashCode());
        }
    }
}
