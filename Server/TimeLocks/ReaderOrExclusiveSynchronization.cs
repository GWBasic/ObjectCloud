using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ObjectCloud.Common;

namespace TimeLocks
{
    class ReaderOrExclusiveSynchronization : ISyncronized
    {
        ReaderOrExclusiveLock ReaderOrExclusiveLock = new ReaderOrExclusiveLock();

        public int Prop
        {
            get 
            {
                using (ReaderOrExclusiveLock.LockForQuickRead())
                    return _Prop; 
            }
            set
            {
                using (ReaderOrExclusiveLock.LockExclusive())
                    _Prop = value; 
            }
        }
        private int _Prop;
    }
}
