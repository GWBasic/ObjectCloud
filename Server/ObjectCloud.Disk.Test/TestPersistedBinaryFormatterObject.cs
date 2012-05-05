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
	public class TestPersistedBinaryFormatterObject
	{
		[Test]
		public void TestOverwriteRecreate()
		{
			var path = Path.GetTempFileName();
			
			try
			{
				new PersistedBinaryFormatterObject<string>(path, "this is a test");
				
				new PersistedBinaryFormatterObject<string>(path).Read(value =>
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
				new PersistedBinaryFormatterObject<string>(path, () => "this is a test");
				
				new PersistedBinaryFormatterObject<string>(path).Read(value =>
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
				var toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path, () => "this is a test");
				
				toUpdate.Write(wrapper => wrapper.Value = "updated");
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
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
				var toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path, () => "this is a test");
				
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
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
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
				PersistedBinaryFormatterObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path, () => "this is a test");
				
				toUpdate.WriteEventual(wrapper => wrapper.Value = "updated");
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
				
				Thread.Sleep(200);
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
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
				PersistedBinaryFormatterObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path, () => "this is a test");
				
				toUpdate.WriteEventual(wrapper => wrapper.Value = "updated");
				toUpdate.Write(wrapper => wrapper.Value = "updated2");
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("updated2", value.Value));
				
				// Change this in a different incarnation
				toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path);
				toUpdate.Write(wrapper => wrapper.Value = "updated3");

				Thread.Sleep(200);
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
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
				PersistedBinaryFormatterObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path, () => "this is a test");
				
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
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
				
				Thread.Sleep(200);
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestWriteReentrant()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				PersistedBinaryFormatterObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path, () => "this is a test");
				
				toUpdate.WriteReentrant(wrapper => 
				{
					wrapper.Value = "updated1";
					
					toUpdate.WriteReentrant(wrapper2 => 
					{
						wrapper2.Value = "updated2";
					
						toUpdate.WriteReentrant(wrapper3 => 
						{
							wrapper3.Value = "updated3";
					
							toUpdate.WriteReentrant(wrapper4 => 
							{
								wrapper4.Value = "updated4";
							});
							
							Assert.AreEqual("updated4", wrapper3.Value);
						});
							
						Assert.AreEqual("updated4", wrapper2.Value);
					});
							
					Assert.AreEqual("updated4", wrapper.Value);
				});
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("updated4", value.Value));
				
				Thread.Sleep(200);
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("updated4", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		private class TestWriteReentrantExceptionException : Exception{}
		
		[Test]
		public void TestWriteReentrantException()
		{
			var path = Path.GetTempFileName();
			File.Delete(path);
			
			try
			{
				PersistedBinaryFormatterObject<Wrapped<string>>.EventualWriteFrequency = TimeSpan.FromMilliseconds(100);
				
				var toUpdate = new PersistedBinaryFormatterObject<Wrapped<string>>(path, () => "this is a test");
				
				try
				{
					toUpdate.WriteReentrant(wrapper => 
					{
						wrapper.Value = "updated1";
						
						toUpdate.WriteReentrant(wrapper2 => 
						{
							wrapper2.Value = "updated2";
						
							toUpdate.WriteReentrant(wrapper3 => 
							{
								wrapper3.Value = "updated3";
						
								toUpdate.WriteReentrant(wrapper4 => 
								{
									wrapper4.Value = "updated4";
									throw new TestWriteReentrantExceptionException();
								});
							});
						});
					});
				}
				catch (TestWriteReentrantExceptionException) {}
				
				new PersistedBinaryFormatterObject<Wrapped<string>>(path).Read(value =>
					Assert.AreEqual("this is a test", value.Value));
			}
			finally
			{
				File.Delete(path);
			}
		}
	}
}

