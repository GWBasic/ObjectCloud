// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

namespace ObjectCloud.Disk.FileHandlers
{
	public class PersistedBinaryFormatterObject<T> : PersistedObjectBase<T>
	{
		public PersistedBinaryFormatterObject(string path, Func<T> constructor) : base(path, constructor) 
		{
			this.Load();
		}

		/// <summary>
		/// A single binary formatter instanciated onces for quick reuse
		/// </summary>
		private readonly BinaryFormatter binaryFormatter = new BinaryFormatter();
		
		protected override T Deserialize (Stream readStream)
		{
			return (T)this.binaryFormatter.Deserialize(readStream);
		}
		
		protected override void Serialize (Stream writeStream, T persistedObject)
		{
			this.binaryFormatter.Serialize(writeStream, persistedObject);
		}
	}
}