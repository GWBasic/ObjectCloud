// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Templating
{
    /// <summary>
    /// Plugin for template post processors
    /// </summary>
    public interface ITemplateProcessor
    {
        /// <summary>
        /// Allows the template processor to register event handlers so that it can manipulate the template into a complete document
        /// </summary>
        /// <param name="templateParsingState"></param>
        void Register(ITemplateParsingState templateParsingState);
    }
}
