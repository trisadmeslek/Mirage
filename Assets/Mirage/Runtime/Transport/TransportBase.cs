namespace Mirage.Runtime
{
    public abstract class TransportBase: ITransport
    {
        public abstract void ReceiveData(out MiragePacket buffer, int length, out IConnection endpoint);

        public abstract void SendData(MiragePacket buffer, int length, IConnection endpoint);

        /// <summary>
        /// Determines if this transport is supported in the current platform
        /// </summary>
        /// <returns>true if the transport works in this platform</returns>
        public abstract bool Supported { get; set; }

        /// <summary>
        /// Gets the total amount of received data
        /// </summary>
        public virtual long ReceivedBytes => 0;

        /// <summary>
        /// Gets the total amount of sent data
        /// </summary>
        public virtual long SentBytes => 0;
    }
}
