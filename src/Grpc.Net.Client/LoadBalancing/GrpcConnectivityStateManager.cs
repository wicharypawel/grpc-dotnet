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
