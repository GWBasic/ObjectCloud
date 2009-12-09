// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Represents a cookie that will be set in the browser
    /// </summary>
    public class CookieToSet
    {
        /// <summary>
        /// Creates an insecure cookie that expires at the end of the browser session
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public CookieToSet() { }

        /// <summary>
        /// Creates an insecure cookie that expires at the end of the browser session
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public CookieToSet(string name)
        {
            _Name = name;
        }

        /// <summary>
        /// Creates an insecure cookie that expires at the end of the browser session
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public CookieToSet(string name, string value)
        {
            _Name = name;
            _Value = value;
        }

        /// <summary>
        /// Creates an insecure cookie that expires at the end of the browser session
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public CookieToSet(string name, string value, string path)
        {
            _Name = name;
            _Value = value;
            _Path = path;
        }

        public CookieToSet(string name, string value, string path, DateTime? expires)
        {
            _Name = name;
            _Value = value;
            _Expires = expires;
            _Path = path;
        }

        public CookieToSet(string name, string value, string path, bool secure)
        {
            _Name = name;
            _Value = value;
            _Secure = secure;
            _Path = path;
        }

        public CookieToSet(string name, string value, string path, DateTime? expires, bool secure)
        {
            _Name = name;
            _Value = value;
            _Expires = expires;
            _Secure = secure;
            _Path = path;
        }

        /// <summary>
        /// The name of the cookie
        /// </summary>
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }
        private string _Name;

        /// <summary>
        /// The value of the cookie
        /// </summary>
        public string Value
        {
            get { return _Value; }
            set { _Value = value; }
        }
        private string _Value = null;

        /// <summary>
        /// When the cookie expires, or null for it to expire at the end of the
        /// session
        /// </summary>
        public DateTime? Expires
        {
            get { return _Expires; }
            set { _Expires = value; }
        }
        private DateTime? _Expires;

        /// <summary>
        /// True if the cookie is secure, false otherwise
        /// </summary>
        public bool Secure
        {
            get { return _Secure; }
            set { _Secure = value; }
        }
        private bool _Secure = false;

        /// <summary>
        /// The cookie's path.  When this is set, the cookie will be served only to the path and sub-paths
        /// </summary>
        public string Path
        {
            get { return _Path; }
            set { _Path = value; }
        }
        private string _Path = null;
    }
}
