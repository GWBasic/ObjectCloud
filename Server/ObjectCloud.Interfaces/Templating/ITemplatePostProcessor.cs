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
    /// Plugin for template post processors
    /// </summary>
    public interface ITemplatePostProcessor
    {
        /// <summary>
        /// Returns all post processors that will run on the document
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="template"></param>
        /// <returns></returns>
        TemplatePostProcessors GetTemplatePostProcessors(IWebConnection webConnection, XmlDocument template);
    }

    /// <summary>
    /// Encapsulates all post processors that can be returned from a post processing plugin.  This is a struct so that more post processors can be added without modifying ITemplatePostProcessor
    /// </summary>
    public struct TemplatePostProcessors
    {
        /// <summary>
        /// All of these are called for every element in a document that's returned to the user
        /// </summary>
        public IEnumerable<ElementProcessorFunction> ElementProcessorFunctions;
    }

    /// <summary>
    /// Delegate for functions that are called on all elements in a template before returning the resulting document
    /// </summary>
    /// <param name="webConnection"></param>
    /// <param name="element"></param>
    public delegate void ElementProcessorFunction(IWebConnection webConnection, XmlNode element);
}
