// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Generates complete files from templates
    /// </summary>
    public class TemplateEngine : WebHandler
    {
        private static ILog log = LogManager.GetLogger<TemplateEngine>();

        /// <summary>
        /// ObjectCloud's templating xml namespace
        /// </summary>
        private const string TemplateNamespace = "objectcloud_templating";

        /// <summary>
        /// A temporary namespace for tagging nodes; all nodes and attributes of this namespace will be removed prior to returning a document
        /// </summary>
        private const string TaggingNamespace = "objectcloud_templating_GHDTTGXDNHT";

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults Evaluate(IWebConnection webConnection, string filename)
        {
            EvaluateToString(
                webConnection,
                webConnection.GetParameters, 
                filename, 
                delegate(Stream results)
                {
                    IWebResults toReturn = WebResults.FromStream(Status._200_OK, results);
                    toReturn.ContentType = "text/xml";
                    webConnection.SendResults(toReturn);
                });

            return null;
        }

        /// <summary>
        /// Assists in tracking the current working directory
        /// </summary>
        private class CWDTracker
        {
            public string GetCWD(XmlNode xmlNode)
            {
                while (null != xmlNode)
                {
                    XmlAttribute cwdAttribute = xmlNode.Attributes.GetNamedItem(
                        "cwd",
                        TaggingNamespace) as XmlAttribute;

                    if (null != cwdAttribute)
                        return cwdAttribute.Value;

                    xmlNode = xmlNode.ParentNode;
                }

                throw new KeyNotFoundException("Can not find CWD: " + xmlNode.OuterXml);
            }

            public void SetCWD(XmlNodeList xmlNodeList, string cwd)
            {
                foreach (XmlNode xmlNode in xmlNodeList)
                    SetCWD(xmlNode, cwd);
            }

            public void SetCWD(XmlNode xmlNode, string cwd)
            {
                // Really, the CWD can only be set on XmlElements
                // But sometimes, an XmlText can come in, when that happens, set the cwd on its children

                if (xmlNode is XmlElement)
                {
                    XmlAttribute xmlAttribute = xmlNode.OwnerDocument.CreateAttribute("cwd", TaggingNamespace);
                    xmlAttribute.Value = cwd;
                    xmlNode.Attributes.Append(xmlAttribute);
                }
                else if (xmlNode is XmlText)
                    SetCWD(xmlNode.ChildNodes, cwd);
            }
        }

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="getParameters"></param>
        /// <param name="filename"></param>
        /// <param name="resultsCallback"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public void EvaluateToString(
            IWebConnection webConnection,
            IDictionary<string, string> getParameters,
            string filename,
            GenericArgument<Stream> resultsCallback)
        {
            XmlDocument templateDocument = null;

            Thread myThread = Thread.CurrentThread;
            TimerCallback timerCallback = delegate(object state)
            {
#if DEBUG
                // If the program is in the debugger, play a few tricks to not abort while stepping through
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    DateTime start = DateTime.UtcNow;

                    for (int ctr = 0; ctr < 10; ctr++)
                        Thread.Sleep(10);

                    if (start.AddSeconds(1) < DateTime.UtcNow)
                        return;
                }
#endif

                myThread.Abort();
            };

            using (new Timer(timerCallback, null, 15000, 15000))
                try
                {
                    IFileContainer templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);

                    webConnection.TouchedFiles.Add(templateFileContainer);

                    CWDTracker cwdTracker = new CWDTracker();
                    templateDocument = ResolveHeaderFooter(webConnection, getParameters, templateFileContainer, cwdTracker);

                    LinkedList<string> scripts = new LinkedList<string>();
                    LinkedList<string> cssFiles = new LinkedList<string>();
                    SortedDictionary<double, LinkedList<XmlNode>> headerNodes = new SortedDictionary<double, LinkedList<XmlNode>>();

                    // Keep resolving oc:if, oc:component, oc:script, and oc:css tags while they're loaded
                    bool continueResolving;
                    do
                    {
                        continueResolving = ResolveComponentsAndConditionals(webConnection, getParameters, templateDocument, cwdTracker);
                        continueResolving = continueResolving | ResolveScriptsAndCSSInsertHead(
                            webConnection, getParameters, templateDocument, cwdTracker, scripts, cssFiles, headerNodes);
                    } while (continueResolving);

                    XmlNode headNode = GetHeadNode(templateDocument);

                    // Add script and css tags to the header
                    GenerateScriptAndCssTags(templateDocument, scripts, cssFiles, headNode);

                    // Add <oc:inserthead> tags to the header
                    GenerateHeadTags(templateDocument, headerNodes, headNode);
                }
                catch (ThreadAbortException tae)
                {
                    log.Warn("Timeout rendering a template", tae);

                    if (null == templateDocument)
                        webConnection.SendResults(
                            WebResults.FromString(Status._500_Internal_Server_Error, "Timeout"));
                    else
                        Thread.ResetAbort();
                }

            // Remove all tagging nodes and attributes
            // TODO:  This should be XPath; it might be faster
            // TODO:  Disable when in "debug" mode so the developer can see "tagging" nodes and attributes
            LinkedList<XmlNode> nodesToRemove = new LinkedList<XmlNode>();
            LinkedList<XmlNode> attributesToRemove = new LinkedList<XmlNode>();
            foreach (XmlNode xmlNode in XmlHelper.IterateAllElements(templateDocument))
            {
                if (xmlNode.NamespaceURI == TaggingNamespace)
                    nodesToRemove.AddLast(xmlNode);

                if (null != xmlNode.Attributes)
                    foreach (XmlAttribute xmlAttribute in xmlNode.Attributes)
                        if (xmlAttribute.NamespaceURI == TaggingNamespace)
                            attributesToRemove.AddLast(xmlAttribute);
            }
            foreach (XmlNode xmlNode in nodesToRemove)
                xmlNode.ParentNode.RemoveChild(xmlNode);
            foreach (XmlAttribute xmlAttribute in attributesToRemove)
                xmlAttribute.OwnerElement.Attributes.Remove(xmlAttribute);
            
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.CloseOutput = true;
            xmlWriterSettings.ConformanceLevel = ConformanceLevel.Document;
            xmlWriterSettings.Encoding = Encoding.UTF8;

            if (webConnection.CookiesFromBrowser.ContainsKey("developer_prettyprintXML"))
            {
                xmlWriterSettings.Indent = true;
                xmlWriterSettings.IndentChars = "\t";
                xmlWriterSettings.NewLineHandling = NewLineHandling.Replace;
                xmlWriterSettings.NewLineOnAttributes = true;
                xmlWriterSettings.NewLineChars = "\n";
            }
            else
                xmlWriterSettings.NewLineHandling = NewLineHandling.None;

            MemoryStream stream = new MemoryStream();
            XmlWriter xmlWriter = XmlWriter.Create(stream, xmlWriterSettings);
            templateDocument.Save(xmlWriter);

            stream.Seek(0, SeekOrigin.Begin);

            resultsCallback(stream);
        }

        private static XmlNode GetHeadNode(XmlDocument templateDocument)
        {
            XmlNodeList headNodeList = templateDocument.GetElementsByTagName("head");
            if (headNodeList.Count != 1)
                throw new WebResultsOverrideException(WebResults.FromString(Status._500_Internal_Server_Error, "Generated document does not have a <head>:\n" + templateDocument.OuterXml));

            XmlNode headNode = headNodeList[0];
            return headNode;
        }

        private static void GenerateScriptAndCssTags(XmlDocument templateDocument, LinkedList<string> scripts, LinkedList<string> cssFiles, XmlNode headNode)
        {
            Set<string> addedScripts = new Set<string>();
            foreach (string script in scripts)
                if (!addedScripts.Contains(script))
                {
                    XmlNode scriptNode = templateDocument.CreateElement("script", headNode.NamespaceURI);

                    XmlAttribute srcAttribute = templateDocument.CreateAttribute("src");
                    srcAttribute.Value = script;

                    // type="text/javascript"
                    XmlAttribute typeAttribute = templateDocument.CreateAttribute("type");
                    typeAttribute.Value = "text/javascript";

                    scriptNode.Attributes.Append(srcAttribute);
                    scriptNode.Attributes.Append(typeAttribute);

                    headNode.AppendChild(scriptNode);

                    addedScripts.Add(script);
                }

            Set<string> addedCssFiles = new Set<string>();
            foreach (string cssFile in cssFiles)
                if (!addedCssFiles.Contains(cssFile))
                {
                    XmlNode cssNode = templateDocument.CreateElement("link", headNode.NamespaceURI);

                    // <link rel="stylesheet" type="text/css" media="screen, projection" href="//a.fsdn.com/sd/core-tidied.css?T_2_5_0_299" > 

                    XmlAttribute srcAttribute = templateDocument.CreateAttribute("href");
                    srcAttribute.Value = cssFile;

                    XmlAttribute typeAttribute = templateDocument.CreateAttribute("type");
                    typeAttribute.Value = "text/css";

                    XmlAttribute relAttribute = templateDocument.CreateAttribute("rel");
                    relAttribute.Value = "stylesheet";

                    cssNode.Attributes.Append(srcAttribute);
                    cssNode.Attributes.Append(typeAttribute);
                    cssNode.Attributes.Append(relAttribute);

                    headNode.AppendChild(cssNode);

                    addedCssFiles.Add(cssFile);
                }
        }

        private static void GenerateHeadTags(XmlDocument templateDocument, SortedDictionary<double, LinkedList<XmlNode>> headerNodes, XmlNode headNode)
        {
            foreach (double loc in headerNodes.Keys)
                if (loc < 0)
                    foreach (XmlNode xmlNode in headerNodes[loc])
                        headNode.PrependChild(xmlNode);
                else
                    foreach (XmlNode xmlNode in headerNodes[loc])
                        headNode.InsertAfter(xmlNode, headNode.LastChild);

            // handle oc:title, if present
            // TODO:  Use XPATH
            XmlNodeList ocTitleNodes = headNode.OwnerDocument.GetElementsByTagName("title", TemplateNamespace);

            if (ocTitleNodes.Count > 1)
                for (int ctr = 1; ctr < ocTitleNodes.Count; ctr++)
                {
                    XmlNode ocTitleNode = ocTitleNodes[ctr];
                    ocTitleNode.ParentNode.RemoveChild(ocTitleNode);
                }

            if (ocTitleNodes.Count > 0)
            {
                XmlNode ocTitleNode = ocTitleNodes[0];

                bool titleNodePresent = false;
                foreach (XmlNode titleNode in headNode.OwnerDocument.GetElementsByTagName("title", headNode.NamespaceURI))
                    if (titleNode.ParentNode == ocTitleNode.ParentNode)
                        titleNodePresent = true;

                if (!titleNodePresent)
                {
                    XmlNode titleNode = headNode.OwnerDocument.CreateElement("title", headNode.NamespaceURI);

                    foreach (XmlNode subNode in ocTitleNode.ChildNodes)
                        titleNode.AppendChild(subNode);

                    foreach (XmlAttribute attribute in ocTitleNode.Attributes)
                        titleNode.Attributes.Append(attribute);

                    ocTitleNode.ParentNode.InsertAfter(titleNode, ocTitleNode);
                }

                ocTitleNode.ParentNode.RemoveChild(ocTitleNode);
            }
        }

        /// <summary>
        /// Handles oc:if and oc:component tags
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="getParameters"></param>
        /// <param name="templateDocument"></param>
        /// <param name="cwdTracker"></param>
        private bool ResolveComponentsAndConditionals(IWebConnection webConnection, IDictionary<string, string> getParameters, XmlDocument templateDocument, CWDTracker cwdTracker)
        {
            bool continueResolvingComponentsAndConditionals;
            bool handledTags = false;

            do
            {
                continueResolvingComponentsAndConditionals = false;

                XmlNodeList conditionalNodes = templateDocument.GetElementsByTagName("if", TemplateNamespace);
                if (conditionalNodes.Count > 0)
                {
                    continueResolvingComponentsAndConditionals = true;
                    handledTags = true;

                    // TODO:  These should be evaluated
                    for (int ctr = 0; ctr < conditionalNodes.Count; ctr++)
                        HandleConditional(webConnection, getParameters, cwdTracker, conditionalNodes[ctr]);
                }

                XmlNodeList componentNodes = templateDocument.GetElementsByTagName("component", TemplateNamespace);
                if (componentNodes.Count > 0)
                {
                    continueResolvingComponentsAndConditionals = true;
                    handledTags = true;

                    // TODO:  These should be evaluated
                    for (int ctr = 0; ctr < componentNodes.Count; ctr++)
                        LoadComponent(webConnection, getParameters, cwdTracker, componentNodes[ctr]);

                }
            } while (continueResolvingComponentsAndConditionals);

            return handledTags;
        }

        /// <summary>
        /// Handles oc:if and oc:component tags
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="getParameters"></param>
        /// <param name="templateDocument"></param>
        /// <param name="cwdTracker"></param>
        /// <param name="cssFiles"></param>
        /// <param name="scripts"></param>
        /// <param name="headerNodes"></param>
        private bool ResolveScriptsAndCSSInsertHead(
            IWebConnection webConnection, 
            IDictionary<string, string> getParameters, 
            XmlDocument templateDocument, 
            CWDTracker cwdTracker,
            LinkedList<string> scripts,
            LinkedList<string> cssFiles,
            SortedDictionary<double, LinkedList<XmlNode>> headerNodes)
        {
            LinkedList<XmlNode> toRemove = new LinkedList<XmlNode>();

            // TODO:  Another situation where XPATH would be better...

            foreach (XmlNode xmlNode in XmlHelper.IterateAllElements(templateDocument))
                if (xmlNode.NamespaceURI == TemplateNamespace)
                {
                    if (xmlNode.LocalName == "script")
                    {
                        XmlAttribute srcAttribute = xmlNode.Attributes["src"];

                        if (null != srcAttribute)
                            scripts.AddLast(FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                                cwdTracker.GetCWD(xmlNode),
                                srcAttribute.Value));

                        toRemove.AddLast(xmlNode);
                    }
                    else if (xmlNode.LocalName == "open")
                    {
                        XmlAttribute filenameAttribute = xmlNode.Attributes["filename"];
                        XmlAttribute varnameAttribute = xmlNode.Attributes["varname"];

                        if ((null != filenameAttribute) && (null != varnameAttribute))
                            scripts.AddLast(string.Format(
                                "{0}?Method=GetJSW&assignToVariable={1}",
                                FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(cwdTracker.GetCWD(xmlNode), filenameAttribute.Value),
                                varnameAttribute.Value));

                        toRemove.AddLast(xmlNode);
                    }
                    else if (xmlNode.LocalName == "css")
                    {
                        XmlAttribute srcAttribute = xmlNode.Attributes["src"];

                        if (null != srcAttribute)
                            cssFiles.AddLast(FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                                cwdTracker.GetCWD(xmlNode),
                                srcAttribute.Value));

                        toRemove.AddLast(xmlNode);
                    }
                    else if (xmlNode.LocalName == "inserthead")
                    {
                        // Try reading loc, default to 0

                        XmlAttribute locAttribute = xmlNode.Attributes["loc"];
                        double loc;

                        if (null != locAttribute)
                        {
                            if (!double.TryParse(locAttribute.Value, out loc))
                                loc = 0;
                        }
                        else
                            loc = 0;

                        LinkedList<XmlNode> insertNodes;
                        if (!headerNodes.TryGetValue(loc, out insertNodes))
                        {
                            insertNodes = new LinkedList<XmlNode>();
                            headerNodes[loc] = insertNodes;
                        }

                        foreach (XmlNode headNode in xmlNode.ChildNodes)
                            if (loc < 0)
                                insertNodes.AddFirst(headNode);
                            else
                                insertNodes.AddLast(headNode);

                        toRemove.AddLast(xmlNode);
                    }
                }

            if (toRemove.Count > 0)
            {
                foreach (XmlNode xmlNode in toRemove)
                    xmlNode.ParentNode.RemoveChild(xmlNode);

                return true;
            }

            return false;
        }

        private void HandleConditional(
            IWebConnection webConnection,
            IDictionary<string, string> getParameters,
            CWDTracker cwdTracker,
            XmlNode conditionalNode)
        {
            Dictionary<string, ITemplateConditionHandler> templateConditionHandlers = FileHandlerFactoryLocator.TemplateHandlerLocator.TemplateConditionHandlers;

            XmlNode current = conditionalNode.FirstChild;
            bool conditionMet = false;

            // Note:  Looping continues after the condition is met to ensure that all nodes are in the correct namespace
            while (null != current)
            {
                // Make sure the namespace is proper
                if (current.NamespaceURI != TemplateNamespace)
                    conditionalNode.ParentNode.InsertBefore(
                        GenerateWarningNode(conditionalNode.OwnerDocument, "All nodes within an <if> must be of " + TemplateNamespace + " namespace: " + current.OuterXml),
                        conditionalNode);

                else
                {
                    // Make sure the node is supported
                    ITemplateConditionHandler templateConditionHandler;
                    if (!templateConditionHandlers.TryGetValue(current.LocalName, out templateConditionHandler))
                        conditionalNode.ParentNode.InsertBefore(
                            GenerateWarningNode(conditionalNode.OwnerDocument, "There is no condition handler for " + current.LocalName + ": " + current.OuterXml),
                            conditionalNode);

                    if (!conditionMet)
                        try
                        {
                            if (templateConditionHandler.IsConditionMet(webConnection, current, cwdTracker.GetCWD(current)))
                            {
                                conditionMet = true;

                                foreach (XmlNode xmlNode in current.ChildNodes)
                                    conditionalNode.ParentNode.InsertBefore(xmlNode, conditionalNode);
                            }
                        }
                        catch (Exception e)
                        {
                            log.Error("Unhandled error for condition tag " + current.LocalName + ": " + current.OuterXml, e);

                            conditionalNode.ParentNode.InsertBefore(
                                GenerateWarningNode(conditionalNode.OwnerDocument, "An unhandled error occured processing: " + current.OuterXml),
                                conditionalNode);
                        }
                }

                current = current.NextSibling;
            }

            conditionalNode.ParentNode.RemoveChild(conditionalNode);
        }


        private void LoadComponent(
            IWebConnection webConnection,
            IDictionary<string, string> getParameters,
            CWDTracker cwdTracker,
            XmlNode componentNode)
        {
            XmlAttribute srcAttribute = (XmlAttribute)componentNode.Attributes.GetNamedItem("src", TemplateNamespace);
            XmlAttribute urlAttribute = (XmlAttribute)componentNode.Attributes.GetNamedItem("url", TemplateNamespace);

            if ((null == srcAttribute) && (null == urlAttribute))
                // Remove empty components
                componentNode.ParentNode.RemoveChild(componentNode);

            else if ((null != srcAttribute) && (null != urlAttribute))
                ReplaceNode(
                    componentNode,
                    GenerateWarningNode(componentNode.OwnerDocument, "Either src or div can be specified; you can not choose both: " + componentNode.OuterXml));

            else if (null != srcAttribute)
            {
                // handle GET parameters
                // First, handle oc:getpassthrough
                IDictionary<string, string> myGetParameters;
                XmlAttribute getpassthroughAttribute = (XmlAttribute)componentNode.Attributes.GetNamedItem("getpassthough", TemplateNamespace);
                if (null == getpassthroughAttribute)
                    myGetParameters = DictionaryFunctions.Create<string, string>(getParameters);
                else
                {
                    if ("false" == getpassthroughAttribute.Value)
                        myGetParameters = new Dictionary<string, string>();
                    else
                        myGetParameters = DictionaryFunctions.Create<string, string>(getParameters);
                }

                // Next, pull out get parameters from the tag
                foreach (XmlAttribute attribute in componentNode.Attributes)
                    if ("" == attribute.NamespaceURI)
                        myGetParameters[attribute.LocalName] = attribute.Value;

                string fileName = FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                    cwdTracker.GetCWD(componentNode),
                    srcAttribute.Value);

                IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(fileName);
                webConnection.TouchedFiles.Add(fileContainer);

                // If the user doesn't have permission for the component and the component has something to use instead, use it
                if (null == fileContainer.LoadPermission(webConnection.Session.User.Id) && componentNode.HasChildNodes)
                {
                    XmlNode replacement = componentNode.OwnerDocument.CreateElement("div");

                    foreach (XmlNode errorNode in componentNode.ChildNodes)
                        replacement.AppendChild(errorNode);

                    componentNode.ParentNode.InsertAfter(replacement, componentNode);
                    componentNode.ParentNode.RemoveChild(componentNode);
                }
                else
                {
                    // If the user has permission, resolve the component and deal with default errors

                    XmlDocument componentDocument;

                    // TODO:  GET Parameters

                    try
                    {
                        componentDocument = LoadXmlDocumentAndReplaceGetParameters(
                            webConnection,
                            myGetParameters,
                            fileContainer);

                        XmlNode firstChild = componentDocument.FirstChild;
                        XmlNodeList replacementNodes;
                        if ((firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplateNamespace))
                            replacementNodes = firstChild.ChildNodes;
                        else
                            replacementNodes = componentDocument.ChildNodes;

                        cwdTracker.SetCWD(replacementNodes, fileContainer.ParentDirectoryHandler.FileContainer.FullPath);
                        ReplaceNode(componentNode, replacementNodes);
                    }
                    catch (WebResultsOverrideException wroe)
                    {
                        ReplaceNode(
                            componentNode,
                            GenerateWarningNode(componentNode.OwnerDocument, wroe.WebResults.ResultsAsString));
                    }
                    catch (Exception e)
                    {
                        log.Error("An error occured when loading a component", e);

                        ReplaceNode(
                            componentNode,
                            GenerateWarningNode(componentNode.OwnerDocument, "An unhandled error occured.  See the system logs for more information"));
                    }
                }
            }

            else if (null != urlAttribute)
            {
                XmlNode resultNode;
                string url = urlAttribute.Value;

                foreach (XmlAttribute attribute in componentNode.Attributes)
                    if ("" == attribute.NamespaceURI)
                        url = HTTPStringFunctions.AppendGetParameter(url, attribute.LocalName, attribute.Value);

                try
                {
                    if (url.StartsWith("https://"))
                        resultNode = GenerateWarningNode(componentNode.OwnerDocument, "https component nodes aren't supported due to certificate complexities");
                    else if (url.StartsWith("http://"))
                    {
                        HttpResponseHandler httpResponse = webConnection.Session.HttpWebClient.Get(url);

                        if ("text/xml" == httpResponse.ContentType)
                        {
                            XmlDocument resultDocument = new XmlDocument();
                            resultDocument.Load(httpResponse.AsString());
                            resultNode = resultDocument;
                        }
                        else
                            resultNode = componentNode.OwnerDocument.CreateTextNode(httpResponse.AsString());
                    }
                    else
                        try
                        {
                            ShellWebConnection shellWebConnection = new BlockingShellWebConnection(
                                url,
                                webConnection,
                                null,
                                webConnection.CookiesFromBrowser);

                            IWebResults shellResults = shellWebConnection.GenerateResultsForClient();

                            if ("text/xml" == shellResults.ContentType)
                            {
                                XmlDocument resultDocument = new XmlDocument();
                                resultDocument.Load(shellResults.ResultsAsStream);
                                resultNode = resultDocument;
                            }
                            else
                                resultNode = componentNode.OwnerDocument.CreateTextNode(shellResults.ResultsAsString);
                        }
                        catch (WebResultsOverrideException wroe)
                        {
                            resultNode = GenerateWarningNode(componentNode.OwnerDocument, wroe.WebResults.ResultsAsString);
                        }
                }
                catch (Exception e)
                {
                    log.Error("An error occured when loading a component", e);
                    resultNode = GenerateWarningNode(componentNode.OwnerDocument, "An unhandled error occured.  See the system logs for more information");
                }

                ReplaceNode(componentNode, resultNode);
            }
        }

        /// <summary>
        /// Assists in generating a warning node
        /// </summary>
        /// <param name="ownerDocument"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static XmlNode GenerateWarningNode(XmlDocument ownerDocument, string message)
        {
            XmlNode toReturn = ownerDocument.CreateNode(XmlNodeType.Element, "pre", ownerDocument.DocumentElement.NamespaceURI);
            toReturn.InnerText = message;

            SetErrorClass(toReturn);

            return toReturn;
        }

        const string WarningNodeClass = "oc_template_warning";

        /// <summary>
        /// Adds the class="oc_template_warning" to the node
        /// </summary>
        /// <param name="htmlNode"></param>
        private static void SetErrorClass(XmlNode htmlNode)
        {
            XmlAttribute classAttribute = (XmlAttribute)htmlNode.OwnerDocument.CreateAttribute("class");
            classAttribute.Value = WarningNodeClass;
            htmlNode.Attributes.Append(classAttribute);
        }

        /// <summary>
        /// Returns the file with all header/footers resolved as XML for further processing
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="templateFileContainer"></param>
        /// <param name="webConnection"></param>
        /// <param name="cwdTracker"></param>
        /// <returns></returns>
        private XmlDocument ResolveHeaderFooter(
            IWebConnection webConnection,
            IDictionary<string, string> getParameters,
            IFileContainer templateFileContainer,
            CWDTracker cwdTracker)
        {
            XmlDocument templateDocument = LoadXmlDocumentAndReplaceGetParameters(webConnection, getParameters, templateFileContainer);

            /*IFileHandler templateFileHandler = templateFileContainer.FileHandler;

            if (!(templateFileHandler is ITextHandler))
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, templateFileContainer.FullPath + " must be a text file"));

            string templateContents = ((ITextHandler)templateFileHandler).ReadAll();
            templateContents = ReplaceGetParameters(getParameters, templateContents);

            XmlDocument templateDocument = new XmlDocument();

            try
            {
                templateDocument.LoadXml(templateContents);
            }
            catch (XmlException xmlException)
            {
                // If the current user is the owner or an administrator; display an informative error

                // For everyone else, just let the error bubble up
                throw;
            }*/

            // While the first node isn't HTML, keep loading header/footers
            while ("html" != templateDocument.FirstChild.LocalName)
            {
                XmlNode firstChild = templateDocument.FirstChild;
                string headerFooter = "/DefaultTemplate/headerfooter.ochf";

                XmlNodeList nodesToInsert;
                if (("componentdef" == firstChild.LocalName) && (TemplateNamespace == firstChild.NamespaceURI))
                {
                    XmlAttribute headerFooterAttribue = firstChild.Attributes["headerfooter"];
                    if (null != headerFooterAttribue)
                        headerFooter = FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                            templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath,
                            headerFooterAttribue.Value);

                    nodesToInsert = firstChild.ChildNodes;
                }
                else
                    nodesToInsert = templateDocument.ChildNodes;

                cwdTracker.SetCWD(nodesToInsert, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);

                templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(headerFooter);

                templateDocument = LoadXmlDocumentAndReplaceGetParameters(
                    webConnection,
                    getParameters,
                    templateFileContainer);

                // find oc:component tag
                XmlNodeList componentTags = templateDocument.GetElementsByTagName("component", TemplateNamespace);
                for (int ctr = 0; ctr < componentTags.Count; ctr++)
                {
                    XmlNode componentNode = componentTags[ctr];

                    if ((null == componentNode.Attributes.GetNamedItem("url", TemplateNamespace)) && (null == componentNode.Attributes.GetNamedItem("src", TemplateNamespace)))
                        ReplaceNode(componentNode, nodesToInsert);
                }
            }

            cwdTracker.SetCWD(templateDocument.ChildNodes, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);

            return templateDocument;
        }

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="getParameters"></param>
        /// <param name="fileContainer"></param>
        /// <returns></returns>
        private static XmlDocument LoadXmlDocumentAndReplaceGetParameters(
            IWebConnection webConnection, 
            IDictionary<string, string> getParameters, 
            IFileContainer fileContainer)
        {
            webConnection.TouchedFiles.Add(fileContainer);

            // Verify permission
            if (null == fileContainer.LoadPermission(webConnection.Session.User.Id))
                throw new WebResultsOverrideException(WebResults.FromString(Status._401_Unauthorized, "You do not have permission to read " + fileContainer.FullPath));

            if (!(fileContainer.FileHandler is ITextHandler))
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, fileContainer.FullPath + " must be a text file"));

            XmlDocument xmlDocument = new XmlDocument();
            string xml = ((ITextHandler)fileContainer.FileHandler).ReadAll();

            try
            {
                xmlDocument.LoadXml(ReplaceGetParameters(getParameters, xml));
            }
            catch (XmlException xmlException)
            {
                // Double-check read permission (in case later edits allow non-readers to still use a template with a named permission
                if (null == fileContainer.LoadPermission(webConnection.Session.User.Id))
                    throw;

                // Everyone else can see a nice descriptive error
                StringBuilder errorBuilder = new StringBuilder(string.Format("An error occured while loading {0}\n", fileContainer.FullPath));
                errorBuilder.AppendFormat("{0}\n\n\nFrom:\n{1}", xmlException.Message, xml);

                throw new WebResultsOverrideException(WebResults.FromString(Status._500_Internal_Server_Error, errorBuilder.ToString()));
            }


            return xmlDocument;
        }

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        private static void ReplaceNode(XmlNode componentNode, XmlNodeList newNodes)
        {
            ReplaceNode(componentNode, Enumerable<XmlNode>.Cast(newNodes));
        }

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        private static void ReplaceNode(XmlNode componentNode, params XmlNode[] newNodes)
        {
            ReplaceNode(componentNode, newNodes as IEnumerable<XmlNode>);
        }

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        private static void ReplaceNode(XmlNode componentNode, IEnumerable<XmlNode> newNodes)
        {
            XmlNode previousNode = componentNode;

            // replace this node with the document
            foreach (XmlNode loadedNode in newNodes)
            {
                XmlNode newNode = componentNode.OwnerDocument.ImportNode(loadedNode, true);
                componentNode.ParentNode.InsertAfter(newNode, previousNode);
                previousNode = newNode;
            }

            componentNode.ParentNode.RemoveChild(componentNode);
        }

        /// <summary>
        /// Delimeter for the beginning of GET arguments
        /// </summary>
        static string[] ArgBegin = new string[] { "[_" };

        /// <summary>
        /// Delimeter for the end of GET arguments
        /// </summary>
        static string[] ArgEnd = new string[] { "_]" };

        /// <summary>
        /// Replaces all of the GET parameters in a string
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="templateContents"></param>
        /// <returns></returns>
        private static string ReplaceGetParameters(IDictionary<string, string> getParameters, string templateContents)
        {
            // generate [_ ! _]
            string unique = "u" + SRandom.Next<uint>().ToString();

            // Split at [_
            StringBuilder getArgumentsResolvedBuilder = new StringBuilder(Convert.ToInt32(1.1 * Convert.ToDouble(templateContents.Length)));

            // Allocate the results builder, give a little breathing room in case the size grows
            string[] templateSplitAtArgs = templateContents.Split(ArgBegin, StringSplitOptions.None);

            int ctr;
            if (templateContents.StartsWith("[_"))
                ctr = 0;
            else
            {
                ctr = 1;
                getArgumentsResolvedBuilder.Append(templateSplitAtArgs[0]);
            }

            for (; ctr < templateSplitAtArgs.Length; ctr++)
            {
                string[] argumentAndTemplateParts = templateSplitAtArgs[ctr].Split(ArgEnd, 2, StringSplitOptions.None);

                if (argumentAndTemplateParts.Length != 2)
                {
                    // If there is no _], put this back as-is
                    getArgumentsResolvedBuilder.Append("[_");
                    getArgumentsResolvedBuilder.Append(templateSplitAtArgs[ctr]);
                }
                else
                {
                    string argument = StringParser.XmlDecode(argumentAndTemplateParts[0].Trim());
                    string remainder = argumentAndTemplateParts[1];

                    if (getParameters.ContainsKey(argument))
                        getArgumentsResolvedBuilder.Append(StringParser.XmlEncode(getParameters[argument]));
                    else if ("!" == argument)
                        getArgumentsResolvedBuilder.Append(unique);

                    getArgumentsResolvedBuilder.Append(remainder);
                }
            }

            return getArgumentsResolvedBuilder.ToString();
        }


    }
}
