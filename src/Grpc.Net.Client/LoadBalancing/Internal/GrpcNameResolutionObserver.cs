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
using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class GrpcNameResolutionObserver : IGrpcNameResolutionObserver
    {
        private readonly GrpcChannel _channel;
        private readonly IGrpcHelper _helper;
        private readonly ILogger _logger;

        public GrpcNameResolutionObserver(GrpcChannel channel, IGrpcHelper helper)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _logger = channel.LoggerFactory.CreateLogger<GrpcNameResolutionObserver>();
        }

        public void OnNext(GrpcNameResolutionResult value)
        {
            _helper.GetSynchronizationContext().Execute(() =>
            {
                _logger.LogDebug("Name resolution results received.");
                if (_channel.LastResolutionState != GrpcResolutionState.Success)
                {
                    _channel.LastResolutionState = GrpcResolutionState.Success;
                }
                _channel.NameResolverRefreshBackoffPolicy = null;
                var effectiveServiceConfig = value.ServiceConfig.Config ?? new object();
                var resolvedAddresses = new GrpcResolvedAddresses(value.HostsAddresses, effectiveServiceConfig, value.Attributes);
                if (_helper != _channel.Helper) return;
                Status handleResult = _channel.TryHandleResolvedAddresses(resolvedAddresses);
                if (handleResult.StatusCode != StatusCode.OK)
                {
                    HandleErrorInSyncContext(handleResult);
                }
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
                _logger.LogDebug("Name resolution error received.");
                HandleErrorInSyncContext(error);
            });
        }

        private void HandleErrorInSyncContext(Status error)
        {
            if (_channel.LastResolutionState != GrpcResolutionState.Error)
            {
                _channel.LastResolutionState = GrpcResolutionState.Error;
            }
            // Call LB only if it's not shutdown. If LB is shutdown, lbHelper won't match. This is required for idle mode implementation.
            if (_helper != _channel.Helper) return;
            _channel.HandleNameResolutionError(error);
            ScheduleExponentialBackOffInSyncContext();
        }

        /// Think about moving this method to <see cref="GrpcChannel"/>.
        private void ScheduleExponentialBackOffInSyncContext()
        {
            _channel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            if (_channel.NameResolverRefreshSchedule?.IsPending() ?? false)
            {
                // The name resolver may invoke onError multiple times, but we only want to
                // schedule one backoff attempt.
                return;
            }
            if (_channel.NameResolverRefreshBackoffPolicy == null)
            {
                _channel.NameResolverRefreshBackoffPolicy = _channel.BackoffPolicyProvider.CreateBackoffPolicy();
            }
            var delay = _channel.NameResolverRefreshBackoffPolicy.NextBackoff();
            _logger.LogDebug($"Scheduling name resolution backoff for {delay}.");
            _channel.NameResolverRefreshSchedule = _helper.GetSynchronizationContext().Schedule(() =>
            {
                _channel.NameResolverRefreshSchedule = null;
                _channel.RefreshNameResolution();
            }, delay);
        }
    }
}
