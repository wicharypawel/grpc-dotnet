#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Internal;
using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcNameResolutionObserver : IGrpcNameResolutionObserver
    {
        private readonly GrpcChannel _grpcChannel;
        private readonly IGrpcHelper _helper;
        private readonly ILogger _logger;

        public GrpcNameResolutionObserver(GrpcChannel grpcChannel, IGrpcHelper helper)
        {
            _grpcChannel = grpcChannel ?? throw new ArgumentNullException(nameof(grpcChannel));
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _logger = grpcChannel.LoggerFactory.CreateLogger<GrpcNameResolutionObserver>();
        }

        public void OnNext(GrpcNameResolutionResult value)
        {
            _logger.LogDebug("Name resolution results received.");
            _helper.GetSynchronizationContext().Execute(() =>
            {
                var lastResolutionStateCopy = _grpcChannel.LastResolutionState;
                if (_grpcChannel.LastResolutionState != GrpcResolutionState.Success)
                {
                    _grpcChannel.LastResolutionState = GrpcResolutionState.Success;
                }
                var effectiveServiceConfig = value.ServiceConfig.Config ?? new object();
                var resolvedAddresses = new GrpcResolvedAddresses(value.HostsAddresses, effectiveServiceConfig, value.Attributes);
                if (_helper != _grpcChannel.Helper) return;
                Status handleResult = _grpcChannel.TryHandleResolvedAddresses(resolvedAddresses);
                if (handleResult.StatusCode != StatusCode.OK)
                {
                    if (value.HostsAddresses.Count == 0 && lastResolutionStateCopy == GrpcResolutionState.Success)
                    {
                        ScheduleExponentialBackOffInSyncContext();
                    }
                    else
                    {
                        HandleErrorInSyncContext(handleResult);
                    }
                }
            });
        }

        public void OnError(Status error)
        {
            if (error.StatusCode == StatusCode.OK)
            {
                throw new ArgumentException("The error status must not be OK.");
            }
            _logger.LogDebug("Name resolution error received.");
            _helper.GetSynchronizationContext().Execute(() =>
            {
                HandleErrorInSyncContext(error);
            });
        }

        private void HandleErrorInSyncContext(Status error)
        {
            if (_grpcChannel.LastResolutionState != GrpcResolutionState.Error)
            {
                _grpcChannel.LastResolutionState = GrpcResolutionState.Error;
            }
            // Call LB only if it's not shutdown. If LB is shutdown, lbHelper won't match. This is required for idle mode implementation.
            if (_helper != _grpcChannel.Helper) return;
            _grpcChannel.HandleNameResolutionError(error);
            ScheduleExponentialBackOffInSyncContext();
        }

        /// Think about moving this method to <see cref="GrpcChannel"/>.
        private void ScheduleExponentialBackOffInSyncContext()
        {
            _grpcChannel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            if (_grpcChannel.NameResolverRefreshSchedule?.IsPending() ?? false)
            {
                // The name resolver may invoke onError multiple times, but we only want to
                // schedule one backoff attempt.
                return;
            }
            if (_grpcChannel.NameResolverRefreshBackoffPolicy == null)
            {
                _grpcChannel.NameResolverRefreshBackoffPolicy = _grpcChannel.BackoffPolicyProvider.CreateBackoffPolicy();
            }
            var delay = _grpcChannel.NameResolverRefreshBackoffPolicy.NextBackoff();
            _logger.LogDebug($"Scheduling name resolution backoff for {delay}.");
            _grpcChannel.NameResolverRefreshSchedule = _helper.GetSynchronizationContext().Schedule(() =>
            {
                _grpcChannel.NameResolverRefreshSchedule = null;
                _grpcChannel.RefreshNameResolution();
            }, delay);
        }
    }
}
