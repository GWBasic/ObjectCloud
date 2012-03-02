// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer.UserAgent
{
    class Browser : IBrowser
    {
        /// <summary>
        /// Returns an IBrowser object that has information about the connection's browser
        /// </summary>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        public static IBrowser GetBrowser(string userAgent)
        {
            userAgent = userAgent.ToLowerInvariant();

            if (userAgent.Contains("ipad"))
                return new ApplePad();

            if (userAgent.Contains("iphone"))
                return new ApplePhone();

            if (userAgent.Contains("ipod"))
                return new ApplePod();

            if (userAgent.Contains("series60") || userAgent.Contains("symbian"))
                return new Blackberry();

            if (userAgent.Contains("android"))
                return new Android();

            if (userAgent.Contains("windows ce"))
                return new WindowsCE();

            if (userAgent.Contains("palm"))
                return new Palm();

            // The user-agent is unidentified
            return new Browser();
        }
    }
}
