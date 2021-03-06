// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

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
