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
                            templateParsingState.GenerateWarningNode("Both oc:src and oc:url must be specified: " + element.OuterXml));

                        return;
                    }

                    IFileContainer templateContainer;
                    try
                    {
                        string src = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                            templateParsingState.GetCWD(element), srcAttribute.Value);
                        templateContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(src);

                        templateParsingState.WebConnection.TouchedFiles.Add(templateContainer);

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

                            int statusCode = (int)httpResponse.StatusCode;
                            if ((statusCode >= 200) && (statusCode < 300))
                                jsonReader = httpResponse.AsJsonReader();
                            else
                            {
                                templateParsingState.ReplaceNodes(
                                    element,
                                    templateParsingState.GenerateWarningNode(httpResponse.AsString()));

                                return;
                            }
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

                                int statusCode = (int)shellResults.Status;
                                if ((statusCode >= 200) && (statusCode < 300))
                                    jsonReader = new JsonReader(shellResults.ResultsAsStream);
                                else
                                {
                                    templateParsingState.ReplaceNodes(
                                        element,
                                        templateParsingState.GenerateWarningNode(shellResults.ResultsAsString));

                                    return;
                                }
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
            else
            {
                Dictionary<string, string> getParameters = new Dictionary<string, string>();

                if (templateInput is Dictionary<string, object>)
                    Flatten(getParameters, "", templateInput);
                else
                    getParameters["Value"] = JsonWriter.Serialize(templateInput);

                XmlDocument newDocument = templateParsingState.LoadXmlDocumentAndReplaceGetParameters(getParameters, templateContainer);

                XmlNode firstChild = newDocument.FirstChild;
                XmlNodeList replacementNodes;
                if ((firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplatingConstants.TemplateNamespace))
                    replacementNodes = firstChild.ChildNodes;
                else
                    replacementNodes = newDocument.ChildNodes;

                templateParsingState.SetCWD(replacementNodes, templateContainer.ParentDirectoryHandler.FileContainer.FullPath);
                templateParsingState.ReplaceNodes(element, replacementNodes);
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
                    Flatten(getParameters, string.Format("{0}[{1}]", prefix, ctr.ToString()), objects[ctr]);
            }
            else if (templateInput is Dictionary<string, object>)
            {
                // Keep a "naked" one just in case, but only do naked if we're not too deep in a tree
                if (prefix.Length < 15)
                    getParameters[prefix] = JsonWriter.Serialize(templateInput);

                foreach (KeyValuePair<string, object> kvp in (Dictionary<string, object>)templateInput)
                    if (kvp.Value is Dictionary<string, object>)
                        Flatten(getParameters, string.Format("{0}{1}.", prefix, kvp.Key), kvp.Value);
                    else
                        Flatten(getParameters, string.Format("{0}{1}", prefix, kvp.Key), kvp.Value);
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