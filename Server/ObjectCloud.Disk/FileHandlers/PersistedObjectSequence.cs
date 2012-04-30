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
		where T : IHasTimeStamp
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
			
			this.currentWriteStream = File.Open(this.currentWriteStreamFilename, FileMode.OpenOrCreate);
			
			// Read through all existing objects; incomplete writes will be overwritten
			try
			{
				while (lastSuccess < this.currentWriteStream.Length)
				{
					this.binaryFormatter.Deserialize(this.currentWriteStream);
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
			lock (key)
			{
				this.CloseCurrentWriteStream();
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
		/// Used to sync
		/// </summary>
		private readonly object key = new object();
		
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
		/// The size of the last object writen
		/// </summary>
		private long lastWriteSize = 400;
		
		/// <summary>
		/// Append the specified item to the sequence. The caller must garantee that item's timestamp is always newer then the last call
		/// </summary>
		/// <param name='item'>
		/// Item.
		/// </param>
		public void Append(T item)
		{
			lock (key)
			{
				if (null == this.currentWriteStream)
					return;
		
				var lastPosition = this.currentWriteStream.Length;
				
				this.binaryFormatter.Serialize(
					this.currentWriteStream,
					item);
				
				this.lastWriteSize = this.currentWriteStream.Position - lastPosition;
				
				this.CreateNewChunkIfNeeded();
			}
		}
		
		/// <summary>
		/// Loads all of the items in the stream
		/// </summary>
		private List<T> LoadItemsFromStream(Stream stream)
		{
			var items = new List<T>(Convert.ToInt32((stream.Length / this.lastWriteSize) * 2));
			
			while (stream.Position < stream.Length)
				items.Add((T)this.binaryFormatter.Deserialize(stream));
			
			return items;
		}
		
		/// <summary>
		/// Creates the new chunk if needed.
		/// </summary>
		private void CreateNewChunkIfNeeded()
		{
			if (this.currentWriteStream.Length >= this.maxChunkSize)
			{
				// Get the oldest object
				this.currentWriteStream.Position = 0;
				
				var newestItems = LoadItemsFromStream(this.currentWriteStream);
				
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
					var oldest = fileSizes.Keys.OrderBy(s => s).ToArray();
					
					if (oldest.Length > 0)
					{
						File.Delete(oldest[0]);
						fileSizes.Remove(oldest[0]);
					}
				}
				
				// Reverse the newest chunk
				// ************************************
				
				// The chunk's file is always named after the oldest item in the chunk
				var oldestItem = newestItems[0];
				string chunkPath = Path.Combine(this.path, oldestItem.TimeStamp.Ticks.ToString());
				
				// Always overwrite any previous attempts to create a new chunk, this will handle crashes that occur while copying
				using (var chunkStream = File.Open(chunkPath, FileMode.Create))
				{
					for (var ctr = newestItems.Count - 1; ctr >= 0; ctr--)
					{
						var item = newestItems[ctr];
						this.binaryFormatter.Serialize(chunkStream, item);
					}
					
					chunkStream.Flush();
					chunkStream.Close();
				}
				
				// Start the next chunk
				// ***************************
				this.currentWriteStream = File.Open(this.currentWriteStreamFilename, FileMode.Create);
			}
		}
		
		/// <summary>
		/// Returns all events in the sequence, starting with the specified newest date, and progressing through older events until keepIterating is set to false
		/// </summary>
		/// <param name='keepIterating'>
		/// Keep iterating.
		/// </param>
		public IEnumerable<T> ReadSequence(DateTime newest, int max, Func<T, bool> filter)
		{
			var toReturn = new List<T>(max);
			
			lock (key)
			{
				// Re-load objects that were recently serialized
				this.currentWriteStream.Position = 0;
				var newestItems = this.LoadItemsFromStream(this.currentWriteStream);
				this.currentWriteStream.Position = this.currentWriteStream.Length;
				
				for (var ctr = newestItems.Count - 1; ctr >= 0; ctr--)
				{
					var item = newestItems[ctr];

					if (item.TimeStamp <= newest)
						if (filter(item))
						{
							toReturn.Add(item);
						
							if (toReturn.Count >= max)
								return toReturn;
						}
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

								if (deserialized is T)
								{
									var item = (T)deserialized;
									if (item.TimeStamp <= newest)
										if (filter(item))
										{
											toReturn.Add(item);
									
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
		}
	}
}


	