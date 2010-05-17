using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Attribute that declares a named permission that can allow a caller to call a web method even if that caller doesn't have the minimum permission
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class NamedPermissionAttribute : Attribute
    {
        public NamedPermissionAttribute(string namedPermission)
        {
            _NamedPermission = namedPermission;
        }

        /// <summary>
        /// The name of a named permission
        /// </summary>
        public string NamedPermission
        {
            get { return _NamedPermission; }
        }
        private readonly string _NamedPermission;
    }
}
