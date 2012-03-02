// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TimeLocks
{
    class DotNetReadWriteSyncronization : ISyncronized
    {
        private ReaderWriterLock ReaderWriterLock = new ReaderWriterLock();

        public int Prop
        {
            get
            {
                ReaderWriterLock.AcquireReaderLock(int.MaxValue);

                try
                {
                    return _Prop;
                }
                finally
                {
                    ReaderWriterLock.ReleaseReaderLock();
                }
            }
            set 
            {
                ReaderWriterLock.AcquireWriterLock(int.MaxValue);

                try
                {
                    _Prop = value;
                }
                finally
                {
                    ReaderWriterLock.ReleaseWriterLock();
                }
            }
        }
        private int _Prop;
    }
}
