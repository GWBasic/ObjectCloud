// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;

namespace ObjectCloud.Javascript.SubProcess
{
    public class ExecutionEnvironmentFactory : HasFileHandlerFactoryLocator, IExecutionEnvironmentFactory
    {
		private static ILog log = LogManager.GetLogger<ExecutionEnvironmentFactory>();

        public IExecutionEnvironment Create(
            IFileContainer fileContainer,
            IFileContainer javascriptContainer)
        {
            ParentScopeFactory parentScopeFactory = GetParentScopeFactory();

            return new ExecutionEnvironment(
                FileHandlerFactoryLocator,
                javascriptContainer,
                fileContainer,
                parentScopeFactory.SubProcess,
                parentScopeFactory.GetParentScope(javascriptContainer));
        }

        protected override void FileHandlerFactoryLocatorSet()
        {
            base.FileHandlerFactoryLocatorSet();

            for (int ctr = 0; ctr < NumSubProcesses; ctr++)
                ParentScopeFactories.Enqueue(new ParentScopeFactory(
                    FileHandlerFactoryLocator,
                    new SubProcess(FileHandlerFactoryLocator)));
        }

        /// <summary>
        /// The number of javascript sub processes
        /// </summary>
        public int NumSubProcesses
        {
            get { return _NumSubProcesses; }
            set { _NumSubProcesses = value; }
        }
        private int _NumSubProcesses = 2;

        private LockFreeQueue<ParentScopeFactory> ParentScopeFactories = new LockFreeQueue<ParentScopeFactory>();

        /// <summary>
        /// Threadsafe way to get a parent scope factory and sub process.  This ensures that the sub process is still running
        /// </summary>
        /// <returns></returns>
        public ParentScopeFactory GetParentScopeFactory()
        {
            // Spin while there isn't a sub process in the queue
            ParentScopeFactory parentScopeFactory;
            while (!ParentScopeFactories.Dequeue(out parentScopeFactory));

            if (!parentScopeFactory.SubProcess.Alive)
                parentScopeFactory = new ParentScopeFactory(FileHandlerFactoryLocator, new SubProcess(FileHandlerFactoryLocator));

            ParentScopeFactories.Enqueue(parentScopeFactory);

            return parentScopeFactory;
        }
    }
}
