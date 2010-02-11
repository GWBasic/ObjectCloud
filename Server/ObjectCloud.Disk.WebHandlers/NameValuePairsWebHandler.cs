// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Wrapper for web methods that handle name-value pairs
    /// </summary>
    public class NameValuePairsWebHandler : DatabaseWebHandler<INameValuePairsHandler, NameValuePairsWebHandler>
    {
        /// <summary>
        /// Gets the named value
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults Get(IWebConnection webConnection, string Name)
        {
            string value = FileHandler[Name];

            // As far as the web API is concerned, a missing value is equivilent to ""
            if (null == value)
                value = "";

            if (webConnection.GetParameters.ContainsKey("EncodeFor"))
                if (webConnection.GetParameters["EncodeFor"].Equals("HTML"))
                    // The text is encoded for HTML so it can be displayed unaltered
                    value = HTTPStringFunctions.EncodeForHTML(value);

            return WebResults.FromString(Status._200_OK, value);
        }

        /// <summary>
        /// Gets the named value as a JSON object
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults GetAsJson(IWebConnection webConnection, string Name)
        {
            return Get(webConnection, Name);
        }

        /// <summary>
        /// Gets all of the name-values, returns a JSON object with names and values as strings
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetAll(IWebConnection webConnection)
        {
            Dictionary<string, string> toWrite = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> pair in FileHandler)
                toWrite.Add(pair.Key, pair.Value);

            return WebResults.ToJson(toWrite);
        }

        /// <summary>
        /// Returns true if the named value is present, false if it isn't
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults Contains(IWebConnection webConnection, string Name)
        {
            return WebResults.ToJson(FileHandler.Contains(Name));
        }

        /// <summary>
        /// Sets the given value
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="Name"></param>
        /// <param name="Value"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults Set(IWebConnection webConnection, string Name, string Value)
        {
            FileHandler.Set(webConnection.Session.User, Name, Value);

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Deletes the named value
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults Delete(IWebConnection webConnection, string Name)
        {
            FileHandler.Set(webConnection.Session.User, Name, null);

            return WebResults.FromStatus(Status._202_Accepted);
        }

        /// <summary>
        /// Sets all of the values based on the passed JSON object
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="pairs"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_JSON, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults SetAllJson(IWebConnection webConnection, JsonReader pairs)
        {
            // Decode the new pairs
            IDictionary<string, string> newPairs;

            newPairs = pairs.Deserialize<Dictionary<string, string>>();

            FileHandler.WriteAll(webConnection.Session.User, newPairs, true);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }

        /// <summary>
        /// Sets all of the values based on the results of a POST query
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults SetAll(IWebConnection webConnection)
        {
            // Decode the new pairs
            IDictionary<string, string> newPairs;

            newPairs = webConnection.PostParameters;

            FileHandler.WriteAll(webConnection.Session.User, newPairs, true);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }
    }
}
