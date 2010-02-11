// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Encapsulates the content sent from the client
    /// </summary>
    public interface IWebConnectionContent : IDisposable
    {
        /// <summary>
        /// Returns the content as a string
        /// </summary>
        /// <returns></returns>
        string AsString();

        /// <summary>
        /// Returns the content as a byte array
        /// </summary>
        /// <returns></returns>
        byte[] AsBytes();

        /// <summary>
        /// Returns the content as a stream
        /// </summary>
        /// <returns></returns>
        Stream AsStream();

        /// <summary>
        /// Writes the content to a file on disk
        /// </summary>
        /// <param name="filename"></param>
        void WriteToFile(string filename);
    }
}
