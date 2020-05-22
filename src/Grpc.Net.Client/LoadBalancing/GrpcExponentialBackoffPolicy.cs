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

namespace Grpc.Net.Client.LoadBalancing
{
    /// <summary>
    /// Retry Policy for Transport reconnection. Backoff Algorithm from
    /// https://github.com/grpc/grpc/blob/master/doc/connection-backoff.md
    /// </summary>
    internal sealed class GrpcExponentialBackoffPolicy : IGrpcBackoffPolicy
    {
        private readonly object _lockObject = new object();
        private readonly IRandom _random;
        private readonly long _initialBackoffTicks;
        private readonly long _maxBackoffTicks;
        private readonly double _multiplier;
        private readonly double _jitter;
        private long _nextBackoffTicks;

        public GrpcExponentialBackoffPolicy(IRandom random, TimeSpan initialBackoff, TimeSpan maxBackoff, 
            double multiplier, double jitter)
        {
            _random = random;
            _initialBackoffTicks = initialBackoff.Ticks;
            _maxBackoffTicks = maxBackoff.Ticks;
            _multiplier = multiplier;
            _jitter = jitter;
            _nextBackoffTicks = _initialBackoffTicks;
        }

        public TimeSpan NextBackoff()
        {
            lock (_lockObject)
            {
                return NextBackoffCore();
            }
        }

        private TimeSpan NextBackoffCore()
        {
            long currentBackoff = _nextBackoffTicks;
            _nextBackoffTicks = Math.Min((long)(currentBackoff * _multiplier), _maxBackoffTicks);
            return new TimeSpan(currentBackoff + UniformRandom(-_jitter * currentBackoff, _jitter * currentBackoff));
        }

        private long UniformRandom(double low, double high)
        {
            if (high < low)
            {
                throw new InvalidOperationException("High is less then low.");
            }
            double mag = high - low;
            return (long)(_random.NextDouble() * mag + low);
        }

        public interface IRandom
        {
            public double NextDouble();
        }
    }
}
