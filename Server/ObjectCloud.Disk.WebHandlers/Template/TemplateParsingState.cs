// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using Common.Logging;
using HtmlAgilityPack;
using JsonFx.Json;

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
                    TemplateHandlerLocator.TemplatingConstants.TaggingNamespace) as XmlAttribute;

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
                XmlAttribute xmlAttribute = xmlNode.OwnerDocument.CreateAttribute("cwd", TemplateHandlerLocator.TemplatingConstants.TaggingNamespace);
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
            classAttribute.Value = TemplateHandlerLocator.TemplatingConstants.WarningNodeClass;
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
            if (null != componentNode.ParentNode)
                using (IEnumerator<XmlNode> enumerator = newNodes.GetEnumerator())
                    if (enumerator.MoveNext())
                    {
                        XmlNode newNode = enumerator.Current;
                        if ((newNode.OwnerDocument != componentNode.OwnerDocument) || (newNode.ParentNode == componentNode))
                            newNode = componentNode.OwnerDocument.ImportNode(newNode, true);

                        componentNode.ParentNode.ReplaceChild(newNode, componentNode);
                        XmlNode previousNode = newNode;

                        while (enumerator.MoveNext())
                        {
                            newNode = enumerator.Current;
                            if ((newNode.OwnerDocument != componentNode.OwnerDocument) || (newNode.ParentNode == componentNode))
                                newNode = componentNode.OwnerDocument.ImportNode(newNode, true);

                            previousNode.ParentNode.InsertAfter(newNode, previousNode);
                            previousNode = newNode;
                        }
                    }
                    else
                        componentNode.ParentNode.RemoveChild(componentNode);
        }

        /// <summary>
        /// All of the nodes where GET arguments will not be processed, indexed by localname then by namespace
        /// </summary>
        public Dictionary<string, Set<string>> DeferedNodes = new Dictionary<string, Set<string>>();

        /// <summary>
        /// Indicates that ObjectCloud will not replace GET variables or handle sub-nodes in a specific kind of node, but will instead defer processing until the node is handled later
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
                // see http://htmlagilitypack.codeplex.com 
                // see http://htmlagilitypack.codeplex.com/SourceControl/changeset/view/67079#52182

                xml = "<!DOCTYPE html><html>" + xml.Split(new char[] { '>' }, 2)[1];

                HtmlDocument htmlDocument = new HtmlDocument();

                // Create a stream that's used for reading and writing
                Stream stream = new MemoryStream(xml.Length + (xml.Length / 5));

                // Create the XML writer
                XmlWriterSettings settings = new XmlWriterSettings();
                //settings.ConformanceLevel = ConformanceLevel.Fragment;
                settings.OmitXmlDeclaration = true;
                XmlWriter xmlWriter = XmlWriter.Create(stream, settings);

                try
                {
                    htmlDocument.LoadHtml(xml);

                    // Write the html to XML
                    htmlDocument.OptionOutputAsXml = true;
                    htmlDocument.Save(xmlWriter);
                }
                catch (Exception e)
                {
                    log.Error("An error occured trying to parse html:\n" + xml, e);
                    throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, "An error occured trying to parse HTML, see the logs for more information"));
                }

                stream.Seek(0, SeekOrigin.Begin);

                // Read xml from the stream
                XmlReader xmlReader = XmlReader.Create(stream);
                xmlDocument.Load(xmlReader);

                // Get rid of junk nodes
                while ("html" != xmlDocument.FirstChild.LocalName)
                    xmlDocument.RemoveChild(xmlDocument.FirstChild);

                // Convert the namespace and clean up &nbsp; &lt; &gt that are converted incorrectly
                // TODO:  There really needs to be a better way to do this
                XmlAttribute namespaceAttribute = xmlDocument.CreateAttribute("xmlns");
                namespaceAttribute.Value = TemplateHandlerLocator.TemplatingConstants.HtmlNamespace;
                xmlDocument.DocumentElement.Attributes.Append(namespaceAttribute);

                xml = xmlDocument.OuterXml;
                foreach (KeyValuePair<string, string> toReplace in TemplateHandlerLocator.TemplatingConstants.HTMLReplacementChars)
                    xml = xml.Replace(toReplace.Key, toReplace.Value);
            }

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
                element.GetAttribute("xmlparsemode", TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);

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

            if (WebConnection.CookiesFromBrowser.ContainsKey(TemplateHandlerLocator.TemplatingConstants.XMLDebugModeCookie))
            {
                StringBuilder commentBuilder = new StringBuilder("\n\nDEBUG INFO\n");

                SortedDictionary<string, string> sortedGetParameters = new SortedDictionary<string, string>(getParameters);

                foreach (KeyValuePair<string, string> getArgument in sortedGetParameters)
                    commentBuilder.AppendFormat("\t{0}: {1}\n", getArgument.Key, getArgument.Value);

                commentBuilder.Append("\n\n");

                XmlComment comment = xmlDocument.CreateComment(commentBuilder.ToString());

                XmlNode firstChild = xmlDocument.FirstChild;
                if ((firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplateHandlerLocator.TemplatingConstants.TemplateNamespace))
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
            AugmentGetParameters(getParameters);

            // Perform replacements on all attributes
            if (null != xmlNode.Attributes)
                foreach (XmlAttribute xmlAttribute in
                    Enumerable<XmlAttribute>.FastCopy(Enumerable<XmlAttribute>.Cast(xmlNode.Attributes)))
                {
                    // Change the value
                    if (xmlAttribute.Value.Contains(TemplateHandlerLocator.TemplatingConstants.ArgBegin[0]))
                        xmlAttribute.Value = ReplaceGetParametersInt(getParameters, xmlAttribute.Value);

                    // Change the namespace
                    if (xmlAttribute.NamespaceURI.Contains(TemplateHandlerLocator.TemplatingConstants.ArgBegin[0]))
                    {
                        XmlAttribute newAttribute = xmlNode.OwnerDocument.CreateAttribute(
                            xmlAttribute.LocalName,
                            ReplaceGetParametersInt(getParameters, xmlAttribute.NamespaceURI));

                        newAttribute.Value = xmlAttribute.Value;

                        xmlNode.Attributes.InsertAfter(newAttribute, xmlAttribute);
                        xmlNode.Attributes.Remove(xmlAttribute);
                    }
                }

            // If the node is text and has a [_, then perform replacements
            if (xmlNode is XmlText)
            {
                XmlText xmlText = (XmlText)xmlNode;

                if (xmlText.InnerText.Contains(TemplateHandlerLocator.TemplatingConstants.ArgBegin[0]))
                    xmlText.InnerText = ReplaceGetParametersInt(getParameters, xmlText.InnerText);
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
                if (xmlNode.NamespaceURI.Contains(TemplateHandlerLocator.TemplatingConstants.ArgBegin[0]))
                {
                    XmlElement newElement = xmlNode.OwnerDocument.CreateElement(
                        xmlNode.LocalName,
                        ReplaceGetParametersInt(getParameters, xmlNode.NamespaceURI));

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
        /// Adds some default runtime metadata to the get parameters
        /// </summary>
        /// <param name="getParameters"></param>
        private void AugmentGetParameters(IDictionary<string, string> getParameters)
        {
            // Add some default user information
            if (!getParameters.ContainsKey("User.Identity"))
                if (WebConnection.Session.User.Id != FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id)
                    getParameters["User.Identity"] = WebConnection.Session.User.Identity;

            if (!getParameters.ContainsKey("User.Name"))
                if (WebConnection.Session.User.Local)
                    getParameters["User.Name"] = WebConnection.Session.User.Name;
        }

        /// <summary>
        /// Replaces all of the GET parameters in a string
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="xmlAsString"></param>
        /// <returns></returns>
        public string ReplaceGetParameters(IDictionary<string, string> getParameters, string xmlAsString)
        {
            AugmentGetParameters(getParameters);
            return ReplaceGetParametersInt(getParameters, xmlAsString);
        }

        /// <summary>
        /// Replaces all of the GET parameters in a string
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="xmlAsString"></param>
        /// <returns></returns>
        private string ReplaceGetParametersInt(IDictionary<string, string> getParameters, string xmlAsString)
        {
            StringBuilder getArgumentsResolvedBuilder = new StringBuilder(Convert.ToInt32(1.1 * Convert.ToDouble(xmlAsString.Length)));

            // generate [_ ! _]
            string unique = "u" + SRandom.Next<uint>().ToString();

            // Split at [_

            // Allocate the results builder, give a little breathing room in case the size grows
            string[] templateSplitAtArgs = xmlAsString.Split(TemplateHandlerLocator.TemplatingConstants.ArgBegin, StringSplitOptions.None);

            if (!xmlAsString.StartsWith("[_"))
                getArgumentsResolvedBuilder.Append(templateSplitAtArgs[0]);

            for (int ctr = 1; ctr < templateSplitAtArgs.Length; ctr++)
            {
                string[] argumentAndTemplateParts = templateSplitAtArgs[ctr].Split(TemplateHandlerLocator.TemplatingConstants.ArgEnd, 2, StringSplitOptions.None);

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

        /// <summary>
        /// Loads a component for use with JSON.  All inner nodes of element will be replaced with nodes from src
        /// </summary>
        /// <param name="element"></param>
        /// <param name="src"></param>
        /// <returns>True if the component nodes were loaded successfully; false if there was an error.  Exceptions are not thrown because processing can continue on other parts of the document</returns>
        public bool LoadComponentForJSON(XmlElement element, string src)
        {
            IFileContainer templateContainer;
            try
            {
                templateContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(src);

                XmlDocument newDocument = LoadXmlDocument(
                    templateContainer,
                    GetXmlParseMode(element));

                // Import the template nodes
                XmlNode firstChild = TemplateDocument.ImportNode(newDocument.FirstChild, true);

                if (!(firstChild.LocalName == "componentdef") && (firstChild.NamespaceURI == TemplateHandlerLocator.TemplatingConstants.TemplateNamespace))
                {
                    ReplaceNodes(
                        element,
                        GenerateWarningNode("src document must have an <oc:componentdef> tag"));

                    return false;
                }

                IEnumerable<XmlNode> replacementNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(firstChild.ChildNodes));


                // Replace child nodes with contents from file
                foreach (XmlNode xmlNode in element.ChildNodes)
                    element.RemoveChild(xmlNode);
                foreach (XmlNode xmlNode in replacementNodes)
                    element.AppendChild(xmlNode);

                SetCWD(replacementNodes, templateContainer.ParentDirectoryHandler.FileContainer.FullPath);

                return true;

            }
            catch (FileDoesNotExist fdne)
            {
                log.Error("Error resolving a template", fdne);
                ReplaceNodes(element, GenerateWarningNode("oc:src does not exist: " + element.OuterXml));

                return false;
            }
        }

        /// <summary>
        /// Runs a template on the child nodes of an element, and then replaces the element with its children
        /// </summary>
        /// <param name="element"></param>
        /// <param name="templateInput"></param>
        public void DoTemplate(XmlNode element, object templateInput)
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
                    ReplaceGetParameters(getParameters, xmlNode);

                ReplaceNodes(element, element.ChildNodes);
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

        /// <summary>
        /// Iterates through all elements except children of deferred elements
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <returns></returns>
        public IEnumerable<XmlElement> IterateNonDeferredElements(XmlNode xmlNode)
        {
            foreach (XmlNode childNode in Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(xmlNode.ChildNodes)))
                if (childNode is XmlElement)
                {
                    yield return (XmlElement)childNode;

                    Set<string> badNamespaces;

                    if (DeferedNodes.TryGetValue(childNode.LocalName, out badNamespaces))
                    {
                        if (!badNamespaces.Contains(childNode.NamespaceURI))
                            foreach (XmlElement subChildNode in IterateNonDeferredElements(childNode))
                                yield return subChildNode;
                    }
                    else
                        foreach (XmlElement subChildNode in IterateNonDeferredElements(childNode))
                            yield return subChildNode;


                }
        }
    }
}
