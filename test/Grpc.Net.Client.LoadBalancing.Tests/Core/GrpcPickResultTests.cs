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

using Grpc.Core;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcPickResultTests
    {
        [Fact]
        public void ForSampleChannel_UsingPickResultWithSubChannel_ReturnsWrappedChannel()
        {
            // Arrange
            var subChannel = new GrpcSubChannelFake(new Uri("http://10.1.5.210:80"), GrpcAttributes.Empty);

            // Act
            var pickResult = GrpcPickResult.WithSubChannel(subChannel);

            // Assert
            Assert.Equal(subChannel, pickResult.SubChannel);
            Assert.Equal(Status.DefaultSuccess, pickResult.Status);
            Assert.False(pickResult.Drop);
        }

        [Fact]
        public void ForNoResult_UsingPickResultWithNoResult_ReturnsNoResultAndNullChannel()
        {
            // Arrange
            // Act
            var pickResult = GrpcPickResult.WithNoResult();

            // Assert
            Assert.Null(pickResult.SubChannel);
            Assert.Equal(Status.DefaultSuccess, pickResult.Status);
            Assert.False(pickResult.Drop);
        }

        [Fact]
        public void ForErrorStatus_UsingPickResultWithError_ReturnsErrorAndNullChannel()
        {
            // Arrange
            var status = new Status(StatusCode.Unavailable, "for test");

            // Act
            var pickResult = GrpcPickResult.WithError(status);

            // Assert
            Assert.Null(pickResult.SubChannel);
            Assert.Equal(status, pickResult.Status);
            Assert.False(pickResult.Drop);
        }

        [Fact]
        public void ForErrorStatus_UsingPickResultWithDrop_ReturnsWrappedChannel()
        {
            // Arrange
            var status = new Status(StatusCode.Unavailable, "for test");

            // Act
            var pickResult = GrpcPickResult.WithDrop(status);

            // Assert
            Assert.Null(pickResult.SubChannel);
            Assert.Equal(status, pickResult.Status);
            Assert.True(pickResult.Drop);
        }

        [Fact]
        public void ForNoResult_UsingIsWithNoResult_ReturnsTrue()
        {
            // Arrange
            var pickResult = GrpcPickResult.WithNoResult();

            // Act
            var result = GrpcPickResult.IsWithNoResult(pickResult);

            // Assert
            Assert.True(result);
        }
    }
}
