// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

using Common.Logging;
using Spring.Context;
using Spring.Context.Support;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Spring.Config
{
    /// <summary>
    /// Loads and tracks Spring contexts
    /// </summary>
	public static class ContextLoader
	{
        /// <summary>
        /// The loaded contexts
        /// </summary>
        private static Dictionary<string, IApplicationContext> LoadedContexts = new Dictionary<string, IApplicationContext>();

        private static IApplicationContext GetApplicationContextForConfigurationFile(string configurationFile)
        {
            using (TimedLock.Lock(LoadedContexts))
            {
                IApplicationContext context;
                if (LoadedContexts.TryGetValue(configurationFile, out context))
                    return context;

                List<string> springFilesToLoad = new List<string>();
                foreach (string springFile in File.ReadAllLines("SpringFiles.txt"))
                    springFilesToLoad.Add("file://" + springFile);

                // Get all of the plugins
                foreach (string pluginFilename in Directory.GetFiles(".", "Plugin.*.xml"))
                    springFilesToLoad.Add("file://" + pluginFilename);

                // Load objects declared in Spring
                context = new XmlApplicationContext(springFilesToLoad.ToArray());

                // Load configuration file for options that can be set up in simplified XML
                ConfigurationFileReader.ReadConfigFile(context, configurationFile);

                LoadedContexts[configurationFile] = context;

                return context;
            }
        }

        /// <summary>
        /// Returns the object
        /// </summary>
        /// <param name="configurationFile"></param>
        /// <returns></returns>
        public static object GetObjectFromConfigurationFile(string configurationFile, string objectId)
        {
            return GetApplicationContextForConfigurationFile(configurationFile)[objectId];
        }

        /// <summary>
        /// Returns the FileHandlerFactoryLocator for the configuration file
        /// </summary>
        /// <param name="configurationFile"></param>
        /// <returns></returns>
        public static FileHandlerFactoryLocator GetFileHandlerFactoryLocatorForConfigurationFile(string configurationFile)
        {
            return (FileHandlerFactoryLocator)GetApplicationContextForConfigurationFile(configurationFile)["FileHandlerFactoryLocator"];
        }
    }
}
