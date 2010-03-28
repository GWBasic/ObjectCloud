// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;

namespace ObjectCloud.Javascript.SubProcess
{
    public class ExecutionEnvironmentFactory : IExecutionEnvironmentFactory
    {
        public IExecutionEnvironment Create(
            FileHandlerFactoryLocator fileHandlerFactoryLocator, 
            IFileContainer fileContainer,
            IFileContainer javascriptContainer)
        {
            return new ExecutionEnvironment(fileHandlerFactoryLocator, fileContainer, javascriptContainer);
        }
    }
}
