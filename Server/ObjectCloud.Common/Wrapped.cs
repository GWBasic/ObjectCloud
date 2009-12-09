using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Wraps any kind of value in an object so it can be syncronized
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
