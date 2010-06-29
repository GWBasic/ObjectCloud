// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Template
{
    /// <summary>
    /// Handles JSON templates
    /// </summary>
    class JSONTemplateResolver : ITemplateProcessor
    {
        static ILog log = LogManager.GetLogger<ComponentAndConditionalsResolver>();

        void ITemplateProcessor.Handle(ITemplateParsingState templateParsingState)
        {
            templateParsingState.ProcessElementForDependanciesAndTemplates += ProcessElementForDependanciesAndTemplates;
        }

        void ProcessElementForDependanciesAndTemplates(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            if (element.NamespaceURI == TemplatingConstants.TemplateNamespace)
                if (element.LocalName == "jsontemplate")
                {
                    XmlAttribute srcAttribute = (XmlAttribute)element.Attributes.GetNamedItem("src", TemplatingConstants.TemplateNamespace);
                    XmlAttribute urlAttribute = (XmlAttribute)element.Attributes.GetNamedItem("url", TemplatingConstants.TemplateNamespace);

                    if ((null == srcAttribute) || (null == urlAttribute))
                    {
                        // Remove empty components and generate warning
                        templateParsingState.ReplaceNodes(
                            element,
                            templateParsingState.GenerateWarningNode("Both src and url must be specified: " + element.OuterXml));

                        return;
                    }

                    IFileContainer templateContainer;
                    try
                    {
                        string src = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                            templateParsingState.GetCWD(element), srcAttribute.Value);
                        templateContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(src);
                    }
                    catch (FileDoesNotExist fdne)
                    {
                        log.Error("Error resolving a template", fdne);
                        templateParsingState.ReplaceNodes(element, templateParsingState.GenerateWarningNode("src does not exist: " + element.OuterXml));

                        return;
                    }

                    // If the user doesn't have permission for the component and the component has something to use instead, use it
                    if (null == templateContainer.LoadPermission(templateParsingState.WebConnection.Session.User.Id) && element.HasChildNodes)
                    {
                        XmlNode replacement = element.OwnerDocument.CreateElement("div");

                        foreach (XmlNode errorNode in element.ChildNodes)
                            replacement.AppendChild(errorNode);

                        element.ParentNode.InsertAfter(replacement, element);
                        element.ParentNode.RemoveChild(element);
                    }

                    // Try to load the destination URL
                    string url = urlAttribute.Value;

                    foreach (XmlAttribute attribute in element.Attributes)
                        if ("" == attribute.NamespaceURI)
                            url = HTTPStringFunctions.AppendGetParameter(url, attribute.LocalName, attribute.Value);

                    JsonReader jsonReader;
                    try
                    {
                        if (url.StartsWith("https://"))
                        {
                            templateParsingState.ReplaceNodes(
                                element, 
                                templateParsingState.GenerateWarningNode("https component nodes aren't supported due to certificate complexities" + element.OuterXml));

                            return;
                        }
                        else if (url.StartsWith("http://"))
                        {
                            HttpResponseHandler httpResponse = templateParsingState.WebConnection.Session.HttpWebClient.Get(url);

                            jsonReader = httpResponse.AsJsonReader();
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
                                jsonReader = new JsonReader(shellResults.ResultsAsStream);
                            }
                            catch (WebResultsOverrideException wroe)
                            {
                                templateParsingState.ReplaceNodes(
                                    element, 
                                    templateParsingState.GenerateWarningNode(wroe.WebResults.ResultsAsString));

                                return;
                            }
                    }
                    catch (Exception e)
                    {
                        log.Error("An error occured when loading a component", e);
                            templateParsingState.ReplaceNodes(
                                element, 
                                templateParsingState.GenerateWarningNode("An unhandled error occured.  See the system logs for more information"));

                            return;
                    }

                    object templateInput = jsonReader.Deserialize();
                    DoTemplate(templateParsingState, element, templateContainer, templateInput);
                }
        }

        private void DoTemplate(ITemplateParsingState templateParsingState, XmlNode element, IFileContainer templateContainer, object templateInput)
        {
            if (templateInput is object[])
            {
                foreach (object o in (object[])templateInput)
                {
                    XmlNode clonedElement = element.CloneNode(true);
                    element.ParentNode.InsertBefore(clonedElement, element);

                    DoTemplate(templateParsingState, clonedElement, templateContainer, o);
                }

                element.ParentNode.RemoveChild(element);
            }
            else if (templateInput is Dictionary<string, object>)
            {
                Dictionary<string, string> getParameters = new Dictionary<string, string>();
                Flatten(getParameters, "", templateInput);

                templateParsingState.LoadXmlDocumentAndReplaceGetParameters(getParameters, templateContainer);
            }

            else
            {
                Dictionary<string, string> getParameters = new Dictionary<string, string>();
                getParameters["Value"] = JsonWriter.Serialize(templateInput);

                templateParsingState.LoadXmlDocumentAndReplaceGetParameters(getParameters, templateContainer);
            }
        }

        /// <summary>
        /// Flattens a JSON object for use with a template
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="prefix"></param>
        /// <param name="templateInput"></param>
        private void Flatten(Dictionary<string, string> getParameters, string prefix, object templateInput)
        {
            if (templateInput is object[])
            {
                // Keep a "naked" one just in case, but only do naked if we're not too deep in a tree
                if (prefix.Length < 15)
                    getParameters[prefix] = JsonWriter.Serialize(templateInput);

                object[] objects = (object[])templateInput;

                for (int ctr = 0; ctr < objects.Length; ctr++)
                    Flatten(getParameters, string.Format("{0}.{1}", prefix, ctr.ToString()), objects[ctr]);
            }
            else if (templateInput is Dictionary<string, object>)
            {
                // Keep a "naked" one just in case, but only do naked if we're not too deep in a tree
                if (prefix.Length < 15)
                    getParameters[prefix] = JsonWriter.Serialize(templateInput);

                foreach (KeyValuePair<string, object> kvp in (Dictionary<string, object>)templateInput)
                    Flatten(getParameters, string.Format("{0}.{1}", prefix, kvp.Key), templateInput);
            }

            else if (templateInput is string)
                getParameters[prefix] = templateInput.ToString();

            else if (templateInput is double)
                getParameters[prefix] = ((double)templateInput).ToString("R");

            else if (templateInput is DateTime)
            {
                DateTime dateTime = (DateTime)templateInput;

                getParameters[prefix + ".Ugly"] = string.Format("{0}, {1}", dateTime.ToShortDateString(), dateTime.ToShortTimeString());
                getParameters[prefix + ".ForJS"] = JsonWriter.Serialize(dateTime);
                getParameters[prefix] = (DateTime.UtcNow - dateTime).TotalDays.ToString() + " days ago";
                /*getParameters[prefix + ".Time"] = dateTime.ToShortTimeString();
                getParameters[prefix + ".Date"] = dateTime.ToShortDateString();
                getParameters[prefix + ".Ticks"] = dateTime.Ticks.ToString();*/

                // todo:  add a span tag with special class and write a jquery script to format them nicely
            }

            else
                getParameters[prefix] = JsonWriter.Serialize(templateInput);
        }
    }
}
/*




                    templateParsingState.ReplaceNodes(element, resultNode);













                        templateString


                        // handle GET parameters
                        // First, handle oc:getpassthrough
                        IDictionary<string, string> myGetParameters;
                        XmlAttribute getpassthroughAttribute = (XmlAttribute)element.Attributes.GetNamedItem("getpassthough", TemplatingConstants.TemplateNamespace);
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
                        foreach (XmlAttribute attribute in element.Attributes)
                            if ("" == attribute.NamespaceURI)
                                myGetParameters[attribute.LocalName] = attribute.Value;

                        string fileName = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                            templateParsingState.GetCWD(element),
                            srcAttribute.Value);

                        IFileContainer fileContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(fileName);
                        templateParsingState.WebConnection.TouchedFiles.Add(fileContainer);

                        // If the user doesn't have permission for the component and the component has something to use instead, use it
                        if (null == fileContainer.LoadPermission(templateParsingState.WebConnection.Session.User.Id) && element.HasChildNodes)
                        {
                            XmlNode replacement = element.OwnerDocument.CreateElement("div");

                            foreach (XmlNode errorNode in element.ChildNodes)
                                replacement.AppendChild(errorNode);

                            element.ParentNode.InsertAfter(replacement, element);
                            element.ParentNode.RemoveChild(element);
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
                                templateParsingState.ReplaceNodes(element, replacementNodes);
                            }
                            catch (WebResultsOverrideException wroe)
                            {
                                templateParsingState.ReplaceNodes(
                                    element,
                                    templateParsingState.GenerateWarningNode(wroe.WebResults.ResultsAsString));
                            }
                            catch (Exception e)
                            {
                                log.Error("An error occured when loading a component", e);

                                templateParsingState.ReplaceNodes(
                                    element,
                                    templateParsingState.GenerateWarningNode("An unhandled error occured.  See the system logs for more information"));
                            }
                        }
                    }

                    else if (null != urlAttribute)
                    {
                        XmlNode resultNode;
                        string url = urlAttribute.Value;

                        foreach (XmlAttribute attribute in element.Attributes)
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
                                    resultNode = element.OwnerDocument.CreateTextNode(httpResponse.AsString());
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
                                        resultNode = element.OwnerDocument.CreateTextNode(shellResults.ResultsAsString);
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

                        templateParsingState.ReplaceNodes(element, resultNode);
                    }
                }
        }
    }
}
                    */