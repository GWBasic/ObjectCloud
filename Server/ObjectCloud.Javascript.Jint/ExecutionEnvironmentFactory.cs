// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Javascript;

namespace ObjectCloud.Javascript.Jint
{
    public class ExecutionEnvironmentFactory : IExecutionEnvironmentFactory
    {
        public IExecutionEnvironment Create(ObjectCloud.Interfaces.Disk.FileHandlerFactoryLocator fileHandlerFactoryLocator, ObjectCloud.Interfaces.Disk.IFileContainer theObject, ObjectCloud.Interfaces.Disk.IFileContainer javascriptContainer)
        {
            return new ExecutionEnvironment(fileHandlerFactoryLocator, theObject, javascriptContainer);
        }
    }
}
