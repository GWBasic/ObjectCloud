using System;

namespace ObjectCloud.Interfaces.Disk
{
	/// <summary>
	/// Interface for objects with a timestamp
	/// </summary>
	public interface IHasTimeStamp
	{
		DateTime TimeStamp { get; }
	}
}

