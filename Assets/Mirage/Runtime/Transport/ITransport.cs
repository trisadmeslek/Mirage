using System;

namespace Mirage.Runtime
{
    public struct MiragePacket
    {
        public byte[] Data;
        public PacketType PacketType;
    }

    public enum PacketType
    {
        Data,
        Disconnect,
        HandShake,
        Connect,
    }

    public interface ITransport
    {
        public void ReceiveData(out MiragePacket buffer, Int32 length, out IConnection endpoint);

        public void SendData(MiragePacket buffer, Int32 length, IConnection endpoint);

        /// <summary>
        /// Determines if this transport is supported in the current platform
        /// </summary>
        /// <returns>true if the transport works in this platform</returns>
        public bool Supported { get; }

        /// <summary>
        /// Gets the total amount of received data
        /// </summary>
        public long ReceivedBytes { get; }

        /// <summary>
        /// Gets the total amount of sent data
        /// </summary>
        public long SentBytes { get; }
    }
}
