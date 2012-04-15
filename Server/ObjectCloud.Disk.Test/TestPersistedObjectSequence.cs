using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

using NUnit.Core;
using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Disk.FileHandlers;

namespace ObjectCloud.Disk.Test
{
	[TestFixture]
	public class TestPersistedObjectSequence
	{
		[Serializable]
		private class Event<T> : IHasTimeStamp
		{
			public Event(T item)
			{
				this.Item = item;
				this.TimeStamp = DateTime.UtcNow;
			}
			
			public DateTime TimeStamp { get; private set; }
			public T Item { get; private set; }
		}
		
		private string path;
		private readonly FileHandlerFactoryLocator fileHandlerFactoryLocator = new FileHandlerFactoryLocator()
		{
			FileSystemResolver = new ObjectCloud.Disk.Implementation.FileSystemResolver()
		};
		
		[SetUp]
		public void Setup()
		{
			this.path = Path.GetTempFileName();
			File.Delete(this.path);
			Directory.CreateDirectory(this.path);
		}
		
		[TearDown]
		public void TearDown()
		{
			foreach (var file in Directory.GetFiles(this.path))
				File.Delete(file);
			
			Directory.Delete(this.path);
		}
		
		[Test]
		public void TestSimpleSequence()
		{
			using (var sequence = new PersistedObjectSequence<Event<int>>(this.path, int.MaxValue, int.MaxValue, this.fileHandlerFactoryLocator))
			{
				sequence.Append(new Event<int>(0));
				sequence.Append(new Event<int>(1));
				sequence.Append(new Event<int>(2));
				sequence.Append(new Event<int>(3));
				sequence.Append(new Event<int>(4));
				sequence.Append(new Event<int>(5));
				sequence.Append(new Event<int>(6));
				sequence.Append(new Event<int>(7));
				sequence.Append(new Event<int>(8));
				sequence.Append(new Event<int>(9));
				
				var events = sequence.ReadSequence(DateTime.MaxValue, 10, e => true).Select(e => e.Item).ToArray();
				
				Assert.AreEqual(10, events.Length);			
				
				Assert.AreEqual(9, events[0]);
				Assert.AreEqual(8, events[1]);
				Assert.AreEqual(7, events[2]);
				Assert.AreEqual(6, events[3]);
				Assert.AreEqual(5, events[4]);
				Assert.AreEqual(4, events[5]);
				Assert.AreEqual(3, events[6]);
				Assert.AreEqual(2, events[7]);
				Assert.AreEqual(1, events[8]);
				Assert.AreEqual(0, events[9]);
			}
		}
		
		[Test]
		public void TestReloadSimpleSequence()
		{
			this.TestSimpleSequence();
			
			using (var sequence = new PersistedObjectSequence<Event<int>>(this.path, int.MaxValue, int.MaxValue, this.fileHandlerFactoryLocator))
			{
				var events = sequence.ReadSequence(DateTime.MaxValue, 10, e => true).Select(e => e.Item).ToArray();
				
				Assert.AreEqual(10, events.Length);			
				
				Assert.AreEqual(9, events[0]);
				Assert.AreEqual(8, events[1]);
				Assert.AreEqual(7, events[2]);
				Assert.AreEqual(6, events[3]);
				Assert.AreEqual(5, events[4]);
				Assert.AreEqual(4, events[5]);
				Assert.AreEqual(3, events[6]);
				Assert.AreEqual(2, events[7]);
				Assert.AreEqual(1, events[8]);
				Assert.AreEqual(0, events[9]);
			}
		}
		
