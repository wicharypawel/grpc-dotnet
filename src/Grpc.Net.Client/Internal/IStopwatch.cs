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

namespace Grpc.Net.Client.Internal
{
    internal interface IStopwatch
    {
        /// <summary>
        /// Gets the total elapsed time measured by the current instance.
        /// </summary>
        public TimeSpan Elapsed { get; }

        /// <summary>
        /// Gets the total elapsed time measured by the current instance, in milliseconds.
        /// </summary>
        public long ElapsedMilliseconds { get; }

        /// <summary>
        /// Gets the total elapsed time measured by the current instance, in timer ticks.
        /// </summary>
        public long ElapsedTicks { get; }

        /// <summary>
        /// Gets a value indicating whether the stopwatch timer is running.
        /// 
        /// true if the stopwatch instance is currently running and measuring
        /// elapsed time for an interval; otherwise, false.
        /// </summary>
        public bool IsRunning { get; }

        /// <summary>
        /// Stops time interval measurement and resets the elapsed time to zero.
        /// </summary>
        public void Reset();

        /// <summary>
        /// Stops time interval measurement, resets the elapsed time to zero, and starts
        /// measuring elapsed time.
        /// </summary>
        public void Restart();

        /// <summary>
        /// Starts, or resumes, measuring elapsed time for an interval.
        /// </summary>
        public void Start();

        /// <summary>
        /// Stops measuring elapsed time for an interval.
        /// </summary>
        public void Stop();
    }
}
