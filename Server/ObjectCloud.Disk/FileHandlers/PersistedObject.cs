// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

namespace ObjectCloud.Disk
{
	public class PersistedObject<T>
		where T:new()
	{
		public PersistedObject(string path)
		{
			this.path = path;
			this.transactionPath = path + ".transaction";
			this.Load();
		}
		
		/// <summary>
		/// The path to the folder that stores the serialized object
		/// </summary>
		public string Path 
		{
			get { return this.path;	}
		}		
		private readonly string path;
		
		/// <summary>
		/// The path used for transactional writes, if this file exists, it means that a transaction failed
		/// </summary>
		private readonly string transactionPath;

		/// <summary>
		/// The persisted object. This must be accessed within the context of a lock. It is assumed that reads are thread-safe, writes are not
		/// </summary>
		private T persistedObject;
		
		/// <summary>
		/// Probides synchronization for reading and writing the object
		/// </summary>
		private readonly ReaderWriterLockSlim readerWriterLockSlim = new ReaderWriterLockSlim();
		
		/// <summary>
		/// A single binary formatter instanciated onces for quick reuse
		/// </summary>
		private readonly BinaryFormatter binaryFormatter = new BinaryFormatter();
		
		/// <summary>
		/// Loads the object from disk
		/// </summary>
		private void Load()
		{
			// Restore failed write transactions
			if (File.Exists(this.transactionPath))
			{
				if (File.Exists(this.path))
					File.Delete(this.path);
				
				File.Move(this.transactionPath, this.path);
			}
			
			// Read the persisted object from disk, if it exists
			if (File.Exists(this.path))
			{
				using (var readStream = File.OpenRead(this.path))
				{
					this.persistedObject = (T)this.binaryFormatter.Deserialize(readStream);
					readStream.Close();
				}

				return;
			}
			
			this.persistedObject = new T();
		}
		
		/// <summary>
		/// Saves the object to disk
		/// </summary>
		private void Save()
		{
			// Delete failed transactional saves
			if (File.Exists(this.transactionPath))
				File.Delete(this.transactionPath);

			// Keep the old object around in case of a failed write
			File.Move(this.path, this.transactionPath);
			
			try
			{
				using (var writeStream = File.OpenWrite(this.path))
				{
					this.binaryFormatter.Serialize(writeStream, this.persistedObject);
					writeStream.Close();
				}
			}
			catch
			{
				// Rollback in case of a failed write

				if (File.Exists(this.path))
					File.Delete(this.path);

				File.Move(this.transactionPath, this.path);
				
				throw;
			}
			
			// On success, delete the transactional file
			File.Delete(this.transactionPath);
		}
		
		/// <summary>
		/// Calls the function within a read context
		/// </summary>
		/// <param name='func'>
		/// Func.
		/// </param>
		/// <typeparam name='R'>
		/// The 1st type parameter.
		/// </typeparam>
		public R Read<R>(Func<T, R> func)
		{
			this.readerWriterLockSlim.EnterReadLock();
			
			try
			{
				return func(this.persistedObject);
			}
			finally
			{
				this.readerWriterLockSlim.ExitReadLock();
			}
		}
		
		/// <summary>
		/// Calls the function within a read context
		/// </summary>
		/// <param name='func'>
		/// Func.
		/// </param>
		/// <typeparam name='R'>
		/// The 1st type parameter.
		/// </typeparam>
		public void Read(Action<T> action)
		{
			this.readerWriterLockSlim.EnterReadLock();
			
			try
			{
				action(this.persistedObject);
			}
			finally
			{
				this.readerWriterLockSlim.ExitReadLock();
			}
		}
		
		/// <summary>
		/// Calls the function within a write context
		/// </summary>
		/// <param name='func'>
		/// Func.
		/// </param>
		/// <typeparam name='R'>
		/// The 1st type parameter.
		/// </typeparam>
		public R Write<R>(Func<T, R> func)
		{
			this.readerWriterLockSlim.EnterWriteLock();
			
			try
			{
				var toReturn = func(this.persistedObject);
				this.Save();
				return toReturn;
			}
			catch
			{
				// Rollback if an exception occurs while writing
				this.Load();
				throw;
			}
			finally
			{
				this.readerWriterLockSlim.ExitWriteLock();
			}
		}
		
		/// <summary>
		/// Calls the function within a write context
		/// </summary>
		/// <param name='func'>
		/// Func.
		/// </param>
		/// <typeparam name='R'>
		/// The 1st type parameter.
		/// </typeparam>
		public void Write(Action<T> action)
		{
			this.readerWriterLockSlim.EnterWriteLock();
			
			try
			{
				action(this.persistedObject);
				this.Save();
			}
			catch
			{
				// Rollback if an exception occurs while writing
				this.Load();
				throw;
			}
			finally
			{
				this.readerWriterLockSlim.ExitWriteLock();
			}
		}
	}
}

