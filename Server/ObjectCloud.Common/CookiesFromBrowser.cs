// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Common.Logging;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Represents all of the cookies passed in a request
    /// </summary>
    public class CookiesFromBrowser : Dictionary<string, string>
    {
		private static ILog log = LogManager.GetLogger(typeof(CookiesFromBrowser));
		
        public CookiesFromBrowser() : base() {}
        /// <summary>
        /// The header, split on line breaks.  (Each separate line is an element
        /// in the enumeration)
        /// </summary>
        /// <param name="header"></param>
        public CookiesFromBrowser(string cookiesToParseFromHeader) : base()
        {
            try
            {
                string[] cookiesToParse = cookiesToParseFromHeader.Split(';');

                foreach (string cookieToParse in cookiesToParse)
                {
                    string[] crumbs = cookieToParse.Trim().Split(new char[] { '=' }, 2);

                    if (crumbs.Length > 1)
                        this[HTTPStringFunctions.DecodeRequestParametersFromBrowser(crumbs[0])] =
                            HTTPStringFunctions.DecodeRequestParametersFromBrowser(crumbs[1]);
                    else
                        log.Warn("Browser sent invalid cookies, bad token: " + cookiesToParse);
                }

                // Remove quotes (because Jetty likes to shove them in...)
                foreach (string cookieName in new List<string>(Keys))
                {
                    string cookieValue = this[cookieName];

                    if (cookieValue.StartsWith("\""))
                        if (cookieValue.EndsWith("\""))
                            this[cookieName] = cookieValue.Substring(1, cookieValue.Length - 2);
                }
            }
            catch (Exception e)
            {
				log.Error("Exception when parsing parameters", e);
            }
        }
    }
}
