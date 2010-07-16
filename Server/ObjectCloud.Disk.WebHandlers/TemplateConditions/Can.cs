﻿// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.TemplateConditions
{
    /// <summary>
    /// 
    /// </summary>
    public class Can : NamedPermissionBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="templateParsingState"></param>
        /// <param name="fileContainer"></param>
        /// <param name="namedPermission"></param>
        /// <returns></returns>
        protected override bool DetermineCondition(ITemplateParsingState templateParsingState, IFileContainer fileContainer, string namedPermission)
        {
            // Always return true for administrators
            if (templateParsingState.WebConnection.WebServer.FileHandlerFactoryLocator.UserManagerHandler.IsUserInGroup(
                templateParsingState.WebConnection.Session.User.Id,
                templateParsingState.WebConnection.WebServer.FileHandlerFactoryLocator.UserFactory.Administrators.Id))
                return true;

            return fileContainer.HasNamedPermissions(templateParsingState.WebConnection.Session.User.Id, namedPermission);
        }
    }
}
