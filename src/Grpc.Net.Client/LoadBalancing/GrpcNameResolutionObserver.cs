﻿using Grpc.Core;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcNameResolutionObserver : IGrpcNameResolutionObserver
    {
        private readonly GrpcChannel _grpcChannel;
        private readonly IGrpcHelper _helper;

        public GrpcNameResolutionObserver(GrpcChannel grpcChannel, IGrpcHelper helper)
        {
            _grpcChannel = grpcChannel ?? throw new ArgumentNullException(nameof(grpcChannel));
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
        }

        public void OnNext(GrpcNameResolutionResult value)
        {
            _helper.GetSynchronizationContext().Execute(() =>
            {
                var effectiveServiceConfig = value.ServiceConfig.Config ?? new object();
                var resolvedAddresses = new GrpcResolvedAddresses(value.HostsAddresses, effectiveServiceConfig, value.Attributes);
                _grpcChannel.HandleResolvedAddresses(resolvedAddresses);
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
            _grpcChannel.HandleNameResolutionError(error);
            _helper.GetSynchronizationContext().Schedule(() =>
            {
                _grpcChannel.RefreshNameResolution();
            }, TimeSpan.FromMilliseconds(100));
        }
    }
}
