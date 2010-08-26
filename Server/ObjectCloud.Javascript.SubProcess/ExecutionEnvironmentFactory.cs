// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.WebServer;

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

            object[] toEnumerate = new object[NumSubProcesses];

            Enumerable<object>.MultithreadedEach(
                1,
                toEnumerate,
                delegate(object o)
                {
                    ParentScopeFactories.Enqueue(new ParentScopeFactory(
                        FileHandlerFactoryLocator,
                        new SubProcess(FileHandlerFactoryLocator)));
                });
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
            ParentScopeFactory parentScopeFactory = null;
            DateTime timeout = DateTime.UtcNow.AddSeconds(SRandom.Next(2,6));

            while ((!ParentScopeFactories.Dequeue(out parentScopeFactory)) && (DateTime.UtcNow < timeout));

            // if spinning occurs for too long, then a new sub process is created
            if (null == parentScopeFactory)
                parentScopeFactory = new ParentScopeFactory(FileHandlerFactoryLocator, new SubProcess(FileHandlerFactoryLocator));

            // If the process died, restart it
            if (!parentScopeFactory.SubProcess.Alive)
                parentScopeFactory = new ParentScopeFactory(FileHandlerFactoryLocator, new SubProcess(FileHandlerFactoryLocator));

            ParentScopeFactories.Enqueue(parentScopeFactory);

            return parentScopeFactory;
        }

        public IWebResults Run(IWebConnection webConnection, IFileContainer javascriptContainer)
        {
            ParentScopeFactory parentScopeFactory  = GetParentScopeFactory();
            ParentScope parentScope = parentScopeFactory.GetParentScope(javascriptContainer);

            object result;

            ScopeWrapper scopeWrapper = new ScopeWrapper(
                FileHandlerFactoryLocator,
                parentScopeFactory.SubProcess,
                javascriptContainer,
                parentScope,
                out result);

            // Disposing happens on the threadpool as a way to return results sooner.  Why wait for cleanup in a multicore world?
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    scopeWrapper.Dispose();
                }
                catch (Exception e)
                {
                    log.Warn("Error disposing temporary scope", e);
                }
            });

            if (result is IWebResults)
                return (IWebResults)result;
            else
                return WebResults.ToJson(result);
        }
    }
}
