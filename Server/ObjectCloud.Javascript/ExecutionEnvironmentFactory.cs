// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Javascript;

namespace ObjectCloud.Javascript
{
    public class ExecutionEnvironmentFactory : IExecutionEnvironmentFactory
    {
        public IExecutionEnvironment Create(ObjectCloud.Interfaces.Disk.FileHandlerFactoryLocator fileHandlerFactoryLocator, ObjectCloud.Interfaces.Disk.IFileContainer theObject, ObjectCloud.Interfaces.Disk.IFileContainer javascriptContainer)
        {
            return new ExecutionEnvironment(fileHandlerFactoryLocator, theObject, javascriptContainer);
        }
    }
}
