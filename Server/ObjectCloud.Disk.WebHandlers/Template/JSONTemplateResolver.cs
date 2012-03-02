// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
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
    class JSONTemplateResolver : HasFileHandlerFactoryLocator, ITemplateProcessor
    {
        static ILog log = LogManager.GetLogger<ComponentAndConditionalsResolver>();

        void ITemplateProcessor.Register(ITemplateParsingState templateParsingState)
        {
            templateParsingState.ProcessElementForDependanciesAndTemplates += ProcessElementForDependanciesAndTemplates;
            templateParsingState.RegisterDeferedNode("jsontemplate", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);
        }

        void ProcessElementForDependanciesAndTemplates(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlElement element)
        {
            if (element.NamespaceURI == templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace)
                if (element.LocalName == "jsontemplate")
                {
                    string src = element.GetAttribute("src", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);
                    string url = element.GetAttribute("url", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);
                    string data = element.GetAttribute("data", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);
                    string datafile = element.GetAttribute("datafile", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);

                    if (url.Length == 0 && data.Length == 0 && datafile.Length == 0)
                    {
                        // Remove empty components and generate warning
                        templateParsingState.ReplaceNodes(
                            element,
                            templateParsingState.GenerateWarningNode("Either oc:url, oc:data, or oc:src must be specified: " + element.OuterXml));

                        return;
                    }

                    // Quote the XML in debug mode so the designer can see the template
                    if (templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(templateParsingState.TemplateHandlerLocator.TemplatingConstants.XMLDebugModeCookie))
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
                    else if (datafile.Length > 0)
					{
						try
						{
							string filename = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
								templateParsingState.GetCWD(element),
                                datafile);
						
							IFileContainer fileContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
						
							jsonReader = new JsonReader(fileContainer.CastFileHandler<ITextHandler>().ReadAll());
                        }
                        catch (Exception e)
                        {
                            log.Error("An error occured when loading a component", e);
                            templateParsingState.ReplaceNodes(
                                element,
                                templateParsingState.GenerateWarningNode("An unhandled error occured.  See the system logs for more information"));

                            return;
                        }
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

                        // Sort, if sorting is enabled
                        if (templateInput is object[])
                        {
                            string sort = element.GetAttribute("sort", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);

                            if (null != sort)
                                if (sort.Length > 0)
                                {
                                    try
                                    {
                                        List<object> toSort = new List<object>((IEnumerable<object>)templateInput);
                                        toSort.Sort(delegate(object inA, object inB)
                                        {
                                            IDictionary<string, object> a = (IDictionary<string, object>)inA;
                                            IDictionary<string, object> b = (IDictionary<string, object>)inB;

                                            object aVal = null;
                                            a.TryGetValue(sort, out aVal);

                                            object bVal = null;
                                            b.TryGetValue(sort, out bVal);

                                            return Comparer.DefaultInvariant.Compare(aVal, bVal);
                                        });

                                        templateInput = toSort.ToArray();
                                    }
                                    catch (Exception e)
                                    {
                                        log.Warn("Can not sort by \"" + sort + "\": " + JsonWriter.Serialize(templateInput), e);
                                    }
                                }
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
                    }
                }
        }
    }
}