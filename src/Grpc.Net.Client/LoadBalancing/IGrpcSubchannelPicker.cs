using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Picker does the actual load-balancing work. It selects a <see cref="GrpcPickResult"/> for each new RPC.
    /// </summary>
    public interface IGrpcSubChannelPicker : IDisposable
    {
        /// <summary>
        /// For each RPC sent, the load balancing policy decides which subchannel (i.e., which server) the RPC should be sent to.
        /// </summary>
        /// <returns>A balancing decision for a new RPC.</returns>
        public GrpcPickResult GetNextSubChannel(IGrpcPickSubchannelArgs arguments);
    }

    /// <summary>
    /// Provides arguments for a <see cref="IGrpcSubChannelPicker.GetNextSubChannel"/>.
    /// 
    /// Interface is designed for future development.
    /// </summary>
    public interface IGrpcPickSubchannelArgs
    {
    }

    /// <summary>
    /// Provides arguments for a <see cref="IGrpcSubChannelPicker.GetNextSubChannel"/>. 
    /// 
    /// Class is designed for future development.
    /// </summary>
    public sealed class GrpcPickSubchannelArgs : IGrpcPickSubchannelArgs
    {
        /// <summary>
        /// Creates new instance of <see cref="GrpcPickSubchannelArgs"/>.
        /// </summary>
        private GrpcPickSubchannelArgs()
        {
        }

        /// <summary>
        /// Returns instance of <see cref="GrpcPickSubchannelArgs"/> with default values.
        /// </summary>
        public static GrpcPickSubchannelArgs Empty { get; } = new GrpcPickSubchannelArgs();
    }
}
