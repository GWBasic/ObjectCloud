using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeLocks
{
    class NoSyncronization : ISyncronized
    {
        public int Prop
        {
            get { return _Prop; }
            set { _Prop = value; }
        }
        private int _Prop;
    }
}
