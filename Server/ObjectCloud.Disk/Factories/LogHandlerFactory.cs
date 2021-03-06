// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;

using ObjectCloud.Common;
using ObjectCloud.Common.StreamEx;
using ObjectCloud.Common.Threading;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
	public class LogHandlerFactory : FileHandlerFactory<LogHandler>
	{
        /// <summary>
        /// Enables writing to the console.  Defaults to false
        /// </summary>
        public bool WriteToConsole
        {
            get { return writeToConsole; }
            set
            {
                writeToConsole = value;

                if (!value)
                    NonBlockingConsoleWriter.EndThread();
            }
        }
        private bool writeToConsole = false;
		
		/// <summary>
		/// The maximum size of a chunk of logging events
		/// </summary>
		public int MaxChunkSize { get; set; }
		
		/// <summary>
		/// The amount of disk to devote to logging
		/// </summary>
		public int MaxSize { get; set; }
		
        public override void CreateFile(string path, FileId fileId)
        {
            Directory.CreateDirectory(path);
        }

        public override LogHandler OpenFile(string path, FileId fileId)
        {
			var delegateQueue = new DelegateQueue("Log Handler");
			
			this.delegateQueues.Enqueue(delegateQueue);
			
			return new LogHandler(
				new PersistedObjectSequence<LoggingEvent>(
					path,
					this.MaxChunkSize,
					this.MaxSize,
					this.FileHandlerFactoryLocator,
					this.Deserialize,
					this.Serialize),
				this.FileHandlerFactoryLocator,
				this.WriteToConsole,
				delegateQueue);
        }

        public override void CopyFile(IFileHandler sourceFileHandler, IFileId fileId, ID<IUserOrGroup, Guid>? ownerID, IDirectoryHandler parentDirectory)
        {
			throw new NotImplementedException();
        }

        public override void RestoreFile(IFileId fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId, IDirectoryHandler parentDirectory)
        {
			throw new NotImplementedException();
        }

        /// <summary>
        /// All of the started delegate queues
        /// </summary>
        private LockFreeQueue<DelegateQueue> delegateQueues = new LockFreeQueue<DelegateQueue>();

        public override void Stop()
        {
            LockFreeQueue<DelegateQueue> delegateQueues = this.delegateQueues;
            this.delegateQueues = new LockFreeQueue<DelegateQueue>();

            DelegateQueue delegateQueue;
            while (delegateQueues.Dequeue(out delegateQueue))
            {
                delegateQueue.Stop();
                delegateQueues.Enqueue(delegateQueue);
            }
        }

		private LoggingEvent Deserialize(Stream stream)
		{
			return new LoggingEvent()
			{
				Classname = stream.ReadString(),
				ExceptionClassname = stream.ReadString(),
				ExceptionMessage = stream.ReadString(),
				ExceptionStackTrace = stream.ReadString(),
				Level = (LoggingLevel)stream.Read<int>(),
				Message = stream.ReadString(),
				RemoteEndPoint = stream.ReadString(),
				SessionId = stream.ReadNullable<ID<ISession, Guid>>(),
				ThreadId = stream.Read<int>(),
				TimeStamp = new DateTime(stream.Read<long>()),
				UserId = stream.ReadNullable<ID<IUserOrGroup, Guid>>()
			};
		}

		private void Serialize(Stream stream, LoggingEvent loggingEvent)
		{
			stream.Write(loggingEvent.Classname);
			stream.Write(loggingEvent.ExceptionClassname);
			stream.Write(loggingEvent.ExceptionMessage);
			stream.Write(loggingEvent.ExceptionStackTrace);
			stream.Write((int)loggingEvent.Level);
			stream.Write(loggingEvent.Message);
			stream.Write(loggingEvent.RemoteEndPoint);
			stream.WriteNullable(loggingEvent.SessionId);
			stream.Write(loggingEvent.ThreadId);
			stream.Write(loggingEvent.TimeStamp.Ticks);
			stream.WriteNullable(loggingEvent.UserId);
		}
    }
}
