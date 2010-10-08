// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    public interface IEndpoints
    {
        /// <summary>
        /// Returns the named endpoint
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        /// <exception cref="UnknownEndpoint"></exception>
        string this[ParticleEndpoint endpoint] { get; }

        /// <summary>
        /// Returns true if the specified endpoint is present
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        bool ContainsEndpoint(ParticleEndpoint endpoint);

        /// <summary>
        /// Returns true if the specified endpoint is present
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        bool TryGetEndpoint(ParticleEndpoint endpoint, out string endpointString);
    }

    /// <summary>
    /// Thrown if an endpoint is unknown
    /// </summary>
    public class UnknownEndpoint : Exception
    {
        public UnknownEndpoint(string message) : base(message) { }
    }
}
