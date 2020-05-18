using System;

namespace Grpc.Net.Client.LoadBalancing
{
    internal sealed class GrpcExponentialBackoffPolicyProvider : IGrpcBackoffPolicyProvider
    {
        /// <summary>
        /// Builds Retry Policy for Transport reconnection. Initial parameters from
        /// https://github.com/grpc/grpc/blob/master/doc/connection-backoff.md
        /// </summary>
        public IGrpcBackoffPolicy CreateBackoffPolicy()
        {
            var initialBackoff = TimeSpan.FromSeconds(1); // INITIAL_BACKOFF = 1 second
            var maxBackoff = TimeSpan.FromMinutes(2); // MAX_BACKOFF = 120 seconds
            var multiplier = 1.6; // MULTIPLIER = 1.6
            var jitter = 0.2; // JITTER = 0.2
            return new GrpcExponentialBackoffPolicy(new SystemRandom(), initialBackoff, maxBackoff, multiplier, jitter);
        }

        private sealed class SystemRandom : GrpcExponentialBackoffPolicy.IRandom
        {
            private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());

            public double NextDouble()
            {
                return _random.NextDouble();
            }
        }
    }
}
