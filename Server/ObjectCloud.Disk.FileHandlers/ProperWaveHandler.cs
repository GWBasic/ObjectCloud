using System;
using System.Collections.Generic;
using System.Text;

using ProtoBuf.ServiceModel.Client;

using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Wave.ProtocolBuffers.Waveserver;

namespace ObjectCloud.Disk.FileHandlers
{
    /// <summary>
    /// Handles Waves properly, that is, it lets Google Wave control all ownership.  This object is essentially a proxy
    /// </summary>
    public class ProperWaveHandler : FileHandler, IProperWaveHandler
    {
        public ProperWaveHandler(string address, uint port)
        {
            _Address = address;
            _Port = port;
        }

        public override void Dump(string path, ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId)
        {
            throw new NotImplementedException("Dumping the wave proxy isn't supported");
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

        /// <summary>
        /// The WaveServer (accessed through Protocol Buffers)
        /// </summary>
        public ProtoClient<IProtocolWaveClientRpc> WaveServer
        {
            get 
            {
                Protocol

                if (null == _WaveServer)
                    _WaveServer = new ProtoClient<IProtocolWaveClientRpc>(new HttpBasicTransport("http://" + Address + ":" + Port));

                return _WaveServer;
            }
        }
        private ProtoClient<IProtocolWaveClientRpc> _WaveServer = null;

        /// <summary>
        /// Creates a new wave with the user as the owner
        /// </summary>
        /// <returns>The WaveID</returns>
        public string CreateWave(IUser owner)
        {
            WaveServer.

            throw new NotImplementedException();
        }
    }
}
