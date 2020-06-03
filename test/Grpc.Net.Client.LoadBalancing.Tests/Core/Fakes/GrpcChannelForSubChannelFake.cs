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

using Grpc.Net.Client.LoadBalancing.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes
{
    internal sealed class GrpcChannelForSubChannelFake : GrpcSubChannel.IGrpcChannel
    {
        private readonly Action<GrpcConnectivityStateInfo> _handleInternalSubchannelState;

        private GrpcChannelForSubChannelFake(ILoggerFactory loggerFactory,
            GrpcSynchronizationContext synchronizationContext, 
            IGrpcBackoffPolicyProvider backoffPolicyProvider,
            Action<GrpcConnectivityStateInfo> handleInternalSubchannelState)
        {
            LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            SyncContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
            BackoffPolicyProvider = backoffPolicyProvider ?? throw new ArgumentNullException(nameof(backoffPolicyProvider));
            _handleInternalSubchannelState = handleInternalSubchannelState ?? throw new ArgumentNullException(nameof(handleInternalSubchannelState));
        }

        public ILoggerFactory LoggerFactory { get; }

        public IGrpcBackoffPolicyProvider BackoffPolicyProvider { get; }

        public GrpcSynchronizationContext SyncContext { get; }

        public void HandleInternalSubchannelState(GrpcConnectivityStateInfo newState)
        {
            _handleInternalSubchannelState(newState);
        }

        public static GrpcChannelForSubChannelFake Get(IGrpcBackoffPolicyProvider backoffPolicyProvider)
        {
            return new GrpcChannelForSubChannelFake(NullLoggerFactory.Instance,
                new GrpcSynchronizationContext((exception) => { }),
                backoffPolicyProvider,
                (stateInfo) => { });
        }

        public static GrpcChannelForSubChannelFake Get()
        {
            return Get(new GrpcSynchronizationContext((exception) => { }));
        }

        public static GrpcChannelForSubChannelFake Get(GrpcSynchronizationContext synchronizationContext)
        {
            return Get(synchronizationContext, (stateInfo) => { });
        }

        public static GrpcChannelForSubChannelFake Get(GrpcSynchronizationContext synchronizationContext, 
            Action<GrpcConnectivityStateInfo> handleInternalSubchannelState)
        {
            
            return new GrpcChannelForSubChannelFake(NullLoggerFactory.Instance, 
                synchronizationContext, 
                new GrpcExponentialBackoffPolicyProvider(), 
                handleInternalSubchannelState);
        }
    }
}
