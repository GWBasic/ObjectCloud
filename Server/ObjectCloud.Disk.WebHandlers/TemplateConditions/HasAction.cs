// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.TemplateConditions
{
    /// <summary>
    /// Returns true if the file supports the action
    /// </summary>
    public class HasAction : CanBase, ITemplateConditionHandler
    {
        /// <summary>
        /// </summary>
        /// <param name="templateParsingState"></param>
        /// <param name="me"></param>
        /// <returns></returns>
        public bool IsConditionMet(ITemplateParsingState templateParsingState, XmlNode me)
        {
            IFileContainer fileContainer = GetFileContainer(templateParsingState, me);
            string action = me.Attributes["action"].Value;

            return fileContainer.FileConfigurationManager.Actions.ContainsKey(action);
        }
    }
}