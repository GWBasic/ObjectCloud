// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ObjectCloud.Common;

namespace TimeLocks
{
    class FastReadWriteLockSynchronization : ISyncronized
    {
        FastReadWriteLock FastReadWriteLock = new FastReadWriteLock();

        public int Prop
        {
            get
            {
                using (FastReadWriteLock.Read())
                    return _Prop;
            }
            set
            {
                using (FastReadWriteLock.Write())
                    _Prop = value;
            }
        }
        private int _Prop;
    }
}
