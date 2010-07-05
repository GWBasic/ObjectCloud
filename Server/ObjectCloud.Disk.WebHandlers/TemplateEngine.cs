﻿// Copyright 2009, 2010 Andrew Rondeau
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
    public class TemplateEngine : WebHandler
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
            Stream results = EvaluateToStream(
                webConnection,
                webConnection.GetParameters, 
                filename);

            IWebResults toReturn;

            // Hack to work around a bug in Mozilla handling xhtml
            // What's going on is that I'm using a horrible hack to remove all namespaces from the <html> tag and turn this into an SGML-HTML document instead of xml-html
            if (webConnection.Headers["USER-AGENT"].Contains(" Firefox/") || webConnection.Headers["USER-AGENT"].Contains(" MSIE "))
            {
                // <?xml version="1.0" encoding="utf-8"?><html xmlns="http://www.w3.org/1999/xhtml" 

                StreamReader sr = new StreamReader(results);
                string result = sr.ReadToEnd();
                //result = result.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:oc=\"objectcloud_templating\">", "<!DOCTYPE html>\n<html>");
                result = result.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?><html", "");
                result = result.Split(new char[] { '>' }, 2)[1];
                result = "<!DOCTYPE html>\n<html>" + result;

                toReturn = WebResults.From(Status._200_OK, result);
                toReturn.ContentType = "text/html";
            }
            else
            {
                // Everyone else gets real XML
                toReturn = WebResults.From(Status._200_OK, results);
                toReturn.ContentType = "text/xml";
            }

            return toReturn;
        }

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="getParameters"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public Stream EvaluateToStream(
            IWebConnection webConnection,
            IDictionary<string, string> getParameters,
            string filename)
        {
            XmlDocument templateDocument = null;
            TemplateParsingState templateParsingState = new TemplateParsingState(webConnection);

            foreach (ITemplateProcessor templateProcessor in FileHandlerFactoryLocator.TemplateHandlerLocator.TemplateProcessors)
                templateProcessor.Register(templateParsingState);

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

                    templateDocument = ResolveHeaderFooter(webConnection, getParameters, templateFileContainer, templateParsingState);
                    templateParsingState.TemplateDocument = templateDocument;

                    templateParsingState.OnDocumentLoaded(getParameters, templateDocument.FirstChild as XmlElement);

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

                            foreach (XmlElement element in Enumerable<XmlElement>.FastCopy(XmlHelper.IterateAllElements(templateDocument)))
                                templateParsingState.OnProcessElementForConditionalsAndComponents(getParameters, element);

                            innerLoopsLeft--;

                        } while (continueResolving && (innerLoopsLeft > 0));

                        foreach (XmlElement element in Enumerable<XmlElement>.FastCopy(XmlHelper.IterateAllElements(templateDocument)))
                            templateParsingState.OnProcessElementForDependanciesAndTemplates(getParameters, element);

                        loopsLeft--;

                    } while (continueResolving && (loopsLeft > 0));

                    templateDocument.NodeChanged -= documentChanged;
                    templateDocument.NodeInserted -= documentChanged;
                    templateDocument.NodeRemoved -= documentChanged;

                    XmlNode headNode = GetHeadNode(templateDocument);

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
            bool removeComments = !webConnection.CookiesFromBrowser.ContainsKey(TemplatingConstants.XMLDebugModeCookie);

            foreach (XmlNode xmlNode in Enumerable<XmlNode>.FastCopy(XmlHelper.IterateAllElementsAndComments(templateDocument)))
            {
                if (xmlNode is XmlElement)
                    templateParsingState.OnPostProcessElement(getParameters, (XmlElement)xmlNode);
                else if (removeComments)
                    xmlNode.ParentNode.RemoveChild(xmlNode);
            }

            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.CloseOutput = true;
            xmlWriterSettings.ConformanceLevel = ConformanceLevel.Document;
            xmlWriterSettings.Encoding = Encoding.UTF8;

            if (webConnection.CookiesFromBrowser.ContainsKey(TemplatingConstants.XMLDebugModeCookie))
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

            return stream;
        }

        private static XmlNode GetHeadNode(XmlDocument templateDocument)
        {
            XmlNodeList headNodeList = templateDocument.GetElementsByTagName("head");
            if (headNodeList.Count != 1)
                throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, "Generated document does not have a <head>:\n" + templateDocument.OuterXml));

            XmlNode headNode = headNodeList[0];
            return headNode;
        }

        private static void GenerateScriptAndCssTags(ITemplateParsingState templateParsingState, XmlNode headNode)
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
            XmlNodeList ocTitleNodes = headNode.OwnerDocument.GetElementsByTagName("title", TemplatingConstants.TemplateNamespace);

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
        /// <param name="getParameters"></param>
        /// <param name="templateFileContainer"></param>
        /// <param name="webConnection"></param>
        /// <param name="templateParsingState"></param>
        /// <returns></returns>
        private XmlDocument ResolveHeaderFooter(
            IWebConnection webConnection,
            IDictionary<string, string> getParameters,
            IFileContainer templateFileContainer,
            TemplateParsingState templateParsingState)
        {
            XmlDocument templateDocument = templateParsingState.LoadXmlDocumentAndReplaceGetParameters(getParameters, templateFileContainer, XmlParseMode.Xml);

            // While the first node isn't HTML, keep loading header/footers
            while ("html" != templateDocument.FirstChild.LocalName)
            {
                XmlNode firstChild = templateDocument.FirstChild;
                string headerFooter = "/DefaultTemplate/headerfooter.ochf";

                XmlNodeList nodesToInsert;
                if (("componentdef" == firstChild.LocalName) && (TemplatingConstants.TemplateNamespace == firstChild.NamespaceURI))
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

                templateParsingState.SetCWD(nodesToInsert, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);

                templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(headerFooter);

                templateDocument = templateParsingState.LoadXmlDocumentAndReplaceGetParameters(
                    getParameters,
                    templateFileContainer,
                    XmlParseMode.Xml);

                // find oc:component tag
                XmlNodeList componentTags = templateDocument.GetElementsByTagName("component", TemplatingConstants.TemplateNamespace);
                for (int ctr = 0; ctr < componentTags.Count; ctr++)
                {
                    XmlNode componentNode = componentTags[ctr];

                    if ((null == componentNode.Attributes.GetNamedItem("url", TemplatingConstants.TemplateNamespace)) && (null == componentNode.Attributes.GetNamedItem("src", TemplatingConstants.TemplateNamespace)))
                        templateParsingState.ReplaceNodes(componentNode, nodesToInsert);
                }
            }

            templateParsingState.SetCWD(templateDocument.ChildNodes, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);

            return templateDocument;
        }
    }
}