// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Interfaces.Disk
{
    public class ParticleException : DiskException
    {
        public ParticleException(string message) : base(message) { }
        public ParticleException(string message, Exception inner) : base(message, inner) { }

        public class CouldNotEstablishTrust : ParticleException
        {
            public CouldNotEstablishTrust(string message) : base(message) { }
            public CouldNotEstablishTrust(string message, Exception inner) : base(message, inner) { }
        }

        public class BadToken : ParticleException
        {
            public BadToken(string message) : base(message) { }
            public BadToken(string message, Exception inner) : base(message, inner) { }
        }

        public class CanNotSendNotification : ParticleException
        {
            public CanNotSendNotification(string message) : base(message) { }
            public CanNotSendNotification(string message, Exception inner) : base(message, inner) { }
        }
    }
}
