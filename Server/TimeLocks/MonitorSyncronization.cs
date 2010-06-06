using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeLocks
{
    class MonitorSyncronization : ISyncronized
    {
        object key = new object();

        public int Prop
        {
            get 
            {
                lock (key)
                    return _Prop; 
            }
            set 
            {
                lock (key)
                    _Prop = value; 
            }
        }
        private int _Prop;
    }
}
