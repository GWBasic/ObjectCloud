// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;


namespace ObjectCloud.Interfaces.WebServer.UserAgent
{
    public class Handheld : IHandheld
    {
    }

    public class AppleHandheld : Handheld { }
    public class ApplePhone : AppleHandheld { }
    public class ApplePod : AppleHandheld { }
    public class Blackberry : Handheld { }
    public class Android : Handheld { }
    public class WindowsCE : Handheld { }
    public class Palm : Handheld { }
}
