using System;

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Determines how long to wait before doing some action (typically a retry, or a reconnect).
    /// </summary>
    internal interface IGrpcBackoffPolicy
    {
        /// <summary>
        /// Returns ascending values of waiting time. In order to start from initial value, create new policy.
        /// </summary>
        /// <returns>Wait time.</returns>
        public TimeSpan NextBackoff();
    }
}
