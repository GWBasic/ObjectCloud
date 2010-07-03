// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Template
{
    internal class TemplateParsingState : ITemplateParsingState
    {
        private ILog log = LogManager.GetLogger<TemplateParsingState>();

        internal TemplateParsingState(IWebConnection webConnection)
        {
            _WebConnection = webConnection;
            _FileHandlerFactoryLocator = webConnection.WebServer.FileHandlerFactoryLocator;
            _TemplateHandlerLocator = _FileHandlerFactoryLocator.TemplateHandlerLocator;
        }

        public XmlDocument TemplateDocument
        {
            get { return _TemplateDocument; }
            set { _TemplateDocument = value; }
        }
        private XmlDocument _TemplateDocument;

        public IWebConnection WebConnection
        {
            get { return _WebConnection; }
        }
        private readonly IWebConnection _WebConnection;

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
        }
        private readonly FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        public TemplateHandlerLocator TemplateHandlerLocator
        {
            get { return _TemplateHandlerLocator; }
        }
        private readonly TemplateHandlerLocator _TemplateHandlerLocator;

        public IEnumerable<string> Scripts
        {
            get { return _Scripts; }
        }
        private readonly LinkedList<string> _Scripts = new LinkedList<string>();

        /// <summary>
        /// set of loaded scripts
        /// </summary>
        private Set<string> ScriptsSet = new Set<string>();

        public void AddScript(string script)
        {
            if (ScriptsSet.Contains(script))
                return;

            // Remote scripts can not be parsed
            if (script.StartsWith("http://"))
                return;
            if (script.StartsWith("https://"))
                return;

            // TODO:  Cache these!

            // Parsing text files is faster then simulating a web request to get dependancies
            if (!script.Contains("?"))
                if (FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(script))
                {
                    IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(script);

                    if (fileContainer.FileHandler is ITextHandler)
                    {
                        ITextHandler scriptHandler = fileContainer.CastFileHandler<ITextHandler>();
                        AddDependancies(scriptHandler.ReadAll());

                        _Scripts.AddLast(script);
                        ScriptsSet.Add(script);

                        return;
                    }
                }

            // If the script can't be loaded as a text file, then its request needs to be simulated
            try
            {
                string shelledScript = WebConnection.ShellTo(script).ResultsAsString;
                AddDependancies(shelledScript);
            }
            catch (Exception ex)
            {
                // Exceptions are swallowed in case there is a problem loading the script
                log.Warn("Error loading dependancies for " + script, ex);
            }

            _Scripts.AddLast(script);
            ScriptsSet.Add(script);
        }

        static readonly char[] AddDependanciesSplit = new char[] { '\n', '\r' };

        private void AddDependancies(string loadedScript)
        {
            foreach (string lineFromScript in loadedScript.Split(AddDependanciesSplit, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = lineFromScript.TrimStart();

                if (line.StartsWith("//"))
                {
                    line = line.Substring(2).TrimStart();

                    if (line.StartsWith("Scripts:"))
                    {
                        line = line.Substring(8).Trim();

                        string[] dependantScripts = line.Split(',');

                        foreach (string dependantScriptItr in dependantScripts)
                        {
                            string dependantScript = dependantScriptItr.Trim();
                            if (dependantScript.Length > 0)
                                AddScript(dependantScript);
                        }
                    }
                }
            }
        }

        public LinkedList<string> CssFiles
        {
            get { return _CssFiles; }
        }
        private readonly LinkedList<string> _CssFiles = new LinkedList<string>();

        public SortedDictionary<double, LinkedList<XmlNode>> HeaderNodes
        {
            get { return _HeaderNodes; }
        }
        private readonly SortedDictionary<double, LinkedList<XmlNode>> _HeaderNodes = new SortedDictionary<double, LinkedList<XmlNode>>();

        internal void OnDocumentLoaded(IDictionary<string, string> getParameters, XmlElement element)
        {
            if (null != DocumentLoaded)
                DocumentLoaded(this, getParameters, element);
        }
        public event ElementProcessorFunction DocumentLoaded;

        internal void OnProcessElementForConditionalsAndComponents(IDictionary<string, string> getParameters, XmlElement element)
        {
            if (null != ProcessElementForConditionalsAndComponents)
                ProcessElementForConditionalsAndComponents(this, getParameters, element);
        }
        public event ElementProcessorFunction ProcessElementForConditionalsAndComponents;

        internal void OnProcessElementForDependanciesAndTemplates(IDictionary<string, string> getParameters, XmlElement element)
        {
            if (null != ProcessElementForDependanciesAndTemplates)
                ProcessElementForDependanciesAndTemplates(this, getParameters, element);
        }
        public event ElementProcessorFunction ProcessElementForDependanciesAndTemplates;

        internal void OnPostProcessElement(IDictionary<string, string> getParameters, XmlElement element)
        {
            if (null != PostProcessElement)
                PostProcessElement(this, getParameters, element);
        }
        public event ElementProcessorFunction PostProcessElement;

        public string GetCWD(XmlNode xmlNode)
        {
            while (null != xmlNode)
            {
                XmlAttribute cwdAttribute = xmlNode.Attributes.GetNamedItem(
                    "cwd",
                    TemplatingConstants.TaggingNamespace) as XmlAttribute;

                if (null != cwdAttribute)
                    return cwdAttribute.Value;

                xmlNode = xmlNode.ParentNode;
            }

            throw new KeyNotFoundException("Can not find CWD: " + xmlNode.OuterXml);
        }

        public void SetCWD(XmlNodeList xmlNodeList, string cwd)
        {
            foreach (XmlNode xmlNode in xmlNodeList)
                SetCWD(xmlNode, cwd);
        }

        public void SetCWD(IEnumerable<XmlNode> xmlNodes, string cwd)
        {
            foreach (XmlNode xmlNode in xmlNodes)
                SetCWD(xmlNode, cwd);
        }

        public void SetCWD(XmlNode xmlNode, string cwd)
        {
            // Really, the CWD can only be set on XmlElements
            // But sometimes, an XmlText can come in, when that happens, set the cwd on its children

            if (xmlNode is XmlElement)
            {
                XmlAttribute xmlAttribute = xmlNode.OwnerDocument.CreateAttribute("cwd", TemplatingConstants.TaggingNamespace);
                xmlAttribute.Value = cwd;
                xmlNode.Attributes.Append(xmlAttribute);
            }
            else if (xmlNode is XmlText)
                SetCWD(xmlNode.ChildNodes, cwd);
        }

        /// <summary>
        /// Assists in generating a warning node
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public XmlNode GenerateWarningNode(string message)
        {
            XmlNode toReturn = TemplateDocument.CreateNode(XmlNodeType.Element, "pre", TemplateDocument.DocumentElement.NamespaceURI);
            toReturn.InnerText = message;

            SetErrorClass(toReturn);

            return toReturn;
        }

        /// <summary>
        /// Adds the class="oc_template_warning" to the node
        /// </summary>
        /// <param name="htmlNode"></param>
        public void SetErrorClass(XmlNode htmlNode)
        {
            XmlAttribute classAttribute = (XmlAttribute)htmlNode.OwnerDocument.CreateAttribute("class");
            classAttribute.Value = TemplatingConstants.WarningNodeClass;
            htmlNode.Attributes.Append(classAttribute);
        }

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        public void ReplaceNodes(XmlNode componentNode, XmlNodeList newNodes)
        {
            ReplaceNodes(componentNode, Enumerable<XmlNode>.Cast(newNodes));
        }

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        public void ReplaceNodes(XmlNode componentNode, params XmlNode[] newNodes)
        {
            ReplaceNodes(componentNode, newNodes as IEnumerable<XmlNode>);
        }

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        public void ReplaceNodes(XmlNode componentNode, IEnumerable<XmlNode> newNodes)
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
        /// All of the nodes where GET arguments will not be processed
        /// </summary>
        public Dictionary<string, Set<string>> DeferedNodes = new Dictionary<string, Set<string>>();

        /// <summary>
        /// Indicates that ObjectCloud will not replace GET variables in a specific kind of node, but will instead defer processing until the node is handled later
        /// </summary>
        /// <param name="localName"></param>
        /// <param name="namespaceURI"></param>
        public void RegisterDeferedNode(string localName, string namespaceURI)
        {
            Set<string> set;

            if (!DeferedNodes.TryGetValue(localName, out set))
            {
                set = new Set<string>();
                DeferedNodes[localName] = set;
            }

            set.Add(namespaceURI);
        }

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="fileContainer"></param>
        /// <param name="xmlParseMode">The kind of text that's being parsed</param>
        /// <returns></returns>
        public XmlDocument LoadXmlDocument(IFileContainer fileContainer, XmlParseMode xmlParseMode)
        {
            WebConnection.TouchedFiles.Add(fileContainer);

            // Verify permission
            if (null == fileContainer.LoadPermission(WebConnection.Session.User.Id))
                throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "You do not have permission to read " + fileContainer.FullPath));

            if (!(fileContainer.FileHandler is ITextHandler))
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, fileContainer.FullPath + " must be a text file"));

            return LoadXmlDocument(
                fileContainer.CastFileHandler<ITextHandler>().ReadAll(),
                xmlParseMode,
                fileContainer.FullPath);
        }

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="xmlParseMode">The kind of text that's being parsed</param>
        /// <param name="fullpath"></param>
        /// <returns></returns>
        public XmlDocument LoadXmlDocument(string xml, XmlParseMode xmlParseMode, string fullpath)
        {
            XmlDocument xmlDocument = new XmlDocument();

            if (XmlParseMode.Html == xmlParseMode)
            {
                // see http://htmlagilitypack.codeplex.com and http://www.codeproject.com/KB/cs/html2xhtmlcleaner.aspx#xx2357981xx


                /*xml = xml.Split(new char[] { '>' }, 2)[1] + "<!DOCTYPE html>";


                HtmlDocument htmlDocument = new HtmlDocument();

                try
                {
                    htmlDocument.LoadHtml(xml);
                }
                catch (Exception e)
                {
                    log.Error("An error occured trying to parse html:\n" + xml, e);
                    throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, "An error occured trying to parse HTML, see the logs for more information"));
                }

                // Create a stream that's used for reading and writing
                Stream stream = new MemoryStream(xml.Length + (xml.Length / 5));
                
                // Write the html to XML
                XmlWriter xmlWriter = XmlWriter.Create(stream);
                htmlDocument.Save(xmlWriter);

                stream.Seek(0, SeekOrigin.Begin);

                // Read xml from the stream
                XmlReader xmlReader = XmlReader.Create(stream);
                xmlDocument.Load(xmlReader);*/

                xmlDocument.LoadXml(string.Format(
                    "<html xmlns=\"{0}\"><div>Converting HTML to XML is not supported</div></html>",
                    TemplateDocument.FirstChild.NamespaceURI));
            }
            else
            {
                try
                {
                    xmlDocument.LoadXml(xml);
                }
                catch (XmlException xmlException)
                {
                    // When the Xml parse mode is to try HTML, then try HTML but if an error occurs swallow it and throw the original
                    if (XmlParseMode.XmlThenHtml == xmlParseMode)
                        try
                        {
                            return LoadXmlDocument(xml, XmlParseMode.Html, fullpath);
                        }
                        catch { }

                    // Everyone else can see a nice descriptive error
                    StringBuilder errorBuilder = new StringBuilder(string.Format("An error occured while loading {0}\n", fullpath));
                    errorBuilder.AppendFormat("{0}\n\n\nFrom:\n", xmlException.Message);

                    string[] xmlLines = xml.Split('\n', '\r');
                    for (int ctr = 0; ctr < xmlLines.Length; ctr++)
                    {
                        int lineNumber = ctr + 1;

                        if (ctr < 9)
                            errorBuilder.Append("    ");
                        else if (ctr < 99)
                            errorBuilder.Append("   ");
                        else if (ctr < 999)
                            errorBuilder.Append("  ");
                        else if (ctr < 9999)
                            errorBuilder.Append(" ");

                        errorBuilder.AppendFormat("{0}: {1}\n", lineNumber, xmlLines[ctr]);

                        if (lineNumber == xmlException.LineNumber)
                            errorBuilder.AppendFormat("    -: {0}^\n", "".PadLeft(xmlException.LinePosition));
                    }

                    throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, errorBuilder.ToString()));
                }
            }

            return xmlDocument;
        }

        /// <summary>
        /// Finds the appropriate XmlParseMode attribute
        /// </summary>
        /// <param name="element">This is scanned to find the XmlParseMode</param>
        /// <returns></returns>
        public XmlParseMode GetXmlParseMode(XmlElement element)
        {
            string xmlParseModeAttribute = 
                element.GetAttribute("xmlparsemode", TemplatingConstants.TemplateNamespace);

            if (null == xmlParseModeAttribute)
                return XmlParseMode.Xml;

            else if ("html" == xmlParseModeAttribute)
                return XmlParseMode.Html;

            else if ("xmlthenhtml" == xmlParseModeAttribute)
                return XmlParseMode.XmlThenHtml;

            else
                return XmlParseMode.Xml;
        }

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="fileContainer"></param>
        /// <param name="xmlParseMode">The kind of text that's being parsed</param>
        /// <returns></returns>
        public XmlDocument LoadXmlDocumentAndReplaceGetParameters(
            IDictionary<string, string> getParameters, 
            IFileContainer fileContainer,
            XmlParseMode xmlParseMode)
        {
            XmlDocument xmlDocument = LoadXmlDocument(fileContainer, xmlParseMode);

            // Do the replacements
            ReplaceGetParameters(getParameters, xmlDocument);

            if (WebConnection.CookiesFromBrowser.ContainsKey(TemplatingConstants.XMLDebugModeCookie))
            {
                StringBuilder commentBuilder = new StringBuilder("\n\nDEBUG INFO\n");

                SortedDictionary<string, string> sortedGetParameters = new SortedDictionary<string, string>(getParameters);

                foreach (KeyValuePair<string, string> getArgument in sortedGetParameters)
                    commentBuilder.AppendFormat("\t{0}: {1}\n", getArgument.Key, getArgument.Value);

                commentBuilder.Append("\n\n");

                XmlComment comment = xmlDocument.CreateComment(commentBuilder.ToString());

                XmlNode firstChild = xmlDocument.FirstChild;
                if ((firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplatingConstants.TemplateNamespace))
                    firstChild.InsertBefore(comment, firstChild.FirstChild);
                else if ("html" != xmlDocument.FirstChild.LocalName)
                    xmlDocument.InsertBefore(comment, firstChild);
                else
                    firstChild.InsertBefore(comment, firstChild.FirstChild);
            }

            return xmlDocument;
        }

        /// <summary>
        /// Replaces all of the GET parameters in an XmlNode
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="xmlNode"></param>
        /// <returns></returns>
        public void ReplaceGetParameters(IDictionary<string, string> getParameters, XmlNode xmlNode)
        {
            // Perform replacements on all attributes
            if (null != xmlNode.Attributes)
                foreach (XmlAttribute xmlAttribute in
                    Enumerable<XmlAttribute>.FastCopy(Enumerable<XmlAttribute>.Cast(xmlNode.Attributes)))
                {
                    // Change the value
                    if (xmlAttribute.Value.Contains(TemplatingConstants.ArgBegin[0]))
                        xmlAttribute.Value = ReplaceGetParameters(getParameters, xmlAttribute.Value);

                    // Change the namespace
                    if (xmlAttribute.NamespaceURI.Contains(TemplatingConstants.ArgBegin[0]))
                    {
                        XmlAttribute newAttribute = xmlNode.OwnerDocument.CreateAttribute(
                            xmlAttribute.LocalName,
                            ReplaceGetParameters(getParameters, xmlAttribute.NamespaceURI));

                        newAttribute.Value = xmlAttribute.Value;

                        xmlNode.Attributes.InsertAfter(newAttribute, xmlAttribute);
                        xmlNode.Attributes.Remove(xmlAttribute);
                    }
                }

            // If the node is text and has a [_, then perform replacements
            if (xmlNode is XmlText)
            {
                XmlText xmlText = (XmlText)xmlNode;

                if (xmlText.InnerText.Contains(TemplatingConstants.ArgBegin[0]))
                    xmlText.InnerText = ReplaceGetParameters(getParameters, xmlText.InnerText);
            }

            // Recurse on all child nodes
            IEnumerable<XmlNode> childNodes = null;

            // Determine if recusion should occur
            if (xmlNode.HasChildNodes)
            {
                bool recurseToChildren = true;
                Set<string> set;

                // If this node is a defered node, then don't recurse to its children
                if (DeferedNodes.TryGetValue(xmlNode.LocalName, out set))
                    if (set.Contains(xmlNode.NamespaceURI))
                        recurseToChildren = false;

                if (recurseToChildren)
                {
                    childNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(xmlNode.ChildNodes));

                    foreach (XmlNode childNode in childNodes)
                        ReplaceGetParameters(getParameters, childNode);
                }
            }

            // Change the namespace
            if (xmlNode is XmlElement)
                if (xmlNode.NamespaceURI.Contains(TemplatingConstants.ArgBegin[0]))
                {
                    XmlElement newElement = xmlNode.OwnerDocument.CreateElement(
                        xmlNode.LocalName,
                        ReplaceGetParameters(getParameters, xmlNode.NamespaceURI));

                    if (xmlNode.HasChildNodes)
                    {
                        if (null == childNodes)
                            childNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(xmlNode.ChildNodes));

                        foreach (XmlNode childNode in childNodes)
                            newElement.AppendChild(childNode);
                    }

                    if (null != xmlNode.Attributes)
                        foreach (XmlAttribute xmlAttribute in
                            Enumerable<XmlAttribute>.FastCopy(Enumerable<XmlAttribute>.Cast(xmlNode.Attributes)))
                        {
                            newElement.Attributes.Append(xmlAttribute);
                        }

                    ReplaceNodes(xmlNode, newElement);
                }
        }

        /// <summary>
        /// Replaces all of the GET parameters in a string
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="xmlAsString"></param>
        /// <returns></returns>
        public string ReplaceGetParameters(IDictionary<string, string> getParameters, string xmlAsString)
        {
            StringBuilder getArgumentsResolvedBuilder = new StringBuilder(Convert.ToInt32(1.1 * Convert.ToDouble(xmlAsString.Length)));

            // generate [_ ! _]
            string unique = "u" + SRandom.Next<uint>().ToString();

            // Split at [_

            // Allocate the results builder, give a little breathing room in case the size grows
            string[] templateSplitAtArgs = xmlAsString.Split(TemplatingConstants.ArgBegin, StringSplitOptions.None);

            if (!xmlAsString.StartsWith("[_"))
                getArgumentsResolvedBuilder.Append(templateSplitAtArgs[0]);

            for (int ctr = 1; ctr < templateSplitAtArgs.Length; ctr++)
            {
                string[] argumentAndTemplateParts = templateSplitAtArgs[ctr].Split(TemplatingConstants.ArgEnd, 2, StringSplitOptions.None);

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
                        getArgumentsResolvedBuilder.Append(getParameters[argument]);
                    else if ("!" == argument)
                        getArgumentsResolvedBuilder.Append(unique);

                    getArgumentsResolvedBuilder.Append(remainder);
                }
            }

            return getArgumentsResolvedBuilder.ToString();
        }
    }
}
