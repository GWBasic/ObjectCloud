﻿// Copyright 2009 - 2012 Andrew Rondeau
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
using ObjectCloud.Interfaces.Utilities;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Template
{
    /// <summary>
    /// Allows insertion of XML in a secure way
    /// </summary>
    class SecurityTagParser : HasFileHandlerFactoryLocator, ITemplateProcessor
    {
        //static ILog log = LogManager.GetLogger<SecurityTagParser>();

        public void Register(ITemplateParsingState templateParsingState)
        {
            templateParsingState.ProcessElementForDependanciesAndTemplates += ProcessElementForDependanciesAndTemplates;
        }

        void ProcessElementForDependanciesAndTemplates(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlElement element)
        {
            if (templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace == element.NamespaceURI)
                if ("safeparse" == element.LocalName)
                    DoSafeParseTag(templateParsingState, getParameters, element);
                else if ("safe" == element.LocalName)
                    DoSafeTag(templateParsingState, getParameters, element);
                else if ("parse" == element.LocalName)
                    DoParseTag(templateParsingState, getParameters, element);
        }

        void DoSafeParseTag(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlElement element)
        {
            MakeSafe(DoParseTag(templateParsingState, getParameters, element));
        }
		
        IEnumerable<XmlNode> DoParseTag(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlElement element)
        {
            // Generate the xml
            StringBuilder xml = new StringBuilder(
                string.Format("<html xmlns=\"{0}\"><head></head><body>", templateParsingState.TemplateDocument.FirstChild.NamespaceURI),
                element.OuterXml.Length);

            foreach (XmlNode xmlNode in element.ChildNodes)
                if (xmlNode is XmlText)
                    xml.Append(((XmlText)xmlNode).InnerText);
                else
                    xml.Append(xmlNode.OuterXml);

            xml.Append("</body></html>");

            XmlDocument xmlDocument;
            try
            {
                xmlDocument = templateParsingState.LoadXmlDocument(
                    xml.ToString(),
                    templateParsingState.GetXmlParseMode(element),
                    element.OuterXml);
            }
            catch (WebResultsOverrideException wroe)
            {
                templateParsingState.ReplaceNodes(
                    element,
                    templateParsingState.GenerateWarningNode(wroe.WebResults.ResultsAsString));

                return new XmlNode[0];
            }

            // import the new tags
            IEnumerable<XmlNode> importedNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(
                xmlDocument.FirstChild.ChildNodes[1].ChildNodes));

            templateParsingState.ReplaceNodes(element, importedNodes);
			
			return importedNodes;
		}

        void DoSafeTag(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlElement element)
        {
            IEnumerable<XmlNode> childNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(element.ChildNodes));

            templateParsingState.ReplaceNodes(element, childNodes);

            MakeSafe(childNodes);
        }

        private void MakeSafe(IEnumerable<XmlNode> xmlNodes)
        {
            foreach (XmlNode xmlNode in xmlNodes)
            {
                bool safe = false;

                HashSet<string> safeTagNames;

                if (xmlNode is XmlText)
                    safe = true;

                else if (xmlNode is XmlComment)
                    safe = true;

                else if (SafeTags.NamedSet.TryGetValue(xmlNode.NamespaceURI, out safeTagNames))
                    if (safeTagNames.Contains(xmlNode.LocalName))
                        safe = true;

                if (safe)
                    MakeSafe(Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(xmlNode.ChildNodes)));
                else
                    XmlHelper.RemoveFromParent(xmlNode);
            }
        }

        /// <summary>
        /// The safe tags to use
        /// </summary>
        public JSONNamedSetReader SafeTags
        {
            get 
            {
                if (null == _SafeTags)
                    Interlocked.CompareExchange<JSONNamedSetReader>(
                        ref _SafeTags,
                        new JSONNamedSetReader(FileHandlerFactoryLocator, "/Shell/Security/safetags.json"),
                        null);

                return _SafeTags; 
            }
        }
        private JSONNamedSetReader _SafeTags;
    }
}
