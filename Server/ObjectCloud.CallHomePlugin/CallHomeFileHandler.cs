// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.CallHomePlugin.DataAccess;
using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

using ICallhomeLog_Writable = ObjectCloud.CallHomePlugin.DataAccessBase.ICallhomeLog_Writable;
using IServers_Readable = ObjectCloud.CallHomePlugin.DataAccessBase.IServers_Readable;
using IServers_Writable = ObjectCloud.CallHomePlugin.DataAccessBase.IServers_Writable;

namespace ObjectCloud.CallHomePlugin
{
    public class CallHomeFileHandler : HasDatabaseFileHandler<ObjectCloud.CallHomePlugin.DataAccessBase.IDatabaseConnector, ObjectCloud.CallHomePlugin.DataAccessBase.IDatabaseConnection, ObjectCloud.CallHomePlugin.DataAccessBase.IDatabaseTransaction>
    {
        public CallHomeFileHandler(ObjectCloud.CallHomePlugin.DataAccessBase.IDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(databaseConnector, fileHandlerFactoryLocator)
        {
            ClearRunningObjectCloudInstanceInfoTimer = new Timer(ClearRunningObjectCloudInstanceInfo, null, 3600000, 3600000);
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Cached information about running objectcloud instances
        /// </summary>
        IEnumerable<string> RunningObjectCloudInstances = null;

        /// <summary>
        /// Syncronizes loading RunningObjectCloudInstanceInfo
        /// </summary>
        object RunningObjectCloudInstancesKey = new object();

        /// <summary>
        /// Periodically clear out RunningObjectCloudInstanceInfo so it can be refreshed
        /// </summary>
        /// <param name="state"></param>
        private void ClearRunningObjectCloudInstanceInfo(object state)
        {
            RunningObjectCloudInstances = null;
        }
        Timer ClearRunningObjectCloudInstanceInfoTimer;

        /// <summary>
        /// Marks a server as running
        /// </summary>
        /// <param name="hostname"></param>
        /// <param name="version"></param>
        public void MarkServerAsRunning(string hostname, string version)
        {
            // Get the host ID and either update or insert
            IServers_Readable server = DatabaseConnection.Servers.SelectSingle(Servers_Table.Hostname == hostname);

            long hostID;
            if (null != server)
            {
                hostID = server.HostID;
                DatabaseConnection.Servers.Update(
                    Servers_Table.HostID == hostID,
                    delegate(IServers_Writable serverW)
                    {
                        serverW.LastCheckin = DateTime.UtcNow;
                        serverW.Version = version;
                    });
            }
            else
            {
                hostID = DatabaseConnection.Servers.InsertAndReturnPK<long>(
                    delegate(IServers_Writable serverW)
                    {
                        serverW.Hostname = hostname;
                        serverW.LastCheckin = DateTime.UtcNow;
                        serverW.Version = version;
                    });

                // When inserting, clear the cache
                RunningObjectCloudInstances = null;
            }

            // Log the entry
            DatabaseConnection.CallhomeLog.Insert(delegate(ICallhomeLog_Writable callhomeLog)
            {
                callhomeLog.HostID = hostID;
                callhomeLog.Timestamp = DateTime.UtcNow;
            });
        }

        /// <summary>
        /// Returns information about hosts that have checked in within the most recent 5 - 6 hours
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetRunningObjectCloudInstances()
        {
            IEnumerable<string> toReturn = RunningObjectCloudInstances;

            if (null == RunningObjectCloudInstances)
                lock (RunningObjectCloudInstancesKey)
                    if (null == RunningObjectCloudInstances)
                    {
                        List<string> toReturnBuilder = new List<string>();

                        foreach (IServers_Readable server in DatabaseConnection.Servers.Select(
                            Servers_Table.LastCheckin >= DateTime.UtcNow.Subtract(TimeSpan.FromHours(5))))
                        {
                            toReturnBuilder.Add(server.Hostname);
                        }

                        toReturn = new ReadOnlyCollection<string>(toReturnBuilder);
                        RunningObjectCloudInstances = toReturn;
                    }

            return toReturn;
        }
    }
}
