// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Wraps any kind of value in an object so it can be syncronized
    /// </summary>
    /// <typeparam name="T"></typeparam>
    //
	[Serializable]
    public class Wrapped<T>
    {
        public Wrapped() { }
        public Wrapped(T value) { Value = value; }

        /// <summary>
        /// The value
        /// </summary>
        public T Value = default(T);

        /// <summary>
        /// Allows automatic boxing of a value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator Wrapped<T>(T value)
        {
            return new Wrapped<T>(value);
        }
    }
}
