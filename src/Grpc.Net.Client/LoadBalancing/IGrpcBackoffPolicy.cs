using System;

namespace Grpc.Net.Client.LoadBalancing
{
    internal interface IGrpcBackoffPolicy
    {
        public TimeSpan NextBackoff();
    }
}
