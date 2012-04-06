using System;
using System.Runtime.Serialization.Formatters.Binary;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.FileHandlers
{
	/// <summary>
	/// The data serialized in a session
	/// </summary>
	[Serializable]
	internal class SessionData
	{
		public ID<IUserOrGroup, Guid> userId;
		public TimeSpan maxAge;
		public DateTime lastQuery;
		public bool keepAlive;
	}
}

