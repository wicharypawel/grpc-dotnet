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
    /// <summary>
    /// This is an internal interface created for testing purposes.
    /// </summary>
    internal interface IGrpcChannel
    {
        public Uri Address { get; }
        public ILoggerFactory LoggerFactory { get; }
        public IGrpcHelper Helper { get; }
        public GrpcResolutionState LastResolutionState { get; set; }
        public GrpcSynchronizationContext.ScheduledHandle? NameResolverRefreshSchedule { get; set; }
        public IGrpcBackoffPolicy? NameResolverRefreshBackoffPolicy { get; set; }
        public GrpcConnectivityStateManager ChannelStateManager { get; }
        public IGrpcBackoffPolicyProvider BackoffPolicyProvider { get; }
        public GrpcSynchronizationContext SyncContext { get; }
        public bool IsShutdown { get; }
        public Status TryHandleResolvedAddresses(GrpcResolvedAddresses resolvedAddresses);
        public void HandleNameResolutionError(Status status);
        public void UpdateSubchannelPicker(IGrpcSubChannelPicker newPicker);
        public void RefreshNameResolution();
        public void RefreshAndResetNameResolution();
        public void HandleInternalSubchannelState(GrpcConnectivityStateInfo newState);
    }

    internal sealed class DelegatingGrpcChannel : IGrpcChannel
    {
        private readonly GrpcChannel _delegateChannel;

        public DelegatingGrpcChannel(GrpcChannel delegateChannel)
        {
            _delegateChannel = delegateChannel ?? throw new ArgumentNullException(nameof(delegateChannel));
        }

        public Uri Address => _delegateChannel.Address;

        public ILoggerFactory LoggerFactory => _delegateChannel.LoggerFactory;

        public IGrpcHelper Helper => _delegateChannel.Helper;

        public GrpcResolutionState LastResolutionState { get => _delegateChannel.LastResolutionState; set => _delegateChannel.LastResolutionState = value; }

        public GrpcSynchronizationContext.ScheduledHandle? NameResolverRefreshSchedule { get => _delegateChannel.NameResolverRefreshSchedule; set => _delegateChannel.NameResolverRefreshSchedule = value; }
        
        public IGrpcBackoffPolicy? NameResolverRefreshBackoffPolicy { get => _delegateChannel.NameResolverRefreshBackoffPolicy; set => _delegateChannel.NameResolverRefreshBackoffPolicy = value; }

        public GrpcConnectivityStateManager ChannelStateManager => _delegateChannel.ChannelStateManager;

        public IGrpcBackoffPolicyProvider BackoffPolicyProvider => _delegateChannel.BackoffPolicyProvider;

        public GrpcSynchronizationContext SyncContext => _delegateChannel.SyncContext;

        public bool IsShutdown => _delegateChannel.IsShutdown;

        public Status TryHandleResolvedAddresses(GrpcResolvedAddresses resolvedAddresses)
        {
            return _delegateChannel.TryHandleResolvedAddresses(resolvedAddresses);
        }

        public void HandleNameResolutionError(Status status)
        {
            _delegateChannel.HandleNameResolutionError(status);
        }

        public void UpdateSubchannelPicker(IGrpcSubChannelPicker newPicker)
        {
            _delegateChannel.UpdateSubchannelPicker(newPicker);
        }

        public void RefreshNameResolution()
        {
            _delegateChannel.RefreshNameResolution();
        }

        public void RefreshAndResetNameResolution()
        {
            _delegateChannel.RefreshAndResetNameResolution();
        }

        public void HandleInternalSubchannelState(GrpcConnectivityStateInfo newState)
        {
            _delegateChannel.HandleInternalSubchannelState(newState);
        }
    }
}
