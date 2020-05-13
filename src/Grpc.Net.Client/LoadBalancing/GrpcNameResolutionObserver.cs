using Grpc.Core;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcNameResolutionObserver : IGrpcNameResolutionObserver
    {
        private readonly GrpcChannel _grpcChannel;

        public GrpcNameResolutionObserver(GrpcChannel grpcChannel)
        {
            _grpcChannel = grpcChannel;
        }

        public void OnNext(GrpcNameResolutionResult value)
        {
            _grpcChannel.HandleResolvedAddresses(value);
        }

        public void OnError(Status error)
        {
            _grpcChannel.HandleResolvedAddressesError(error);
        }
    }
}
