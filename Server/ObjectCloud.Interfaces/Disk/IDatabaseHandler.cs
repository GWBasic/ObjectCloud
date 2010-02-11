// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Data.Common;

using ObjectCloud.Interfaces.Database;

namespace ObjectCloud.Interfaces.Disk
{
	public interface IDatabaseHandler : IFileHandler
	{
		/// <summary>
		/// Creates a connection to the database
		/// </summary>
		/// <returns>
		/// A <see cref="DbConnection"/>
		/// </returns>	
        DbConnection Connection { get; }
		
		/// <value>
		/// The version of the database schema, or null if unknown
		/// </value>
		double? Version { get; set; }
	}
}
