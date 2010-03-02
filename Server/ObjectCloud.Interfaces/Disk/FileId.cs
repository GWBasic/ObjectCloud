// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Allows for handling of IDs in a typesafe manner
    /// </summary>
    /// <typeparam name="T">The type that the ID is for</typeparam>
    /// <typeparam name="TID">The type of the ID</typeparam>
    public struct FileId
    {
        /// <summary>
        /// Parses a FileId from a string
        /// </summary>
        public static ParseFileIdDelegate ParseFileId;

        /// <summary>
        /// The actual value of the ID
        /// </summary>
        private object Value;

        public override string ToString()
        {
            return Value.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is FileId)
                return ((FileId)obj).Value.Equals(Value);

            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(FileId r, FileId l)
        {
            return (r.Value).Equals(l.Value);
        }

        public static bool operator !=(FileId r, FileId l)
        {
            return !(r == l);
        }

        public static bool operator ==(FileId? r, FileId l)
        {
            if (null != r) // if r isn't null
                return r.Value == l;

			return false;   // r is null, l isn't null
        }
        
		public static bool operator ==(FileId r, FileId? l)
		{
			return l == r;
		}
		
        public static bool operator !=(FileId? r, FileId l)
        {
            return !(r == l);
        }

		public static bool operator !=(FileId r, FileId? l)
		{
			return !(r == l);
		}

        public static IEnumerable<TID> ToValues<TID>(IEnumerable<FileId> ids)
        {
            foreach (FileId id in ids)
                yield return (TID)id.Value;
        }
    }

    /// <summary>
    /// Delegate used for parsing FileIds from a string
    /// </summary>
    /// <param name="toParse"></param>
    /// <returns></returns>
    public delegate FileId ParseFileIdDelegate(string toParse);
}
