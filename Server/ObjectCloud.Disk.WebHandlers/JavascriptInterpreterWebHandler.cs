// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Web handler for interpreting server-side Javascript
    /// </summary>
    public class JavascriptInterpreterWebHandler : WebHandler
    {
        /// <summary>
        /// Runs the specified Javascript
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(Interfaces.WebServer.WebCallingConvention.Naked, WebReturnConvention.Naked, FilePermissionEnum.Read)]
        public IWebResults Run(IWebConnection webConnection)
        {
            string filename;
            if (!webConnection.GetParameters.TryGetValue("filename", out filename))
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "filename missing"));

            IFileContainer javascriptContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);

            if (null == javascriptContainer.LoadPermission(webConnection.Session.User.Id))
                throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "Permission denied"));

            if (null == javascriptContainer.OwnerId)
                throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "Only administrators can write server-side Javascript"));

            if (!(FileHandlerFactoryLocator.UserManagerHandler.IsUserInGroup(
                javascriptContainer.OwnerId.Value,
                FileHandlerFactoryLocator.UserFactory.Administrators.Id)))
            {
                throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "Only administrators can write server-side Javascript"));
            }

            webConnection.TouchedFiles.Add(javascriptContainer);

            return FileHandlerFactoryLocator.ExecutionEnvironmentFactory.Run(webConnection, javascriptContainer);
        }
    }
}
