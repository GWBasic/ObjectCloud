// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TimeLocks
{
    class SlimReadWriteSyncronization : ISyncronized
    {
        private ReaderWriterLockSlim ReaderWriterLock = new ReaderWriterLockSlim();

        public int Prop
        {
            get
            {
                ReaderWriterLock.EnterReadLock();

                try
                {
                    return _Prop;
                }
                finally
                {
                    ReaderWriterLock.ExitReadLock();
                }
            }
            set
            {
                ReaderWriterLock.EnterWriteLock();

                try
                {
                    _Prop = value;
                }
                finally
                {
                    ReaderWriterLock.ExitWriteLock();
                }
            }
        }
        private int _Prop;
    }
}
