// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer.UserAgent;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Encapsulates everything about the current web connection
    /// </summary>
    public interface IWebConnection
    {
        /// <summary>
        /// The current session
        /// </summary>
        ISession Session { get; set; }

        /// <summary>
        /// Returns the value of the get argument or throws an exception.  The exception is implementation-dependant
        /// and is intended to fall through to the IWebConnection object
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        string GetArgumentOrException(string argumentName);

        /// <summary>
        /// Returns the value of the post argument or throws an exception.  The exception is implementation-dependant
        /// and is intended to fall through to the IWebConnection object
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        string PostArgumentOrException(string argumentName);

        /// <summary>
        /// Returns the value of the argument or throws an exception.  Get arguments take precidence over Post arguments,
        /// which then take prescidence over cookies.  The exception is implementation-dependant and is intended to fall
        /// through to the IWebConnection object
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        string EitherArgumentOrException(string argumentName);

        /// <summary>
        /// Returns true if the named argument exists as a GET, POST, and/or COOKIE.
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        bool EitherArgumentContains(string argumentName);

        /// <summary>
        /// Returns the value of the cookie argument or throws an exception.  The exception is implementation-dependant
        /// and is intended to fall through to the IWebConnection object
        /// </summary>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        string CookieOrException(string cookieName);

        /// <summary>
        /// The Get Parameters
        /// </summary>
        IDictionary<string, string> GetParameters { get;}

        /// <summary>
        /// The Post Parameters, or null if there aren't any
        /// </summary>
        IDictionary<string, string> PostParameters { get;}

		/// <summary>
        /// The header, indexed by the prefix of each line, in upper case
        /// </summary>
        IDictionary<string, string> Headers { get; }
		
        /// <summary>
        /// The web protocol in use
        /// </summary>
        WebMethod Method { get;}

        /// <summary>
        /// The content type sent, or null if it's not present
        /// </summary>
        string ContentType { get;}

        /// <summary>
        /// The Content sent from the client
        /// </summary>
        IWebConnectionContent Content { get;}

        /// <summary>
        /// Resolves web components in the given string.
        /// </summary>
        /// <param name="toResolve"></param>
        /// <returns></returns>
        string ResolveWebComponents(string toResolve);

        /// <summary>
        /// Generates the contents of the web component
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        string DoWebComponent(string url);

        /// <summary>
        /// Returns a WebConnection that allows for impersonation of the given user
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        IWebConnection CreateShellConnection(IUser user);

        /// <summary>
        /// Returns the results that would occur if the given URL was passed in
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        IWebResults ShellTo(string url);

        /// <summary>
        /// Returns the results that would occur if the given URL was passed in and the specified user was logged in
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        IWebResults ShellTo(string url, IUser user);

        /// <summary>
        /// Returns the results that would occur if the given URL was passed in
        /// </summary>
        /// <param name="url"></param>
        /// <param name="bypassJavascript">Set to true to bypass server-side javascript</param>
        /// <returns></returns>
        IWebResults ShellTo(string url, CallingFrom callingFrom, bool bypassJavascript);

        /// <summary>
        /// Shells to the URL with the given postBody, HTTP method, and calling security level
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <param name="postBody"></param>
        /// <param name="httpVersion"></param>
        /// <param name="callingFrom"></param>
        /// <param name="bypassJavascript">Set to true to bypass server-side javascript</param>
        /// <returns></returns>
        IWebResults ShellTo(
            WebMethod method,
            string url,
            byte[] content,
            string contentType,
            CallingFrom callingFrom,
            bool bypassJavascript);

		/// <summary>
		///The user's permission for the requested object 
		/// </summary>
		FilePermissionEnum? UserPermission { get; }
		
		/// <value>
		/// The host that the user is connecting to.  This can vary if the web server has multiple host names 
		/// </value>
		string RequestedHost { get; }

        /// <summary>
        /// The web server
        /// </summary>
        IWebServer WebServer { get; }

        /// <summary>
        /// Returns true while the WebConnection is considered connected
        /// </summary>
        bool Connected { get; }

        /// <summary>
        /// Replaces [blahblah] with the named user variable
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        string ResolveUserVariables(string url);

        /// <summary>
        /// The incoming MIME message, if sent
        /// </summary>
        MimeReader MimeReader { get; }

        /// <summary>
        /// All of the cookies that the browser sent
        /// </summary>
        CookiesFromBrowser CookiesFromBrowser { get; }

        /// <summary>
        /// The cookies to send to the client
        /// </summary>
        ICollection<CookieToSet> CookiesToSet { get; }

        /// <summary>
        /// The HTTP version sent from the browser
        /// </summary>
        double HttpVersion { get; }

        /// <summary>
        /// The number of times this web connection has been shelled.  If this number is excessively high, it indicates a potential stack overflow problem
        /// </summary>
        uint Generation { get; }

        /// <summary>
        /// Where the call is originating, either from the web or a trusted local source
        /// </summary>
        CallingFrom CallingFrom { get; }

        /// <summary>
        /// Temporally changes CallingFrom while the delegate is called
        /// </summary>
        /// <param name="newCallingFrom"></param>
        /// <param name="toCall"></param>
        void ChangeCallingFrom(CallingFrom newCallingFrom, Action toCall);

        /// <summary>
        /// When set to true, server-side Javascript should be bypassed
        /// </summary>
        bool BypassJavascript { get; }

        /*// <summary>
        /// Temporarily switches the session to tempSession.  Runs the delegate while the session is swapped.
        /// </summary>
        /// <param name="tempSession"></param>
        /// <param name="del"></param>
        void TemporaryChangeSession(ISession tempSession, Action del);*/
		
		/// <value>
		/// The remote endpoint, such as an IPv4 or IPv6 address 
		/// </value>
		EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Sends the results to the client.  Be careful, calling this multiple times can have undesired results!
        /// </summary>
        /// <exception cref="ResultsAlreadySent">Thrown if results have already been sent</exception>
        void SendResults(IWebResults webResults);

        /// <summary>
        /// All of the files that were touched while handling this request.  This is helpful for assisting with cache-control
        /// </summary>
        HashSet<IFileContainer> TouchedFiles { get; }

        /// <summary>
        /// Generates the results that are returned to the client
        /// </summary>
        /// <returns></returns>
        IWebResults GenerateResultsForClient();

        /// <summary>
        /// All of the scripts used with the web connection; check this set before adding another script tag
        /// </summary>
        HashSet<string> Scripts { get; }

        /// <summary>
        /// Returns a url that contains the correct browser cache ID for the given URL
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        string GetBrowserCacheUrl(string url);

        /// <summary>
        /// Object that contains information about the user's browser
        /// See the ObjectCloud.Interfaces.WebServer.UserAgent namespace for different classes
        /// </summary>
        IBrowser UserAgent { get; }
    }
}
