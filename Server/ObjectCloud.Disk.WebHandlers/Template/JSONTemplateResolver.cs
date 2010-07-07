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
                            src = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                                templateParsingState.GetCWD(element), src);

                            if (!templateParsingState.LoadComponentForJSON(element, src))
                                return;
                        }

                        templateParsingState.DoTemplate(
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
    }
}