		[Test]
		public void TestChunking()
		{
			var memoryStream = new MemoryStream();
			var serializer = new BinaryFormatter();
			
			serializer.Serialize(
				memoryStream,
				new Event<byte[]>(new byte[85]));
			
			var serializedObjectLength = memoryStream.Length;
			var chunkLength = serializedObjectLength * 10;
			
			using (var sequence = new PersistedObjectSequence<Event<byte[]>>(this.path, chunkLength, int.MaxValue, this.fileHandlerFactoryLocator))
			{
				for (byte ctr = 0; ctr < 30; ctr++)
				{
					var bytes = new byte[85];
					bytes[0] = ctr;
					
					sequence.Append(new Event<byte[]>(bytes));
				}
			}
			
			var files = Directory.GetFiles(path);
			
			Assert.AreEqual(4, files.Length);
			
			foreach (var file in files)
			{
				var filename = Path.GetFileName(file);
				
				if ("newest" == filename)
					Assert.AreEqual(0, new FileInfo(file).Length);
				else
				{
					Assert.AreEqual(chunkLength, new FileInfo(file).Length);
				
					using (var stream = File.OpenRead(file))
					{
						Event<byte[]> ev;
						
						do
							ev = (Event<byte[]>)serializer.Deserialize(stream);
						while (stream.Position < stream.Length);
						
						Assert.AreEqual(filename, ev.TimeStamp.Ticks.ToString());
					}
				}
			}
		}
		
		[Test]
		public void TestRollover()
		{
			using (var sequence = new PersistedObjectSequence<Event<int[]>>(this.path, 1000, 30000, this.fileHandlerFactoryLocator))
			{
				for (int ctr = 0; ctr < 100; ctr++)
				{
					int[] toSave = new int[40];
					toSave[0] = ctr;
					
					sequence.Append(new Event<int[]>(toSave));
				}
			}
			
			var totalSize = Directory.GetFiles(this.path).Select(file => new FileInfo(file).Length).Sum();
			
			Assert.IsTrue(totalSize >= 30000, "Total size too small");
			Assert.IsTrue(totalSize < 31000, "Total size too big");

			using (var sequence = new PersistedObjectSequence<Event<int[]>>(this.path, 1000, 30000, this.fileHandlerFactoryLocator))
			{
				var items = sequence.ReadSequence(DateTime.MaxValue, 2000, ev => true).Select(ev => ev.Item).ToArray();
				
				var ctr = 99;
				foreach(var item in items)
				{
					Assert.AreEqual(ctr, item[0]);
					ctr--;
				}
			}
		}
		
		[Test]
		public void TestFilter()
		{
			using (var sequence = new PersistedObjectSequence<Event<int>>(this.path, 500, 3000, this.fileHandlerFactoryLocator))
			{
				for (int ctr = 0; ctr < 500; ctr++)
					sequence.Append(new Event<int>(ctr));
				
				foreach (var ctr in sequence.ReadSequence(DateTime.MaxValue, 100, x => 0 == (x.Item % 5)))
					Assert.IsTrue(0 == ctr.Item % 5);
			}
		}
				
		[Test]
		public void TestDateRange()
		{
			using (var sequence = new PersistedObjectSequence<Event<int>>(this.path, 500, 3000, this.fileHandlerFactoryLocator))
			{
				for (int ctr = 0; ctr < 500; ctr++)
					sequence.Append(new Event<int>(ctr));
				
				var events = sequence.ReadSequence(DateTime.MaxValue, 500, e => true);
				
				foreach (var ev in events)
				{
					var result = sequence.ReadSequence(ev.TimeStamp, 1, e => true).ToArray();
					
					Assert.AreEqual(1, result.Length);
					Assert.AreEqual(ev.Item, result[0].Item);
				}
			}
		}
		
		[Test]
		public void TestDatesOrdered()
		{
			using (var sequence = new PersistedObjectSequence<Event<int>>(this.path, 500, 3000, this.fileHandlerFactoryLocator))
			{
				for (int ctr = 0; ctr < 500; ctr++)
					sequence.Append(new Event<int>(ctr));
				
				DateTime prev = DateTime.MaxValue;
				
				foreach (var ev in sequence.ReadSequence(DateTime.MaxValue, 500, e => true))
				{
					Assert.IsTrue(ev.TimeStamp < prev);
					prev = ev.TimeStamp;
				}
			}
		}
	}
}

