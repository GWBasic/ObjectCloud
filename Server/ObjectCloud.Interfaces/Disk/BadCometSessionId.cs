// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Thrown when a filename is bad
    /// </summary>
    public class BadCometSessionId : DiskException
    {
        public BadCometSessionId(ID<ICometSession, ushort> sessionId)
            :
            base("Session " + sessionId.Value.ToString("xxxx") + " is unknown") { }
    }
}