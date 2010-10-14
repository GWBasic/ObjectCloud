// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.CallHomePlugin
{
    class CallHomeRootDirectoryCreator : RootDirectoryCreator
    {
        public override void Syncronize(IDirectoryHandler rootDirectoryHandler)
        {
            base.Syncronize(rootDirectoryHandler);

            IDirectoryHandler statsDirectory;

            if (!rootDirectoryHandler.IsFilePresent("Stats"))
            {
                statsDirectory = rootDirectoryHandler.CreateFile(
                    "Stats",
                    "directory",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id).FileContainer.CastFileHandler<IDirectoryHandler>();

                rootDirectoryHandler.SetPermission(
                    null,
                    "Stats",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id },
                    FilePermissionEnum.Read,
                    false,
                    false);
            }
            else
                statsDirectory = rootDirectoryHandler.OpenFile("Stats").CastFileHandler<IDirectoryHandler>();

            if (!statsDirectory.IsFilePresent("CallHomeTracker"))
            {
                statsDirectory.CreateFile(
                    "CallHomeTracker",
                    "callhome",
                    FileHandlerFactoryLocator.UserFactory.RootUser.Id);

                statsDirectory.SetPermission(
                    null,
                    "CallHomeTracker",
                    new ID<IUserOrGroup, Guid>[] { FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id },
                    FilePermissionEnum.Read,
                    false,
                    false);
            }
        }
    }
}
