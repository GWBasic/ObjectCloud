// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

using ObjectCloud.Disk.FileHandlers;

namespace ObjectCloud.Disk.Test
{
	public class PersistedBinaryFormatterObject<T> : PersistedObject<T>
	{
		public PersistedBinaryFormatterObject(string path, Func<T> constructor) : 
			base(
				path,
				constructor,
				PersistedBinaryFormatterObject<T>.Deserialize,
				PersistedBinaryFormatterObject<T>.Serialize) 
		{
			this.Load();
		}

		/// <summary>
		/// A single binary formatter instanciated onces for quick reuse
		/// </summary>
		private static readonly BinaryFormatter binaryFormatter = new BinaryFormatter();
		
		private static T Deserialize (Stream readStream)
		{
			lock (PersistedBinaryFormatterObject<T>.binaryFormatter)
				return (T)PersistedBinaryFormatterObject<T>.binaryFormatter.Deserialize(readStream);
		}
		
		private static void Serialize (Stream writeStream, T persistedObject)
		{
			lock (PersistedBinaryFormatterObject<T>.binaryFormatter)
				PersistedBinaryFormatterObject<T>.binaryFormatter.Serialize(writeStream, persistedObject);
		}
	}
}