using System;

namespace ObjectCloud.Interfaces.Disk
{
	/// <summary>
	/// The various levels that a logging message can be
	/// </summary>
	public enum LoggingLevel : int
	{
		Trace,
		Debug,
		Info,
		Warn,
		Error,
		Fatal
	}
}
