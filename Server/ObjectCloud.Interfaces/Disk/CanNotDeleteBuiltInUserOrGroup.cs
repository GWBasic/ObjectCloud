// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
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
