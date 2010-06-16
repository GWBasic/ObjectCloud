// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common.Threading;
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
            // This loop is in case the process aborts
            int ctr = 0;

            do
            {
                try
                {
                    return RunInt(webConnection, filename);
                }
                catch (ObjectDisposedException)
                {
                    ctr++;

                    if (3 == ctr)
                        throw;
                }
            } while (true);
        }

        private IWebResults RunInt(IWebConnection webConnection, string filename)
        {
            IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
            ITextHandler file = fileContainer.CastFileHandler<ITextHandler>();

            // Load script and resolve webcomponents
            string script = file.ReadAll();
            script = FileHandlerFactoryLocator.WebServer.WebComponentResolver.ResolveWebComponents(script, webConnection);

            string[] brokenAtTags = script.Split(new string[] {"<?"}, StringSplitOptions.None);

            int ctr;
            StringBuilder stringBuilder;

            if (0 == script.IndexOf("<?"))
            {
                ctr = 0;
                stringBuilder = new StringBuilder("");
            }
            else
            {
                ctr = 1;
                stringBuilder = new StringBuilder(brokenAtTags[0]);
            }

            using (IScopeWrapper scopeWrapper = GetPreConstructedScope())
                for (; ctr < brokenAtTags.Length; ctr++)
                    RunBlock(stringBuilder, filename, webConnection, brokenAtTags[ctr], scopeWrapper);

            return WebResults.FromString(Status._200_OK, stringBuilder.ToString());
        }

        /// <summary>
        /// Pre-constructed scope wrappers
        /// </summary>
        private LockFreeQueue<IScopeWrapper> PreConstructedScopeWrappers = null;

        /// <summary>
        /// The number of a scope wrappers to pre-construct
        /// </summary>
        public int NumScopeWrappersToPreConstruct
        {
            get { return _NumScopeWrappersToPreConstruct; }
            set { _NumScopeWrappersToPreConstruct = value; }
        }
        private int _NumScopeWrappersToPreConstruct = 10;

        /// <summary>
        /// Gets a pre-constructed scope if its available, else, creates one
        /// </summary>
        /// <returns></returns>
        private IScopeWrapper GetPreConstructedScope()
        {
            if (null == PreConstructedScopeWrappers)
            {
                ThreadPool.QueueUserWorkItem(PreConstructScopes);
                return ConstructScope();
            }

            IScopeWrapper scopeWrapper;
            if (PreConstructedScopeWrappers.Dequeue(out scopeWrapper))
            {
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    PreConstructedScopeWrappers.Enqueue(ConstructScope());
                });

                if (scopeWrapper.SubProcess.Alive)
                    return scopeWrapper;
            }

            return ConstructScope();
        }

        private void PreConstructScopes(object state)
        {
            if (null == Interlocked.CompareExchange<LockFreeQueue<IScopeWrapper>>(
                ref PreConstructedScopeWrappers, new LockFreeQueue<IScopeWrapper>(), null))
            {
                for (int ctr = 0; ctr < NumScopeWrappersToPreConstruct; ctr++)
                    PreConstructedScopeWrappers.Enqueue(ConstructScope());
            }
        }

        /// <summary>
        /// Constructs a scope with the needed semantics
        /// </summary>
        /// <returns></returns>
        private IScopeWrapper ConstructScope()
        {
            // This is for compatibility with older scripts that use the "scope" convention
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            metadata["scope"] = new Dictionary<string, object>();

            return FileHandlerFactoryLocator.SubProcessFactory.GenerateScopeWrapper(
                metadata,
                new object[0],
                FileContainer);
        }

        private void RunBlock(
            StringBuilder stringBuilder, 
            string filename, 
            IWebConnection webConnection, 
            string toRun, 
            IScopeWrapper scopeWrapper)
        {
            if (0 == toRun.IndexOf(" Scripts("))
            {
                stringBuilder.Append("<? " + toRun);
                return;
            }

            string[] scriptAndPostString = toRun.Split(new string[] {"?>"}, StringSplitOptions.None);

            // If there isn't a single matching close tag, return as-is
            if (2 != scriptAndPostString.Length)
            {
                stringBuilder.Append("<? " + toRun);
                return;
            }

            string script = scriptAndPostString[0];
            int scriptHash = script.GetHashCode();

            int scriptId = FileHandlerFactoryLocator.SubProcessFactory.CompiledJavascriptManager.GetScriptID(
                filename + "___HASH___" + scriptHash.ToString(),
                scriptHash.ToString(),
                script,
                scopeWrapper.SubProcess);

            string endString = scriptAndPostString[1];

            object[] results;
            try
            {
                results = scopeWrapper.EvalScope(webConnection, new object[] { scriptId });
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (JavascriptException je)
            {
                log.ErrorFormat("An error occured in server-side javascript {0}", je, filename);
                stringBuilder.Append(je.Message + endString);
                return;
            }
            catch (Exception e)
            {
                log.ErrorFormat("An error occured in server-side javascript {0}", e, filename);
                stringBuilder.Append("An error occurred inside of ObjectCloud.  For more information, see the system logs " + endString);
                return;
            }

            // If the result isn't an array, make it an array.  All elements in the array will be iterated over
            IEnumerable toIterate;
            if (results[0] is string)
                toIterate = new object[] { results[0] };
            else if (results[0] is IEnumerable)
                toIterate = (IEnumerable)results[0];
            else
                toIterate = new object[] {results[0]};

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
                    stringBuilder.Append(endString);

                // boolean results conditionally display the trailing string
                else if (res is bool)
                {
                    if ((bool)res)
                        stringBuilder.Append(endString);
                }

                // else result is 'object'
                else if (res is Dictionary<string, object>)
                {
                    string toParse = endString;
                    Dictionary<string, object> result = (Dictionary<string, object>)res;

                    foreach (string prop in result.Keys)
                        toParse = toParse.Replace('%' + prop + '%', result[prop].ToString());

                    stringBuilder.Append(toParse);
                }

                // strings, numbers, and functions are returned as-is, and no manipulations are performed to the trailing string
                else
                {
                    stringBuilder.Append(res);
                    stringBuilder.Append(endString);
                }
            }
        }
    }
}
