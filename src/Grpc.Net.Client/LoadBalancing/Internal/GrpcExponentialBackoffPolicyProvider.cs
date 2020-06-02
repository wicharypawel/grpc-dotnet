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

namespace Grpc.Net.Client.LoadBalancing.Internal
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
