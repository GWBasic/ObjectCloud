// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using NUnit.Framework;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebServer.Implementation;
using ObjectCloud.WebServer.Test;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.WebServer.Test.PermissionsTests
{
    /// <summary>
    /// Creates a session for a user
    /// </summary>
    public interface IUserLogoner
    {
        void Login(HttpWebClient httpWebClient, IWebServer webServer);

        /// <summary>
        /// The name to use when sharing an item
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The directory that can be used to write files that the user owns into
        /// </summary>
        string WritableDirectory { get; }
    }
}
