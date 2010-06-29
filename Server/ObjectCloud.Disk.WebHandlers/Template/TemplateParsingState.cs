// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Template
{
    internal class TemplateParsingState : ITemplateParsingState
    {
        internal TemplateParsingState(IWebConnection webConnection, XmlDocument templateDocument)
        {
            _TemplateDocument = templateDocument;
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
            string shelledScript = WebConnection.ShellTo(script).ResultsAsString;
            AddDependancies(shelledScript);

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
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="fileContainer"></param>
        /// <returns></returns>
        public XmlDocument LoadXmlDocumentAndReplaceGetParameters(
            IDictionary<string, string> getParameters, 
            IFileContainer fileContainer)
        {
            WebConnection.TouchedFiles.Add(fileContainer);

            // Verify permission
            if (null == fileContainer.LoadPermission(WebConnection.Session.User.Id))
                throw new WebResultsOverrideException(WebResults.From(Status._401_Unauthorized, "You do not have permission to read " + fileContainer.FullPath));

            if (!(fileContainer.FileHandler is ITextHandler))
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, fileContainer.FullPath + " must be a text file"));

            XmlDocument xmlDocument = new XmlDocument();
            string xml = ((ITextHandler)fileContainer.FileHandler).ReadAll();

            try
            {
                xmlDocument.LoadXml(ReplaceGetParameters(getParameters, xml));
            }
            catch (XmlException xmlException)
            {
                // Double-check read permission (in case later edits allow non-readers to still use a template with a named permission
                if (null == fileContainer.LoadPermission(WebConnection.Session.User.Id))
                    throw;

                // Everyone else can see a nice descriptive error
                StringBuilder errorBuilder = new StringBuilder(string.Format("An error occured while loading {0}\n", fileContainer.FullPath));
                errorBuilder.AppendFormat("{0}\n\n\nFrom:\n{1}", xmlException.Message, xml);

                throw new WebResultsOverrideException(WebResults.From(Status._500_Internal_Server_Error, errorBuilder.ToString()));
            }

            return xmlDocument;
        }

        /// <summary>
        /// Replaces all of the GET parameters in a string
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="xmlAsString"></param>
        /// <returns></returns>
        public string ReplaceGetParameters(IDictionary<string, string> getParameters, string xmlAsString)
        {
            // generate [_ ! _]
            string unique = "u" + SRandom.Next<uint>().ToString();

            // Split at [_
            StringBuilder getArgumentsResolvedBuilder = new StringBuilder(Convert.ToInt32(1.1 * Convert.ToDouble(xmlAsString.Length)));

            // Allocate the results builder, give a little breathing room in case the size grows
            string[] templateSplitAtArgs = xmlAsString.Split(TemplatingConstants.ArgBegin, StringSplitOptions.None);

            int ctr;
            if (xmlAsString.StartsWith("[_"))
                ctr = 0;
            else
            {
                ctr = 1;
                getArgumentsResolvedBuilder.Append(templateSplitAtArgs[0]);
            }

            for (; ctr < templateSplitAtArgs.Length; ctr++)
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
