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
