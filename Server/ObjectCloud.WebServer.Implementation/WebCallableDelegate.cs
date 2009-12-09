using System;
using System.Collections.Generic;
using System.Text;

namespace WebServerClasses
{
    /// <summary>
    /// Delegate that all web-callable functions must match
    /// </summary>
    /// <param name="cookiesFromClient"></param>
    /// <param name="cookiesToSend"></param>
    /// <param name="getParameters"></param>
    /// <param name="postParameters"></param>
    /// <returns></returns>
    public delegate WebResultsBase WebCallableDelegate(
        Cookies cookiesFromClient,
        Cookies cookiesToSend,
        RequestParameters getParameters,
        RequestParameters postParameters);
}
