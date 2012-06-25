using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk
{
	public class PersistedObjectSequence_BinaryFormatter<T> : PersistedObjectSequence<T>
		where T : IHasTimeStamp
	{
		public PersistedObjectSequence_BinaryFormatter(
			string path,
			long maxChunkSize,
			long maxSize,
			FileHandlerFactoryLocator fileHandlerFactoryLocator)
			: this(
				path,
				maxChunkSize,
				maxSize,
				fileHandlerFactoryLocator,
				new BinaryFormatter()) { }

		private PersistedObjectSequence_BinaryFormatter(
			string path,
			long maxChunkSize,
			long maxSize,
			FileHandlerFactoryLocator fileHandlerFactoryLocator,
			BinaryFormatter binaryFormatter)
			: base(
				path,
				maxChunkSize,
				maxSize,
				fileHandlerFactoryLocator,
				stream => (T)binaryFormatter.Deserialize(stream),
				binaryFormatter.Serialize) { }
	}
}

