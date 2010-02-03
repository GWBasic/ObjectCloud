// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Allows for handling of IDs in a typesafe manner
    /// </summary>
    /// <typeparam name="T">The type that the ID is for</typeparam>
    /// <typeparam name="TID">The type of the ID</typeparam>
    public struct ID<T, TID> : IID
        where TID : struct
    {
        public ID(TID value)
        {
            _Value = value;
        }

        /// <summary>
        /// The actual value of the ID
        /// </summary>
        public TID Value
        {
            get { return _Value; }
        }
        private TID _Value;

        /// <summary>
        /// Returns the ID's value, or null if it is null
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static TID? GetValueOrNull(ID<T, TID>? id)
        {
            if (null != id)
                return id.Value.Value;
            else
                return null;
        }

        public override string ToString()
        {
            return _Value.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is ID<T, TID>)
                return ((ID<T, TID>)obj)._Value.Equals(_Value);

            return false;
        }

        public override int GetHashCode()
        {
            return _Value.GetHashCode();
        }

        object IID.Value
        {
            get { return _Value; }
        }

        public static bool operator ==(ID<T, TID> r, ID<T, TID> l)
        {
            return ((TID)r.Value).Equals((TID)l.Value);
        }

        public static bool operator !=(ID<T, TID> r, ID<T, TID> l)
        {
            return !(r == l);
        }

        public static bool operator ==(ID<T, TID>? r, ID<T, TID> l)
        {
            if (null != r) // if r isn't null
                return r.Value == l;

			return false;   // r is null, l isn't null
        }
        
		public static bool operator ==(ID<T, TID> r, ID<T, TID>? l)
		{
			return l == r;
		}
		
        public static bool operator !=(ID<T, TID>? r, ID<T, TID> l)
        {
            return !(r == l);
        }

		public static bool operator !=(ID<T, TID> r, ID<T, TID>? l)
		{
			return !(r == l);
		}

        public static IEnumerable<TID> ToValues(IEnumerable<ID<T, TID>> ids)
        {
            foreach (ID<T, TID> id in ids)
                yield return id.Value;
        }

        public static Set<TID> ToSet(IEnumerable<ID<T, TID>> ids)
        {
            Set<TID> toReturn = new Set<TID>();

            foreach (ID<T, TID> id in ids)
                toReturn.Add(id.Value);

            return toReturn;
        }
    }
}
