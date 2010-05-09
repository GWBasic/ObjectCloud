// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Simple sub process factory that holds a limited set of sub processes
    /// </summary>
    public class SubProcessFactory : ISubProcessFactory
    {
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// All of the sub processes, indexed by the full path to their class
        /// </summary>
        private Dictionary<string, SubProcess> SubProcessesByClass = new Dictionary<string, SubProcess>();

        /// <summary>
        /// When the sub processes were last modified, used to kill a sub process when its class is modified
        /// </summary>
        private Dictionary<string, DateTime> ClassLastModified = new Dictionary<string, DateTime>();

        /// <summary>
        /// Returns the corresponding sub process for the given class.  Creates it if it isn't running, restarts if the class was modified
        /// </summary>
        /// <param name="javascriptContainer"></param>
        /// <returns></returns>
        public SubProcess GetOrCreateSubProcess(IFileContainer javascriptContainer)
        {
            SubProcess toReturn = null;

            using (TimedLock.Lock(javascriptContainer))
            {
                using (TimedLock.Lock(SubProcessesByClass))
                {
                    if (SubProcessesByClass.TryGetValue(javascriptContainer.FullPath, out toReturn))
                    {
                        DateTime classLastModified;
                        if (ClassLastModified.TryGetValue(javascriptContainer.FullPath, out classLastModified))
                            if (javascriptContainer.LastModified == classLastModified)
                                if (toReturn.Alive)
                                    return toReturn;
                    }

                    if (null != toReturn)
                        toReturn.Dispose();

                    ClassLastModified[javascriptContainer.FullPath] = javascriptContainer.LastModified;
                }

                toReturn = new SubProcess(javascriptContainer, FileHandlerFactoryLocator);

                using (TimedLock.Lock(SubProcessesByClass))
                    SubProcessesByClass[javascriptContainer.FullPath] = toReturn;

                return toReturn;
            }
        }

        /// <summary>
        /// If the corresponding sub process is based on an outdated version of the class, disposes it
        /// </summary>
        /// <param name="javascriptContainer"></param>
        public void DisposeSubProcessIfOutdated(IFileContainer javascriptContainer)
        {
            using (TimedLock.Lock(SubProcessesByClass))
                if (javascriptContainer.LastModified != ClassLastModified[javascriptContainer.FullPath])
                {
                    SubProcessesByClass[javascriptContainer.FullPath].Dispose();
                    SubProcessesByClass.Remove(javascriptContainer.FullPath);
                    ClassLastModified.Remove(javascriptContainer.FullPath);
                }
        }
    }
}