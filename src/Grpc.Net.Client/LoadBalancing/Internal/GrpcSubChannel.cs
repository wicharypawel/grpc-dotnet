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
using Grpc.Net.Client.Internal;
using Microsoft.Extensions.Logging;
using System;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class GrpcSubChannel : IGrpcSubChannel
    {
        private readonly IGrpcChannel _channel;
        private readonly GrpcSynchronizationContext _synchronizationContext;
        private readonly ILogger _logger;
        private GrpcConnectivityStateInfo _stateInfo = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.IDLE);
        private IGrpcSubchannelStateObserver? _observer = null;
        private bool _started = false;
        private bool _shutdown = false;
        private IGrpcBackoffPolicy? _backoffPolicy = null;
        private GrpcSynchronizationContext.ScheduledHandle? _reconnectTaskSchedule = null;

        public Uri Address { get; private set; }

        public GrpcAttributes Attributes { get; private set; }

        public GrpcSubChannel(IGrpcChannel channel, CreateSubchannelArgs arguments)
        {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            Address = arguments.Address;
            Attributes = arguments.Attributes;
            _channel = channel;
            _synchronizationContext = channel.SyncContext ?? throw new ArgumentNullException(nameof(channel.SyncContext));
            _logger = channel.LoggerFactory.CreateLogger(nameof(GrpcSubChannel) + arguments.Address.ToString());
        }

        public void Start(IGrpcSubchannelStateObserver observer)
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (_started) throw new InvalidOperationException("Already started.");
            if (_shutdown) throw new InvalidOperationException("Already shutdown.");
            _started = true;
            _logger.LogDebug("Start GrpcSubChannel.");
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public void Shutdown()
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (_shutdown) return;
            _shutdown = true;
            _logger.LogDebug("Shutdown GrpcSubChannel.");
            SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.SHUTDOWN));
        }

        public void RequestConnection()
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (!_started) throw new InvalidOperationException("Not started.");
            _logger.LogDebug("RequestConnection GrpcSubChannel.");
            #region HTTP_CLIENT_MISSING_MONITORING_WORKAROUND
            _synchronizationContext.Execute(() =>
            {
                if (_stateInfo.State == GrpcConnectivityState.IDLE || _stateInfo.State == GrpcConnectivityState.TRANSIENT_FAILURE)
                {
                    SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.CONNECTING));
                }
            });
            _synchronizationContext.Execute(() =>
            {
                if (_stateInfo.State == GrpcConnectivityState.CONNECTING)
                {
                    SetState(GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.READY));
                }
            });
            #endregion
        }

        private void SetState(GrpcConnectivityStateInfo newState)
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }
            if (_stateInfo.State != newState.State)
            {
                _logger.LogDebug($"Change state from {_stateInfo.State} to {newState.State}");
                if (_stateInfo.State == GrpcConnectivityState.SHUTDOWN)
                {
                    throw new InvalidOperationException("Cannot transition out of SHUTDOWN state.");
                }
                _stateInfo = newState;
                _channel.HandleInternalSubchannelState(newState);
                _observer?.OnNext(newState);
            }
        }

        #region HTTP_CLIENT_MISSING_MONITORING_WORKAROUND

        /// <summary>
        /// This property sets executor responsible for handling background jobs triggered on workaround. By default it is a <see cref="TaskFactoryExecutor"/>.
        /// </summary>
        internal IGrpcExecutor Executor { private get; set; } = TaskFactoryExecutor.Instance;

        /// <summary>
        /// This method allows for the emulation of the missing connectivity state 
        /// monitoring in HttpClient. Call this method whenever the <see cref="GrpcSubChannel"/>
        /// is faulty.
        /// 
        /// This method should return quickly.
        /// </summary>
        internal void TriggerSubChannelFailure(Status status)
        {
            Executor.Execute(() => _synchronizationContext.Execute(() => TriggerSubChannelFailureCore(status)));
        }

        /// <summary>
        /// This method allows for the emulation of the missing connectivity state 
        /// monitoring in HttpClient. Call this method whenever the <see cref="GrpcSubChannel"/>
        /// is working fine.
        /// 
        /// This method must be blazingly fast as this is executed every successful call. 
        /// 
        /// Workaround assumes that <see cref="GrpcSubChannel"/> will be in READY state 
        /// when executing call, there is no point in checking status here. If backoffPolicy 
        /// is set it may inform that subchannel has been in TRANSIENT_FAILURE state before.
        /// </summary>
        internal void TriggerSubChannelSuccess()
        {
            if (_backoffPolicy == null)
            {
                // subchannel was "healthy" and it's "healthy" now so skip as fast as possible to reduce overhead
                return;
            }
            Executor.Execute(() => _synchronizationContext.Execute(() => TriggerSubChannelSuccessCore()));
        }

        private void TriggerSubChannelFailureCore(Status status)
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (_stateInfo.State == GrpcConnectivityState.SHUTDOWN)
            {
                return;
            }
            if (_backoffPolicy == null)
            {
                _backoffPolicy = _channel.BackoffPolicyProvider.CreateBackoffPolicy();
            }
            SetState(GrpcConnectivityStateInfo.ForTransientFailure(status));
            var delay = _backoffPolicy.NextBackoff();
            _logger.LogDebug($"Scheduling subChannel reconnect backoff for {delay}.");
            _reconnectTaskSchedule = _synchronizationContext.Schedule(() => { _reconnectTaskSchedule = null; RequestConnection(); }, delay);
        }

        private void TriggerSubChannelSuccessCore()
        {
            _synchronizationContext.ThrowIfNotInThisSynchronizationContext();
            if (_stateInfo.State == GrpcConnectivityState.SHUTDOWN)
            {
                return;
            }
            _backoffPolicy = null;
        }

        #endregion
    }
}
