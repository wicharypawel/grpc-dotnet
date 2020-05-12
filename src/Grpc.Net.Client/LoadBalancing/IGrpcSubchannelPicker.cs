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
        GrpcPickResult GetNextSubChannel();
    }
}
