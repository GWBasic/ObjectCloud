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

namespace ObjectCloud.Disk.WebHandlers.Template
{
    /// <summary>
    /// Removes debug information from documents
    /// </summary>
    class ComponentAndConditionalsResolver : ITemplateProcessor
    {
        static ILog log = LogManager.GetLogger<ComponentAndConditionalsResolver>();

        void ITemplateProcessor.Handle(ITemplateParsingState templateParsingState)
        {
            templateParsingState.ProcessElementForConditionalsAndComponents += ProcessElementForConditionalsAndComponents;
        }

        void ProcessElementForConditionalsAndComponents(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlNode element)
        {
            if (element.NamespaceURI == TemplatingConstants.TemplateNamespace)
                if (element.LocalName == "if")
                    HandleConditional(templateParsingState, getParameters, element);
                else if (element.LocalName == "component")
                    LoadComponent(templateParsingState, getParameters, element);
        }

        private void HandleConditional(
            ITemplateParsingState templateParsingState,
            IDictionary<string, string> getParameters,
            XmlNode conditionalNode)
        {
            Dictionary<string, ITemplateConditionHandler> templateConditionHandlers = templateParsingState.TemplateHandlerLocator.TemplateConditionHandlers;

            XmlNode current = conditionalNode.FirstChild;
            bool conditionMet = false;

            // Note:  Looping continues after the condition is met to ensure that all nodes are in the correct namespace
            while (null != current)
            {
                // Make sure the namespace is proper
                if (current.NamespaceURI != TemplatingConstants.TemplateNamespace)
                    conditionalNode.ParentNode.InsertBefore(
                        templateParsingState.GenerateWarningNode("All nodes within an <if> must be of " + TemplatingConstants.TemplateNamespace + " namespace: " + current.OuterXml),
                        conditionalNode);

                else
                {
                    // Make sure the node is supported
                    ITemplateConditionHandler templateConditionHandler;
                    if (!templateConditionHandlers.TryGetValue(current.LocalName, out templateConditionHandler))
                        conditionalNode.ParentNode.InsertBefore(
                            templateParsingState.GenerateWarningNode("There is no condition handler for " + current.LocalName + ": " + current.OuterXml),
                            conditionalNode);

                    if (!conditionMet)
                        try
                        {
                            if (templateConditionHandler.IsConditionMet(templateParsingState, current))
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
                                templateParsingState.GenerateWarningNode("An unhandled error occured processing: " + current.OuterXml),
                                conditionalNode);
                        }
                }

                current = current.NextSibling;
            }

            conditionalNode.ParentNode.RemoveChild(conditionalNode);
        }


        private void LoadComponent(
            ITemplateParsingState templateParsingState,
            IDictionary<string, string> getParameters,
            XmlNode componentNode)
        {
            XmlAttribute srcAttribute = (XmlAttribute)componentNode.Attributes.GetNamedItem("src", TemplatingConstants.TemplateNamespace);
            XmlAttribute urlAttribute = (XmlAttribute)componentNode.Attributes.GetNamedItem("url", TemplatingConstants.TemplateNamespace);

            if ((null == srcAttribute) && (null == urlAttribute))
                // Remove empty components
                componentNode.ParentNode.RemoveChild(componentNode);

            else if ((null != srcAttribute) && (null != urlAttribute))
                templateParsingState.ReplaceNodes(
                    componentNode,
                    templateParsingState.GenerateWarningNode("Either src or div can be specified; you can not choose both: " + componentNode.OuterXml));

            else if (null != srcAttribute)
            {
                // handle GET parameters
                // First, handle oc:getpassthrough
                IDictionary<string, string> myGetParameters;
                XmlAttribute getpassthroughAttribute = (XmlAttribute)componentNode.Attributes.GetNamedItem("getpassthough", TemplatingConstants.TemplateNamespace);
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

                string fileName = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                    templateParsingState.GetCWD(componentNode),
                    srcAttribute.Value);

                IFileContainer fileContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(fileName);
                templateParsingState.WebConnection.TouchedFiles.Add(fileContainer);

                // If the user doesn't have permission for the component and the component has something to use instead, use it
                if (null == fileContainer.LoadPermission(templateParsingState.WebConnection.Session.User.Id) && componentNode.HasChildNodes)
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
                        componentDocument = templateParsingState.LoadXmlDocumentAndReplaceGetParameters(
                            myGetParameters,
                            fileContainer);

                        XmlNode firstChild = componentDocument.FirstChild;
                        XmlNodeList replacementNodes;
                        if ((firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplatingConstants.TemplateNamespace))
                            replacementNodes = firstChild.ChildNodes;
                        else
                            replacementNodes = componentDocument.ChildNodes;

                        templateParsingState.SetCWD(replacementNodes, fileContainer.ParentDirectoryHandler.FileContainer.FullPath);
                        templateParsingState.ReplaceNodes(componentNode, replacementNodes);
                    }
                    catch (WebResultsOverrideException wroe)
                    {
                        templateParsingState.ReplaceNodes(
                            componentNode,
                            templateParsingState.GenerateWarningNode(wroe.WebResults.ResultsAsString));
                    }
                    catch (Exception e)
                    {
                        log.Error("An error occured when loading a component", e);

                        templateParsingState.ReplaceNodes(
                            componentNode,
                            templateParsingState.GenerateWarningNode("An unhandled error occured.  See the system logs for more information"));
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
                        resultNode = templateParsingState.GenerateWarningNode("https component nodes aren't supported due to certificate complexities");
                    else if (url.StartsWith("http://"))
                    {
                        HttpResponseHandler httpResponse = templateParsingState.WebConnection.Session.HttpWebClient.Get(url);

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
                                templateParsingState.WebConnection,
                                null,
                                templateParsingState.WebConnection.CookiesFromBrowser);

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
                            resultNode = templateParsingState.GenerateWarningNode(wroe.WebResults.ResultsAsString);
                        }
                }
                catch (Exception e)
                {
                    log.Error("An error occured when loading a component", e);
                    resultNode = templateParsingState.GenerateWarningNode("An unhandled error occured.  See the system logs for more information");
                }

                templateParsingState.ReplaceNodes(componentNode, resultNode);
            }
        }
    }
}
