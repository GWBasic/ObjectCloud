// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;

namespace ObjectCloud.Javascript.SubProcess
{
    public class ExecutionEnvironmentFactory : IExecutionEnvironmentFactory
    {
		private static ILog log = LogManager.GetLogger<ExecutionEnvironmentFactory>();
		
        public IExecutionEnvironment Create(
            FileHandlerFactoryLocator fileHandlerFactoryLocator, 
            IFileContainer fileContainer,
            IFileContainer javascriptContainer)
        {
            return new ExecutionEnvironment(
                fileHandlerFactoryLocator, javascriptContainer, fileContainer, SubProcessFactory);
        }

        public ISubProcessFactory SubProcessFactory
        {
            get { return _SubProcessFactory; }
            set { _SubProcessFactory = value; }
        }
        private ISubProcessFactory _SubProcessFactory;

        public void Start(IEnumerable<IFileContainer> files)
        {
			Enumerable<IFileContainer>.MultithreadedEach(
				1,
			    files,
			    delegate(IFileContainer file)
			    {
					try
					{
	                		SubProcessFactory.GetOrCreateSubProcess(file);
					}
					catch (Exception e)
					{
						log.Error("Error compiling Javascript for " + file.FullPath, e);
					}
				});
        }
    }
}
