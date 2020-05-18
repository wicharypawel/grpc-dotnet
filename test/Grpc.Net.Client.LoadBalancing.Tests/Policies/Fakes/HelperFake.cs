namespace Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes
{
    internal sealed class HelperFake : IGrpcHelper
    {
        public IGrpcSubChannelPicker? SubChannelPicker { get; private set; }

        public IGrpcSubChannel CreateSubChannel(CreateSubchannelArgs args)
        {
            return new GrpcSubChannelFake(args.Address, args.Attributes);
        }

        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker)
        {
            SubChannelPicker = newPicker;
        }

        public void RefreshNameResolution()
        {
            throw new System.NotImplementedException();
        }

        public GrpcSynchronizationContext GetSynchronizationContext()
        {
            throw new System.NotImplementedException();
        }
    }
}
