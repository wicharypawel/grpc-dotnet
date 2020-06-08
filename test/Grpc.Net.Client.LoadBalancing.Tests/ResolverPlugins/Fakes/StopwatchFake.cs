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

using Grpc.Net.Client.Internal;
using System;
using System.Threading;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes
{
    internal sealed class StopwatchFake : IStopwatch
    {
        private int _cleanElapsedCounter = 0;
        private readonly Func<TimeSpan> _elapsed;

        public StopwatchFake(Func<TimeSpan> elapsed)
        {
            IsRunning = false;
            _elapsed = elapsed ?? throw new ArgumentNullException(nameof(elapsed));
        }

        internal int CleanElapsedCounter => _cleanElapsedCounter;

        public TimeSpan Elapsed => _elapsed();

        public long ElapsedMilliseconds => (long)_elapsed().TotalMilliseconds;

        public long ElapsedTicks => _elapsed().Ticks;

        public bool IsRunning { get; private set; }

        public void Reset()
        {
            Stop();
            Interlocked.Increment(ref _cleanElapsedCounter);
        }

        public void Restart()
        {
            Reset();
            Start();
        }

        public void Start()
        {
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }
    }
}
