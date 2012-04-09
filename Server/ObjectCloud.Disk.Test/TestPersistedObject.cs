using System;
using System.IO;
using System.Threading;

using NUnit.Core;
using NUnit.Framework;

using ObjectCloud.Common;
using ObjectCloud.Disk.FileHandlers;

namespace ObjectCloud.Disk.Test
{
	[TestFixture]
	public class TestPersistedObject
	{
		[Test]
		public void TestOverwriteRecreate()
		{
			var path = Path.GetTempFileName();
			
			try
			{
				new PersistedObject<string>(path, "this is a test");
				
				new PersistedObject<string>(path).Read(value =>
					Assert.AreEqual("this is a test", value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestCreateRecreateConstructor()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				new PersistedObject<string>(path, () => "this is a test");
				
				new PersistedObject<string>(path).Read(value =>
					Assert.AreEqual("this is a test", value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestUpdate()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				var toUpdate = new PersistedObject<Wrapped<string>>(path, () => "this is a test");
				
				toUpdate.Write(wrapper => wrapper.Value = "updated");
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("updated", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestExceptionRollsback()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				var toUpdate = new PersistedObject<Wrapped<string>>(path, () => "this is a test");
				
				try
				{
					toUpdate.Write(wrapper => 
					{
						wrapper.Value = "updated";
						throw new Exception("123 678");	
					});
				} 
				catch (Exception e)
				{
					if ("123 678" != e.Message)
						throw;
				}
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestWriteEventual()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				PersistedObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedObject<Wrapped<string>>(path, () => "this is a test");
				
				toUpdate.WriteEventual(wrapper => wrapper.Value = "updated");
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
				
				Thread.Sleep(200);
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("updated", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestWriteEventualThenCallWrite()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				PersistedObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedObject<Wrapped<string>>(path, () => "this is a test");
				
				toUpdate.WriteEventual(wrapper => wrapper.Value = "updated");
				toUpdate.Write(wrapper => wrapper.Value = "updated2");
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("updated2", value.Value));
				
				// Change this in a different incarnation
				toUpdate = new PersistedObject<Wrapped<string>>(path);
				toUpdate.Write(wrapper => wrapper.Value = "updated3");

				Thread.Sleep(200);
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("updated3", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestWriteEventualExceptionsRollback()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				PersistedObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedObject<Wrapped<string>>(path, () => "this is a test");
				
				try
				{
					toUpdate.WriteEventual(wrapper => 
					{
						wrapper.Value = "updated";
						throw new Exception("123 678");	
					});
				} 
				catch (Exception e)
				{
					if ("123 678" != e.Message)
						throw;
				}
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
				
				Thread.Sleep(200);
				
				new PersistedObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
	}
}

