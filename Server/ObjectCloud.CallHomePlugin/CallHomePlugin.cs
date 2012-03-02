// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Data.Common;
using System.IO;

using ObjectCloud.CallHomePlugin.DataAccess;
using ObjectCloud.CallHomePlugin.DataAccessBase;
using ObjectCloud.Common;
using ObjectCloud.Disk.Factories;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.ORM.DataAccess.SQLite;

namespace ObjectCloud.CallHomePlugin
{
    public class CallHomePlugin : Plugin
    {
        public override void Initialize()
        {
            FileHandlerFactoryLocator.FileHandlerFactories["callhome"] = CallHomeFileHandlerFactory;
            FileHandlerFactoryLocator.WebHandlerClasses["callhome"] = typeof(CallHomeWebHandler);

            FileHandlerFactoryLocator.RootDirectoryCreator = RootDirectoryCreator;
        }

        public IRootDirectoryCreator RootDirectoryCreator { get; set; }

        public CallHomeFileHandlerFactory CallHomeFileHandlerFactory { get; set; }
    }
}
