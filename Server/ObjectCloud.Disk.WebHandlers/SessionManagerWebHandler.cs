// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Globalization;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Methods to manage the session
    /// </summary>
    class SessionManagerWebHandler : WebHandler<ISessionManagerHandler>
    {
        /// <summary>
        /// Updates if the browser should remember the session after being closed
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="KeepAlive"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults SetKeepAlive(IWebConnection webConnection, bool KeepAlive)
        {
            webConnection.Session.KeepAlive = KeepAlive;

            return WebResults.From(Status._202_Accepted, "KeepAlive set to " + KeepAlive.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Updates the maximum age that a session can be without being pinged
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="MaxAge">The maxiumum age, in days</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults SetMaxAge(IWebConnection webConnection, double MaxAge)
        {
            TimeSpan maxAgeTimespan = TimeSpan.FromDays(MaxAge);

            webConnection.Session.MaxAge = maxAgeTimespan;

            return WebResults.From(Status._202_Accepted, "MaxAge set to " + maxAgeTimespan.ToString());
        }
    }
}
