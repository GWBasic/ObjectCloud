using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Disk.Factories
{
    /// <summary>
    /// Constructs a Wave proxy
    /// </summary>
    public class ProperWaveHandlerFactory : FileHandlerFactory<ProperWaveHandler>
    {
        public override IFileHandler CopyFile(IFileHandler sourceFileHandler, string path, ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid>? ownerID)
        {
            throw new NotImplementedException("Copying the Wave proxy isn't supported");
        }

        public override IFileHandler RestoreFile(string path, string pathToRestoreFrom, ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException("Restoring the Wave proxy isn't supported");
        }

        public override ProperWaveHandler CreateFile(string path)
        {
            return OpenFile(path);
        }

        public override ProperWaveHandler OpenFile(string path)
        {
            return new ProperWaveHandler(Address, Port);
        }

        /// <summary>
        /// The wave server's port
        /// </summary>
        public uint Port
        {
            get { return _Port; }
            set { _Port = value; }
        }
        private uint _Port;

        /// <summary>
        /// The wave server's address
        /// </summary>
        public string Address
        {
            get { return _Address; }
            set { _Address = value; }
        }
        private string _Address;
    }
}
