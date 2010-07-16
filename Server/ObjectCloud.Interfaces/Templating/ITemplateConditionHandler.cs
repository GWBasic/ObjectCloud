// Copyright 2009, 2010 Andrew Rondeau
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
    /// Interface for handling a template condition
    /// </summary>
    public interface ITemplateConditionHandler
    {
        /// <summary>
        /// Returns true if the condition is met
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="me"></param>
        /// <returns></returns>
        bool IsConditionMet(ITemplateParsingState templateParsingState, XmlNode me);
    }
}
