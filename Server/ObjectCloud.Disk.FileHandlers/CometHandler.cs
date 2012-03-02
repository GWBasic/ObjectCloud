// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Javascript;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.FileHandlers
{
    public class CometHandler : DirectoryHandler, ICometHandler
    {
        public CometHandler(
            IDatabaseConnector databaseConnector,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            GenericArgument<ICometSession> callOnNewSession)
            : base(databaseConnector, fileHandlerFactoryLocator)
        {
            CallOnNewSession = callOnNewSession;
        }

        /// <summary>
        /// Delegate that's called on a new session
        /// </summary>
        GenericArgument<ICometSession> CallOnNewSession;

        public ICometSession CreateNewSession()
        {
            ID<ICometSession, ushort> sessionId;
            int tries = 0;

            do
            {
                sessionId = new ID<ICometSession, ushort>(SRandom.Next<ushort>());
                tries++;

                if (tries > 10000)
                    throw new TooManyCometSessions();
            }
            while (CometSessions.ContainsKey(sessionId));

            CometSession toReturn = new CometSession(sessionId);
            CometSessions[sessionId] = toReturn;

            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                CallOnNewSession((ICometSession)state);
            }, toReturn);

            return toReturn;
        }

        ICometSession ICometHandler.this[ID<ICometSession, ushort> sessionId]
        {
            get { return this[sessionId]; }
        }
        public CometSession this[ID<ICometSession, ushort> sessionId]
        {
            get 
            {
                CometSession toReturn;
                if (CometSessions.TryGetValue(sessionId, out toReturn))
                    return toReturn;

                throw new BadCometSessionId(sessionId);
            }
        }

        public void Close(ID<ICometSession, ushort> sessionId)
        {
            CometSession cometSession = this[sessionId];
            CometSessions.Remove(sessionId);

            cometSession.Close();
        }

        /// <summary>
        /// Dictionary of comet sessions
        /// </summary>
        private Dictionary<ID<ICometSession, ushort>, CometSession> CometSessions = new Dictionary<ID<ICometSession,ushort>,CometSession>();

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }
    }
}
