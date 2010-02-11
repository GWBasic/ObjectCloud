// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Xml;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.Directory;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class TestCometHandlerFactory : CometHandlerFactory
    {
        private void RunTestSession(ICometSession cometSession)
        {
            TestSession testSession = new TestSession();
            testSession.CometSession = cometSession;
            cometSession.DataRecieved += new EventHandler<ICometSession, EventArgs<string>>(testSession.CometSession_DataRecieved);

            Thread thread = new Thread(testSession.HandleCometSession);
            thread.Start();
            thread.IsBackground = true;
        }

        private class TestSession
        {
            public ICometSession CometSession;

            string recieved = "";

            public void HandleCometSession()
            {
                CometSession.EnqueueToSend("Packet 1\r\n");
                Thread.Sleep(3000);
                CometSession.EnqueueToSend("Packet 2\r\n");
                Thread.Sleep(300);
                CometSession.EnqueueToSend("Packet 3\r\n");
                Thread.Sleep(30);
                CometSession.EnqueueToSend("Packet 4\r\n");
                Thread.Sleep(3);
                CometSession.EnqueueToSend("Packet 5\r\n");
                Thread.Sleep(3000);
                CometSession.EnqueueToSend("Packet 6\r\n");
                Thread.Sleep(300);
                CometSession.EnqueueToSend("Packet 7\r\n");
                Thread.Sleep(30);
                CometSession.EnqueueToSend("Packet 8\r\n");
                Thread.Sleep(3);

                for (int ctr = 0; ctr < 5; ctr++)
                {
                    Thread.Sleep(SRandom.Next(2000));
                    CometSession.EnqueueToSend(DateTime.Now.ToLongTimeString() + "\r\n");
                }
            }

            public void CometSession_DataRecieved(ICometSession sender, EventArgs<string> e)
            {
                recieved += e.Value;

                CometSession.EnqueueToSend("\r\n\r\nEverything you've sent me is: " + recieved + "\r\n\r\n");
            }
        }

        public override GenericArgument<ICometSession> CallOnNewSession
        {
            get { return RunTestSession; }
        }
    }
}
