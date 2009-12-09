// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
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
            FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(id, name, builtIn, fileHandlerFactoryLocator) { }

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
			return new User(new ID<IUserOrGroup, Guid>(new Guid(id)), name, true, fileHandlerFactoryLocator);
		}

        public string Identity
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

        public override string ToString()
        {
            return Identity;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
