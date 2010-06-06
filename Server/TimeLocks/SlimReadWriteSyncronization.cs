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
