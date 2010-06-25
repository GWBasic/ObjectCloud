// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Generates complete files from templates
    /// </summary>
    public class TemplateEngine : WebHandler
    {
        private static ILog log = LogManager.GetLogger<TemplateEngine>();

        private const string TemplateNamespace = "objectcloud_templating";

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
                delegate(string results)
                {
                    IWebResults toReturn = WebResults.FromString(Status._200_OK, results);
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
            Dictionary<XmlNode, string> CWDs = new Dictionary<XmlNode, string>();

            public string GetCWD(XmlNode xmlNode)
            {
                string toReturn;

                while (!CWDs.TryGetValue(xmlNode, out toReturn))
                {
                    try
                    {
                        xmlNode = xmlNode.ParentNode;
                    }
                    catch (Exception e)
                    {
                        throw new KeyNotFoundException("Can not find CWD: " + xmlNode.OuterXml, e);
                    }

                    if (null == xmlNode)
                        throw new KeyNotFoundException("Can not find CWD: " + xmlNode.OuterXml);
                }

                return toReturn;
            }

            public void SetCWD(XmlNodeList xmlNodeList, string cwd)
            {
                foreach (XmlNode xmlNode in xmlNodeList)
                    SetCWD(xmlNode, cwd);
            }

            public void SetCWD(XmlNode xmlNode, string cwd)
            {
                CWDs[xmlNode] = cwd;
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
            GenericArgument<string> resultsCallback)
        {
            IFileContainer templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);

            webConnection.TouchedFiles.Add(templateFileContainer);

            XmlDocument templateDocument = ResolveHeaderFooter(webConnection, getParameters, templateFileContainer);

            CWDTracker cwdTracker = new CWDTracker();
            cwdTracker.SetCWD(templateDocument, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);
            cwdTracker.SetCWD(templateDocument.ChildNodes, templateFileContainer.ParentDirectoryHandler.FileContainer.FullPath);

            bool continueResolvingComponentsAndConditionals;

            do
            {
                continueResolvingComponentsAndConditionals = false;

                XmlNodeList conditionalNodes = templateDocument.GetElementsByTagName("if", TemplateNamespace);
                if (conditionalNodes.Count > 0)
                {
                    continueResolvingComponentsAndConditionals = true;

                    // TODO:  These should be evaluated
                    for (int ctr = 0; ctr < conditionalNodes.Count; ctr++)
                    {
                        XmlNode conditionalNode = conditionalNodes[ctr];
                        conditionalNode.ParentNode.RemoveChild(conditionalNode);
                    }
                }

                XmlNodeList componentNodes = templateDocument.GetElementsByTagName("component", TemplateNamespace);
                if (componentNodes.Count > 0)
                {
                    continueResolvingComponentsAndConditionals = true;

                    // TODO:  These should be evaluated
                    for (int ctr = 0; ctr < componentNodes.Count; ctr++)
                    {
                        XmlNode componentNode = componentNodes[ctr];

                        // TODO:  Handle permissions issues

                        LoadComponent(webConnection, getParameters, cwdTracker, componentNode);
                    }
                }
            } while (continueResolvingComponentsAndConditionals);

            resultsCallback(templateDocument.OuterXml);
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
                        //resultNode = GenerateWarningNode(componentNode.OwnerDocument, "https component nodes aren't supported due to certificate complexities");

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

        private static XmlNode GenerateWarningNode(XmlDocument parentDocument, string message)
        {
            XmlNode toReturn = parentDocument.CreateNode(XmlNodeType.Element, "div", parentDocument.DocumentElement.NamespaceURI);
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
        /// <returns></returns>
        private XmlDocument ResolveHeaderFooter(IWebConnection webConnection, IDictionary<string, string> getParameters, IFileContainer templateFileContainer)
        {
            IFileHandler templateFileHandler = templateFileContainer.FileHandler;

            if (!(templateFileHandler is ITextHandler))
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, templateFileContainer.FullPath + " must be a text file"));

            string templateContents = ((ITextHandler)templateFileHandler).ReadAll();
            templateContents = ReplaceGetParameters(getParameters, templateContents);

            XmlDocument templateDocument = new XmlDocument();
            templateDocument.LoadXml(templateContents);

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

                templateDocument = LoadXmlDocumentAndReplaceGetParameters(
                    webConnection,
                    getParameters,
                    headerFooter);

                // find oc:component tag
                XmlNodeList componentTags = templateDocument.GetElementsByTagName("component", TemplateNamespace);
                for (int ctr = 0; ctr < componentTags.Count; ctr++)
                {
                    XmlNode componentNode = componentTags[ctr];

                    if ((null == componentNode.Attributes.GetNamedItem("url", TemplateNamespace)) && (null == componentNode.Attributes.GetNamedItem("src", TemplateNamespace)))
                        ReplaceNode(componentNode, nodesToInsert);
                }
            }
            return templateDocument;
        }

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="getParameters"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private XmlDocument LoadXmlDocumentAndReplaceGetParameters(
            IWebConnection webConnection,
            IDictionary<string, string> getParameters,
            string fileName)
        {
            IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(fileName);
            return LoadXmlDocumentAndReplaceGetParameters(webConnection, getParameters, fileContainer);
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
            xmlDocument.LoadXml(ReplaceGetParameters(getParameters, ((ITextHandler)fileContainer.FileHandler).ReadAll()));

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
