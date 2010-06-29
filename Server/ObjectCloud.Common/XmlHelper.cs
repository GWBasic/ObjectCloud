﻿// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Helps with Xml
    /// </summary>
    public static class XmlHelper
    {
        /// <summary>
        /// Helps iterate through all elements in an XmlNode, recursively going through all child elements.
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <returns></returns>
        public static IEnumerable<XmlElement> IterateAllElements(XmlNode xmlNode)
        {
            foreach (XmlNode childNode in xmlNode.ChildNodes)
                if (childNode is XmlElement)
                {
                    foreach (XmlElement subChildNode in IterateAllElements(childNode))
                        yield return subChildNode;

                    yield return (XmlElement)childNode;
                }
        }

        /// <summary>
        /// Helps iterate through all elements and comments in an XmlNode, recursively going through all child elements.
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <returns></returns>
        public static IEnumerable<XmlNode> IterateAllElementsAndComments(XmlNode xmlNode)
        {
            foreach (XmlNode childNode in xmlNode.ChildNodes)
                if (childNode is XmlElement)
                {
                    foreach (XmlNode subChildNode in IterateAllElementsAndComments(childNode))
                        yield return subChildNode;

                    yield return childNode;
                }
                else if (childNode is XmlComment)
                    yield return childNode;
        }

        /// <summary>
        /// Removes the node from its parent node
        /// </summary>
        /// <param name="xmlNode"></param>
        public static void RemoveFromParent(XmlNode xmlNode)
        {
            xmlNode.ParentNode.RemoveChild(xmlNode);
        }
    }
}
