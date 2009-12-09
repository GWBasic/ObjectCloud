using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Proxy to the wave server, handles waves "properly" as in this object gives access to multiple waves through their ID and the wave server determines ownership and permissions
    /// </summary>
    public interface IProperWaveHandler : IFileHandler
    {
        /// <summary>
        /// Creates a new wave with the user as the owner
        /// </summary>
        /// <returns>The WaveID</returns>
        string CreateWave(IUser owner);
    }
}
