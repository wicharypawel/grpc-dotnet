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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Manages connectivity states of the channel. Used for <see cref="GetState"/> to read the
    /// current state of the channel, for <see cref="NotifyWhenStateChanged"/> to add
    /// one-time listeners to state change events, and for update the state <see cref="SetState"/>.
    /// </summary>
    internal sealed class GrpcConnectivityStateManager
    {
        private List<Listener> _listeners = new List<Listener>();
        private GrpcConnectivityState _state = GrpcConnectivityState.IDLE;

        public void NotifyWhenStateChanged(Action callback, GrpcConnectivityState sourceState)
        {
            var listener = new Listener(callback ?? throw new ArgumentNullException(nameof(callback)));
            if (_state != sourceState)
            {
                listener.RunCallback();
            }
            else
            {
                _listeners.Add(listener);
            }
        }

        public void SetState(GrpcConnectivityState newState)
        {
            if (_state != newState && _state != GrpcConnectivityState.SHUTDOWN)
            {
                _state = newState;
                if (_listeners.Count == 0)
                {
                    return;
                }
                var listeners = Interlocked.Exchange(ref _listeners, new List<Listener>());
                foreach (var listener in listeners)
                {
                    listener.RunCallback();
                }
            }
        }

        public GrpcConnectivityState GetState()
        {
            return _state;
        }

        private sealed class Listener
        {
            private readonly Action _callback;

            public Listener(Action callback)
            {
                _callback = callback;
            }

            public void RunCallback()
            {
                Task.Factory.StartNew(_callback);
            }
        }
    }
}
