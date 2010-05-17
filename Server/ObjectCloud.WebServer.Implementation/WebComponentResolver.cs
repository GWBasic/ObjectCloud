// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebServer.Implementation
{
    /// <summary>
    /// Resolves WebComponents
    /// TODO:  This needs to move into its own assembly and have its own set of tests
    /// </summary>
    internal class WebComponentResolver : IWebComponentResolver
    {
        private static ILog log = LogManager.GetLogger<WebComponentResolver>();

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        // use something like
        // <? WebComponent($_GET["Directory"] . "?Method=ListFiles") ?>
        //
        //
        // This system should closely follow PHP syntax so that the WebComponent system is compatible with many pre-existing commodity web hosting
        // services
        //
        // Supported:
        //  $_GET
        //  $_POST
        //  $_COOKIE
        //  $_REQUEST, order: GET, POST, Cookie
        //
        //
        // In addition, <? Scripts("/a/b/c/lib.rjs", "/e/f/g/another.rjs") ?> allows for recursively loading needed JavaScript libraries

        /// <summary>
        /// This is set to false on recurse web components
        /// </summary>
        [ThreadStatic]
        private static object IsTopLevelToken = null;

        /// <summary>
        /// Resolves all web components.  In the event that the WebComponent syntax is incorrect, no exceptions should occur.  Incorrect syntax will
        /// be left as-is
        /// </summary>
        /// <param name="toResolve"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        public string ResolveWebComponents(string toResolve, IWebConnection webConnection)
        {
            bool isTopLevel = (null == IsTopLevelToken);
            IsTopLevelToken = new object();

            try
            {
                string resolved = toResolve;

                do
                {
                    toResolve = resolved;
                    resolved = ParseWebComponents(toResolve, webConnection);
                } while (!(toResolve.Equals(resolved)));

                // Only resolve <? Scripts... tags at the top level, resolving them at lower-levels can lead to scripts being added too low in the file
                // if some funny business causes <? Scripts... tags that are initially un-parseable
                if (isTopLevel)
                    resolved = ParseScripts(toResolve, webConnection);

                return resolved;
            }
            catch (FileDoesNotExist fdne)
            {
                throw new WebResultsOverrideException(
                    WebResults.FromString(Status._404_Not_Found, fdne.Message));
            }
            finally
            {
                if (isTopLevel)
                    IsTopLevelToken = null;
            }
        }

        /// <summary>
        /// Parses all of the web components
        /// </summary>
        /// <param name="toResolve"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private string ParseWebComponents(string toResolve, IWebConnection webConnection)
        {
            string[] splitAtOpenTag = toResolve.Split(new string[] { "<?" }, StringSplitOptions.None);

            // If nothing was found, just return toResolve unparsed
            if (splitAtOpenTag.Length <= 1)
                return toResolve;

            for (int ctr = 1; ctr < splitAtOpenTag.Length; ctr++)
            {
                string tagContentsAndTrailer = splitAtOpenTag[ctr];
                int? closeLoc = FindClose(tagContentsAndTrailer);

                if (null == closeLoc)
                    // no ?>
                    splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                else
                {
                    string webComponentTag = tagContentsAndTrailer.Substring(0, closeLoc.Value - 2).Trim();
                    string afterWebComponentTag = tagContentsAndTrailer.Substring(closeLoc.Value);

                    if (webComponentTag.StartsWith("WebComponent(", true, CultureInfo.InvariantCulture))
                    {
                        // <? WebComponent(...
                        try
                        {
                            string arguments = webComponentTag.Substring("WebComponent(".Length);

                            StringBuilder parsedArguments = new StringBuilder();

                            foreach (string subString in SplitAtPeriods(arguments))
                            {
                                string parsed = Parse(subString, webConnection);
                                parsedArguments.Append(parsed);
                            }

                            try
                            {
                                try
                                {
                                    string webComponentResults = webConnection.DoWebComponent(parsedArguments.ToString());
                                    splitAtOpenTag[ctr] = webComponentResults + afterWebComponentTag;
                                }
                                catch (Exception e)
                                {
                                    log.Error("Exception when handling a web component", e);
                                    throw;
                                }
                            }
                            catch (Exception e)
                            {
                                if (e is WebResultsOverrideException)
                                {
                                    WebResultsOverrideException wroe = (WebResultsOverrideException)e;

                                    log.Error("Exception when handling a web component", wroe);
                                    splitAtOpenTag[ctr] = wroe.WebResults.ResultsAsString + afterWebComponentTag;
                                }
                                else
                                {
                                    log.Error("Exception when handling a web component", e);
                                    splitAtOpenTag[ctr] = " Unknown error, see logs " + afterWebComponentTag;
                                }
                            }
                        }
                        catch (CanNotParse cnp)
                        {
                            log.Error(cnp);
                            splitAtOpenTag[ctr] = "<? PARSE ERROR + " + webComponentTag + " PARSE ERROR ?>" + afterWebComponentTag;
                        }
                    }
                    else if (webComponentTag.StartsWith("$_"))
                    {
                        // $_GET...  $_POST...  $_COOKIE...  $_REQUEST...

                        string[] varSourceAndName = webComponentTag.Substring(2).Split('[');

                        if (2 != varSourceAndName.Length)
                            splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                        else
                        {
                            string varSource = varSourceAndName[0];

                            string[] variableAndJunk = varSourceAndName[1].Split('"');

                            if (3 != variableAndJunk.Length)
                                splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                            else
                            {
                                string name = variableAndJunk[1];

                                string result = null;

                                try
                                {
                                    switch (varSource)
                                    {
                                        case ("GET"):
                                            result = webConnection.GetArgumentOrException(name);
                                            break;
                                        case ("GETENCODE"):
                                            result = HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.GetArgumentOrException(name));
                                            break;
                                        case ("POST"):
                                            result = webConnection.PostArgumentOrException(name);
                                            break;
                                        case ("POSTENCODE"):
                                            result = HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.PostArgumentOrException(name));
                                            break;
                                        case ("COOKIE"):
                                            result = webConnection.CookieOrException(name);
                                            break;
                                        case ("COOKIEENCODE"):
                                            result = HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.CookieOrException(name));
                                            break;
                                        case ("REQUEST"):
                                            result = webConnection.EitherArgumentOrException(name);
                                            break;
                                        case ("REQUESTENCODE"):
                                            result = HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.EitherArgumentOrException(name));
                                            break;
                                    }

                                    if (null != result)
                                        splitAtOpenTag[ctr] = result + afterWebComponentTag;
                                    else
                                        splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;

                                }
                                catch (Exception e)
                                {
                                    log.Error("Error when handing a $_ ", e);
                                    splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                                }
                            }
                        }
                    }
                    else if (webComponentTag.StartsWith("Cache("))
                    {
                        // <? Cache(...

                        string[] splitAtCloseTag = splitAtOpenTag[ctr].Split(new string[] { "?>" }, 2, StringSplitOptions.None);

                        if (2 != splitAtCloseTag.Length)
                            splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                        else
                        {
                            string requestedUrl = splitAtCloseTag[0].Split(new char[] { '(' }, 2)[1].Split(')')[0];
                            splitAtOpenTag[ctr] = webConnection.GetBrowserCacheUrl(requestedUrl) + splitAtCloseTag[1];
                        }
                    }
                    else
                        splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                }
            }

            StringBuilder toReturn = new StringBuilder();

            foreach (string toAdd in splitAtOpenTag)
                toReturn.Append(toAdd);

            return toReturn.ToString();
        }

        /// <summary>
        /// Parses all of the script tags.  This is only called in the highest-level component because it might run a few parses in a loop, thus pushing script
        /// entries lower then they should be
        /// </summary>
        /// <param name="toResolve"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private string ParseScripts(string toResolve, IWebConnection webConnection)
        {
            string[] splitAtOpenTag = toResolve.Split(new string[] { "<?" }, StringSplitOptions.None);

            // If nothing was found, just return toResolve unparsed
            if (splitAtOpenTag.Length <= 1)
                return toResolve;

            for (int ctr = 1; ctr < splitAtOpenTag.Length; ctr++)
            {
                string tagContentsAndTrailer = splitAtOpenTag[ctr];
                int? closeLoc = FindClose(tagContentsAndTrailer);

                if (null == closeLoc)
                    // no ?>
                    splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                else
                {
                    string webComponentTag = tagContentsAndTrailer.Substring(0, closeLoc.Value - 2).Trim();

                    if (webComponentTag.StartsWith("Scripts("))
                    {
                        // <? Scripts(...

                        string[] splitAtCloseTag = splitAtOpenTag[ctr].Split(new string[] { "?>" }, 2, StringSplitOptions.None);

                        if (2 != splitAtCloseTag.Length)
                            splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                        else
                        {
                            string[] requestedScripts = splitAtCloseTag[0].Split(new char[] { '(' }, 2)[1].Split(',');

                            for (int subCtr = 0; subCtr < requestedScripts.Length; subCtr++)
                                requestedScripts[subCtr] = requestedScripts[subCtr].Trim();

                            if (requestedScripts[requestedScripts.Length - 1].EndsWith(")"))
                                requestedScripts[requestedScripts.Length - 1] = requestedScripts[requestedScripts.Length - 1].Substring(0, requestedScripts[requestedScripts.Length - 1].Length - 1).Trim();

                            IEnumerable<ScriptAndMD5> dependantScripts = DetermineDependantScripts(requestedScripts, webConnection);

                            StringBuilder scriptBuilder = new StringBuilder();
                            foreach (ScriptAndMD5 scriptAndMD5 in dependantScripts)
                            {
                                string scriptAndArgs = HTTPStringFunctions.AppendGetParameter(scriptAndMD5.ScriptName, "BrowserCache", scriptAndMD5.MD5);
                                scriptAndArgs = HTTPStringFunctions.AppendGetParameter(scriptAndArgs, "EncodeFor", "JavaScript");
                                scriptBuilder.AppendFormat("<script src=\"{0}\" ></script>", scriptAndArgs);
                            }

                            splitAtOpenTag[ctr] = scriptBuilder.ToString() + splitAtCloseTag[1];
                        }
                    }
                    else
                        splitAtOpenTag[ctr] = "<?" + tagContentsAndTrailer;
                }
            }

            StringBuilder toReturn = new StringBuilder();

            foreach (string toAdd in splitAtOpenTag)
                toReturn.Append(toAdd);

            return toReturn.ToString();
        }

        /// <summary>
        /// Signal that something can't be parsed
        /// </summary>
        private class CanNotParse : Exception
        {
            internal CanNotParse() : base() { }
        }

        /// <summary>
        /// Used when parsing the WebComponent tag
        /// </summary>
        private enum ParseState
        {
            NoState, InDoubleQuote, InDoubleQuoteEscape, InSingleQuote, InSingleQuoteEscape, InQuestionMark
        }

        /// <summary>
        /// Returns the exact character number where the closing ?> is in the string.  This can be used with the SubString method
        /// </summary>
        /// <param name="tagContentsAndTrailer"></param>
        /// <returns></returns>
        private int? FindClose(string tagContentsAndTrailer)
        {
            char[] toParse = tagContentsAndTrailer.ToCharArray();

            ParseState parseState = ParseState.NoState;

            for (int loc = 0; loc < toParse.Length; loc++)
            {
                switch (parseState)
                {
                    case (ParseState.NoState):

                        switch (toParse[loc])
                        {
                            case ('"'):
                                parseState = ParseState.InDoubleQuote;
                                break;
                            
                            case ('\''):
                                parseState = ParseState.InSingleQuote;
                                break;

                            case ('?'):
                                parseState = ParseState.InQuestionMark;
                                break;
                        }

                        break;

                        // last char was ?
                    case (ParseState.InQuestionMark):

                        if ('>' == toParse[loc])
                            return loc + 1;

                        break;

                    // We're in a "
                    case (ParseState.InDoubleQuote):

                        switch (toParse[loc])
                        {
                            case ('\\'):
                                parseState = ParseState.InDoubleQuoteEscape;
                                break;

                            case ('"'):
                                parseState = ParseState.NoState;
                                break;
                        }

                        break;

                    // We're in a " and the last char was \
                    case (ParseState.InDoubleQuoteEscape):
                        parseState = ParseState.InDoubleQuote;
                        break;

                    // We're in a '
                    case (ParseState.InSingleQuote):

                        switch (toParse[loc])
                        {
                            case ('\\'):
                                parseState = ParseState.InSingleQuoteEscape;
                                break;

                            case ('\''):
                                parseState = ParseState.NoState;
                                break;
                        }

                        break;

                    // We're in a ' and the last char was \
                    case (ParseState.InSingleQuoteEscape):
                        parseState = ParseState.InSingleQuote;
                        break;
                }
            }

            // String did not have a closing ?>
            return null;
        }

        /// <summary>
        /// Splits the string at periods, but ignores periods in quotes and single quotes
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private IEnumerable<string> SplitAtPeriods(string arguments)
        {
            char[] toParse = arguments.ToCharArray();

            StringBuilder toYield = new StringBuilder();

            ParseState parseState = ParseState.NoState;

            for (int loc = 0; loc < toParse.Length; loc++)
            {
                switch (parseState)
                {
                    case (ParseState.NoState):
                        switch (toParse[loc])
                        {
                            // ignore whitespace
                            case (' '):
                                break;
                            case ('\n'):
                                break;
                            case ('\r'):
                                break;
                            case ('\t'):
                                break;

                            // break at .
                            case ('.'):
                                yield return toYield.ToString().Trim();
                                toYield = new StringBuilder();
                                break;

                            // Just stop if ) is hit; everything else will be ignored.  This is okay for now
                            case (')'):
                                yield return toYield.ToString();
                                yield break;

                            case ('"'):
                                toYield.Append(toParse[loc]);
                                parseState = ParseState.InDoubleQuote;
                                break;

                            case ('\''):
                                toYield.Append(toParse[loc]);
                                parseState = ParseState.InSingleQuote;
                                break;

                            default:
                                toYield.Append(toParse[loc]);
                                break;
                        }

                        break;

                    case (ParseState.InSingleQuote):
                        toYield.Append(toParse[loc]);

                        switch (toParse[loc])
                        {
                            case ('\''):
                                parseState = ParseState.NoState;
                                break;

                            case ('\\'):
                                parseState = ParseState.InSingleQuoteEscape;
                                break;
                        }

                        break;

                    // We're in a ' and the last char was \
                    case (ParseState.InSingleQuoteEscape):
                        toYield.Append(toParse[loc]);

                        parseState = ParseState.InSingleQuote;
                        break;

                    case (ParseState.InDoubleQuote):
                        toYield.Append(toParse[loc]);

                        switch (toParse[loc])
                        {
                            case ('"'):
                                parseState = ParseState.NoState;
                                break;

                            case ('\\'):
                                parseState = ParseState.InDoubleQuoteEscape;
                                break;
                        }

                        break;

                    // We're in a " and the last char was \
                    case (ParseState.InDoubleQuoteEscape):
                        toYield.Append(toParse[loc]);

                        parseState = ParseState.InDoubleQuote;
                        break;
                }
            }
            // In theory, there shouldn't be trailing characters...
            if (toYield.Length > 0)
                yield return toYield.ToString();
        }

        /// <summary>
        /// Parses the given string.  The given string can either be " quoted, ' quoted, $_GET, $_POST, $_COOKIE, $_REQUEST
        /// </summary>
        /// <param name="toParse"></param>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        private string Parse(string toParse, IWebConnection webConnection)
        {

            if (toParse.StartsWith("\""))
            {
                if (!toParse.EndsWith("\""))
                    throw new CanNotParse();

                return HandleEscapes(toParse, '"');
            }
            else if (toParse.StartsWith("'"))
            {
                if (!toParse.EndsWith("'"))
                    throw new CanNotParse();

                return HandleEscapes(toParse, '\'');
            }
            else if (toParse.StartsWith("$_GET["))
            {
                string argumentName = GetArgument(toParse, "$_GET");

                return webConnection.GetArgumentOrException(argumentName);
            }
            else if (toParse.StartsWith("$_GETENCODE["))
            {
                string argumentName = GetArgument(toParse, "$_GETENCODE");

                return HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.GetArgumentOrException(argumentName));
            }
            else if (toParse.StartsWith("$_POST["))
            {
                string argumentName = GetArgument(toParse, "$_POST");

                return webConnection.PostArgumentOrException(argumentName);
            }
            else if (toParse.StartsWith("$_POSTENCODE["))
            {
                string argumentName = GetArgument(toParse, "$_POSTENCODE");

                return HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.PostArgumentOrException(argumentName));
            }
            else if (toParse.StartsWith("$_COOKIE["))
            {
                string argumentName = GetArgument(toParse, "$_COOKIE");

                return webConnection.CookieOrException(argumentName);
            }
            else if (toParse.StartsWith("$_COOKIEENCODE["))
            {
                string argumentName = GetArgument(toParse, "$_COOKIEENCODE");

                return HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.CookieOrException(argumentName));
            }
            else if (toParse.StartsWith("$_REQUEST["))
            {
                string argumentName = GetArgument(toParse, "$_REQUEST");

                return webConnection.EitherArgumentOrException(argumentName);
            }
            else if (toParse.StartsWith("$_REQUESTENCODE["))
            {
                string argumentName = GetArgument(toParse, "$_REQUESTENCODE");

                return HTTPStringFunctions.EncodeRequestParametersForBrowser(webConnection.EitherArgumentOrException(argumentName));
            }

            throw new CanNotParse();
        }

        private string HandleEscapes(string toUnEscape, char breakChar)
        {
            char[] toParse = toUnEscape.ToCharArray();

            StringBuilder toReturn = new StringBuilder();

            ParseState parseState = ParseState.NoState;

            for (int loc = 0; loc < toParse.Length; loc++)
            {
                switch (parseState)
                {
                    case (ParseState.NoState):
                        switch (toParse[loc])
                        {
                            // Just stop if ) is hit; everything else will be ignored.  This is okay for now
                            case (']'):
                                return toReturn.ToString();

                            case ('"'):
                                parseState = ParseState.InDoubleQuote;
                                break;

                            case ('\''):
                                parseState = ParseState.InSingleQuote;
                                break;
                        }

                        break;

                    case (ParseState.InSingleQuote):

                        switch (toParse[loc])
                        {
                            case ('\''):
                                parseState = ParseState.NoState;
                                break;

                            case ('\\'):
                                parseState = ParseState.InSingleQuoteEscape;
                                break;

                            default:
                                toReturn.Append(toParse[loc]);
                                break;
                        }

                        break;

                    // We're in a ' and the last char was \
                    case (ParseState.InSingleQuoteEscape):
                        toReturn.Append(toParse[loc]);

                        parseState = ParseState.InSingleQuote;
                        break;

                    case (ParseState.InDoubleQuote):

                        switch (toParse[loc])
                        {
                            case ('"'):
                                parseState = ParseState.NoState;
                                break;

                            case ('\\'):
                                parseState = ParseState.InDoubleQuoteEscape;
                                break;

                            default:
                                toReturn.Append(toParse[loc]);
                                break;
                        }

                        break;

                    // We're in a " and the last char was \
                    case (ParseState.InDoubleQuoteEscape):
                        toReturn.Append(toParse[loc]);

                        parseState = ParseState.InDoubleQuote;
                        break;
                }
            }
            // In theory, there shouldn't be trailing characters...
            return toReturn.ToString();

        }

        private string GetArgument(string toParse, string methodName)
        {
            return HandleEscapes(toParse.Substring(methodName.Length + 1), '"');
        }

        /// <summary>
        /// The objects that calculate the MD5.  This weird stack thing is because HashAlgorithm isn't ThreadSafe
        /// </summary>
        private Stack<HashAlgorithm> HashCalculators = new Stack<HashAlgorithm>();

        /// <summary>
        /// Given an enumeration of script names, this returns ALL of the scripts that are needed, and their MD5s for caching.  This
        /// recursively inspects scripts to find dependant scripts underneath
        /// </summary>
        /// <param name="requestedScripts"></param>
        /// <param name="addedScripts">Scripts that have already been added through script tags</param>
        /// <returns></returns>
        public IEnumerable<ScriptAndMD5> DetermineDependantScripts(IEnumerable<string> requestedScripts, IWebConnection webConnection)
        {
            List<string> uninspected = new List<string>();

            // resolve user variables like [blahblah]
            foreach (string requestedScript in requestedScripts)
                uninspected.Add(webConnection.ResolveUserVariables(requestedScript));

            requestedScripts = uninspected.ToArray();

            // Each needed script, and the scripts that it depends on
            Dictionary<string, List<string>> dependancies = new Dictionary<string, List<string>>();

            // Cache of loaded scripts
            Dictionary<string, string> loadedScriptCache = new Dictionary<string, string>();

            // Find all of the needed scripts
            while (uninspected.Count > 0)
            {
                // These are all of the scripts that this script depends on
                List<string> scriptDependancies = new List<string>();

                string scriptToInspect = uninspected[0];
                uninspected.RemoveAt(0);

                if (!webConnection.Scripts.Contains(scriptToInspect))
                {
                    IWebResults shelledScript = webConnection.ShellTo(scriptToInspect);

                    if (null != shelledScript)
                    {
                        string script = shelledScript.ResultsAsString;

                        loadedScriptCache[scriptToInspect] = script;

                        string firstLine = script.Split('\n', '\r')[0];
                        if (firstLine.StartsWith("// Scripts:"))
                        {
                            string unbrokenScripts = firstLine.Substring(11).Trim(); // (The list of scripts must be all on the first line, seperated by commas)

                            foreach (string dependantScriptUntrimmed in unbrokenScripts.Split(','))
                            {
                                string dependantScript = webConnection.ResolveUserVariables(dependantScriptUntrimmed.Trim());

                                // If the script hasn't been scanned, queue it for scanning
                                if (!uninspected.Contains(dependantScript))
                                    if (!dependancies.ContainsKey(dependantScript))
                                        uninspected.Add(dependantScript);

                                scriptDependancies.Add(dependantScript);
                            }
                        }

                        dependancies[scriptToInspect] = scriptDependancies;
                    }
                }
            }

            // Now that all of the needed scripts, and what depends on what are known, create a complete list of needed
            // scripts, sorted so that the lowest-level dependancy comes first
            List<string> sortedDependancies = new List<string>();
            SortDependantScriptsHelper(requestedScripts, dependancies, sortedDependancies);

            // Calculate the results to return
            List<ScriptAndMD5> toReturn = new List<ScriptAndMD5>();
            foreach (string scriptName in sortedDependancies)
                if (!webConnection.Scripts.Contains(scriptName))
                {
                    ScriptAndMD5 scriptAndMD5 = new ScriptAndMD5();
                    scriptAndMD5.ScriptName = scriptName;

                    byte[] scriptBytes = System.Text.Encoding.UTF8.GetBytes(loadedScriptCache[scriptName]);

                    // Get a free hash calculator
                    HashAlgorithm hashAlgorithm = null;
                    using (TimedLock.Lock(HashCalculators))
                        if (HashCalculators.Count > 0)
                            hashAlgorithm = HashCalculators.Pop();

                    // If one isn't free, make it
                    if (null == hashAlgorithm)
                        hashAlgorithm = new System.Security.Cryptography.MD5CryptoServiceProvider();

                    byte[] scriptHash;
                    try
                    {
                        scriptHash = hashAlgorithm.ComputeHash(scriptBytes);
                    }
                    finally
                    {
                        // Save the hash calculator for reuse
                        using (TimedLock.Lock(HashCalculators))
                            HashCalculators.Push(hashAlgorithm);
                    }

                    scriptAndMD5.MD5 = Convert.ToBase64String(scriptHash);

                    toReturn.Add(scriptAndMD5);

                    webConnection.Scripts.Add(scriptName);
                }

            return toReturn;
        }

        /// <summary>
        /// Recursive helper method to sort and order dependancies
        /// </summary>
        /// <param name="requestedScript"></param>
        /// <param name="dependancies"></param>
        /// <param name="sortedDependancies"></param>
        private void SortDependantScriptsHelper(IEnumerable<string> requestedScripts, IDictionary<string, List<string>> dependancies, List<string> sortedDependancies)
        {
            foreach (string requestedScript in requestedScripts)
			{
                List<string> subRequestedScripts = null;
                if (dependancies.TryGetValue(requestedScript, out subRequestedScripts))
                    SortDependantScriptsHelper(subRequestedScripts, dependancies, sortedDependancies);
	
                if (!sortedDependancies.Contains(requestedScript))
                    sortedDependancies.Add(requestedScript);
            }
        }
    }
}
