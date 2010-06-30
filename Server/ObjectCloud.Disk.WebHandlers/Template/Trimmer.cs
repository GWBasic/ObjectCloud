﻿// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
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
    class Trimmer : ITemplateProcessor
    {
        void ITemplateProcessor.Handle(ITemplateParsingState templateParsingState)
        {
            templateParsingState.PostProcessElement += PostProcessElement;
        }

        void PostProcessElement(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            if (element.LocalName == "trim" && element.NamespaceURI == TemplatingConstants.TemplateNamespace)
            { }
        }
    }
}
