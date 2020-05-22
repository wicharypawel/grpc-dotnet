using Grpc.Net.Client.LoadBalancing;
using System;

namespace Grpc.Net.Client.Tests.Fakes
{
    internal sealed class GrpcSubChannelFake : IGrpcSubChannel
    {
        public Uri Address { get; set; }

        public GrpcAttributes Attributes { get; set; }

        public GrpcSubChannelFake(Uri address, GrpcAttributes attributes)
        {
            Address = address;
            Attributes = attributes;
        }

        public void RequestConnection()
        {
        }

        public void Shutdown()
        {
        }

        public void Start(IGrpcSubchannelStateObserver observer)
        {
        }

        public void UpdateAddress(Uri address)
        {
        }
    }
}
