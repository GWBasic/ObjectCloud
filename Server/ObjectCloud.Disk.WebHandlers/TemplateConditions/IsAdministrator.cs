// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;

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
    public class IsAdministrator : ITemplateConditionHandler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="me"></param>
        /// <param name="currentWorkingDirectory"></param>
        /// <returns></returns>
        public bool IsConditionMet(IWebConnection webConnection, System.Xml.XmlNode me, string currentWorkingDirectory)
        {
            return webConnection.WebServer.FileHandlerFactoryLocator.UserManagerHandler.IsUserInGroup(
                webConnection.Session.User.Id,
                webConnection.WebServer.FileHandlerFactoryLocator.UserFactory.Administrators.Id);
        }
    }
}
