// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.FileHandlers.Particle
{
    /// <summary>
    /// Holds the Particle endpoints for an openID
    /// </summary>
    public class Endpoints : IEndpoints
    {
        private static ILog log = LogManager.GetLogger<Endpoints>();
		
		static Endpoints()
		{
			// Work around a compiler issue
			if (null == CleanEndpointsTimer)
				CleanEndpointsTimer = null;
		}

        /// <summary>
        /// Every hour remove old endpoints
        /// </summary>
        private static Timer CleanEndpointsTimer = new Timer(CleanOldEndpoints, null, 3600000, 3600000);

        /// <summary>
        /// Removes old endpoints on an hourly basis
        /// </summary>
        /// <param name="state"></param>
        private static void CleanOldEndpoints(object state)
        {
            CacheLock.EnterWriteLock();

            try
            {
                foreach (string user in Enumerable<string>.FastCopy(Cache.Keys))
                {
                    Endpoints endpoints = Cache[user];

                    if (endpoints.Loaded.AddHours(24) < DateTime.UtcNow)
                        Cache.Remove(user);
                }
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }
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
                        string endpointString = endpointPrefix[endpointPrefix.Length - 1].Split('"')[0];

                        ParticleEndpoint endpoint;
                        if (Enum<ParticleEndpoint>.TryParseCaseInsensitive(endpointString, out endpoint))
                        {
                            // Get the specific href...
                            string[] hrefPrefix = linkTag.Split(new string[] { "href=\"" }, StringSplitOptions.RemoveEmptyEntries);
                            string href = hrefPrefix[hrefPrefix.Length - 1].Split('"')[0];

                            KnownEndpoints[endpoint] = href;
                        }
                    }
            }
        }

        /// <summary>
        /// The OpenId that the endpoints are for
        /// </summary>
        public string OpenIdOrWebFinger
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
        private readonly Dictionary<ParticleEndpoint, string> KnownEndpoints = new Dictionary<ParticleEndpoint, string>();

        /// <summary>
        /// Returns the url for the endpoint, or null if it doesn't exist
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public string this[ParticleEndpoint endpoint]
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
        /// Returns true if the endpoint exists
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public bool ContainsEndpoint(ParticleEndpoint endpoint)
        {
            return KnownEndpoints.ContainsKey(endpoint);
        }

        /// <summary>
        /// Returns true if the endpoint exists
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public bool TryGetEndpoint(ParticleEndpoint endpoint, out string endpointString)
        {
            return KnownEndpoints.TryGetValue(endpoint, out endpointString);
        }

        /// <summary>
        /// Cache of loaded endpoints
        /// </summary>
        private static readonly Dictionary<string, Endpoints> Cache = new Dictionary<string,Endpoints>();

        /// <summary>
        /// Synchronizes access to the cache
        /// </summary>
        private static readonly ReaderWriterLockSlim CacheLock = new ReaderWriterLockSlim();

        private static void LoadEndpoints(
            string name,
            Action<Endpoints> callback,
            Action<Exception> errorCallback)
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            Action<HttpResponseHandler> responseHandler = delegate(HttpResponseHandler response)
            {
                // If there was an error
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Endpoints endpoints = new Endpoints(name, response.AsString());

                    CacheLock.EnterWriteLock();

                    try
                    {
                        Cache[name] = endpoints;
                    }
                    finally
                    {
                        CacheLock.ExitWriteLock();
                    }

                    callback(endpoints);
                }
                else
                {
                    log.Error("Error loading endpoints:\n" + response.AsString());
                    errorCallback(new DiskException("Error loading endpoint for " + name));
                }
            };

            httpWebClient.BeginGet(name, responseHandler, errorCallback);
        }

        /// <summary>
        /// Gets the endpoints for the given OpenID.  Endpoints are garanteed to be no older then 24 hours
        /// </summary>
        /// <param name="opendId"></param>
        /// <param name="forceRefresh">Set to true to force refreshing the openId</param>
        /// <returns></returns>
        public static void GetEndpoints(
            string name,
            bool forceRefresh,
            Action<Endpoints> callback,
            Action<Exception> errorCallback)
        {
            if (forceRefresh)
            {
                LoadEndpoints(name, callback, errorCallback);
                return;
            }


            // Try to get endpoints from the cache

            CacheLock.EnterReadLock();

            try
            {
                Endpoints endpoints;
                if (Cache.TryGetValue(name, out endpoints))
                {
                    callback(endpoints);
                    return;
                }
            }
            finally
            {
                CacheLock.ExitReadLock();
            }

            // No cached endpoints, load exclusively
            LoadEndpoints(name, callback, errorCallback);
        }
    }


    /// <summary>
    /// Holds the Particle endpoints for an openID
    /// </summary>
    public class OLD_Endpoints
    {
        static OLD_Endpoints()
        {
            Cache = new Cache<string, OLD_Endpoints>(LoadEndpoints);
        }

        /// <summary>
        /// Constructs an endpoints object from the given HTML
        /// </summary>
        /// <param name="html"></param>
        private OLD_Endpoints(string openId, string html)
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

        private static readonly Cache<string, OLD_Endpoints> Cache;

        private static OLD_Endpoints LoadEndpoints(string openId)
        {
            HttpWebClient httpWebClient = new HttpWebClient();

            HttpResponseHandler response = httpWebClient.Get(openId);

            // If there was an error
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception();

            return new OLD_Endpoints(openId, response.AsString());
        }

        /// <summary>
        /// Gets the endpoints for the given OpenID.  Endpoints are garanteed to be no older then 6 hours
        /// </summary>
        /// <param name="opendId"></param>
        /// <returns></returns>
        public static OLD_Endpoints GetEndpoints(string openId)
        {
            return GetEndpoints(openId, false);
        }

        /// <summary>
        /// Gets the endpoints for the given OpenID.  Endpoints are garanteed to be no older then 6 hours
        /// </summary>
        /// <param name="opendId"></param>
        /// <param name="forceRefresh">Set to true to force refreshing the openId</param>
        /// <returns></returns>
        public static OLD_Endpoints GetEndpoints(string openId, bool forceRefresh)
        {
            if (forceRefresh)
            {
                Cache.Remove(openId);
                return Cache[openId];
            }

            OLD_Endpoints toReturn = Cache[openId];

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
