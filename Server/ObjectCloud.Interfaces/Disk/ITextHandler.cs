// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

﻿using System;
using System.Collections.Generic;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    public interface ITextHandler : IFileHandler
    {
        /// <summary>
        /// Returns all text in the text file
        /// </summary>
        /// <returns></returns>
        string ReadAll();

        /// <summary>
        /// Returns all of the text in the file in a line-by-line manner
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> ReadLines();

        /// <summary>
        /// Writes all of the contents into the text file
        /// </summary>
        /// <param name="changer">User making the change</param>
        /// <param name="contents"></param>
        void WriteAll(IUser changer, string contents);

        /// <summary>
        /// Appends to the text file
        /// </summary>
        /// <param name="changer"></param>
        /// <param name="toAppend"></param>
        void Append(IUser changer, string toAppend);

        /// <summary>
        /// Occurs whenever the text changes
        /// </summary>
        event EventHandler<ITextHandler, EventArgs> ContentsChanged;
    }
}
