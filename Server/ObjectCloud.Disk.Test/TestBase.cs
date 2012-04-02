// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Spring.Config;

namespace ObjectCloud.Disk.Test
{
    public abstract class TestBase
    {
        public TestBase()
        {
            FileHandlerFactoryLocator.FileSystemResolver.Start();

            DoAdditionalSetup();
        }

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get
            {
                if (null == _FileHandlerFactoryLocator)
					try
					{
                    	_FileHandlerFactoryLocator =
                        	ContextLoader.GetFileHandlerFactoryLocatorForConfigurationFile("Test.ObjectCloudConfig.xml");
					}
					catch (Exception e)
					{
						Exception inner = e;
					
						do
						{
							Console.WriteLine(inner);
							inner = inner.InnerException;
						} while (null != inner);
					
						throw;
					}

                return _FileHandlerFactoryLocator; 
            }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator = null;

/*        [TestFixtureSetUp]
        public void SetUpFileSystem()
        {
            FileHandlerFactoryLocator.FileSystemResolver.Start();

            DoAdditionalSetup();
        }*/

        protected virtual void DoAdditionalSetup()
        {
        }

        /*[TestFixtureTearDown]
        public void TearDownFileSystem()
        {
            DoAdditionalTearDown();

            FileHandlerFactoryLocator.FileSystemResolver.Stop();
        }*/

        protected virtual void DoAdditionalTearDown()
        {
        }
    }
}
