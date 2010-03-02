// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Data.Common;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk.FileHandlers
{
    /// <summary>
    /// Allows for handling of IDs in a typesafe manner
    /// </summary>
    /// <typeparam name="T">The type that the ID is for</typeparam>
    /// <typeparam name="long">The type of the ID</typeparam>
    public struct FileId : IFileId
    {
        public FileId(long value)
        {
            _Value = value;
        }

        /// <summary>
        /// The actual value of the ID
        /// </summary>
        public long Value
        {
            get { return _Value; }
        }
        private long _Value;

        /// <summary>
        /// Returns the ID's value, or null if it is null
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static long? GetValueOrNull(FileId? id)
        {
            if (null != id)
                return id.Value.Value;
            else
                return null;
        }

        public override string ToString()
        {
            return _Value.ToString(CultureInfo.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            if (obj is FileId)
                return ((FileId)obj)._Value.Equals(_Value);

            return false;
        }

        public override int GetHashCode()
        {
            return _Value.GetHashCode();
        }

        public static bool operator ==(FileId r, FileId l)
        {
            return ((long)r.Value).Equals((long)l.Value);
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

        public static IEnumerable<long> ToValues(IEnumerable<FileId> ids)
        {
            foreach (FileId id in ids)
                yield return id.Value;
        }

        public static Set<long> ToSet(IEnumerable<FileId> ids)
        {
            Set<long> toReturn = new Set<long>();

            foreach (FileId id in ids)
                toReturn.Add(id.Value);

            return toReturn;
        }

        public static implicit operator ID<IFileContainer, long>(FileId fileId)
        {
            return new ID<IFileContainer, long>(fileId.Value);
        }

        public static implicit operator FileId(ID<IFileContainer, long> fileId)
        {
            return new FileId(fileId.Value);
        }
    }
}
