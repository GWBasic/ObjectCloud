// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Generic event args where T is a value passed around
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EventArgs<T> : EventArgs
    {
        public EventArgs()
        {
            Value = default(T);
        }

        public EventArgs(T value)
        {
            Value = value;
        }

        /// <summary>
        /// The value
        /// </summary>
        public T Value
        {
            get { return _Value; }
            set { _Value = value; }
        }
        private T _Value;
    }
}
