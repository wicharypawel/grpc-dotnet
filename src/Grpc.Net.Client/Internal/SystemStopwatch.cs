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
using System.Diagnostics;

namespace Grpc.Net.Client.Internal
{
    internal sealed class SystemStopwatch : IStopwatch
    {
        private readonly Stopwatch _stopwatch;

        public SystemStopwatch()
        {
            _stopwatch = new Stopwatch();
        }

        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        public long ElapsedTicks => _stopwatch.ElapsedTicks;

        public bool IsRunning => _stopwatch.IsRunning;

        public void Reset()
        {
            _stopwatch.Reset();
        }

        public void Restart()
        {
            _stopwatch.Restart();
        }

        public void Start()
        {
            _stopwatch.Start();
        }

        public void Stop()
        {
            _stopwatch.Stop();
        }
    }
}
