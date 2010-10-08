﻿// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Represents all supported particle endpoints
    /// </summary>
    public enum ParticleEndpoint
    {
        RespondTrust,
        EstablishTrust,
        ReceiveNotification,
        ConfirmLinkPage,
        ConfirmLink
    }
}
