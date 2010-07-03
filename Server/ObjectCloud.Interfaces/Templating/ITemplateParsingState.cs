// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Templating
{
    /// <summary>
    /// Interface for objects that encapsulate the state of parsing a template
    /// </summary>
    public interface ITemplateParsingState
    {
        /// <summary>
        /// The document that's being built and modified
        /// </summary>
        XmlDocument TemplateDocument { get; }

        /// <summary>
        /// The web connection
        /// </summary>
        IWebConnection WebConnection { get; }

        FileHandlerFactoryLocator FileHandlerFactoryLocator { get; }
        TemplateHandlerLocator TemplateHandlerLocator { get; }

        /// <summary>
        /// Adds a script to be loaded.  Automatically adds any dependancies via the // Scripts: convention
        /// </summary>
        /// <param name="script"></param>
        void AddScript(string script);

        /// <summary>
        /// All of the scripts that will be loaded
        /// </summary>
        IEnumerable<string> Scripts { get; }

        /// <summary>
        /// All of the css files that will be loaded
        /// </summary>
        LinkedList<string> CssFiles { get; }

        /// <summary>
        /// All of the header nodes that will be inserted
        /// </summary>
        SortedDictionary<double, LinkedList<XmlNode>> HeaderNodes { get; }

        /// <summary>
        /// This event occurs once the document is first loaded, before any processing typically occurs
        /// </summary>
        event ElementProcessorFunction DocumentLoaded;

        /// <summary>
        /// This event occurs on each element in a document while processing for conditionals and components.  It can occur many times on the same element, and continues to occur repeatedly while the document is modified.
        /// </summary>
        event ElementProcessorFunction ProcessElementForConditionalsAndComponents;

        /// <summary>
        /// This event occurs on each element in a document while collecting dependancies and evaluating templates.  It can occur many times on the same element, and continues to occur repeatedly while the document is modified.
        /// </summary>
        event ElementProcessorFunction ProcessElementForDependanciesAndTemplates;

        /// <summary>
        /// The event occurs on each element after document processing occurs.  It allows for final modification of a document once it's completely built
        /// </summary>
        event ElementProcessorFunction PostProcessElement;

        /// <summary>
        /// Generates a warning node
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        XmlNode GenerateWarningNode(string message);

        /// <summary>
        /// Adds the class="oc_template_warning" to the node
        /// </summary>
        /// <param name="htmlNode"></param>
        void SetErrorClass(XmlNode htmlNode);

        /// <summary>
        /// Gets the current working directory of a node
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <returns></returns>
        string GetCWD(XmlNode xmlNode);

        /// <summary>
        /// Sets the current working directory of all nodes in an XmlNodeList
        /// </summary>
        /// <param name="xmlNodeList"></param>
        /// <param name="cwd"></param>
        void SetCWD(XmlNodeList xmlNodeList, string cwd);

        /// <summary>
        /// Sets the current working directory of all nodes in an XmlNodeList
        /// </summary>
        /// <param name="xmlNodes"></param>
        /// <param name="cwd"></param>
        void SetCWD(IEnumerable<XmlNode> xmlNodes, string cwd);

        /// <summary>
        /// Sets the current working directory of a node
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <param name="cwd"></param>
        void SetCWD(XmlNode xmlNode, string cwd);

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        void ReplaceNodes(XmlNode componentNode, XmlNodeList newNodes);

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        void ReplaceNodes(XmlNode componentNode, params XmlNode[] newNodes);

        /// <summary>
        /// Replaces componentNode with newNodes.  Performs all needed imports
        /// </summary>
        /// <param name="newNodes"></param>
        /// <param name="componentNode"></param>
        void ReplaceNodes(XmlNode componentNode, IEnumerable<XmlNode> newNodes);

        /// <summary>
        /// Indicates that ObjectCloud will not replace GET variables in child nodes of this specific node; although GET variables still apply to the specified node's attributes and namespaces
        /// </summary>
        /// <param name="localName"></param>
        /// <param name="namespaceURI"></param>
        void RegisterDeferedNode(string localName, string namespaceURI);

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="fileContainer"></param>
        /// <returns></returns>
        XmlDocument LoadXmlDocument(
            IFileContainer fileContainer,
            XmlParseMode xmlParseMode);

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="fullpath">This should be the full path to the source file</param>
        /// <returns></returns>
        XmlDocument LoadXmlDocument(
            string xml,
            XmlParseMode xmlParseMode,
            string fullpath);

        /// <summary>
        /// Finds the appropriate XmlParseMode attribute
        /// </summary>
        /// <param name="element">This is scanned to find the XmlParseMode</param>
        XmlParseMode GetXmlParseMode(XmlElement element);

        /// <summary>
        /// Loads an XmlDocument from the filecontainer, replacing GET parameters and verifying permissions
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="fileContainer"></param>
        /// <returns></returns>
        XmlDocument LoadXmlDocumentAndReplaceGetParameters(
            IDictionary<string, string> getParameters,
            IFileContainer fileContainer,
            XmlParseMode xmlParseMode);

        /// <summary>
        /// Replaces all of the GET parameters in an XmlNode
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="xmlNode"></param>
        /// <returns></returns>
        void ReplaceGetParameters(IDictionary<string, string> getParameters, XmlNode xmlNode);

        /// <summary>
        /// Replaces all of the GET parameters in a string
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="xmlAsString"></param>
        /// <returns></returns>
        string ReplaceGetParameters(IDictionary<string, string> getParameters, string xmlAsString);
    }

    /// <summary>
    /// Delegate for functions that are called on all elements in a template before returning the resulting document
    /// </summary>
    /// <param name="webConnection"></param>
    /// <param name="element"></param>
    public delegate void ElementProcessorFunction(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element);
}
