// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation
{
    public class User : UserOrGroup, IUser
    {
        public User(
            ID<IUserOrGroup, Guid> id,
            string name,
            bool builtIn,
            bool local,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            string displayName,
            IIdentityProvider identityProvider)
            : base(id, name, builtIn, fileHandlerFactoryLocator, displayName) 
        {
            _Local = local;
            _IdentityProvider = identityProvider;
        }

		/// <summary>
		/// Helper to allow built-in users to be declared in Spring
		/// </summary>
		/// <param name="id">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="name">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="User"/>
		/// </returns>
        public static User SpringContructor(string id, string name, FileHandlerFactoryLocator fileHandlerFactoryLocator)
		{
			return new User(new ID<IUserOrGroup, Guid>(new Guid(id)), name, true, true, fileHandlerFactoryLocator, name, LocalIdentityProvider.Instance);
		}

        public override string Identity
        {
            get { return Url; }
        }

        public override string Url
        {
            get
            {
                if (Name.StartsWith("http://") || Name.StartsWith("https://"))
                    return Name;

                return string.Format(
                    "http://{0}/Users/{1}.user",
                    FileHandlerFactoryLocator.HostnameAndPort,
                    Name);
            }
        }

        public override string AvatarUrl
        {
            get { return Url + "?Method=GetAvatar"; }
        }

		public IUserHandler UserHandler 
		{
        	get 
			{
                if (null == _UserHandler)
                {
                    string filename = "/Users/" + Name + ".user";

                    // In some rare cases the UserHandler might not be available
                    if (FileHandlerFactoryLocator.FileSystemResolver.IsFilePresent(filename))
                        _UserHandler = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(
                            filename).CastFileHandler<IUserHandler>();
                }

				return _UserHandler;
        	}
		}
		private IUserHandler _UserHandler = null;

        public bool Local
        {
            get { return _Local; }
        }
        private readonly bool _Local;

        public override string ToString()
        {
            return Identity;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public IIdentityProvider IdentityProvider
        {
            get { return _IdentityProvider; }
        }
        private IIdentityProvider _IdentityProvider;
    }
}
