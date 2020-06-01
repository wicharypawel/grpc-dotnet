﻿#region Copyright notice and license

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

using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcHelper : IGrpcHelper
    {
        private readonly GrpcChannel _channel;
        private readonly ILogger _logger;

        public GrpcHelper(GrpcChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _logger = channel.LoggerFactory.CreateLogger<GrpcHelper>();
        }

        public IGrpcSubChannel CreateSubChannel(CreateSubchannelArgs arguments)
        {
            _channel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            return new GrpcSubChannel(_channel, arguments, this);
        }

        public void UpdateBalancingState(GrpcConnectivityState newState, IGrpcSubChannelPicker newPicker)
        {
            _channel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            if (newPicker == null)
            {
                throw new ArgumentNullException(nameof(newPicker));
            }
            _channel.SyncContext.Execute(() => 
            { 
                if (this != _channel.Helper)
                {
                    return;
                }
                _channel.UpdateSubchannelPicker(newPicker);
                // It's not appropriate to report SHUTDOWN state from lb.
                // Ignore the case of newState == SHUTDOWN for now.
                if (newState != GrpcConnectivityState.SHUTDOWN)
                {
                    _logger.LogDebug($"Entering {newState} state with picker: {newPicker.GetType().Name}");
                    _channel.ChannelStateManager.SetState(newState);
                }
            });
        }

        public void RefreshNameResolution()
        {
            _channel.SyncContext.ThrowIfNotInThisSynchronizationContext();
            _channel.SyncContext.Execute(() => { _channel.RefreshAndResetNameResolution(); });
        }

        public GrpcSynchronizationContext GetSynchronizationContext()
        {
            return _channel.SyncContext;
        }
    }
}
