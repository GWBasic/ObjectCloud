﻿// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Template
{
    /// <summary>
    /// Removes debug information from documents
    /// </summary>
    class DebugInformationRemover : HasFileHandlerFactoryLocator, ITemplateProcessor
    {
        void ITemplateProcessor.Register(ITemplateParsingState templateParsingState)
        {
            if (!templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(templateParsingState.TemplateHandlerLocator.TemplatingConstants.XMLDebugModeCookie))
                templateParsingState.PostProcessElement += RemoveIfInternalData;
        }

        private void RemoveIfInternalData(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlNode element)
        {
            if (element.NamespaceURI == templateParsingState.TemplateHandlerLocator.TemplatingConstants.TaggingNamespace)
                element.ParentNode.RemoveChild(element);

            if (null != element.Attributes)
            {
                LinkedList<XmlAttribute> attributesToRemove = new LinkedList<XmlAttribute>();

                foreach (XmlAttribute xmlAttribute in element.Attributes)
                    if (xmlAttribute.NamespaceURI == templateParsingState.TemplateHandlerLocator.TemplatingConstants.TaggingNamespace)
                        attributesToRemove.AddLast(xmlAttribute);

                foreach (XmlAttribute xmlAttribute in attributesToRemove)
                    element.Attributes.Remove(xmlAttribute);
            }
        }
    }
}