using Grpc.Core;

namespace Grpc.Net.Client.LoadBalancing.Extensions.Internal
{
    internal sealed class XdsChannelFactory
    {
        internal ChannelBase? OverrideChannel { private get; set; }

        public ChannelBase CreateChannel(string address, GrpcChannelOptions channelOptions)
        {
            return OverrideChannel ?? GrpcChannel.ForAddress(address, channelOptions);
        }
    }
}
