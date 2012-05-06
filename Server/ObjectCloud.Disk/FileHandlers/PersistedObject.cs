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
	public class PersistedObject<T> : PersistedObjectBase<T>
	{
		public PersistedObject(string path, Func<Stream, T> deserializeCallback, Action<Stream, T> serializeCallback)
			: base(path)
		{
			this.deserializeCallback = deserializeCallback;
			this.serializeCallback = serializeCallback;
		}
		
		public PersistedObject(string path, Func<T> constructor, Func<Stream, T> deserializeCallback, Action<Stream, T> serializeCallback)
			: base(path, constructor)
		{
			this.deserializeCallback = deserializeCallback;
			this.serializeCallback = serializeCallback;
		}
		
		public PersistedObject(string path, T persistedObject, Func<Stream, T> deserializeCallback, Action<Stream, T> serializeCallback)
			: base(path, persistedObject)
		{
			this.deserializeCallback = deserializeCallback;
			this.serializeCallback = serializeCallback;
		}
		
		/// <summary>
		/// Callback to deserialize the object
		/// </summary>
		private readonly Func<Stream, T> deserializeCallback;
		
		/// <summary>
		/// Callback to serialize the object
		/// </summary>
		private readonly Action<Stream, T> serializeCallback;
		
		protected override T Deserialize (Stream readStream)
		{
			return this.deserializeCallback(readStream);
		}
		
		protected override void Serialize (Stream writeStream, T persistedObject)
		{
			this.serializeCallback(writeStream, persistedObject);
		}
	}
}