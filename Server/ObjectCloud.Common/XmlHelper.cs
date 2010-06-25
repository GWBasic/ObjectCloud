// Copyright 2009, 2010 Andrew Rondeau
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
        public static IEnumerable<XmlNode> IterateAllElements(XmlNode xmlNode)
        {
            foreach (XmlNode childNode in xmlNode.ChildNodes)
            {
                foreach (XmlNode subChildNode in IterateAllElements(childNode))
                    yield return subChildNode;

                yield return childNode;
            }
        }
    }
}
