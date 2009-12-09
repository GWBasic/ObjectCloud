// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Attribute that declares the minimum permission needed to connect to a two-way channel endpoint on a server
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false)]
    public class ChannelEndpointMinimumPermissionAttribute : Attribute
    {
        /// <summary>
        /// The minimum permission needed to access the channel endpoint
        /// </summary>
        /// <param name="minimumPermission"></param>
        public ChannelEndpointMinimumPermissionAttribute(FilePermissionEnum minimumPermission)
        {
            _MinimumPermission = minimumPermission;
        }

        /// <summary>
        /// The minimum permission needed to access the channel endpoint
        /// </summary>
        public FilePermissionEnum MinimumPermission
        {
            get { return _MinimumPermission; }
        }
        private readonly FilePermissionEnum _MinimumPermission;
    }
}
