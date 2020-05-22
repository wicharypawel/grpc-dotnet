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
using System.Threading;

namespace Grpc.Net.Client.Internal
{
    internal interface ITimer : IDisposable
    {
        /// <summary>
        /// Starts a timer with specified callback and settings. Startup can be called once.
        /// </summary>
        /// <param name="callback">A delegate representing a method to be executed.</param>
        /// <param name="state">An object containing information to be used by the callback method, or null.</param>
        /// <param name="dueTime">
        /// The amount of time to delay before the callback parameter invokes its methods. 
        /// Specify negative one (-1) milliseconds to prevent the timer from starting. Specify
        /// zero (0) to start the timer immediately.
        /// </param>
        /// <param name="period">
        /// The time interval between invocations of the methods referenced by callback.
        /// Specify negative one (-1) milliseconds to disable periodic signaling.
        /// </param>
        public void Start(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period);

        /// <summary>
        /// Changes the start time and the interval between method invocations for a timer,
        /// using System.TimeSpan values to measure time intervals.
        /// </summary>
        /// <param name="dueTime">
        /// A System.TimeSpan representing the amount of time to delay before invoking the
        /// callback method specified when the timer was started. Specify
        /// negative one (-1) milliseconds to prevent the timer from restarting. Specify
        /// zero (0) to restart the timer immediately. 
        /// </param>
        /// <param name="period">
        /// The time interval between invocations of the methods referenced by callback.
        /// Specify negative one (-1) milliseconds to disable periodic signaling.
        /// </param>
        /// <returns>true if the timer was successfully updated; otherwise, false.</returns>
        public bool Change(TimeSpan dueTime, TimeSpan period);
    }
}
