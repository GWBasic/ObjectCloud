// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
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
    class DependancyResolver : ITemplateProcessor
    {
        //static ILog log = LogManager.GetLogger<ComponentAndConditionalsResolver>();

        void ITemplateProcessor.Register(ITemplateParsingState templateParsingState)
        {
            templateParsingState.ProcessElementForDependanciesAndTemplates += ProcessElementForDependanciesAndTemplates;
        }

        void ProcessElementForDependanciesAndTemplates(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            if (element.NamespaceURI == TemplatingConstants.TemplateNamespace)
                if (element.LocalName == "script")
                {
                    XmlAttribute srcAttribute = element.Attributes["src"];

                    if (null != srcAttribute)
                        templateParsingState.AddScript(templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                            templateParsingState.GetCWD(element),
                            srcAttribute.Value));

                    XmlHelper.RemoveFromParent(element);
                }
                else if (element.LocalName == "open")
                {
                    XmlAttribute filenameAttribute = element.Attributes["filename"];
                    XmlAttribute varnameAttribute = element.Attributes["varname"];

                    if ((null != filenameAttribute) && (null != varnameAttribute))
                        templateParsingState.AddScript(string.Format(
                            "{0}?Method=GetJSW&assignToVariable={1}",
                            templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(templateParsingState.GetCWD(element), filenameAttribute.Value),
                            varnameAttribute.Value));

                    XmlHelper.RemoveFromParent(element);
                }
                else if (element.LocalName == "css")
                {
                    XmlAttribute srcAttribute = element.Attributes["src"];

                    if (null != srcAttribute)
                        templateParsingState.CssFiles.AddLast(templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                            templateParsingState.GetCWD(element),
                            srcAttribute.Value));

                    XmlHelper.RemoveFromParent(element);
                }
                else if (element.LocalName == "inserthead")
                {
                    // Try reading loc, default to 0

                    XmlAttribute locAttribute = element.Attributes["loc"];
                    double loc;

                    if (null != locAttribute)
                    {
                        if (!double.TryParse(locAttribute.Value, out loc))
                            loc = 0;
                    }
                    else
                        loc = 0;

                    LinkedList<XmlNode> insertNodes;
                    if (!templateParsingState.HeaderNodes.TryGetValue(loc, out insertNodes))
                    {
                        insertNodes = new LinkedList<XmlNode>();
                        templateParsingState.HeaderNodes[loc] = insertNodes;
                    }

                    foreach (XmlNode headNode in element.ChildNodes)
                        if (loc < 0)
                            insertNodes.AddFirst(headNode);
                        else
                            insertNodes.AddLast(headNode);

                    XmlHelper.RemoveFromParent(element);
                }
        }
    }
}
