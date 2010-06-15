// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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
            string toReturn;

            if (0 == script.IndexOf("<?"))
            {
                ctr = 0;
                toReturn = "";
            }
            else
            {
                ctr = 1;
                toReturn = brokenAtTags[0];
            }

            ISubProcess subProcess = FileHandlerFactoryLocator.SubProcessFactory.GetSubProcess();
            bool createScope = true;

            int scopeId = FileHandlerFactoryLocator.SubProcessFactory.GenerateScopeId();
            try
            {
                for (; ctr < brokenAtTags.Length; ctr++)
                    toReturn += RunBlock(brokenAtTags[ctr], subProcess, scopeId, ref createScope);
                    createScope = false;
            }
            finally
            {
                subProcess.DisposeScope(scopeId, Thread.CurrentThread.ManagedThreadId);
            }

            return WebResults.FromString(Status._200_OK, toReturn);
        }

        private string RunBlock(string toRun, ISubProcess subProcess, int scopeId, ref bool createScope)
        {
            if (0 == toRun.IndexOf(" Scripts("))
              return "<? " + toRun;

            string[] scriptAndPostString = toRun.Split(new string[] {"?>"}, StringSplitOptions.None);

            // If there isn't a single matching close tag, return as-is
            if (2 != scriptAndPostString.Length)
              return "<? " + toRun;

            string endString = scriptAndPostString[1];
            IEnumerable scripts;
            if (createScope)
            {
                scripts = new object[]
                {
                    "var scope = {};",
                    scriptAndPostString[0]
                };

                createScope = false;
            }
            else
                scripts = new object[]
                {
                    scriptAndPostString[0]
                };

            EvalScopeResults results;
            try
            {
                results = subProcess.EvalScope(
                    scopeId,
                    Thread.CurrentThread.ManagedThreadId,
                    null,
                    scripts,
                    null,
                    false);
            }
            catch (JavascriptException je)
            {
                return je.Message + endString;
            }
            catch (Exception)
            {
              return "An error occurred inside of ObjectCloud.  For more information, see the system logs "+ endString;
            }

            // If the result isn't an array, make it an array.  All elements in the array will be iterated over
            IEnumerable toIterate;
            if (results.Results[0] is IEnumerable)
                toIterate = (IEnumerable)results.Results[0];
            else
                toIterate = new object[] {results.Results[0]};

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
