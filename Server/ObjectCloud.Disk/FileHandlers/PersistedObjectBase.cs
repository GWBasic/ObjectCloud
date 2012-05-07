// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ObjectCloud.Disk.FileHandlers
{
	public abstract class PersistedObjectBase<T>
	{
		public PersistedObjectBase(string path)
			: this(path, () => default(T)) { }
		
		public PersistedObjectBase(string path, Func<T> constructor)
		{
			this.constructor = constructor;
			this.path = path;
			this.transactionPath = path + ".transaction";
		}
		
		public PersistedObjectBase(string path, T persistedObject)
		{
			this.constructor = () => default(T);
			this.path = path;
			this.transactionPath = path + ".transaction";
			this.persistedObject = persistedObject;
		}
		
		/// <summary>
		/// The constructor when no object is present on disk
		/// </summary>
		private readonly Func<T> constructor;
		
		/// <summary>
		/// The path to the serialized object
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
		
		/*// <summary>
		/// Allows reading or writing without waiting for a lock.
		/// </summary>
		public T DirtyObject
		{
			get { return this.persistedObject; }
		}*/		
		
		/// <summary>
		/// The persisted object. This must be accessed within the context of a lock. It is assumed that reads are thread-safe, writes are not
		/// </summary>
		private T persistedObject;

		/// <summary>
		/// Probides synchronization for reading and writing the object
		/// </summary>
		private readonly ReaderWriterLockSlim readerWriterLockSlim = new ReaderWriterLockSlim();
		
		/// <summary>
		/// Loads the object from disk
		/// </summary>
		protected void Load()
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
					this.persistedObject = this.Deserialize(readStream);
					readStream.Close();
				}

				return;
			}
			
			this.persistedObject = this.constructor();
			this.Save();
		}
		
		/// <summary>
		/// Deserializes the object from the stream
		/// </summary>
		protected abstract T Deserialize(Stream readStream);
		
		/// <summary>
		/// Saves the object to disk
		/// </summary>
		protected void Save()
		{
			// If there's an eventual write scheduled, it no longer is needed
			if (null != this.timer)
			{
				this.timer.Dispose();
				this.timer = null;
			}	
			
			// Delete failed transactional saves
			if (File.Exists(this.transactionPath))
				File.Delete(this.transactionPath);

			// Keep the old object around in case of a failed write
			var hasRollback = File.Exists(this.path);
			if (hasRollback)
				File.Move(this.path, this.transactionPath);
			
			try
			{
				using (var writeStream = File.OpenWrite(this.path))
				{
					this.Serialize(writeStream, this.persistedObject);
					writeStream.Close();
				}
			}
			catch
			{
				// Rollback in case of a failed write

				if (File.Exists(this.path))
					File.Delete(this.path);
				
				if (hasRollback)
					File.Move(this.transactionPath, this.path);
				
				throw;
			}
			
			// On success, delete the transactional file
			if (hasRollback)
				File.Delete(this.transactionPath);
		}
		
		protected abstract void Serialize(Stream writeStream, T persistedObject);
		
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
			//Console.WriteLine("BeginRead");
			if (Thread.CurrentThread != this.writeReentrantThread)
				this.readerWriterLockSlim.EnterReadLock();
			
			try
			{
				return func(this.persistedObject);
			}
			finally
			{
				//Console.WriteLine("EndRead");
				if (Thread.CurrentThread != this.writeReentrantThread)
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
			//Console.WriteLine("BeginRead");
			if (Thread.CurrentThread != this.writeReentrantThread)
				this.readerWriterLockSlim.EnterReadLock();
			
			try
			{
				action(this.persistedObject);
			}
			finally
			{
				//Console.WriteLine("EndRead");
				if (Thread.CurrentThread != this.writeReentrantThread)
					this.readerWriterLockSlim.ExitReadLock();
			}
		}
		
		/// <summary>
		/// Calls the function within a write context
		/// </summary>
		public R Write<R>(Func<T, R> func)
		{
			//Console.WriteLine("BeginWrite");
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
				//Console.WriteLine("EndWrite");
				this.readerWriterLockSlim.ExitWriteLock();
			}
		}
		
		/// <summary>
		/// Calls the function within a write context
		/// </summary>
		public void Write(Action<T> action)
		{
			//Console.WriteLine("BeginWrite");
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
				//Console.WriteLine("EndWrite");
				this.readerWriterLockSlim.ExitWriteLock();
			}
		}
		
		/// <summary>
		/// The thread that is in WriteReentrant, or null
		/// </summary>
		Thread writeReentrantThread = null;
		
		/// <summary>
		/// The number of calls into WriteReentrant
		/// </summary>
		int writeReentrantCount = 0;
		
		/// <summary>
		/// An exception thrown within any call to WriteReentrant. If an exception is leaked, this will continuously be re-thrown until the object is restored
		/// </summary>
		Exception writeReentrantException = null;
		
		/// <summary>
		/// Calls the function within a write context, can be called re-entrantly. The object is only written to disk when there are no re-entrant calls on the stack
		/// </summary>
		public void WriteReentrant(Action<T> action)
		{
			if (Thread.CurrentThread != this.writeReentrantThread)
			{
				this.readerWriterLockSlim.EnterWriteLock();
				this.writeReentrantThread = Thread.CurrentThread;
			}
			
			if (null != this.writeReentrantException)
				throw writeReentrantException;
			
			this.writeReentrantCount++;
			
			try
			{
				action(this.persistedObject);
				
				if (null != this.writeReentrantException)
					throw writeReentrantException;
				
				// Only save if there are no re-entrant operations
				if (1 == this.writeReentrantCount)
					this.Save();
			}
			catch (Exception e)
			{
				this.writeReentrantException = e;
				
				// Rollback if an exception occurs while writing
				if (1 == this.writeReentrantCount)
					this.Load();
				
				throw;
			}
			finally
			{
				this.writeReentrantCount--;
				
				if (0 == this.writeReentrantCount)
				{
					this.readerWriterLockSlim.ExitWriteLock();
					this.writeReentrantThread = null;
					this.writeReentrantException = null;
				}
			}
		}
		
		/// <summary>
		/// The timer when writes will happen eventually.
		/// </summary>
		private Timer timer = null;
		
		/// <summary>
		/// How often ObjectCloud flushes eventual writes to disk. More frequent writes improve correctness at the expense of performance
		/// </summary>
		public static TimeSpan EventualWriteFrequency 
		{
			get { return PersistedBinaryFormatterObject<T>.eventualWriteFrequency; }
			set { PersistedBinaryFormatterObject<T>.eventualWriteFrequency = value; }
		}		
		private static TimeSpan eventualWriteFrequency = TimeSpan.FromMinutes(1);

		/// <summary>
		/// Calls the function within a write context. Changes made are saved eventually, and may be lost if any future writes rollback
		/// </summary>
		public void WriteEventual(Action<T> action)
		{
			this.readerWriterLockSlim.EnterWriteLock();
			
			try
			{
				action(this.persistedObject);
				
				if (null == this.timer)
					this.timer = new Timer(this.Flush, null, PersistedBinaryFormatterObject<T>.eventualWriteFrequency, TimeSpan.Zero);
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
		/// Writes changes made during an eventual write to disk
		/// </summary>
		private void Flush(object state)
		{
			this.readerWriterLockSlim.EnterWriteLock();
			
			try
			{
				this.Save();
			}
			catch
			{
				// Rollback if an exception occurs while writing
				this.Load();
			}
			finally
			{
				//Console.WriteLine("EndWrite");
				this.readerWriterLockSlim.ExitWriteLock();
			}
		}
	}
}

