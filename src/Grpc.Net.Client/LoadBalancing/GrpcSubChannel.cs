using System;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcSubChannel : IGrpcSubChannel
    {
        public Uri Address { get; }

        public GrpcAttributes Attributes { get; }

        public GrpcSubChannel(Uri address) : this(address, GrpcAttributes.Empty)
        {
        }

        public GrpcSubChannel(Uri address, GrpcAttributes attributes)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Attributes = attributes ?? GrpcAttributes.Empty;
        }

        public void Start(IGrpcSubchannelStateObserver observer)
        {
            Task.Run(() =>
            {
                observer.OnNext(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
            });
        }

        public void Shutdown()
        {
        }

        public void RequestConnection()
        {
        }

        public void UpdateAddress(Uri address)
        {
        }
    }
}
