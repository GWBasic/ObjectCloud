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
    public static class TemplatingConstants
    {
        /// <summary>
        /// ObjectCloud's templating xml namespace
        /// </summary>
        public const string TemplateNamespace = "objectcloud_templating";

        /// <summary>
        /// A temporary namespace for tagging nodes; all nodes and attributes of this namespace will be removed prior to returning a document
        /// </summary>
        public const string TaggingNamespace = "objectcloud_templating_GHDTTGXDNHT";

        /// <summary>
        /// Cookie name for enabling XML debugging
        /// </summary>
        public const string XMLDebugModeCookie = "developer_prettyprintXML";

        /// <summary>
        /// Cookie name to disable javascript minization
        /// </summary>
        public const string JavascriptDebugModeCookie = "developer_disableMinimizeJavascript";

        /// <summary>
        /// The CSS class for warning nodes
        /// </summary>
        public const string WarningNodeClass = "oc_template_warning";

        /// <summary>
        /// Delimeter for the beginning of GET arguments
        /// </summary>
        public static readonly string[] ArgBegin = new string[] { "[_" };

        /// <summary>
        /// Delimeter for the end of GET arguments
        /// </summary>
        public static readonly string[] ArgEnd = new string[] { "_]" };
    }
}
