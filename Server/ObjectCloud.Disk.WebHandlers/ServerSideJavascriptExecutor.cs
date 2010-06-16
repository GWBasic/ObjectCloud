// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Pre-compiles and executes .ssjs files
    /// </summary>
    public class ServerSideJavascriptExecutor : WebHandler<IFileHandler>
    {
        ILog log = LogManager.GetLogger<ServerSideJavascriptExecutor>();

        /// <summary>
        /// Runs server-side javascript
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults Run(IWebConnection webConnection, string filename)
        {
            IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
            ITextHandler file = fileContainer.CastFileHandler<ITextHandler>();

            // Load script and resolve webcomponents
            string script = file.ReadAll();
            script = FileHandlerFactoryLocator.WebServer.WebComponentResolver.ResolveWebComponents(script, webConnection);

            string[] brokenAtTags = script.Split(new string[] {"<?"}, StringSplitOptions.None);

            int ctr;
            StringBuilder toReturn;

            if (0 == script.IndexOf("<?"))
            {
                ctr = 0;
                toReturn = new StringBuilder("");
            }
            else
            {
                ctr = 1;
                toReturn = new StringBuilder(brokenAtTags[0]);
            }

            // This is for compatibility with older scripts that use the "scope" convention
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            metadata["scope"] = new Dictionary<string, object>();

            using (IScopeWrapper scopeWrapper = FileHandlerFactoryLocator.SubProcessFactory.GenerateScopeWrapper(
                metadata,
                new object[0],
                FileContainer))
            {
                for (; ctr < brokenAtTags.Length; ctr++)
                    toReturn.Append(RunBlock(filename, webConnection, brokenAtTags[ctr], scopeWrapper));
            }

            return WebResults.FromString(Status._200_OK, toReturn.ToString());
        }

        private string RunBlock(string filename, IWebConnection webConnection, string toRun, IScopeWrapper scopeWrapper)
        {
            if (0 == toRun.IndexOf(" Scripts("))
              return "<? " + toRun;

            string[] scriptAndPostString = toRun.Split(new string[] {"?>"}, StringSplitOptions.None);

            // If there isn't a single matching close tag, return as-is
            if (2 != scriptAndPostString.Length)
              return "<? " + toRun;

            string endString = scriptAndPostString[1];

            object[] results;
            try
            {
                results = scopeWrapper.EvalScope(webConnection, new string[] { scriptAndPostString[0] });
            }
            catch (JavascriptException je)
            {
                log.ErrorFormat("An error occured in server-side javascript {0}", je, filename);
                return je.Message + endString;
            }
            catch (Exception e)
            {
                log.ErrorFormat("An error occured in server-side javascript {0}", e, filename);
                return "An error occurred inside of ObjectCloud.  For more information, see the system logs " + endString;
            }

            // If the result isn't an array, make it an array.  All elements in the array will be iterated over
            IEnumerable toIterate;
            if (results[0] is string)
                toIterate = new object[] { results[0] };
            else if (results[0] is IEnumerable)
                toIterate = (IEnumerable)results[0];
            else
                toIterate = new object[] {results[0]};

            StringBuilder toReturn = new StringBuilder();

            // for each result returned, use it to dictate how the trailing string is rendered
            foreach (object res in toIterate)
            {
                /*// strings, numbers, and functions are returned as-is, and no manipulations are performed to the trailing string
                if ((res is string) || (res is int) || (res is long) || (res is float) || (res is double) || (res is decimal))
                {
                    toReturn.Append(res);
                    toReturn.Append(endString);
                }*/

                // undefined and null results are ignored, and the trailing string returned without any manipulation
                if ((null == res) || (res is Undefined))
                    toReturn.Append(endString);

                // boolean results conditionally display the trailing string
                else if (res is bool)
                {
                    if ((bool)res)
                        toReturn.Append(endString);
                }

                // else result is 'object'
                else if (res is Dictionary<string, object>)
                {
                    string toParse = endString;
                    Dictionary<string, object> result = (Dictionary<string, object>)res;

                    foreach (string prop in result.Keys)
                        toParse = toParse.Replace('%' + prop + '%', result[prop].ToString());

                    toReturn.Append(toReturn);
                }

                // strings, numbers, and functions are returned as-is, and no manipulations are performed to the trailing string
                else
                {
                    toReturn.Append(res);
                    toReturn.Append(endString);
                }
            }

            return toReturn.ToString();
        }
    }
}
