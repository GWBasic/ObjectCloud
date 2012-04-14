using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk
{
	/// <summary>
	/// Allows for persisting an ordered sequence of objects. Objects can be read in reverse order by specifying a date
	/// </summary>
	public class PersistedObjectSequence<T> : IDisposable
	{
		public PersistedObjectSequence(string path, long maxChunkSize, long maxSize, FileHandlerFactoryLocator fileHandlerFactoryLocator)
		{
			this.fileHandlerFactoryLocator = fileHandlerFactoryLocator;
			this.path = path;
			this.maxChunkSize = maxChunkSize;
			this.maxSize = maxSize;
			this.currentWriteStreamFilename = Path.Combine(this.path, "newest");
			
			this.fileHandlerFactoryLocator.FileSystemResolver.Stopping += HandleFileHandlerFactoryLocatorFileSystemResolverStopping;
			
			long lastSuccess = 0;
			
			// Read in all existing objects
			this.currentWriteStream = File.Open(this.currentWriteStreamFilename, FileMode.OpenOrCreate);
			
			try
			{
				while (lastSuccess < this.currentWriteStream.Length)
				{
					var deserialized = this.binaryFormatter.Deserialize(this.currentWriteStream);
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
			
			this.CreateNewChunkIfNeeded();
		}
		
		private readonly FileHandlerFactoryLocator fileHandlerFactoryLocator;
		
		public void Dispose()
		{
			this.HandleFileHandlerFactoryLocatorFileSystemResolverStopping(null, null);
			this.fileHandlerFactoryLocator.FileSystemResolver.Stopping -= HandleFileHandlerFactoryLocatorFileSystemResolverStopping;
		}

		void HandleFileHandlerFactoryLocatorFileSystemResolverStopping (IFileSystemResolver sender, EventArgs e)
		{
			this.readerWriterLockSlim.EnterWriteLock();
			
			try
			{
				this.CloseCurrentWriteStream();
			}
			finally
			{
				this.readerWriterLockSlim.ExitWriteLock();
			}
		}

		private void CloseCurrentWriteStream()
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
		/// Whenever currentWriteStream meets or exceeds this size, a new chunk is created
		/// </summary>
		private readonly long maxChunkSize;
		
		/// <summary>
		/// The maximum total space that the sequence will occupy. Older chunks are deleted to keep the sequence within this constraint
		/// </summary>
		private readonly long maxSize;
		
		/// <summary>
		/// Probides synchronization for reading and writing the sequence
		/// </summary>
		private readonly ReaderWriterLockSlim readerWriterLockSlim = new ReaderWriterLockSlim();
		
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
		public Event Append(T item)
		{
			this.readerWriterLockSlim.EnterWriteLock();
				
			try
			{
				var ev = new Event(item);

				if (null != this.currentWriteStream)
				{				
					this.binaryFormatter.Serialize(
						this.currentWriteStream,
						ev);
					
					this.newestObjects.Add(ev);
					
					this.CreateNewChunkIfNeeded();
				}
				
				return ev;
			}
			finally
			{
				this.readerWriterLockSlim.ExitWriteLock();
			}
		}
		
		/// <summary>
		/// Creates the new chunk if needed.
		/// </summary>
		private void CreateNewChunkIfNeeded()
		{
			if (this.currentWriteStream.Length >= this.maxChunkSize)
			{
				// Make sure that the current file is complete
				this.CloseCurrentWriteStream();
				
				// Delete old chunks
				// ***********************
				
				// First get the size of all files in the directory
				var fileSizes = new Dictionary<string, long>();
				foreach (var fileName in Directory.GetFiles(this.path))
				{
					var fileInfo = new FileInfo(fileName);
					fileSizes[fileName] = fileInfo.Length;
				}
				
				// Then keep deleting the oldest file until the size is appropriate
				while (fileSizes.Values.Sum() >= this.maxSize)
				{
					string oldest = fileSizes.Keys.OrderBy(s => s).ElementAt(0);
					File.Delete(oldest);
					fileSizes.Remove(oldest);
				}
				
				// Reverse the newest chunk
				// ************************************
				
				// The chunk's file is always named after the newest item in the chunk
				var oldestObject = newestObjects[0];
				string chunkPath = Path.Combine(this.path, oldestObject.DateTime.Ticks.ToString());
				
				// Always overwrite any previous attempts to create a new chunk, this will handle crashes that occur while copying
				using (var chunkStream = File.Open(chunkPath, FileMode.Create))
				{
					newestObjects.Reverse();
					foreach (var ev in this.newestObjects)
						this.binaryFormatter.Serialize(chunkStream, ev);
					
					chunkStream.Flush();
					chunkStream.Close();
				}
				
				// Start the next chunk
				// ***************************
				newestObjects.Clear();
				this.currentWriteStream = File.Open(this.currentWriteStreamFilename, FileMode.Create);
			}
		}
		
		/// <summary>
		/// Returns all events in the sequence, starting with the specified newest date, and progressing through older events until keepIterating is set to false
		/// </summary>
		/// <param name='keepIterating'>
		/// Keep iterating.
		/// </param>
		public IEnumerable<Event> ReadSequence(DateTime newest, int max, Func<Event, bool> filter)
		{
			var toReturn = new List<Event>(max);
			
			this.readerWriterLockSlim.EnterReadLock();
				
			try
			{
				var newestObjects = this.newestObjects.ToArray().Reverse();
				
				foreach (var ev in newestObjects)
					if (ev.DateTime <= newest)
						if (filter(ev))
						{
							toReturn.Add(ev);
						
							if (toReturn.Count >= max)
								return toReturn;
						}
				
				var binaryFormatter = new BinaryFormatter();
				
				var files = Directory.GetFiles(this.path).Where(s => s != this.currentWriteStreamFilename).ToList();
				files.Sort();
				files.Reverse();
				
				foreach (var file in files)
				{
					var oldestInFileString = Path.GetFileName(file);
					var oldestInFile = new DateTime(long.Parse(oldestInFileString));
					
					if (oldestInFile <= newest)
					{
						using (var fileStream = File.OpenRead(file))
							do
							{
								object deserialized = null;

								try
								{
									deserialized = binaryFormatter.Deserialize(fileStream);
								}
								catch (Exception e)
								{
									// This is managing the logger, thus it can't log
									Console.Error.WriteLine("Exception reading from {0}, {1}", file, e);
									fileStream.Position = fileStream.Length;
								}

								if (deserialized is Event)
								{
									var ev = (Event)deserialized;
									if (ev.DateTime <= newest)
										if (filter(ev))
										{
											toReturn.Add(ev);
									
											if (toReturn.Count >= max)
												return toReturn;
										}
								}
							} while (fileStream.Position < fileStream.Length);
					}
				}
				
				// Less events were found then requested
				return toReturn;
			}
			finally
			{
				this.readerWriterLockSlim.ExitReadLock();
			}
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


	