// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Comet
{
    /// <summary>
    /// Handles Comet IO once a session is established
    /// </summary>
    public class SendWebHandler : BaseCometWebHandler
    {
        /// <summary>
        /// Performs a comet handshake
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.Naked, WebReturnConvention.Naked)]
        public IWebResults DoComet(IWebConnection webConnection)//, string d, string s)
        {
            ushort sessionIdValue = default(ushort);

            if (!ushort.TryParse(
                webConnection.EitherArgumentOrException("s"),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out sessionIdValue))
            {
                return WebResults.FromString(Status._417_Expectation_Failed, "Invalid session ID");
            }

            ID<ICometSession, ushort> sessionId = new ID<ICometSession, ushort>(sessionIdValue);

            ICometSession cometSession;
            try
            {
                cometSession = CometHandler[sessionId];
            }
            catch (BadCometSessionId)
            {
                return WebResults.FromString(Status._400_Bad_Request, "Bad SESSION_KEY");
            }

            //object data = JsonReader.Deserialize("{\"d\": " + webConnection.Content.AsString() + "}");
            // This is silly, but for some reason the array is quoted when it's sent...
            string wtf = JsonReader.Deserialize<string>(webConnection.Content.AsString());
            object[] packets = JsonReader.Deserialize<object[]>(wtf);

            foreach (object[] packet in packets)
                cometSession.RecieveData(
                    Convert.ToUInt64(packet[0]),
                    packet[2].ToString());

            return WebResults.FromStatus(Status._200_OK);
        }
    }
}
