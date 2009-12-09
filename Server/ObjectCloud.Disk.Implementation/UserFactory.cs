// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Implementation
{
    public class UserFactory : IUserFactory
    {
        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        /// <summary>
        /// The user handler associated with the anonymous user
        /// </summary>
        public IUserHandler AnonymousUserHandler
        {
            get
            {
                if (null == _AnonymousUserHandler)
                {
                    _AnonymousUserHandler =
                        FileHandlerFactoryLocator.FileSystemResolver.ResolveFile("Users/anonymous.user").CastFileHandler<IUserHandler>();
                }

                return _AnonymousUserHandler;
            }
        }
        private IUserHandler _AnonymousUserHandler = null;

		public IUser RootUser 
		{
        	get	{ return _RootUser; }
		}
		private IUser _RootUser = null;
		
        /// <summary>
        /// The user associated with the anonymous user
        /// </summary>
        public IUser AnonymousUser
        {
            get { return _AnonymousUser; }
        }        
        private static IUser _AnonymousUser = null;
				
        public IGroup Everybody
		{
        	get { return _Everybody; }
		}
        private IGroup _Everybody = null;
        
        public IGroup AuthenticatedUsers 
		{
        	get	{ return _AuthenticatedUsers; }
		}
        private IGroup _AuthenticatedUsers = null;
		
        public IGroup LocalUsers
		{
        	get { return _LocalUsers; }
		}
        private IGroup _LocalUsers = null;
        
        public IGroup Administrators
		{
        	get { return _Administrators; }
		}		private IGroup _Administrators = null;
		
		private IList<ID<IUserOrGroup, Guid>> SystemUserOrGroupIds
		{
			get
			{
				if (null == _SystemUserOrGroupIds)
				{
					// The system user and group IDs are loaded through reflection
					// If the user or group is contained within a property of this object, it is considered system.
											
					_SystemUserOrGroupIds = new List<ID<IUserOrGroup, Guid>>();
					
					foreach (PropertyInfo property in typeof(UserFactory).GetProperties())
					{
						object o = property.GetValue(this, null);
						
						if (o is IUserOrGroup)
							_SystemUserOrGroupIds.Add(((IUserOrGroup)o).Id);
					}
				}
				
				return _SystemUserOrGroupIds;
			}
        }
		private IList<ID<IUserOrGroup, Guid>> _SystemUserOrGroupIds = null;
		
        public bool IsSystemUserOrGroup (ID<IUserOrGroup, Guid> userOrGroupId)
        {
        	return SystemUserOrGroupIds.Contains(userOrGroupId);
        }  
    }
}
