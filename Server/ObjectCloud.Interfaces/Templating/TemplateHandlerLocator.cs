// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Templating
{
    /// <summary>
    /// Assists in locating information about templates
    /// </summary>
    public class TemplateHandlerLocator
    {
        /// <summary>
        /// All of the template condition handlers, by their tag names
        /// </summary>
        public Dictionary<string, ITemplateConditionHandler> TemplateConditionHandlers
        {
            get { return _TemplateConditionHandlers; }
            set { _TemplateConditionHandlers = value; }
        }
        private Dictionary<string, ITemplateConditionHandler> _TemplateConditionHandlers;
    }
}
