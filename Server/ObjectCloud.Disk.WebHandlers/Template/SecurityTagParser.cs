// Copyright 2009, 2010 Andrew Rondeau
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
    /// Allows insertion of XML in a secure way
    /// </summary>
    class SecurityTagParser : ITemplateProcessor
    {
        static ILog log = LogManager.GetLogger<SecurityTagParser>();

        public void Register(ITemplateParsingState templateParsingState)
        {
            templateParsingState.ProcessElementForDependanciesAndTemplates += ProcessElementForDependanciesAndTemplates;
        }

        void ProcessElementForDependanciesAndTemplates(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            if (TemplatingConstants.TemplateNamespace == element.NamespaceURI)
                if ("safeparse" == element.LocalName)
                    DoSafeParseTag(templateParsingState, getParameters, element);
                else if ("safe" == element.LocalName)
                    DoSafeTag(templateParsingState, getParameters, element);
        }

        void DoSafeParseTag(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            // Generate the xml
            StringBuilder xml = new StringBuilder(
                string.Format("<safeparse xmlns=\"{0}\">", templateParsingState.TemplateDocument.FirstChild.NamespaceURI),
                element.OuterXml.Length);

            foreach (XmlNode xmlNode in element.ChildNodes)
                if (xmlNode is XmlText)
                    xml.Append(((XmlText)xmlNode).InnerText);
                else
                    xml.Append(xmlNode.OuterXml);

            xml.Append("</safeparse>");

            // load it
            XmlDocument xmlDocument = new XmlDocument();

            try
            {
                xmlDocument.LoadXml(xml.ToString());
            }
            catch (XmlException e)
            {
                log.Error("Error parsing xml for use in oc:safeparse", e);

                templateParsingState.ReplaceNodes(
                    element,
                    templateParsingState.GenerateWarningNode("Can not parse XML in tag: " + xml.ToString()));

                return;
            }

            // import the new tags
            LinkedList<XmlNode> importedNodes = new LinkedList<XmlNode>();
            foreach (XmlNode xmlNode in xmlDocument.FirstChild.ChildNodes)
                importedNodes.AddLast(templateParsingState.TemplateDocument.ImportNode(xmlNode, true));

            templateParsingState.ReplaceNodes(element, importedNodes);

            MakeSafe(importedNodes, GetSafeTags(templateParsingState.FileHandlerFactoryLocator));
        }

        void DoSafeTag(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            IEnumerable<XmlNode> childNodes = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(element.ChildNodes));

            templateParsingState.ReplaceNodes(element, childNodes);

            MakeSafe(childNodes, GetSafeTags(templateParsingState.FileHandlerFactoryLocator));
        }

        private void MakeSafe(IEnumerable<XmlNode> xmlNodes, Dictionary<string, Set<string>> safeTags)
        {
            foreach (XmlNode xmlNode in xmlNodes)
            {
                bool safe = false;

                Set<string> safeTagNames;
                if (safeTags.TryGetValue(xmlNode.NamespaceURI, out safeTagNames))
                    if (safeTagNames.Contains(xmlNode.LocalName))
                        safe = true;

                if (safe)
                    MakeSafe(Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(xmlNode.ChildNodes)), safeTags);
                else
                    XmlHelper.RemoveFromParent(xmlNode);
            }
        }

        private Dictionary<IFileSystemResolver, Dictionary<string, Set<string>>> SafeTagsCache = new Dictionary<IFileSystemResolver, Dictionary<string, Set<string>>>();

        private ReaderWriterLockSlim SafeTagsCacheLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Gets the safe tags to use
        /// </summary>
        /// <param name="fileHandlerFactoryLocator"></param>
        /// <returns>Dictionary, indexed by namespace, then sets of valid tags</returns>
        private Dictionary<string, Set<string>> GetSafeTags(FileHandlerFactoryLocator fileHandlerFactoryLocator)
        {
            Dictionary<string, Set<string>> toReturn;

            SafeTagsCacheLock.EnterReadLock();

            try
            {
                if (SafeTagsCache.TryGetValue(fileHandlerFactoryLocator.FileSystemResolver, out toReturn))
                    return toReturn;
            }
            finally
            {
                SafeTagsCacheLock.ExitReadLock();
            }

            SafeTagsCacheLock.EnterWriteLock();

            try
            {
                toReturn = new Dictionary<string, Set<string>>();

                IFileContainer fileContainer = fileHandlerFactoryLocator.FileSystemResolver.ResolveFile("/Shell/Security/safetags.json");
                ITextHandler textHandler = fileContainer.CastFileHandler<ITextHandler>();

                Dictionary<string, object> safeTags = JsonReader.Deserialize<Dictionary<string, object>>(textHandler.ReadAll());
                foreach (KeyValuePair<string, object> namespaceKVP in safeTags)
                {
                    Set<string> validTags = new Set<string>(Enumerable<string>.Cast((IEnumerable)namespaceKVP.Value));
                    toReturn[namespaceKVP.Key] = validTags;
                }

                textHandler.ContentsChanged += new EventHandler<ITextHandler, EventArgs>(textHandler_ContentsChanged);

                SafeTagsCache[fileHandlerFactoryLocator.FileSystemResolver] = toReturn;
            }
            catch (Exception e)
            {
                log.Error("Error when parsing /Shell/Security/safetags.json", e);
                throw;
            }
            finally
            {
                SafeTagsCacheLock.ExitWriteLock();
            }

            return toReturn;
        }

        void textHandler_ContentsChanged(ITextHandler sender, EventArgs e)
        {
            SafeTagsCacheLock.EnterWriteLock();

            try
            {
                SafeTagsCache.Remove(sender.FileContainer.FileHandlerFactoryLocator.FileSystemResolver);
            }
            finally
            {
                SafeTagsCacheLock.ExitWriteLock();
            }
        }
    }
}
