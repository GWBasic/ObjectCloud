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

        void ITemplateProcessor.Register(ITemplateParsingState templateParsingState)
        {
            templateParsingState.ProcessElementForDependanciesAndTemplates += ProcessElementForDependanciesAndTemplates;
            templateParsingState.RegisterDeferedNode("jsontemplate", TemplatingConstants.TemplateNamespace);
        }

        void ProcessElementForDependanciesAndTemplates(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            if (element.NamespaceURI == TemplatingConstants.TemplateNamespace)
                if (element.LocalName == "jsontemplate")
                {
                    XmlAttribute srcAttribute = (XmlAttribute)element.Attributes.GetNamedItem("src", TemplatingConstants.TemplateNamespace);
                    XmlAttribute urlAttribute = (XmlAttribute)element.Attributes.GetNamedItem("url", TemplatingConstants.TemplateNamespace);

                    if (null == urlAttribute)
                    {
                        // Remove empty components and generate warning
                        templateParsingState.ReplaceNodes(
                            element,
                            templateParsingState.GenerateWarningNode("oc:url must be specified: " + element.OuterXml));

                        return;
                    }

                    // Quote the XML in debug mode so the designer can see the template
                    if (templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(TemplatingConstants.XMLDebugModeCookie))
                    {
                        XmlComment debugComment = templateParsingState.TemplateDocument.CreateComment("Template: " + element.OuterXml);
                        element.ParentNode.InsertBefore(debugComment, element);
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

                    IEnumerable<XmlNode> replacementNodes;

                    // Either load the nodes or 
                    if (null != srcAttribute)
                    {
                        IFileContainer templateContainer;
                        try
                        {
                            string src = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                                templateParsingState.GetCWD(element), srcAttribute.Value);
                            templateContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(src);

                            XmlDocument newDocument = templateParsingState.LoadXmlDocument(templateContainer);

                            // Import the template nodes
                            XmlNode firstChild = templateParsingState.TemplateDocument.ImportNode(newDocument.FirstChild, true);

                            if ((firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplatingConstants.TemplateNamespace))
                                replacementNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(firstChild.ChildNodes));
                            else
                            {
                                templateParsingState.ReplaceNodes(
                                    element,
                                    templateParsingState.GenerateWarningNode("src document must have an <oc:componentdef> tag"));

                                return;
                            }

                            // Replace child nodes with contents from file
                            foreach (XmlNode xmlNode in element.ChildNodes)
                                element.RemoveChild(xmlNode);
                            foreach (XmlNode xmlNode in replacementNodes)
                                element.AppendChild(xmlNode);

                            templateParsingState.SetCWD(replacementNodes, templateContainer.ParentDirectoryHandler.FileContainer.FullPath);

                        }
                        catch (FileDoesNotExist fdne)
                        {
                            log.Error("Error resolving a template", fdne);
                            templateParsingState.ReplaceNodes(element, templateParsingState.GenerateWarningNode("src does not exist: " + element.OuterXml));

                            return;
                        }
                    }
                    else
                        replacementNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(element.ChildNodes));


                    DoTemplate(
                        templateParsingState, 
                        element, 
                        //replacementNodes, 
                        templateInput);
                }
        }

        private void DoTemplate(ITemplateParsingState templateParsingState, XmlNode element, /*IEnumerable<XmlNode> replacementNodes,*/ object templateInput)
        {
            if (templateInput is object[])
            {
                foreach (object o in (object[])templateInput)
                {
                    XmlNode clonedElement = element.CloneNode(true);
                    element.ParentNode.InsertBefore(clonedElement, element);

                    DoTemplate(
                        templateParsingState, 
                        clonedElement,
                        //Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(clonedElement.ChildNodes)), 
                        o);
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

                foreach (XmlNode xmlNode in element.ChildNodes)
                    templateParsingState.ReplaceGetParameters(getParameters, xmlNode);

                templateParsingState.ReplaceNodes(element, element.ChildNodes);
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