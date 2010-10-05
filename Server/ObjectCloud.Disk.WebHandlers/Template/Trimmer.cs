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

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Template
{
    /// <summary>
    /// Assists in trimming large XML blobs
    /// </summary>
    class Trimmer : HasFileHandlerFactoryLocator, ITemplateProcessor
    {
        void ITemplateProcessor.Register(ITemplateParsingState templateParsingState)
        {
            templateParsingState.PostProcessElement += PostProcessElement;
        }

        void PostProcessElement(ITemplateParsingState templateParsingState, IDictionary<string, object> getParameters, XmlElement element)
        {
            if (element.LocalName == "trim" && element.NamespaceURI == templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace)
            {
                XmlAttribute maxtagsAttribute = element.Attributes["maxtags"];
                XmlAttribute maxlengthAttribute = element.Attributes["maxlength"];

                ulong maxtags = ulong.MaxValue;
                if (null != maxtagsAttribute)
                    ulong.TryParse(maxtagsAttribute.Value, out maxtags);

                ulong maxlength = ulong.MaxValue;
                if (null != maxlengthAttribute)
                    ulong.TryParse(maxlengthAttribute.Value, out maxlength);

                ulong numTags = 0;
                ulong length = 0;
                
                bool trimming = false;
                foreach (XmlNode xmlNode in Enumerable<XmlNode>.FastCopy(XmlHelper.IterateAll<XmlNode>(element)))
                {
                    if (trimming)
                        xmlNode.ParentNode.RemoveChild(xmlNode);
                    else if (!(xmlNode is XmlComment))
                    {
                        numTags++;

                        if (numTags > maxtags)
                        {
                            xmlNode.ParentNode.RemoveChild(xmlNode);
                            trimming = true;
                        }
                        else if (xmlNode is XmlText)
                        {
                            XmlText xmlText = (XmlText)xmlNode;
                            ulong textLength = Convert.ToUInt64(xmlText.InnerText.Length);

                            if (length + textLength > maxlength)
                            {
                                xmlText.InnerText = xmlText.InnerText.Substring(0, Convert.ToInt32(maxlength - length));
                                trimming = true;
                            }
                            else
                                length += textLength;
                        }
                    }
                }

                templateParsingState.ReplaceNodes(element, element.ChildNodes);
            }
        }
    }
}
