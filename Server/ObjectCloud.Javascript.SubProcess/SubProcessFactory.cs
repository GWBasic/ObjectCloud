// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Javascript.SubProcess
{
    /// <summary>
    /// Simple sub process factory that holds a limited set of sub processes
    /// </summary>
    public class SubProcessFactory : ISubProcessFactory
    {
        /// <summary>
        /// The number of sub processes to create
        /// </summary>
        public int NumSubProcesses
        {
            get { return _NumSubProcesses; }
            set { _NumSubProcesses = value; }
        }
        private int _NumSubProcesses = 6;

        private Rotater<SubProcess> SubProcessRotater;

        /// <summary>
        /// All of the sub processes
        /// </summary>
        private List<SubProcess> SubProcesses = new List<SubProcess>();

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set 
            {
                _FileHandlerFactoryLocator = value;

                _CompiledJavascriptManager = new CompiledJavascriptManager(value);

                for (int ctr = 0; ctr < NumSubProcesses; ctr++)
                    SubProcesses.Add(new SubProcess());

                SubProcessRotater = new Rotater<SubProcess>(SubProcesses);
            }
        }
        FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        public CompiledJavascriptManager CompiledJavascriptManager
        {
            get { return _CompiledJavascriptManager; }
        }
        private CompiledJavascriptManager _CompiledJavascriptManager;

        /// <summary>
        /// Returns a sub-process
        /// </summary>
        /// <param name="javascriptContainer"></param>
        /// <returns></returns>
        public SubProcess GetSubProcess()
        {
            return SubProcessRotater.Next();           
        }

        ~SubProcessFactory()
        {
            try
            {
                foreach (SubProcess subProcess in SubProcesses)
                    try
                    {
                        subProcess.Dispose();
                    }
                    catch { }
            }
            catch { }
        }
    }
}