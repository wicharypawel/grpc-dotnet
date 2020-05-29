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
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class SystemTimerTests
    {
        [Fact]
        public void ForNonPeriodicTask_UsingSystemTimer_VerifyExecutedOnce()
        {
            // Arrange
            using ITimer timer = new SystemTimer();
            var i = 0;

            // Act
            timer.Start((state) => { Interlocked.Increment(ref i); }, null, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(-1));
            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();

            // Assert
            Assert.Equal(1, i);
        }

        [Fact]
        public void ForPeriodicTask_UsingSystemTimer_VerifyExecutedMoreThanOnce()
        {
            // Arrange
            using ITimer timer = new SystemTimer();
            var i = 0;

            // Act
            timer.Start((state) => { Interlocked.Increment(ref i); }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(10));
            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
            timer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));

            // Assert
            Assert.True(i > 1);
        }
    }
}
