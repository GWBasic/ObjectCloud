// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Comet
{
    /// <summary>
    /// Web handler for comet handshakes
    /// </summary>
    public class HandshakeWebHandler : BaseCometWebHandler
    {
        /// <summary>
        /// Performs a comet handshake
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.Naked, WebReturnConvention.Naked)]
        public IWebResults DoComet(IWebConnection webConnection)
        {
            ICometSession session = CometHandler.CreateNewSession();

            /*
HTTP/1.0 200 OK\r\n
Content-length: 26\r\n
Content-type: text/html\r\n
\r\n
({ "session": "abcdefg" })
             */

            Dictionary<string, string> toReturn = new Dictionary<string, string>();
            toReturn["session"] = session.ID.Value.ToString("x4");

            IWebResults webResults = WebResults.FromString(Status._200_OK, "(" + JsonFx.Json.JsonWriter.Serialize(toReturn) + ")");
            webResults.ContentType = "text/html";

            return webResults;
        }
    }
}
