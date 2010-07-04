// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.Templating;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.TemplateConditions
{
    /// <summary>
    /// 
    /// </summary>
    public class HasValue : ITemplateConditionHandler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="TemplateParsingState"></param>
        /// <param name="me"></param>
        /// <returns></returns>
        public bool IsConditionMet(ITemplateParsingState TemplateParsingState, System.Xml.XmlNode me)
        {
            XmlAttribute valueAttribute = me.Attributes["value"];
            if (null != valueAttribute)
                if (null != valueAttribute.Value)
                    if (valueAttribute.Value.Length > 0)
                        return true;

            return false;
        }
    }
}
