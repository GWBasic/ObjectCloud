// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    public interface INameValuePairsHandler : IFileHandler, IEnumerable<KeyValuePair<string, string>>
    {
        /// <summary>
        /// Sets the value associated with the name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        string this[string name] { get; }

        /// <summary>
        /// Sets the value associated with the name
        /// </summary>
        /// <param name="changer">User making the change</param>
        /// <param name="name"></param>
        /// <param name="value">Set to null to delete</param>
        void Set(IUser changer, string name, string value);

        /// <summary>
        /// Clears all existing values
        /// </summary>
        /// <param name="changer">User making the change</param>
        void Clear(IUser changer);

        /// <summary>
        /// True if there is a value set for the given name, false otherwise
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        bool Contains(string name);

        /// <summary>
        /// Writes all of the contents into the name-value pairs.  Does not clear existing pairs that
        /// have names not in contents
        /// </summary>
        /// <param name="changer">User making the change</param>
        /// <param name="contents"></param>
        /// <param name="clearExisting">Set to true to erase any values that are saved but not part of contents</param>
        void WriteAll(IUser changer, IEnumerable<KeyValuePair<string, string>> contents, bool clearExisting);
    }
}
