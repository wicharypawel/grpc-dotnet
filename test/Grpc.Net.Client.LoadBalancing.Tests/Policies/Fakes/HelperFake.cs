namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class HelperFake : IGrpcHelper
    {
        public IGrpcSubChannelPicker? SubChannelPicker { get; private set; }

        public GrpcSubChannel CreateSubChannel(CreateSubchannelArgs args)
        {
            return new GrpcSubChannel(args.Address, args.Attributes);
        }

        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker)
        {
            SubChannelPicker = newPicker;
        }
    }
}
