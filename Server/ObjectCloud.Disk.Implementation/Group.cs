// Copyright 2009 - 2012 Andrew Rondeau
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
	public class Group : UserOrGroup, IGroup
	{
		public Group(ID<IUserOrGroup, Guid>? ownerId,
			ID<IUserOrGroup, Guid> id,
		    string name,
		    bool builtIn,
            bool automatic,
            GroupType type,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            string displayName)
            : base(id, name, builtIn, fileHandlerFactoryLocator, displayName)
		{
			_OwnerId = ownerId;
			_Id = id;
			_Name = name;
			_Automatic = automatic;
            _Type = type;
		}
		
		public Group(ID<IUserOrGroup, Guid> id,
		    string name,
		    bool builtIn,
            bool automatic,
            GroupType type,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            string displayName)
            : base(id, name, builtIn, fileHandlerFactoryLocator, displayName)
		{
			_Id = id;
			_Name = name;
			_Automatic = automatic;
            _Type = type;
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
        public static Group SpringContructor(string id, string name, bool automatic, FileHandlerFactoryLocator fileHandlerFactoryLocator)
		{
			return new Group(new ID<IUserOrGroup, Guid>(new Guid(id)), name, true, automatic, GroupType.Private, fileHandlerFactoryLocator, name);
		}

		public ID<IUserOrGroup, Guid>? OwnerId 
		{
			get { return _OwnerId; }
		}
		private ID<IUserOrGroup, Guid>? _OwnerId;

		public bool Automatic 
		{
			get { return _Automatic; }
		}
		private bool _Automatic;

        public GroupType Type
        {
            get { return _Type; }
        }
        private readonly GroupType _Type;

        public override string Url
        {
            get
            {
                return string.Format(
                    "http://{0}/Users/{1}.group",
                    FileHandlerFactoryLocator.HostnameAndPort,
                    Name);
            }
        }

        public override string Identity
        {
            get { return Url; }
        }

        public override string AvatarUrl
        {
            get { return Url + "?Method=GetAvatar"; }
        }
    }

    /// <summary>
    /// Container for a group and alias
    /// </summary>
    public class GroupAndAlias : Group, IGroupAndAlias
    {
        public GroupAndAlias(ID<IUserOrGroup, Guid>? ownerId,
            ID<IUserOrGroup, Guid> id,
            string name,
            bool builtIn,
            bool automatic,
            GroupType type,
            string alias,
            FileHandlerFactoryLocator fileHandlerFactoryLocator,
            string displayName)
            : base(ownerId, id, name, builtIn, automatic, type, fileHandlerFactoryLocator, displayName)
        {
            _Alias = alias;
        }

        public string Alias
        {
            get { return _Alias; }
        }
        private readonly string _Alias;
    }
}
