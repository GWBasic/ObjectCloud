using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// Opaque ID for a file.  Specific implemenations must use their own FileId implementations with types that match their data schemas
    /// </summary>
    public interface IFileId { }
}
