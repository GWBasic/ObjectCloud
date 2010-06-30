// Copyright 2009, 2010 Andrew Rondeau
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
    /// Enables aggressive caching on a document prior to returning it
    /// </summary>
    class AggressiveCachingEnabler : ITemplateProcessor
    {
        void ITemplateProcessor.Handle(ITemplateParsingState templateParsingState)
        {
            templateParsingState.PostProcessElement += PostProcessElement;
        }

        void PostProcessElement(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
        {
            if (element.NamespaceURI == templateParsingState.TemplateDocument.DocumentElement.NamespaceURI)
                if (element.LocalName == "script")
                {
                    // Don't allow empty <script /> tags
                    if (null == element.InnerText)
                        element.InnerText = "";

                    if (element.InnerText.Length == 0)
                    {
                        XmlAttribute srcAttribute = element.Attributes["src"];

                        if (null != srcAttribute)
                        {
                            AddBrowserCache(templateParsingState, srcAttribute);

                            if (!templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(TemplatingConstants.JavascriptDebugModeCookie))
                                srcAttribute.Value = HTTPStringFunctions.AppendGetParameter(srcAttribute.Value, "EncodeFor", "JavaScript");
                            else if ((!srcAttribute.Value.StartsWith("http://")) && (!srcAttribute.Value.StartsWith("https://")))
                            {
                                // If Javascript debug mode is on, verify that the script exists

                                string fileName = srcAttribute.Value.Split(new char[] {'?'}, 2)[0];

                                if (!templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(fileName))
                                {
                                    element.InnerText = "alert('" + (srcAttribute.Value + " doesn't exist: " + element.OuterXml).Replace("'", "\\'") + "');";
                                    element.Attributes.Remove(srcAttribute);
                                }
                            }
                        }
                    }
                    else
                        if (!templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(TemplatingConstants.JavascriptDebugModeCookie))
                            foreach (XmlText scriptContentsNode in Enumerable<XmlText>.Filter(element.ChildNodes))
                                scriptContentsNode.InnerText = JavaScriptMinifier.Instance.Minify(scriptContentsNode.InnerText);

                }
                else if (element.LocalName == "link")
                    AddBrowserCache(templateParsingState, element.Attributes["href"]);

                else if (element.LocalName == "img")
                    AddBrowserCache(templateParsingState, element.Attributes["src"]);
        }

        private enum BrowserCacheEnum
        {
            Date, MD5, Disable
        }

        private void AddBrowserCache(ITemplateParsingState templateParsingState, XmlAttribute attribute)
        {
            string attributeValue = attribute.InnerText;
            XmlElement xmlElement = attribute.OwnerElement;

            if (null == attribute)
                return;
            if (attributeValue.StartsWith("http://"))
                return;
            if (attributeValue.StartsWith("https://"))
                return;

            BrowserCacheEnum browserCache = BrowserCacheEnum.Disable;
            string browserCacheValue = xmlElement.GetAttribute("browsercache", TemplatingConstants.TemplateNamespace);
            if (browserCacheValue.Length > 0)
            {
                if ("date" == browserCacheValue)
                    browserCache = BrowserCacheEnum.Date;
                else if ("md5" == browserCacheValue)
                    browserCache = BrowserCacheEnum.MD5;
            }
            else
                if (attributeValue.Contains("?"))
                    browserCache = BrowserCacheEnum.MD5;
                else
                    browserCache = BrowserCacheEnum.Date;

            if (BrowserCacheEnum.MD5 == browserCache)
            {
                // Don't add a dupe cache key
                if (attributeValue.Contains("?BrowserCache=") || attributeValue.Contains("&BrowserCache="))
                    return;

                IWebResults shelled = templateParsingState.WebConnection.ShellTo(attributeValue);

                // if it's a 4xx result, and  if minimizing javascript is disabled,  alert that there's an error

                // Get a free hash calculator
                MD5CryptoServiceProvider hashAlgorithm = Recycler<MD5CryptoServiceProvider>.Get();

                byte[] scriptHash;
                try
                {
                    scriptHash = hashAlgorithm.ComputeHash(shelled.ResultsAsStream);
                }
                finally
                {
                    // Save the hash calculator for reuse
                    Recycler<MD5CryptoServiceProvider>.Recycle(hashAlgorithm);
                }

                attribute.InnerText = HTTPStringFunctions.AppendGetParameter(
                    attributeValue,
                    "BrowserCache",
                    'h' + Convert.ToBase64String(scriptHash));

            }
            else if (BrowserCacheEnum.Date == browserCache)
            {
                if (templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(attributeValue))
                {
                    IFileContainer fileContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(attributeValue);

                    attribute.InnerText = HTTPStringFunctions.AppendGetParameter(
                        attributeValue,
                        "BrowserCache",
                        'd' + Convert.ToBase64String(BitConverter.GetBytes(fileContainer.LastModified.Ticks)));
                }
                // else: if minimizing javascript is disabled, alert that the script is missing!
            }
        }
    }
}
