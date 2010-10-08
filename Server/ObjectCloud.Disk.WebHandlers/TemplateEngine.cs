// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Disk.WebHandlers.Template;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Generates complete files from templates
    /// </summary>
    public class TemplateEngine : WebHandler, ITemplateEngine
    {
        private static ILog log = LogManager.GetLogger<TemplateEngine>();

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults Evaluate(IWebConnection webConnection, string filename)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            foreach (KeyValuePair<string, string> getParameter in webConnection.GetParameters)
                arguments[getParameter.Key] = getParameter.Value;

            return Evaluate(webConnection, filename, arguments);
        }

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public IWebResults Evaluate(IWebConnection webConnection, string filename, IDictionary<string, object> arguments)
        {
            try
            {
                Stream results = EvaluateToStream(
                    webConnection,
                    arguments,
                    filename);

                IWebResults toReturn;

                // Hack to work around a bug in IE handling xhtml
                // Basically, IE won't handle &gt; and &lt; in xhtml
                string userAgent;
                if (webConnection.Headers.TryGetValue("USER-AGENT", out userAgent))
                    if (userAgent.Contains(" MSIE "))
                    {
                        StreamReader sr = new StreamReader(results);
                        string result = sr.ReadToEnd();
						result = result.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?><html", "");
                        result = result.Split(new char[] { '>' }, 2)[1];
                        result = "<!DOCTYPE html>\n<html>" + result;
					
					    string[] splitAtScriptTag = result.Split(new string[] {"<script>"}, StringSplitOptions.None);
						StringBuilder resultBuilder = new StringBuilder(splitAtScriptTag[0]);
					
						for (int ctr = 1; ctr < splitAtScriptTag.Length; ctr++)
						{
							string[] scriptAndPostTags = splitAtScriptTag[ctr].Split(new string[] {"</script>"}, StringSplitOptions.None);
						
							if (scriptAndPostTags.Length == 1)
								resultBuilder.Append(scriptAndPostTags[0]);
							else
							{
								string script = scriptAndPostTags[0].Replace("&gt;", ">").Replace("&lt;", "<");
								resultBuilder.Append("<script>");
								resultBuilder.Append(script);
								resultBuilder.Append("</script>");
								resultBuilder.Append(scriptAndPostTags[1]);
							}
						}

                        toReturn = WebResults.From(Status._200_OK, resultBuilder.ToString());
		                toReturn.ContentType = "text/html";
		
		                return toReturn;
                    }

                // Everyone else gets real XML
                toReturn = WebResults.From(Status._200_OK, results);
                toReturn.ContentType = ContentType;

                return toReturn;
            }
            catch (TemplateException te)
            {
                throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, te.Message), te);
            }
        }

        /// <summary>
        /// The default content type
        /// </summary>
        public const string ContentType = "application/xhtml+xml";

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="arguments"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public Stream EvaluateToStream(
            IWebConnection webConnection,
            IDictionary<string, object> arguments,
            string filename)
        {
            TemplateParsingState templateParsingState = CreateTemplateParsingState(webConnection);

            // The code below mostly gets in the way while debugging
            /*            Thread myThread = Thread.CurrentThread;
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

                        using (new Timer(timerCallback, null, 15000, 15000))*/
            try
            {
                IFileContainer templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);

                if (null == templateFileContainer.LoadPermission(webConnection.Session.User.Id))
                    throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "Permission denied"));

                webConnection.TouchedFiles.Add(templateFileContainer);

                ResolveHeaderFooter(webConnection, arguments, templateFileContainer, templateParsingState);

                ResolveDocument(arguments, templateParsingState.TemplateDocument, templateParsingState);

                XmlNode headNode = GetHeadNode(templateParsingState.TemplateDocument);

                // Add script and css tags to the header
                GenerateScriptAndCssTags(templateParsingState, headNode);

                // Add <oc:inserthead> tags to the header
                GenerateHeadTags(templateParsingState, headNode);
            }
            catch (ThreadAbortException tae)
            {
                log.Warn("Timeout rendering a template", tae);
                throw;
                //Thread.ResetAbort();
                //throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, "Timeout"));
            }

            // Copy all elements into an immutable list, run document post-processors, and remove comments
            bool removeComments = !webConnection.CookiesFromBrowser.ContainsKey(templateParsingState.TemplateHandlerLocator.TemplatingConstants.XMLDebugModeCookie);

            foreach (XmlNode xmlNode in Enumerable<XmlNode>.FastCopy(XmlHelper.IterateAllElementsAndComments(templateParsingState.TemplateDocument)))
            {
                if (xmlNode is XmlElement)
                {
                    // This is to work around a bug where oc:... nodes show up
                    // Unfortunately, it's commented out because it's causing some weird behavior.  Sometimes components depend on
                    // these tags being present, thus the bugs for oc: tags being left around need to be fixed at a lower level
                    /*if (xmlNode.NamespaceURI ==
                        webConnection.WebServer.FileHandlerFactoryLocator.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace)
                    {
                        if (null != xmlNode.ParentNode)
                        {
                            XmlNode parentNode = xmlNode.ParentNode;

                            if (xmlNode.ChildNodes.Count > 0)
                                foreach (XmlNode childNode in Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Filter(xmlNode.ChildNodes)))
                                    parentNode.InsertBefore(childNode, xmlNode);

                            parentNode.RemoveChild(xmlNode);
                        }
                    }
                    else */
                        templateParsingState.OnPostProcessElement(arguments, (XmlElement)xmlNode);
                }
                else if (removeComments)
                    xmlNode.ParentNode.RemoveChild(xmlNode);
            }

            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.CloseOutput = true;
            xmlWriterSettings.ConformanceLevel = ConformanceLevel.Document;
            xmlWriterSettings.Encoding = Encoding.UTF8;

            if (webConnection.CookiesFromBrowser.ContainsKey(templateParsingState.TemplateHandlerLocator.TemplatingConstants.XMLDebugModeCookie))
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
            templateParsingState.TemplateDocument.Save(xmlWriter);

            stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }

        private TemplateParsingState CreateTemplateParsingState(IWebConnection webConnection)
        {
            TemplateParsingState templateParsingState = new TemplateParsingState(webConnection);

            templateParsingState.PostProcessElement += EnsureNoEmptyTextareas;

            foreach (ITemplateProcessor templateProcessor in FileHandlerFactoryLocator.TemplateHandlerLocator.TemplateProcessors)
                templateProcessor.Register(templateParsingState);
            return templateParsingState;
        }

        private static void ResolveDocument(IDictionary<string, object> arguments, XmlDocument templateDocument, TemplateParsingState templateParsingState)
        {
            templateParsingState.OnDocumentLoaded(arguments, templateDocument.FirstChild as XmlElement);

            bool continueResolving;

            XmlNodeChangedEventHandler documentChanged = delegate(object sender, XmlNodeChangedEventArgs e)
            {
                continueResolving = true;
            };

            templateDocument.NodeChanged += documentChanged;
            templateDocument.NodeInserted += documentChanged;
            templateDocument.NodeRemoved += documentChanged;

            // Keep resolving oc:if, oc:component, oc:script, and oc:css tags while they're loaded
            int loopsLeft = 20;
            do
            {
                int innerLoopsLeft = 20;

                do
                {
                    continueResolving = false;

                    foreach (XmlElement element in templateParsingState.IterateNonDeferredElements(templateDocument))
                        try
                        {
                            templateParsingState.OnProcessElementForConditionalsAndComponents(arguments, element);
                        }
                        catch (Exception e)
                        {
                            log.Error("An error occured while processing " + element.OuterXml, e);
                            templateParsingState.ReplaceNodes(
                                element,
                                templateParsingState.GenerateWarningNode("An error occured processing " + element.OuterXml));
                        }

                    innerLoopsLeft--;

                } while (continueResolving && (innerLoopsLeft > 0));

                foreach (XmlElement element in templateParsingState.IterateNonDeferredElements(templateDocument))
                    try
                    {
                        templateParsingState.OnProcessElementForDependanciesAndTemplates(arguments, element);
                    }
                    catch (Exception e)
                    {
                        log.Error("An error occured while processing " + element.OuterXml, e);
                        templateParsingState.ReplaceNodes(
                            element,
                            templateParsingState.GenerateWarningNode("An error occured processing " + element.OuterXml));
                    }

                loopsLeft--;

            } while (continueResolving && (loopsLeft > 0));

            templateDocument.NodeChanged -= documentChanged;
            templateDocument.NodeInserted -= documentChanged;
            templateDocument.NodeRemoved -= documentChanged;
        }

        /*// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults EvaluateComponent(IWebConnection webConnection, string filename)
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            foreach (KeyValuePair<string, string> getParameter in webConnection.GetParameters)
                arguments[getParameter.Key] = getParameter.Value;

            return WebResults.From(Status._200_OK,
                EvaluateComponent(webConnection, filename, arguments));
        }*/

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <param name="templateInput"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public string EvaluateComponent(IWebConnection webConnection, string filename, object templateInput)
        {
            XmlDocument templateDocument;
            TemplateParsingState templateParsingState = CreateTemplateParsingState(webConnection);

            Dictionary<string, object> arguments = new Dictionary<string,object>();
            templateParsingState.Flatten(arguments, "", templateInput);

            IFileContainer templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);

            webConnection.TouchedFiles.Add(templateFileContainer);

            templateDocument = templateParsingState.LoadXmlDocument(templateFileContainer, XmlParseMode.Xml);
            templateParsingState.TemplateDocument = templateDocument;
            templateParsingState.HandleArguments(arguments, templateDocument);

            templateParsingState.SetCWD(templateDocument.ChildNodes, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);

            ResolveDocument(arguments, templateDocument, templateParsingState);

            foreach (XmlNode xmlNode in Enumerable<XmlNode>.FastCopy(XmlHelper.IterateAllElementsAndComments(templateDocument)))
                if (xmlNode is XmlElement)
                    templateParsingState.OnPostProcessElement(arguments, (XmlElement)xmlNode);
                else if (xmlNode is XmlComment)
                    xmlNode.ParentNode.RemoveChild(xmlNode);

            return templateDocument.FirstChild.InnerXml;
        }

        /// <summary>
        /// This is to work around a Mozilla quirk where it can't handle empty text areas
        /// </summary>
        /// <param name="templateParsingState"></param>
        /// <param name="getParameters"></param>
        /// <param name="element"></param>
        void EnsureNoEmptyTextareas(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlElement element)
        {
            if (element.LocalName == "textarea" && templateParsingState.TemplateHandlerLocator.TemplatingConstants.HtmlNamespaces.Contains(element.NamespaceURI))
                if (element.IsEmpty)
                    element.IsEmpty = false;
        }

        private static XmlNode GetHeadNode(XmlDocument templateDocument)
        {
            XmlNodeList headNodeList = templateDocument.GetElementsByTagName("head");
            if (headNodeList.Count != 1)
                throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, "Generated document does not have a <head>:\n" + templateDocument.OuterXml));

            XmlNode headNode = headNodeList[0];
            return headNode;
        }

        private void GenerateScriptAndCssTags(ITemplateParsingState templateParsingState, XmlNode headNode)
        {
            XmlDocument templateDocument = templateParsingState.TemplateDocument;
            LinkedList<string> cssFiles = templateParsingState.CssFiles;

            foreach (string script in templateParsingState.Scripts)
            {
                XmlNode scriptNode = templateDocument.CreateElement("script", headNode.NamespaceURI);

                XmlAttribute srcAttribute = templateDocument.CreateAttribute("src");
                srcAttribute.Value = script;

                // type="text/javascript"
                XmlAttribute typeAttribute = templateDocument.CreateAttribute("type");
                typeAttribute.Value = "text/javascript";

                scriptNode.Attributes.Append(srcAttribute);
                scriptNode.Attributes.Append(typeAttribute);
                scriptNode.InnerText = "";

                headNode.AppendChild(scriptNode);
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

            // If javascript debug mode is off, combile all of the scripts into one that loads all at once
            if (!(templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(
                templateParsingState.FileHandlerFactoryLocator.TemplateHandlerLocator.TemplatingConstants.JavascriptDebugModeCookie)))
            {
                // Generate composite script tag
                XmlElement lastLocalScriptTag = null;

                LinkedList<string> scriptUrls = new LinkedList<string>();
                LinkedList<XmlElement> scriptElements = new LinkedList<XmlElement>();

                foreach (XmlElement node in XmlHelper.IterateAllElements(headNode))
                    if (node.LocalName == "script")
                        if (templateParsingState.FileHandlerFactoryLocator.TemplateHandlerLocator.TemplatingConstants.HtmlNamespaces.Contains(node.NamespaceURI))
                        {
                            // This is a script node
                            // get the script that's being loaded
                            string src = node.GetAttribute("src");

                            if (null != src)
                                if (src.Length > 0)
                                    if (src.StartsWith("/"))

                                        // Hueristic:  All scripts without a ? are merged into one
                                        // If a script has a ?, but it's for a system file or the user database, it can also be merged
                                        if ((!(src.Contains("?"))) || (src.StartsWith("/System/")) || (src.StartsWith("/Users/UserDB?")))
                                        {
                                            lastLocalScriptTag = node;
                                            scriptElements.AddLast(node);
                                            scriptUrls.AddLast(src);
                                        }
                                        else
                                        {
                                            // If the script cannot be merged, then add the BrowserCache GET argument so that the browser can cache it

                                            IWebResults scriptResults = templateParsingState.WebConnection.ShellTo(src);

                                            src = HTTPStringFunctions.AppendGetParameter(
                                                src,
                                                "BrowserCache",
                                                StringGenerator.GenerateHash(scriptResults.ResultsAsString));

                                            node.SetAttribute("src", src);
                                        }
                }

                // Remove dead script tags and update the last one to load a composite script
                if (null != lastLocalScriptTag)
                {
                    foreach (XmlElement node in scriptElements)
                        if (node != lastLocalScriptTag)
                            node.ParentNode.RemoveChild(node);

                    string serializedScriptList = JsonWriter.Serialize(scriptUrls);
                    string compositeScript = GenerateCompositeJavascript(templateParsingState.WebConnection, scriptUrls);
                    string hash = StringGenerator.GenerateHash(compositeScript);

                    string url = FileContainer.FullPath + "?Method=GetCompositeScript";
                    url = HTTPStringFunctions.AppendGetParameter(url, "scriptUrls", serializedScriptList);
                    url = HTTPStringFunctions.AppendGetParameter(url, "BrowserCache", hash);

                    lastLocalScriptTag.SetAttribute("src", url);
                }
            }
            else
            {
                // Warn when including a missing script
                foreach (XmlElement node in XmlHelper.IterateAllElements(headNode))
                    if (node.LocalName == "script")
                    {
                        string src = node.GetAttribute("src");

                        if (null != src)
                            if (src.Length > 0)
                                if (src.StartsWith("/"))
                                {
                                    src = src.Split('?')[0];

                                    if (!src.Contains("[")) // quick hack so scripts can contain [user]
                                        if (!templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(src))
                                        {
                                            node.InnerText = "alert('" + (src + " doesn't exist: " + node.OuterXml).Replace("'", "\\'") + "');";
                                            node.RemoveAttribute("src");
                                        }
                                }
                    }
            }
        }

        private static void GenerateHeadTags(ITemplateParsingState templateParsingState, XmlNode headNode)
        {
            SortedDictionary<double, LinkedList<XmlNode>> headerNodes = templateParsingState.HeaderNodes;

            foreach (double loc in headerNodes.Keys)
                if (loc < 0)
                    foreach (XmlNode xmlNode in headerNodes[loc])
                        headNode.PrependChild(xmlNode);
                else
                    foreach (XmlNode xmlNode in headerNodes[loc])
                        headNode.InsertAfter(xmlNode, headNode.LastChild);

            // handle oc:title, if present
            // TODO:  Use XPATH
            XmlNodeList ocTitleNodes = headNode.OwnerDocument.GetElementsByTagName("title", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);

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
        /// Returns the file with all header/footers resolved as XML for further processing
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="templateFileContainer"></param>
        /// <param name="webConnection"></param>
        /// <param name="templateParsingState"></param>
        /// <returns></returns>
        private void ResolveHeaderFooter(
            IWebConnection webConnection,
            IDictionary<string, object> arguments,
            IFileContainer templateFileContainer,
            TemplateParsingState templateParsingState)
        {
            Set<string> checkedHeaderFooters = new Set<string>();

            XmlDocument templateDocument = templateParsingState.LoadXmlDocumentAndReplaceGetParameters(arguments, templateFileContainer, XmlParseMode.Xml);
			
			// I think this is to work around an issue when directly viewing a template with an empty <oc:component /> tag
			templateParsingState.TemplateDocument = templateDocument;

            // While the first node isn't HTML, keep loading header/footers
            while ("html" != templateDocument.FirstChild.LocalName)
            {
                XmlNode firstChild = templateDocument.FirstChild;
                string headerFooter = "/DefaultTemplate/headerfooter.ochf";

                XmlNodeList nodesToInsert;
                if (("componentdef" == firstChild.LocalName) && (templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace == firstChild.NamespaceURI))
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
				
				string headerFooterOverride;
				if (webConnection.GetParameters.TryGetValue("HeaderFooterOverride", out headerFooterOverride))
					headerFooter = headerFooterOverride;

                if (checkedHeaderFooters.Contains(headerFooter))
                    throw new TemplateException("Looping within templates:  " + headerFooter + " eventually points to itself as a headerfooter!!!");
                else
                    checkedHeaderFooters.Add(headerFooter);

                templateParsingState.SetCWD(nodesToInsert, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);

				try
				{
                	templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(headerFooter);
				} catch (FileDoesNotExist fdne)
				{
					log.Error(headerFooter + " does not exist", fdne);
					
					throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, headerFooter + " does not exist"));
				}

                templateDocument = templateParsingState.LoadXmlDocumentAndReplaceGetParameters(
                    arguments,
                    templateFileContainer,
                    XmlParseMode.Xml);

                templateParsingState.TemplateDocument = templateDocument;

                // find <oc:component /> tag
                int numOcComponentTags = 0;
                XmlNodeList componentTags = templateDocument.GetElementsByTagName("component", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);
                foreach (XmlNode componentNode in Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(componentTags)))
                    if ((null == componentNode.Attributes.GetNamedItem("url", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace)) && (null == componentNode.Attributes.GetNamedItem("src", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace)))
                    {
                        numOcComponentTags++;

                        if (1 == numOcComponentTags)
                            templateParsingState.ReplaceNodes(componentNode, nodesToInsert);

                        else
                            templateParsingState.ReplaceNodes(
                                componentNode,
                                templateParsingState.GenerateWarningNode("Warning:  Duplicate <oc:component /> tag, count " + numOcComponentTags.ToString()));
                    }

                // Attempt to recover from a missing <oc:component /> tag
                if (0 == numOcComponentTags)
                {
                    XmlNodeList bodyTags = templateDocument.GetElementsByTagName(
                        "body", templateParsingState.TemplateHandlerLocator.TemplatingConstants.HtmlNamespace);

                    XmlElement bodyTag = null;

                    if (null != bodyTags)
                        if (bodyTags.Count > 0)
                            bodyTag = (XmlElement)bodyTags[0];

                    if (null == bodyTag)
                    {
                        bodyTag = (XmlElement)templateDocument.FirstChild;
                    }

                    bodyTag.AppendChild(
                        templateParsingState.GenerateWarningNode(
@"WARNING!!!
--------------
" + headerFooter + @" is missing a needed <oc:component /> tag!!!
The tag must be empty with no attributes.  This is where the content for each page is displayed"));

                    XmlElement componentNode = templateDocument.CreateElement(
                        "component", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);

                    bodyTag.AppendChild(componentNode);
                    templateParsingState.ReplaceNodes(componentNode, nodesToInsert);
                }
            }

            templateParsingState.SetCWD(templateDocument.ChildNodes, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);
        }

        /// <summary>
        /// Exceptions that originate from within the templating engine
        /// </summary>
        public class TemplateException : Exception
        {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="message"></param>
            public TemplateException(string message) : base(message) { }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="message"></param>
            /// <param name="inner"></param>
            public TemplateException(string message, Exception inner) : base(message, inner) { }
        }

        /// <summary>
        /// Generates composite Javascript from a list of scripts
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="scriptUrls"></param>
        /// <returns></returns>
        private string GenerateCompositeJavascript(IWebConnection webConnection, IEnumerable<string> scriptUrls)
        {
            StringBuilder scriptBuilder = new StringBuilder();

            foreach (string scriptUrl in scriptUrls)
            {
                try
                {
                    IWebResults webResult = webConnection.ShellTo(scriptUrl);

                    int statusCode = (int)webResult.Status;

                    if ((statusCode >= 200) && (statusCode < 300))
                    {
                        string script = webResult.ResultsAsString;
                        script = JavaScriptMinifier.Instance.Minify(script);

                        scriptBuilder.AppendFormat("\n// {0}\n", scriptUrl);
                        scriptBuilder.Append("try {");
                        scriptBuilder.Append(script);
                        scriptBuilder.Append("} catch (exception) { }");

                        // Note: exceptions are swallowed
                        // This form of compression shouldn't be used when a developer is trying to debug, instead, they should
                        // turn on Javascript debug mode, which disables this compression and allows them to use the browser's
                        // debugger
                    }
                }
                catch (Exception e)
                {
                    log.Error("Error loading script in GenerateCompositeJavascript for script " + scriptUrl, e);
                }
            }

            return scriptBuilder.ToString();
        }

        /// <summary>
        /// Returns a composite script that contains all of the scripts passed in for low-latency loading
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="scriptUrls"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults GetCompositeScript(IWebConnection webConnection, string[] scriptUrls)
        {
            return WebResults.From(
                Status._200_OK,
                GenerateCompositeJavascript(webConnection, scriptUrls));
        }
    }
}
