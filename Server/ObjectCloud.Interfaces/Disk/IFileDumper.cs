using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Interface for objects that can dump a file completely to a stream
    /// </summary>
    public interface IFileDumper
    {
        /// <summary>
        /// Dumps the file to the stream
        /// </summary>
        /// <param name="fileContainer"></param>
        /// <param name="stream"></param>
        void DoDump(IFileContainer fileContainer, ID<IUserOrGroup, Guid> userId, Stream stream);
    }
}
