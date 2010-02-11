// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk.FileHandlers.Particle
{
    /// <summary>
    /// Holds the Particle endpoints for an openID
    /// </summary>
    public class Endpoints
    {
        static Endpoints()
        {
            Cache = new Cache<string, Endpoints>(LoadEndpoints);
        }

        /// <summary>
        /// Constructs an endpoints object from the given HTML
        /// </summary>
        /// <param name="html"></param>
        private Endpoints(string openId, string html)
        {
            _OpenId = openId;

            // TODO:  This parser is awful, rewrite it!

            string[] linkTags = html.Split(new string[] { "<link " }, StringSplitOptions.RemoveEmptyEntries);

            for (int ctr = 0; ctr < linkTags.Length; ctr++)
            {
                // Get the link tag
                string linkTag = linkTags[ctr].Split('>')[0];

                if (linkTag.Contains("rel=\"particle."))
                    if (linkTag.Contains("href=\""))
                    {
                        // This link tag is for particle...

                        // Get the specific action...
                        string[] endpointPrefix = linkTag.Split(new string[] { "rel=\"particle." }, StringSplitOptions.RemoveEmptyEntries);
                        string endpoint = endpointPrefix[endpointPrefix.Length - 1].Split('"')[0];

                        // Get the specific href...
                        string[] hrefPrefix = linkTag.Split(new string[] { "href=\"" }, StringSplitOptions.RemoveEmptyEntries);
                        string href = hrefPrefix[hrefPrefix.Length - 1].Split('"')[0];

                        KnownEndpoints[endpoint] = href;
                    }
            }
        }

        /// <summary>
        /// The OpenId that the endpoints are for
        /// </summary>
        public string OpenId
        {
            get { return _OpenId; }
        }
        private readonly string _OpenId;

        /// <summary>
        /// When the endpoints were loaded
        /// </summary>
        public DateTime Loaded
        {
            get { return _Loaded; }
        }
        private readonly DateTime _Loaded = DateTime.UtcNow;

        /// <summary>
        /// The known endpoints
        /// </summary>
        private readonly Dictionary<string, string> KnownEndpoints = new Dictionary<string,string>();

        /// <summary>
        /// Returns the url for the endpoint, or null if it doesn't exist
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public string this[string endpoint]
        {
            get
            {
                string toReturn;
                
                if (KnownEndpoints.TryGetValue(endpoint, out toReturn))
                    return toReturn;
                else
                    throw new UnknownEndpoint(endpoint + " isn't a valid endpoint");
            }
        }

        /// <summary>
        /// Thrown if an endpoint is unknown
        /// </summary>
        public class UnknownEndpoint : ParticleException
        {
            public UnknownEndpoint(string message) : base(message) { }
        }

        /// <summary>
        /// Returns true if the endpoint exists
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public bool ContainsEndpoint(string endpoint)
        {
            return KnownEndpoints.ContainsKey(endpoint);
        }

        private static readonly Cache<string, Endpoints> Cache;

        private static Endpoints LoadEndpoints(string openId)
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler response = httpWebClient.Get(openId);

            // If there was an error
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception();

            return new Endpoints(openId, response.AsString());
        }

        /// <summary>
        /// Gets the endpoints for the given OpenID.  Endpoints are garanteed to be no older then 6 hours
        /// </summary>
        /// <param name="opendId"></param>
        /// <returns></returns>
        public static Endpoints GetEndpoints(string openId)
        {
            return GetEndpoints(openId, false);
        }

        /// <summary>
        /// Gets the endpoints for the given OpenID.  Endpoints are garanteed to be no older then 6 hours
        /// </summary>
        /// <param name="opendId"></param>
        /// <param name="forceRefresh">Set to true to force refreshing the openId</param>
        /// <returns></returns>
        public static Endpoints GetEndpoints(string openId, bool forceRefresh)
        {
            if (forceRefresh)
            {
                Cache.Remove(openId);
                return Cache[openId];
            }

            Endpoints toReturn = Cache[openId];

            // make sure the endpoints aren't stale
            using (TimedLock.Lock(Cache))
            {
                if (toReturn.Loaded.AddHours(6) > DateTime.UtcNow)
                    // endpoint isn't stale
                    return toReturn;
                else
                    // cache is stale
                    Cache.Remove(openId);
            }

            return Cache[openId];
        }

        /// <summary>
        /// Thrown when there is an error getting an OpenId
        /// </summary>
        public class Exception : DiskException
        {
            public Exception() : base("Could not load OpenID particle endpoints") { }
        }
    }
}
