// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Various utility functions
    /// </summary>
    public static class HTTPStringFunctions
    {
        /// <summary>
        /// Returns a URI with a get parameter added
        /// </summary>
        /// <param name="URI"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string AppendGetParameter(string URI, string name, string value)
        {
            name = EncodeRequestParametersForBrowser(name);
            value = EncodeRequestParametersForBrowser(value);

            if ((URI.Contains(string.Format("{0}=", name)))
                && (URI.Contains("?")))
            {
                string[] urlAndParms = URI.Split(new char[] { '?' });

                // Request already contains name
                RequestParameters rp = new RequestParameters(urlAndParms[1]);

                rp[name] = value;

                StringBuilder toReturn = new StringBuilder(urlAndParms[0]);
                toReturn.Append("?");

                foreach (string prevName in rp.Keys)
                    toReturn.AppendFormat("{0}={1}&", prevName, rp[prevName]);

                toReturn.Remove(toReturn.Length - 1, 1);
                return toReturn.ToString();
            }
            if (URI.Contains("?"))
                return string.Format("{0}&{1}={2}", URI, name, value);
            else
                return string.Format("{0}?{1}={2}", URI, name, value);
        }

        /// <summary>
        /// Decodes request parameters from browser
        /// </summary>
        /// <param name="toDecode"></param>
        /// <returns></returns>
        public static string DecodeRequestParametersFromBrowser(string toDecode)
        {
            if (toDecode.Length == 0)
                return "";

            toDecode = toDecode.Replace('+', ' ');

            StringBuilder toReturn = new StringBuilder();

            string[] tokens = toDecode.Split(new char[] { '%' }, StringSplitOptions.RemoveEmptyEntries);

            int ctr;

            if (toDecode.StartsWith("%"))
                ctr = 0;
            else
            {
                toReturn.Append(tokens[0]);
                ctr = 1;
            }

            for (; ctr < tokens.Length; ctr++)
            {
                // Get the ASCII code
                byte val = byte.Parse(tokens[ctr].Substring(0, 2), NumberStyles.HexNumber);
                toReturn.Append(ASCIIEncoding.ASCII.GetChars(new byte[] { val })[0]);

                toReturn.Append(tokens[ctr].Substring(2));
            }

            return toReturn.ToString();
        }

        
        /// <summary>
        /// Encodes request parameters from browser
        /// </summary>
        /// <param name="toDecode"></param>
        /// <returns></returns>
        public static string EncodeRequestParametersForBrowser(string toEncode)
        {
            StringBuilder toReturn = new StringBuilder();

            foreach (char c in toEncode.ToCharArray())
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    toReturn.Append(c);
                else if (c == ' ')
                    toReturn.Append('+');
                else
                {
                    byte asciiVal = ASCIIEncoding.ASCII.GetBytes(new char[] {c})[0];

                    if (asciiVal < 10)
                        toReturn.AppendFormat(
                            "%0{0}", 
                            asciiVal.ToString("X"));
                    else
                        toReturn.AppendFormat(
                            "%{0}",
                            asciiVal.ToString("X"));
                }

            return toReturn.ToString();
        }

        /// <summary>
        /// Encodes the given string so that it is displayed unparsed in the HTML
        /// viewer
        /// </summary>
        /// <param name="toEncode"></param>
        /// <returns></returns>
        public static string EncodeForHTML(string toEncode)
        {
            StringBuilder toReturn = new StringBuilder();

            foreach (char c in toEncode.ToCharArray())
                if (
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    (c == ' '))
                {
                    toReturn.Append(c);
                }
                else
                {
                    byte asciiVal = ASCIIEncoding.ASCII.GetBytes(new char[] { c })[0];

                    toReturn.AppendFormat("&#{0};", asciiVal);
                }

            return toReturn.ToString();
        }

        #region http://refactormycode.com/codes/333-sanitize-html

        private static Regex _tags = new Regex("<[^>]*(>|$)",
    RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
        private static Regex _whitelist = new Regex(@"
    ^</?(b(lockquote)?|code|d(d|t|l|el)|em|h(1|2|3)|i|kbd|li|ol|p(re)?|s(ub|up|trong|trike)?|ul)>$|
    ^<(b|h)r\s?/?>$",
            RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
        private static Regex _whitelist_a = new Regex(@"
    ^<a\s
    href=""(\#\d+|(https?|ftp)://[-a-z0-9+&@#/%?=~_|!:,.;\(\)]+)""
    (\stitle=""[^""<>]+"")?\s?>$|
    ^</a>$",
            RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);
        private static Regex _whitelist_img = new Regex(@"
    ^<img\s
    src=""https?://[-a-z0-9+&@#/%?=~_|!:,.;\(\)]+""
    (\swidth=""\d{1,3}"")?
    (\sheight=""\d{1,3}"")?
    (\salt=""[^""<>]*"")?
    (\stitle=""[^""<>]*"")?
    \s?/?>$",
            RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);


        /// <summary>
        /// sanitize any potentially dangerous tags from the provided raw HTML input using 
        /// a whitelist based approach, leaving the "safe" HTML tags
        /// CODESNIPPET:4100A61A-1711-4366-B0B0-144D1179A937
        /// </summary>
        public static string RemoveMaliciousHTML(string html)
        {
            if (String.IsNullOrEmpty(html)) return html;

            string tagname;
            Match tag;

            // match every HTML tag in the input
            MatchCollection tags = _tags.Matches(html);
            for (int i = tags.Count - 1; i > -1; i--)
            {
                tag = tags[i];
                tagname = tag.Value.ToLowerInvariant();

                if (!(_whitelist.IsMatch(tagname) || _whitelist_a.IsMatch(tagname) || _whitelist_img.IsMatch(tagname)))
                {
                    html = html.Remove(tag.Index, tag.Length);
                    System.Diagnostics.Debug.WriteLine("tag sanitized: " + tagname);
                }
            }

            return html;
        }

        #endregion

        #region from http://refactormycode.com/codes/360-balance-html-tags

        private static Regex _namedtags = new Regex
    (@"</?(?<tagname>\w+)[^>]*(\s|$|>)",
    RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        /// <summary>
        /// attempt to balance HTML tags in the html string
        /// by removing any unmatched opening or closing tags
        /// </summary>
        public static string BalanceTags(string html)
        {
            string tagname = "";
            string tag = "";
            string ignoredtags = "<p<img<br<li";
            int match = 0;

            MatchCollection tags = _namedtags.Matches(html);
            bool[] tagpaired = new bool[tags.Count];

            // loop through matched tags in reverse order
            for (int i = tags.Count - 1; i > -1; i--)
            {
                tagname = tags[i].Groups["tagname"].Value.ToLower();
                tag = tags[i].Value;
                match = -1;

                // skip any tags in our ignore list; assume they're self-closed
                if (!tagpaired[i] && !ignoredtags.Contains("<" + tagname))
                {
                    if (tag.StartsWith("</"))
                    {
                        // if searching backwards, look for opening tags
                        for (int j = i - 1; j > -1; j--)
                        {
                            if (!tagpaired[j] && tags[j].Value.ToLower().StartsWith("<" + tagname))
                            {
                                match = j;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // if searching forwards, look for closing tags
                        for (int j = i + 1; j < tags.Count; j++)
                        {
                            if (!tagpaired[j] && tags[j].Value.ToLower().StartsWith("</" + tagname))
                            {
                                match = j;
                                break;
                            }
                        }
                    }

                    if (match > -1)
                    {
                        // found matching opening/closing tag
                        tagpaired[match] = true;
                        tagpaired[i] = true;
                    }
                    else
                    {
                        // no matching opening/closing tag found -- remove this tag!
                        html = html.Remove(tags[i].Index, tags[i].Length);
                        tagpaired[i] = true;
                        System.Diagnostics.Debug.WriteLine("unbalanced tag removed: " + tags[i]);
                    }
                }
            }

            return html;
        }

        #endregion

        /// <summary>
        /// Sanitizes HTML.  Removes potentially malicious tags and then removes unclosed tags
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string Sanitize(string html)
        {
            return RemoveMaliciousHTML(BalanceTags(html));
        }
    }
}
