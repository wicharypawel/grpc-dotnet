using Grpc.Core;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcNameResolutionObserver : IGrpcNameResolutionObserver
    {
        private readonly GrpcChannel _grpcChannel;
        private readonly IGrpcResolverPlugin _resolverPlugin;
        private readonly IGrpcHelper _helper;

        public GrpcNameResolutionObserver(GrpcChannel grpcChannel, IGrpcHelper helper, IGrpcResolverPlugin resolverPlugin)
        {
            _grpcChannel = grpcChannel ?? throw new ArgumentNullException(nameof(grpcChannel));
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _resolverPlugin = resolverPlugin ?? throw new ArgumentNullException(nameof(resolverPlugin));
        }

        public void OnNext(GrpcNameResolutionResult value)
        {
            _helper.GetSynchronizationContext().Execute(() =>
            {
                _grpcChannel.HandleResolvedAddresses(value);
            });
        }

        public void OnError(Status error)
        {
            if (error.StatusCode == StatusCode.OK)
            {
                throw new ArgumentException("The error status must not be OK.");
            }
            _helper.GetSynchronizationContext().Execute(() =>
            {
                HandleErrorInSyncContext(error);
            });
        }

        private void HandleErrorInSyncContext(Status error)
        {
            _grpcChannel.HandleResolvedAddressesError(error);
            _helper.GetSynchronizationContext().Schedule(() =>
            {
                _grpcChannel.RefreshNameResolution();
            }, TimeSpan.FromMilliseconds(100));
        }
    }
}
