// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Disk.WebHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.CallHomePlugin
{
    public class CallHomeWebHandler : DatabaseWebHandler<CallHomeFileHandler, CallHomeWebHandler>
    {
        static ILog log = LogManager.GetLogger<CallHomeWebHandler>();

        /// <summary>
        /// Handles when another ObjectCloud server calls home
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="host"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults CallHome(IWebConnection webConnection, string host)
        {
            log.Info("Incoming call home request from " + host);

            HttpWebClient webClient = new HttpWebClient();

            webClient.BeginGet(
                "http://" + host + "/Shell/version.json",
                delegate(HttpResponseHandler response)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        string versionInfo = response.AsString();

                        try
                        {
                            // "compress" the version information
                            versionInfo = JsonWriter.Serialize(JsonReader.Deserialize(versionInfo));
                        }
                        catch (Exception e)
                        {
                            log.Error("Unexpected version information when getting host information for " + host + ":\n" + versionInfo.Substring(0, 300), e);
                            return;
                        }

                        FileHandler.MarkServerAsRunning(
                            host,
                            versionInfo);
                    }
                    else
                        log.Error("Unexpected response when getting version information from " + host + ":\n" + response.AsString());
                },
                delegate(Exception e)
                {
                    log.Error("Exception when getting version information from " + host, e);
                });

            return WebResults.From(Status._204_No_Content);
        }

        /// <summary>
        /// Returns all of hosts that have called home in the last 4-5 hours as a JSON array
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults GetRunningHosts(IWebConnection webConnection)
        {
            return WebResults.ToJson(FileHandler.GetRunningObjectCloudInstances());
        }
    }
}
