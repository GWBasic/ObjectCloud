using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Provides run-time information about the browser accessing the server
    /// </summary>
    public class BrowserInfoWebHandler : WebHandler<IFileHandler>
    {
        /// <summary>
        /// Returns true if thie browser is a legacy browser without ChromePlug
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive, ObjectCloud.Interfaces.Security.FilePermissionEnum.Read)]
        public IWebResults IsLegacyWithoutChromePlug(IWebConnection webConnection)
        {
            string userAgent;
            if (webConnection.Headers.TryGetValue("USER-AGENT", out userAgent))
                if (userAgent.Contains("MSIE"))
                    if (!userAgent.Contains("chromeframe"))
                        return WebResults.From(Status._200_OK, "true");

            return WebResults.From(Status._200_OK, "false");
        }
    }
}
