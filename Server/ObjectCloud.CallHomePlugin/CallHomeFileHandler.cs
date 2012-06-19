// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.CallHomePlugin
{
    public class CallHomeFileHandler : FileHandler
    {
		[Serializable]
		public class Server
		{
			public DateTime lastCheckin;
			public string version;
		}
		
        public CallHomeFileHandler(PersistedObjectBase<Dictionary<string, Server>> persistedServers, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(fileHandlerFactoryLocator)
        {
			this.persistedServers = persistedServers;
        }
		
		PersistedObjectBase<Dictionary<string, Server>> persistedServers;

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Marks a server as running
        /// </summary>
        /// <param name="hostname"></param>
        /// <param name="version"></param>
        public void MarkServerAsRunning(string hostname, string version)
        {
			this.persistedServers.WriteEventual(servers =>
            {
				servers[hostname] = new Server()
				{
					lastCheckin = DateTime.UtcNow,
					version = version
				};
			});
        }
		
        /// <summary>
        /// The oldest checkin to be considered running
        /// </summary>
		private static readonly TimeSpan maxLastCheckinAge = TimeSpan.FromHours(5);

        /// <summary>
        /// Returns information about hosts that have checked in within the most recent 5 - 6 hours
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetRunningObjectCloudInstances()
        {
			return this.persistedServers.Read(servers =>
			{
                List<string> toReturn = new List<string>(servers.Count);

				foreach (var serverAndHostname in servers)
				{
					var server = serverAndHostname.Value;
					
					if (server.lastCheckin + CallHomeFileHandler.maxLastCheckinAge >= DateTime.UtcNow)
					{
						var hostname = serverAndHostname.Key;
						toReturn.Add(hostname);
					}
				}
				
				return toReturn;
			});
        }
    }
}
