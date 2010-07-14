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
    /// Assists in processing permissions on a file
    /// </summary>
    public abstract class CanBase
    {
        private static ILog log = LogManager.GetLogger<CanBase>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="templateParsingState"></param>
        /// <param name="me"></param>
        /// <returns></returns>
        protected IFileContainer GetFileContainer(ITemplateParsingState templateParsingState, System.Xml.XmlNode me)
        {
            string currentWorkingDirectory = templateParsingState.GetCWD(me);
            XmlAttribute filenameAttribute = me.Attributes["filename"];

            if (null == filenameAttribute)
            {
                me.ParentNode.ParentNode.InsertBefore(
                    templateParsingState.GenerateWarningNode("filename not specified: " + me.OuterXml),
                    me.ParentNode);

                return null;
            }

            string filename = templateParsingState.WebConnection.WebServer.FileHandlerFactoryLocator.FileSystemResolver.GetAbsolutePath(
                currentWorkingDirectory,
                filenameAttribute.Value);

            try
            {
                return templateParsingState.WebConnection.WebServer.FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
            }
            catch (FileNotFoundException fnfe)
            {
                log.Warn("Attempted to get permission for a non-existant file: " + filenameAttribute.Value, fnfe);

                me.ParentNode.ParentNode.InsertBefore(
                    templateParsingState.GenerateWarningNode("File doesn't exist: " + me.OuterXml),
                    me.ParentNode);

                return null;
            }
        }
    }
}
