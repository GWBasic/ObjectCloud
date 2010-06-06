using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ObjectCloud.Common;

namespace TimeLocks
{
    class TimedLockSynchronization : ISyncronized
    {
        object key = new object();

        public int Prop
        {
            get
            {
                using (TimedLock.Lock(key))
                    return _Prop;
            }
            set
            {
                using (TimedLock.Lock(key))
                    _Prop = value;
            }
        }
        private int _Prop;
    }
}
