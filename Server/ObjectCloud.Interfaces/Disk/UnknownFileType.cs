// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when an unknown file type is requested
    /// </summary>
    public class UnknownFileType : DiskException, IHasWebResults
    {
        public UnknownFileType(string fileType)
            : base("File type \"" + fileType + "\" is unknown") { }

        public IWebResults WebResults
        {
            get { return ObjectCloud.Interfaces.WebServer.WebResults.FromString(Status._400_Bad_Request, Message); }
        }
    }
}