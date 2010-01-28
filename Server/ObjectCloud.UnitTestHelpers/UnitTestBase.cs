// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Spring.Context;
using Spring.Context.Support;

using ObjectCloud.Common;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.UnitTestHelpers
{
    public abstract class UnitTestBase
    {
        public UnitTestBase(params string[] springConfigFiles)
        {
            SpringConfigFiles = new List<string>(springConfigFiles);

            // Get all of the plugins
            foreach (string pluginFilename in Directory.GetFiles(".", "Plugin.*.xml"))
                SpringConfigFiles.Add("file://" + pluginFilename);

            try
            {
                if (Enumerable.Equals(SpringConfigFiles as IEnumerable, LastContextFiles as IEnumerable))
                    _SpringContext = LastContext;
                else
                {
                    _SpringContext = LoadContext("Test.ObjectCloudConfig.xml");
                    LastContext = _SpringContext;
                    LastContextFiles = SpringConfigFiles;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Can not start Spring.", e);
            }
        }

        public IApplicationContext LoadContext(string configFile)
        {
            IApplicationContext toReturn = new XmlApplicationContext(SpringConfigFiles.ToArray());
            ConfigurationFileReader.ReadConfigFile(toReturn, configFile);

            return toReturn;
        }

        protected IApplicationContext SpringContext
        {
            get { return _SpringContext; }
        }
        private IApplicationContext _SpringContext = null;

        private static IApplicationContext LastContext = null;
        private static List<string> LastContextFiles = null;

        public List<string> SpringConfigFiles
        {
            get { return _SpringConfigFiles; }
            set { _SpringConfigFiles = value; }
        }
        private List<string> _SpringConfigFiles;
    }
}
