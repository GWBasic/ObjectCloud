using System;
using System.IO;

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
		public void NoOp()
		{
			throw new NotImplementedException("These tests need to be written");
		}
		
		[Test]
		public void TestOverwriteRecreate()
		{
			var path = Path.GetTempFileName();
			
			try
			{
				new PersistedObject<string>(path, "this is a test");
				Assert.AreEqual("this is a test", new PersistedObject<string>(path).DirtyObject);
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
				Assert.AreEqual("this is a test", new PersistedObject<string>(path).DirtyObject);
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
				
				Assert.AreEqual("updated", new PersistedObject<Wrapped<string>>(path).DirtyObject.Value);
			}
			finally
			{
				File.Delete(path);
			}
		}
		
		[Test]
		public void TestExceptionRollsback()
		{
			throw new NotImplementedException();
		}
	}
}

