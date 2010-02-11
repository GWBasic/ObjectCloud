// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    /// <summary>
    /// Wraps invoking the appropriate method
    /// </summary>
    public class DelegateWrapper
    {
        public DelegateWrapper(WebCallableMethod webCallableMethod, IWebHandlerPlugin webHandlerPlugin)
        {
            WebCallableMethod = webCallableMethod;
            FileContainer = webHandlerPlugin.FileContainer;
            WebHandlerPlugin = webHandlerPlugin;
        }

        private readonly WebCallableMethod WebCallableMethod;
        private IFileContainer FileContainer;
        private readonly IWebHandlerPlugin WebHandlerPlugin;

        public IWebResults CallMethod(IWebConnection webConnection, CallingFrom callingFrom)
        {
            object toReturn;

            FilePermissionEnum? minimumPermission;
            switch (callingFrom)
            {
                case CallingFrom.Local:
                    minimumPermission = WebCallableMethod.WebCallableAttribute.MinimumPermissionForTrusted;
                    break;

                case CallingFrom.Web:
                    minimumPermission = WebCallableMethod.WebCallableAttribute.MinimumPermissionForWeb;
                    break;

                default:
                    // This clause shouldn't be hit, but in case it is, require the strictest permission possible
                    minimumPermission = FilePermissionEnum.Administer;
                    break;
            }

            try
            {
                ID<IUserOrGroup, Guid> userId = webConnection.Session.User.Id;

                // If this user isn't the owner, then verify that the user has the appropriate permission
                if (null != minimumPermission)
                    if (FileContainer.OwnerId != userId)
                    {
                        bool hasPermission = false;

                        // Get appropriate permission
                        FilePermissionEnum? userPermission = FileContainer.LoadPermission(userId);

                        if (null != userPermission)
                            if (userPermission.Value >= minimumPermission.Value)
                                hasPermission = true;

                        // If the user doesn't explicitly have the needed permission, try loading any potentially-needed declaritive permissions
                        if (!hasPermission)
                            hasPermission = FileContainer.HasNamedPermissions(userId, WebCallableMethod.NamedPermissions);

                        if (!hasPermission)
                            return WebResults.FromString(Status._401_Unauthorized, "Permission Denied");
                    }

                if (null != WebCallableMethod.WebMethod)
                    if (WebCallableMethod.WebMethod.Value != webConnection.Method)
                        return WebResults.FromString(Status._405_Method_Not_Allowed, "Allowed method: " + WebCallableMethod.WebMethod.Value.ToString());

                toReturn = WebCallableMethod.CallMethod(webConnection, WebHandlerPlugin);
            }
            catch (Exception e)
            {
                // Invoke wraps exceptions
                throw e.InnerException;
            }

            return (IWebResults)toReturn;
        }
    }
}