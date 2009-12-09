// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Provides internal user-management functionality
    /// </summary>
    public interface IUser : IUserOrGroup
    {
		//// <value>
		/// Returns the OpenID Identity 
		/// </value>
		string Identity { get; }
		
		/// <value>
		/// The user handler
		/// </value>
		IUserHandler UserHandler { get; }
    }
}
