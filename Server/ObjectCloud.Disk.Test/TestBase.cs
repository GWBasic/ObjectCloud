// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using Spring.Context;
using Spring.Context.Support;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.Disk.Test
{
    public abstract class TestBase : ObjectCloud.UnitTestHelpers.UnitTestBase
    {
        public TestBase() : base("file://Database.xml", "file://Factories.xml", "file://Disk.xml") { }

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get
            {
                if (null == _FileHandlerFactoryLocator)
                    _FileHandlerFactoryLocator = (FileHandlerFactoryLocator)SpringContext["FileHandlerFactoryLocator"];

                return _FileHandlerFactoryLocator; 
            }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator = null;

        [TestFixtureSetUp]
        public void SetUpFileSystem()
        {
            FileHandlerFactoryLocator.FileSystemResolver.Start();

            DoAdditionalSetup();
        }

        protected virtual void DoAdditionalSetup()
        {
        }

        [TestFixtureTearDown]
        public void TearDownFileSystem()
        {
            DoAdditionalTearDown();

            FileHandlerFactoryLocator.FileSystemResolver.Stop();

            GC.Collect();
        }

        protected virtual void DoAdditionalTearDown()
        {
        }
    }
}
