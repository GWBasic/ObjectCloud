// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Reflection;
using System.Xml;

using Common.Logging;

using Spring.Context;

namespace ObjectCloud.Spring.Config
{
    /// <summary>
    /// Assists in configuring objects instanciated with Spring using a simpler XML format.  This allows easy exposure of configuration to technical people without them seeing lots of Spring cruft
    /// </summary>
    public static class ConfigurationFileReader
    {
        private static ILog log = LogManager.GetLogger(typeof(ConfigurationFileReader));

        /// <summary>
        /// Opens configurationFile with an XML reader.  For each tag, the object in the applicationContext with the same name is loaded.  Each attribute in the tag sets the property with the corresponding name
        /// </summary>
        /// <param name="applicationContext">
        /// A <see cref="IApplicationContext"/>
        /// </param>
        /// <param name="configurationFile">
        /// A <see cref="System.String"/>
        /// </param>
        public static void ReadConfigFile(IApplicationContext applicationContext, string configurationFile)
        {
            using (TextReader tr = File.OpenText(configurationFile))
            using (XmlReader xmlReader = XmlReader.Create(tr))
            {
                xmlReader.MoveToContent();

                while (xmlReader.Read())
                    if (xmlReader.NodeType == XmlNodeType.Element)
                    {
                        string objectName = xmlReader.Name;

                        if (applicationContext.ContainsObject(objectName))
                        {
                            object toConfigure = applicationContext.GetObject(objectName);

                            Type type = toConfigure.GetType();

                            while (xmlReader.MoveToNextAttribute())
                            {
                                string propertyName = xmlReader.Name;
                                string valueString = xmlReader.Value;

                                PropertyInfo property = type.GetProperty(propertyName);

                                if (null != property)
                                {
                                    object value = Convert.ChangeType(valueString, property.PropertyType);

                                    property.SetValue(toConfigure, value, null);
                                }
                                else
                                    log.Warn("No property named \"" + propertyName + "\" found in " + objectName + ", of type " + toConfigure.GetType().FullName + ".");
                            }
                        }
                        else
                            log.Warn("No object named \"" + objectName + "\" found.");
                    }
            }
        }
    }
}
