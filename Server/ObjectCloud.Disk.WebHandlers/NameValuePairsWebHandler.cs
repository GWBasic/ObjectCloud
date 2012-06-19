// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
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
    public class NameValuePairsWebHandler : WebHandler<INameValuePairsHandler>
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

            return WebResults.From(Status._200_OK, value);
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
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Read)]
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
            EnforceSetSecurity(webConnection, Name);

            FileHandler.Set(webConnection.Session.User, Name, Value);

            return WebResults.From(Status._202_Accepted);
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
            EnforceSetSecurity(webConnection, Name);

            FileHandler.Set(webConnection.Session.User, Name, null);

            return WebResults.From(Status._202_Accepted);
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
            return SetAllHelper(webConnection, pairs.Deserialize<Dictionary<string, string>>());
        }

        /// <summary>
        /// Sets all of the values based on the results of a POST query
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults SetAll(IWebConnection webConnection)
        {
            return SetAllHelper(webConnection, webConnection.PostParameters);
        }

        private IWebResults SetAllHelper(IWebConnection webConnection, IDictionary<string, string> newPairs)
        {
            foreach (string Name in newPairs.Keys)
                EnforceSetSecurity(webConnection, Name);

            FileHandler.WriteAll(webConnection.Session.User, newPairs, true);

            return WebResults.From(Status._202_Accepted, "Saved");
        }

        /// <summary>
        /// Throws an exception if the current metadata item can not be set
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="webConnection"></param>
        private void EnforceSetSecurity(IWebConnection webConnection, string Name)
        {
            if (Name.StartsWith("Privileged"))
                if (webConnection.CallingFrom != CallingFrom.Local)
                    if (!(FileHandlerFactoryLocator.UserManagerHandler.IsUserInGroup(webConnection.Session.User.Id, FileHandlerFactoryLocator.UserFactory.Administrators.Id)))
                        throw new SecurityException("Priveliged metadata items can only be set within the context of elevate");
        }
    }
}
