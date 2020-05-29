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
using Grpc.Net.Client.LoadBalancing;
using System;
using System.Collections.Generic;

namespace Grpc.Net.Client.Internal
{
    internal sealed class GrpcDelayedClientTransport : IDisposable
    {
        private readonly object _lockObject = new object();
        private readonly IGrpcExecutor _executor;
        private readonly GrpcSynchronizationContext _synchronizationContext;
        private HashSet<PendingCall> _pendingCalls = new HashSet<PendingCall>();
        private Status? _shutdownStatus = null;
        private IGrpcSubChannelPicker? _lastPicker;
        private long _lastPickerVersion;

        public GrpcDelayedClientTransport(IGrpcExecutor executor, GrpcSynchronizationContext synchronizationContext)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
        }

        public void BufforPendingCall(Action<GrpcPickResult> callDelegate, IGrpcPickSubchannelArgs pickSubchannelArgs)
        {
            if (callDelegate == null) throw new ArgumentNullException(nameof(callDelegate));
            if (pickSubchannelArgs == null) throw new ArgumentNullException(nameof(pickSubchannelArgs));
            IGrpcSubChannelPicker? picker = null;
            long pickerVersion = -1;
            while (true)
            {
                lock (_lockObject)
                {
                    if (_shutdownStatus != null)
                    {
                        Status errorStatus = _shutdownStatus.Value;
                        _executor.Execute(() => callDelegate(GrpcPickResult.WithError(errorStatus)));
                        return;
                    }
                    if (_lastPicker == null)
                    {
                        _pendingCalls.Add(new PendingCall(callDelegate, pickSubchannelArgs));
                        return;
                    }
                    if (picker != null && pickerVersion == _lastPickerVersion)
                    {
                        _pendingCalls.Add(new PendingCall(callDelegate, pickSubchannelArgs));
                        return;
                    }
                    picker = _lastPicker;
                    pickerVersion = _lastPickerVersion;
                }
                var pickResult = picker.GetNextSubChannel(pickSubchannelArgs);
                if (IsTransportReadyOrError(pickResult))
                {
                    _executor.Execute(() => callDelegate(pickResult));
                }
            }
        }             

        // This method MUST NOT be called concurrently with itself.
        public void Reprocess(IGrpcSubChannelPicker? picker)
        {
            var toProcess = new List<PendingCall>();
            lock (_lockObject)
            {
                _lastPicker = picker;
                _lastPickerVersion++;
                if (picker == null || !HasPendingCalls())
                {
                    return;
                }
                toProcess = new List<PendingCall>(_pendingCalls);
            }
            var toRemove = new List<PendingCall>();
            foreach (var call in toProcess)
            {
                var pickResult = picker.GetNextSubChannel(call.PickSubchannelArgs);
                if (IsTransportReadyOrError(pickResult))
                {
                    _executor.Execute(() => call.CallDelegate(pickResult));
                    toRemove.Add(call);
                }
                // else: stay pending
            }
            lock (_lockObject)
            {
                if (!HasPendingCalls()) // Shutdown can be called in between locks
                {
                    return;
                }
                foreach (var call in toRemove)
                {
                    _pendingCalls.Remove(call);
                }
                if (!HasPendingCalls()) //Because delayed transport is long-lived, we take this opportunity to down-size the collection
                {
                    _pendingCalls = new HashSet<PendingCall>();
                }
            }
        }

        public void ShutdownNow(Status status)
        {
            lock (_lockObject)
            {
                if (_shutdownStatus != null)
                {
                    return;
                }
                _shutdownStatus = status;
                _pendingCalls = new HashSet<PendingCall>();
            }
        }

        public void Dispose()
        {
            ShutdownNow(new Status(StatusCode.Unavailable, "Dispose"));
        }

        private bool IsTransportReadyOrError(GrpcPickResult pickResult)
        {
            if (pickResult.Status.StatusCode != StatusCode.OK)
            {
                return true;
            }
            if (pickResult.SubChannel != null && pickResult.Status.StatusCode == StatusCode.OK)
            {
                return true;
            }
            return false;
        }

        private bool HasPendingCalls()
        {
            lock (_lockObject)
            {
                return _pendingCalls.Count != 0;
            }
        }

        /// <summary>
        /// The method can only be used for testing purposes.
        /// </summary>
        internal int GetPendingCallsCount()
        {
            lock (_lockObject)
            {
                return _pendingCalls.Count;
            }
        }

        private sealed class PendingCall
        {
            public Action<GrpcPickResult> CallDelegate { get; }
            public IGrpcPickSubchannelArgs PickSubchannelArgs { get; }

            public PendingCall(Action<GrpcPickResult> callDelegate, IGrpcPickSubchannelArgs pickSubchannelArgs)
            {
                CallDelegate = callDelegate;
                PickSubchannelArgs = pickSubchannelArgs;
            }
        }
    }
}
