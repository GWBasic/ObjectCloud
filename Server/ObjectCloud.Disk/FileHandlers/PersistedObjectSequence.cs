using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk
{
	/// <summary>
	/// Allows for persisting an ordered sequence of objects. Objects can be read in reverse order by specifying a date
	/// </summary>
	public class PersistedObjectSequence<T>
	{
		public PersistedObjectSequence(string path, FileHandlerFactoryLocator fileHandlerFactoryLocator)
		{
			this.path = path;
			this.currentWriteStreamFilename = Path.Combine(this.path, "newest");
			
			fileHandlerFactoryLocator.FileSystemResolver.Stopping += this.CloseAllOpenSequences;
			
			long lastSuccess = 0;
			
			// Read in all existing objects
			this.currentWriteStream = File.Open(this.currentWriteStreamFilename, FileMode.OpenOrCreate);
			
			try
			{
				while (lastSuccess < this.currentWriteStream.Length)
				{
					object deserialized = this.binaryFormatter.Deserialize(this.currentWriteStream);
					if (deserialized is Event)
						this.newestObjects.Add((Event)deserialized);
					
					lastSuccess = this.currentWriteStream.Position;
				}
			}
			catch (Exception e)
			{
				// No good place to log, considering that this is supposed to be for the log
				Console.Error.WriteLine("Exception reading {0}: {1}", this.currentWriteStreamFilename, e.ToString());
			}
			
			this.currentWriteStream.Position = lastSuccess;
		}

		void CloseAllOpenSequences (IFileSystemResolver sender, EventArgs e)
		{
			if (null != currentWriteStream)
			{
				this.currentWriteStream.Flush();
				this.currentWriteStream.Close();
				this.currentWriteStream.Dispose();
				
				this.currentWriteStream = null;
			}
		}
		
		/// <summary>
		/// The folder on disk where the persisted sequence is stored.
		/// </summary>
		private readonly string path;
		
		/// <summary>
		/// A single binary formatter instanciated onces for quick reuse
		/// </summary>
		private readonly BinaryFormatter binaryFormatter = new BinaryFormatter();
		
		/// <summary>
		/// The current write stream's filename.
		/// </summary>
		private readonly string currentWriteStreamFilename;
		
		/// <summary>
		/// The current write stream.
		/// </summary>
		private FileStream currentWriteStream;

		/// <summary>
		/// The newest objects, in oldest-first order. These are what have been written to the current write stream.
		/// </summary>
		private List<Event> newestObjects = new List<Event>();
		
		/// <summary>
		/// Append the specified item to the sequence
		/// </summary>
		/// <param name='item'>
		/// Item.
		/// </param>
		public void Append(T item)
		{
			var ev = new Event(item);
			
			this.binaryFormatter.Serialize(
				this.currentWriteStream,
				ev);
			
			this.newestObjects.Add(ev);
		}
		
		/// <summary>
		/// Encapsulates an object that's written to the stream
		/// </summary>
		[Serializable]
		public class Event
		{
			public Event(T item)
			{
				this.item = item;
				this.dateTime = DateTime.UtcNow;
			}

			public DateTime DateTime 
			{
				get { return this.dateTime; }
			}
			private readonly DateTime dateTime;

			public T Item 
			{
				get { return this.item; }
			}
			private readonly T item;
		}
	}
}

