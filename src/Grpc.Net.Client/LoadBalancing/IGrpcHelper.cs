using System;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Grpc.Net.Client.LoadBalancing
{
    public interface IGrpcHelper
    {
        public IGrpcSubChannel CreateSubChannel(CreateSubchannelArgs args);
        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker);
        public void RefreshNameResolution();
        public GrpcSynchronizationContext GetSynchronizationContext();
    }

    public sealed class CreateSubchannelArgs
    {
        public Uri Address { get; }
        public GrpcAttributes Attributes { get; }

        public CreateSubchannelArgs(Uri address, GrpcAttributes attributes)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }
    }
}
