// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;

namespace ObjectCloud.Interfaces.Disk
{
	public class CanNotDeleteBuiltInUserOrGroup : SecurityException
	{
		public CanNotDeleteBuiltInUserOrGroup()
			: base("Built-in users and groups can not be deleted") {}
	}
}
