// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ObjectCloud.Common
{
    /// <summary>
    /// A static wrapper for Random.  This is initialized once and provides some additional helper methods
    /// </summary>
    public static class SRandom
    {
		static SRandom()
		{
			Seed = DateTime.UtcNow.Ticks.GetHashCode();
		}
		private static int Seed;
		
        /// <summary>
        /// The wrapped inner random object
        /// </summary>
        private static Random Random
		{
            get
            {
                if (null == _Random)
                    _Random = new Random(Interlocked.Increment(ref Seed));

                return _Random;
            }
		}
        [ThreadStatic]
        static Random _Random = null;

        /// <summary>
        /// Returns a random number between 0.0 and 1.0
        /// </summary>
        /// <returns></returns>
        public static double NextDouble()
        {
            return Random.NextDouble();
        }

        /// <summary>
        /// Returns the next integer
        /// </summary>
        /// <returns></returns>
        public static int Next()
        {
            return Random.Next();
        }

        /// <summary>
        /// Returns a nonnegative random number less than the specified maximum. 
        /// </summary>
        /// <param name="maxValue">The exclusive upper bound of the random number to be generated. maxValue must be greater than or equal to zero. </param>
        /// <returns>A 32-bit signed integer greater than or equal to zero, and less than maxValue; that is, the range of return values includes zero but not maxValue. </returns>
        public static int Next(int maxValue)
        {
            return Random.Next(maxValue);
        }

        /// <summary>
        /// Returns a random number within a specified range. 
        /// </summary>
        /// <param name="minValue">The inclusive lower bound of the random number returned. </param>
        /// <param name="maxValue">The exclusive upper bound of the random number returned. maxValue must be greater than or equal to minValue. </param>
        /// <returns>A 32-bit signed integer greater than or equal to minValue and less than maxValue; that is, the range of return values includes minValue but not maxValue. If minValue equals maxValue, minValue is returned. </returns>
        public static int Next(int minValue, int maxValue)
        {
            return Random.Next(minValue, maxValue);
        }

        /// <summary>
        /// Returns a randomly generated value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Next<T>()
            where T : struct
        {
            byte[] buffer = new byte[Marshal.SizeOf(typeof(T))];
            Random.NextBytes(buffer);

            IntPtr ptr = Marshal.AllocHGlobal(buffer.Length);

            try
            {
                Marshal.Copy(buffer, 0x0, ptr, buffer.Length);
                T toReturn = (T)Marshal.PtrToStructure(ptr, typeof(T));
                
                return toReturn;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Fills the elements of a specified array of bytes with random numbers. 
        /// </summary>
        /// <param name="buffer">An array of bytes to contain random numbers.</param>
        /// <exception cref="ArgumentNullException">buffer is a null reference (Nothing in Visual Basic).</exception>
        public static void NextBytes(byte[] buffer)
        {
            Random.NextBytes(buffer);
        }

        /// <summary>
        /// Returns a randomly-generated byte array of the given length
        /// </summary>
        /// <param name="length">The length of the byte array to return</param>
        public static byte[] NextBytes(int length)
        {
            return NextBytes(Convert.ToUInt64(length));
        }

        /// <summary>
        /// Returns a randomly-generated byte array of the given length
        /// </summary>
        /// <param name="length">The length of the byte array to return</param>
        public static byte[] NextBytes(ulong length)
        {
            byte[] buffer = new byte[length];
            NextBytes(buffer);
            return buffer;
        }
    }
}
