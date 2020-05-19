using System;

namespace Grpc.Net.Client.LoadBalancing.Internal
{
    internal sealed class BaseSubchannelStateObserver : IGrpcSubchannelStateObserver
    {
        private readonly Action<GrpcConnectivityStateInfo> _action;

        public BaseSubchannelStateObserver(Action<GrpcConnectivityStateInfo> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void OnNext(GrpcConnectivityStateInfo value)
        {
            _action(value);
        }
    }
}
