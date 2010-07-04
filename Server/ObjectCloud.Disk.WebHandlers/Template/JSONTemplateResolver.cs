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
                    string src = element.GetAttribute("src", TemplatingConstants.TemplateNamespace);
                    string url = element.GetAttribute("url", TemplatingConstants.TemplateNamespace);
                    string data = element.GetAttribute("data", TemplatingConstants.TemplateNamespace);

                    if (url.Length == 0 && data.Length == 0)
                    {
                        // Remove empty components and generate warning
                        templateParsingState.ReplaceNodes(
                            element,
                            templateParsingState.GenerateWarningNode("Either oc:url or oc:data must be specified: " + element.OuterXml));

                        return;
                    }

                    // Quote the XML in debug mode so the designer can see the template
                    if (templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(TemplatingConstants.XMLDebugModeCookie))
                    {
                        XmlComment debugComment = templateParsingState.TemplateDocument.CreateComment("Template: " + element.OuterXml);
                        element.ParentNode.InsertBefore(debugComment, element);
                    }

                    JsonReader jsonReader;

                    // If oc:url was specified, simulate a request, else, just parse data
                    if (url.Length > 0)
                        try
                        {
                            // Try to load the destination URL
                            foreach (XmlAttribute attribute in element.Attributes)
                                if ("" == attribute.NamespaceURI)
                                    url = HTTPStringFunctions.AppendGetParameter(url, attribute.LocalName, attribute.Value);

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
                    else
                        jsonReader = new JsonReader(data);

                    try
                    {
                        object templateInput = jsonReader.Deserialize();

                        // Either load the nodes or 
                        if (src.Length > 0)
                        {
                            IFileContainer templateContainer;
                            try
                            {
                                src = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                                    templateParsingState.GetCWD(element), src);
                                templateContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(src);

                                XmlDocument newDocument = templateParsingState.LoadXmlDocument(
                                    templateContainer,
                                    templateParsingState.GetXmlParseMode(element));

                                // Import the template nodes
                                XmlNode firstChild = templateParsingState.TemplateDocument.ImportNode(newDocument.FirstChild, true);

                                if (!(firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplatingConstants.TemplateNamespace))
                                {
                                    templateParsingState.ReplaceNodes(
                                        element,
                                        templateParsingState.GenerateWarningNode("src document must have an <oc:componentdef> tag"));

                                    return;
                                }

                                IEnumerable<XmlNode> replacementNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(firstChild.ChildNodes));


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
                                templateParsingState.ReplaceNodes(element, templateParsingState.GenerateWarningNode("oc:src does not exist: " + element.OuterXml));

                                return;
                            }
                        }

                        DoTemplate(
                            templateParsingState,
                            element,
                            templateInput);
                    }
                    catch (Exception e)
                    {
                        log.Error("Error processing JSON template " + element.OuterXml, e);

                        templateParsingState.ReplaceNodes(
                            element,
                            templateParsingState.GenerateWarningNode("Error processing node.  Make sure results are proper JSON: " + element.OuterXml));

                        element.ParentNode.RemoveChild(element);
                    }
                }
        }

        private void DoTemplate(ITemplateParsingState templateParsingState, XmlNode element, object templateInput)
        {
            if (templateInput is object[])
            {
                object[] objects = (object[])templateInput;

                for (int ctr = 0; ctr < objects.Length; ctr++)
                {
                    object o = objects[ctr];

                    XmlNode clonedElement = element.CloneNode(true);
                    element.ParentNode.InsertBefore(clonedElement, element);

                    if (o is Dictionary<string, object>)
                        ((Dictionary<string, object>)o)["i"] = ctr;
                    else
                    {
                        Dictionary<string, object> newObject = new Dictionary<string, object>();
                        newObject["i"] = ctr;
                        newObject[""] = o;
                        o = newObject;
                    }

                    DoTemplate(
                        templateParsingState, 
                        clonedElement,
                        o);
                }

                element.ParentNode.RemoveChild(element);
            }
            else
            {
                Dictionary<string, string> getParameters = new Dictionary<string, string>();

                foreach (XmlAttribute attribute in element.Attributes)
                    if ("" == attribute.NamespaceURI)
                        getParameters["_UP." + attribute.LocalName] = attribute.Value;

                Flatten(getParameters, "", templateInput);

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