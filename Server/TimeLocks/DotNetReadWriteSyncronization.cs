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
                ReaderWriterLock.AcquireWriterLock(TimeSpan.MaxValue);

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
