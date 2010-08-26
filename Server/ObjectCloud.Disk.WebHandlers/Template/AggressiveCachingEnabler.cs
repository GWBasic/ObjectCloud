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
    class AggressiveCachingEnabler : HasFileHandlerFactoryLocator, ITemplateProcessor
    {
        private static ILog log = LogManager.GetLogger<AggressiveCachingEnabler>();

        void ITemplateProcessor.Register(ITemplateParsingState templateParsingState)
        {
            templateParsingState.PostProcessElement += new State().PostProcessElement;
        }

        private class State
        {
            internal void PostProcessElement(ITemplateParsingState templateParsingState, IDictionary<string, string> getParameters, XmlElement element)
            {
                if (templateParsingState.TemplateHandlerLocator.TemplatingConstants.HtmlNamespaces.Contains(element.NamespaceURI))
                    /*if (element.LocalName == "script")
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

                                if (!templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(templateParsingState.TemplateHandlerLocator.TemplatingConstants.JavascriptDebugModeCookie))
                                    srcAttribute.Value = HTTPStringFunctions.AppendGetParameter(srcAttribute.Value, "EncodeFor", "JavaScript");
                                else if ((!srcAttribute.Value.StartsWith("http://")) && (!srcAttribute.Value.StartsWith("https://")))
                                {
                                    // If Javascript debug mode is on, verify that the script exists

                                    string fileName = srcAttribute.Value.Split(new char[] { '?' }, 2)[0];

                                    if (!templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(fileName))
                                    {
                                        element.InnerText = "alert('" + (srcAttribute.Value + " doesn't exist: " + element.OuterXml).Replace("'", "\\'") + "');";
                                        element.Attributes.Remove(srcAttribute);
                                    }
                                }
                            }
                        }
                        else
                            if (!templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(templateParsingState.TemplateHandlerLocator.TemplatingConstants.JavascriptDebugModeCookie))
                                try
                                {
                                    IEnumerable<XmlNode> toIterate = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(element.ChildNodes));

                                    // The xml contents of a script tag are minified in case xml is quoted
                                    StringBuilder scriptBuilder = new StringBuilder((element.InnerXml.Length * 5) / 4);
                                    foreach(XmlNode node in toIterate)
                                        if (node is XmlText)
                                            scriptBuilder.Append(node.InnerText);
                                        else
                                            scriptBuilder.Append(node.OuterXml);

                                    string minified = JavaScriptMinifier.Instance.Minify(scriptBuilder.ToString());

                                    foreach (XmlNode node in toIterate)
                                        element.RemoveChild(node);

                                    element.AppendChild(
                                        templateParsingState.TemplateDocument.CreateTextNode(minified));
                                }
                                catch (Exception e)
                                {
                                    log.Warn("Exception minimizing Javascript:\n" + element.InnerXml, e);
                                }
                                /*foreach (XmlText scriptContentsNode in Enumerable<XmlText>.Filter(element.ChildNodes))
                                    try
                                    {
                                        scriptContentsNode.InnerText = JavaScriptMinifier.Instance.Minify(scriptContentsNode.InnerText);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Warn("Exception minimizing Javascript:\n" + scriptContentsNode.InnerText, e);
                                    }*/

                    /*}
                    else */

                    if (element.LocalName == "script")
                    {
                        // Don't allow empty <script /> tags
                        if (null == element.InnerText)
                            element.InnerText = "";

                        if (element.InnerText.Length > 0)
                            if (!templateParsingState.WebConnection.CookiesFromBrowser.ContainsKey(templateParsingState.TemplateHandlerLocator.TemplatingConstants.JavascriptDebugModeCookie))
                                try
                                {
                                    IEnumerable<XmlNode> toIterate = Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(element.ChildNodes));

                                    // The xml contents of a script tag are minified in case xml is quoted
                                    StringBuilder scriptBuilder = new StringBuilder((element.InnerXml.Length * 5) / 4);
                                    foreach (XmlNode node in toIterate)
                                        if (node is XmlText)
                                            scriptBuilder.Append(node.InnerText);
                                        else
                                            scriptBuilder.Append(node.OuterXml);

                                    string minified = JavaScriptMinifier.Instance.Minify(scriptBuilder.ToString());

                                    foreach (XmlNode node in toIterate)
                                        element.RemoveChild(node);

                                    element.AppendChild(
                                        templateParsingState.TemplateDocument.CreateTextNode(minified));
                                }
                                catch (Exception e)
                                {
                                    log.Warn("Exception minimizing Javascript:\n" + element.InnerXml, e);
                                }
                        /*foreach (XmlText scriptContentsNode in Enumerable<XmlText>.Filter(element.ChildNodes))
                            try
                            {
                                scriptContentsNode.InnerText = JavaScriptMinifier.Instance.Minify(scriptContentsNode.InnerText);
                            }
                            catch (Exception e)
                            {
                                log.Warn("Exception minimizing Javascript:\n" + scriptContentsNode.InnerText, e);
                            }*/

                    }
                    else if (element.LocalName == "link")
                        AddBrowserCache(templateParsingState, element.Attributes["href"]);

                    else if (element.LocalName == "img")
                        AddBrowserCache(templateParsingState, element.Attributes["src"]);

                    else if (element.LocalName == "embed")
                        AddBrowserCache(templateParsingState, element.Attributes["src"]);

                    else
                    {
                        string browserCacheAttributeName = element.GetAttribute(
                            "browsercacheattribute",
                            templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);

                        if (browserCacheAttributeName != null)
                            if (browserCacheAttributeName.Length > 0)
                            {
                                AddBrowserCache(templateParsingState, element.Attributes[browserCacheAttributeName]);
                                element.RemoveAttribute(
                                    "browsercacheattribute",
                                    templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);
                            }
                    }
            }

            private enum BrowserCacheEnum
            {
                Date, MD5, Disable
            }

            /// <summary>
            /// Cache of URLs with the BrowserCache argument added when it's based off of an MD5.  This prevents re-calculating MD5s continuously for common URLs
            /// </summary>
            Dictionary<string, string> PrecalculatedWithMD5 = new Dictionary<string, string>();

            private void AddBrowserCache(ITemplateParsingState templateParsingState, XmlAttribute attribute)
            {
                string attributeValue = attribute.InnerText;
				
                // Don't add a dupe cache key
                if (attributeValue.Contains("?BrowserCache=") || attributeValue.Contains("&BrowserCache="))
                    return;

                XmlElement xmlElement = attribute.OwnerElement;

                if (null == attribute)
                    return;

                string fullPrefix = "http://" + templateParsingState.FileHandlerFactoryLocator.HostnameAndPort;

                if (attributeValue.StartsWith(fullPrefix))
                {
                    attribute.Value = attributeValue.Substring(fullPrefix.Length);
                    attributeValue = attribute.Value;
                }
                else if (attributeValue.StartsWith("http://"))
                    return;
                else if (attributeValue.StartsWith("https://"))
                    return;

                BrowserCacheEnum browserCache = BrowserCacheEnum.Disable;
                string browserCacheValue = xmlElement.GetAttribute("browsercache", templateParsingState.TemplateHandlerLocator.TemplatingConstants.TemplateNamespace);
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
                    string precalculated;
                    if (PrecalculatedWithMD5.TryGetValue(attributeValue, out precalculated))
                    {
                        attribute.Value = precalculated;
                        return;
                    }
                    
                    IWebResults shelled = templateParsingState.WebConnection.ShellTo(attributeValue);

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


                    // Save for reuse
                    PrecalculatedWithMD5[attributeValue] = attribute.InnerText;
                }
                else if (BrowserCacheEnum.Date == browserCache)
                {
                    string filename = attributeValue.Split(new char[] { '?' }, 2)[0];
					
                    if (templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(filename))
                    {
                        IFileContainer fileContainer = templateParsingState.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);

						templateParsingState.WebConnection.TouchedFiles.Add(fileContainer);

                        attribute.InnerText = HTTPStringFunctions.AppendGetParameter(
                            attributeValue,
                            "BrowserCache",
                            'd' + Convert.ToBase64String(BitConverter.GetBytes(fileContainer.LastModified.Ticks)));
                    }
                }
            }
        }
    }
}