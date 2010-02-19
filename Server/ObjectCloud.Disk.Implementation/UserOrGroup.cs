// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Implementation
{
	public abstract class UserOrGroup : IUserOrGroup
	{
        protected UserOrGroup(
            ID<IUserOrGroup, Guid> id,
            string name,
            bool builtIn,
            FileHandlerFactoryLocator fileHandlerFactoryLocator)
        {
            _Id = id;
            _Name = name;
            _BuiltIn = builtIn;
            FileHandlerFactoryLocator = fileHandlerFactoryLocator;
        }

        public FileHandlerFactoryLocator FileHandlerFactoryLocator
        {
            get { return _FileHandlerFactoryLocator; }
            set { _FileHandlerFactoryLocator = value; }
        }
        private FileHandlerFactoryLocator _FileHandlerFactoryLocator;

        public ID<IUserOrGroup, Guid> Id
        {
            get { return _Id; }
        }
        protected ID<IUserOrGroup, Guid> _Id;

        public string Name
        {
            get { return _Name; }
        }
		protected string _Name;
      
        public bool BuiltIn
        {
        	get { return _BuiltIn; }
        }
        protected bool _BuiltIn;

        public abstract string Identity { get; }
	}
}
