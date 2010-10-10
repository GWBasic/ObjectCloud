// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Interfaces.Security
{
    /// <summary>
    /// Allows for temporary impersonation of another user
    /// </summary>
    internal class ShellSession : ISession
    {
        internal ShellSession(ISession parentSession, IUser user)
        {
            _ParentSession = parentSession;
            _User = user;
        }

        /// <summary>
        /// The shell session's parent session
        /// </summary>
        public ISession ParentSession
        {
            get { return _ParentSession; }
            set { _ParentSession = value; }
        }
        private ISession _ParentSession;

        /// <summary>
        /// The shell session's user
        /// </summary>
        public IUser User
        {
            get { return _User; }
            set { _User = value; }
        }
        private IUser _User;

        public ID<ISession, Guid> SessionId
        {
            get { return _ParentSession.SessionId; }
        }

        public void Logout()
        {
            throw new SecurityException("Logout not supported in a shell session");
        }

        public void Login(IUser user)
        {
            throw new SecurityException("Login not supported in a shell session");
        }

        public TimeSpan MaxAge
        {
            get { return _ParentSession.MaxAge; }
            set
            {
                throw new SecurityException("Setting MaxAge not supported in a shell session");
            }
        }

        public DateTime LastQuery
        {
            get { return _ParentSession.LastQuery; }
        }

        public bool KeepAlive
        {
            get { return _ParentSession.KeepAlive; }
            set
            {
                throw new NotImplementedException();
            }
        }

        public HashSet<IFileContainer> GetFilesTouchedForUrl(string url)
        {
            throw new SecurityException("TouchedFilesForUrl not supported in a shell session");
        }

        public void SetFilesTouchedForUrl(string url, HashSet<Disk.IFileContainer> touchedFiles)
        {
            throw new SecurityException("TouchedFilesForUrl not supported in a shell session");
        }

		void ISession.RegisterCometTransport (ObjectCloud.Interfaces.WebServer.ICometTransport cometTransport)
        {
        }
        
        public int MaxCometTransports
		{
        		get { return _MaxCometTransports; }
			set { _MaxCometTransports = value; }
        }
        int _MaxCometTransports = int.MaxValue;

        public HttpWebClient HttpWebClient
        {
            get { return ParentSession.HttpWebClient; }
        }
    }
}
