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
    public class TemplatingConstants
    {
        /// <summary>
        /// ObjectCloud's templating xml namespace
        /// </summary>
        public string TemplateNamespace
        {
            get { return _TemplateNamespace; }
            set { _TemplateNamespace = value; }
        }
        private string _TemplateNamespace = "objectcloud_templating";

        /// <summary>
        /// A temporary namespace for tagging nodes; all nodes and attributes of this namespace will be removed prior to returning a document
        /// </summary>
        public string TaggingNamespace
        {
            get { return _TaggingNamespace; }
            set { _TaggingNamespace = value; }
        }
        private string _TaggingNamespace = "objectcloud_templating_GHDTTGXDNHT";

        /// <summary>
        /// Cookie name for enabling XML debugging
        /// </summary>
        public string XMLDebugModeCookie
        {
            get { return _XMLDebugModeCookie; }
            set { _XMLDebugModeCookie = value; }
        }
        private string _XMLDebugModeCookie = "developer_prettyprintXML";

        /// <summary>
        /// Cookie name to disable javascript minization
        /// </summary>
        public string JavascriptDebugModeCookie
        {
            get { return _JavascriptDebugModeCookie; }
            set { _JavascriptDebugModeCookie = value; }
        }
        private string _JavascriptDebugModeCookie = "developer_disableMinimizeJavascript";

        /// <summary>
        /// The CSS class for warning nodes
        /// </summary>
        public string WarningNodeClass
        {
            get { return _WarningNodeClass; }
            set { _WarningNodeClass = value; }
        }
        private string _WarningNodeClass = "oc_template_warning";

        /// <summary>
        /// Delimeter for the beginning of GET arguments
        /// </summary>
        public string[] ArgBegin
        {
            get { return _ArgBegin; }
            set { _ArgBegin = value; }
        }
        private string[] _ArgBegin = new string[] { "[_" };

        /// <summary>
        /// Delimeter for the end of GET arguments
        /// </summary>
        public string[] ArgEnd
        {
            get { return _ArgEnd; }
            set { _ArgEnd = value; }
        }
        private string[] _ArgEnd = new string[] { "_]" };

        /// <summary>
        /// Preffered Namespace for HTML tags
        /// </summary>
        public string HtmlNamespace
        {
            get { return _HtmlNamespace; }
            set { _HtmlNamespace = value; }
        }
        private string _HtmlNamespace;

        /// <summary>
        /// Namespaces for HTML tags
        /// </summary>
        public HashSet<string> HtmlNamespaces
        {
            get { return _HtmlNamespaces; }
            set { _HtmlNamespaces = value; }
        }
        private HashSet<string> _HtmlNamespaces;

        /// <summary>
        /// All of the indexed strings will be replaced by their corresponding value when converting HTML to XHTML
        /// </summary>
        public Dictionary<string, string> HTMLReplacementChars
        {
            get { return _HTMLReplacementChars; }
            set { _HTMLReplacementChars = value; }
        }
        private Dictionary<string, string> _HTMLReplacementChars;
    }

    /// <summary>
    /// The mode to use when parsing Xml when loading a document
    /// </summary>
    public enum XmlParseMode
    {
        /// <summary>
        /// The text is XML, if an error occurs, then throw it
        /// </summary>
        Xml,

        /// <summary>
        /// The text is probably XML, but if an error occurs, try converting it from HTML to XML before throwing an error
        /// </summary>
        XmlThenHtml,

        /// <summary>
        /// The text is HTML, convert it to XML before parsing
        /// </summary>
        Html
    }
}
