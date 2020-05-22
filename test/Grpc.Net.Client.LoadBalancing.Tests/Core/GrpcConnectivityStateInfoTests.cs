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
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcConnectivityStateInfoTests
    {
        [Fact]
        public void ForNonErrorIdle_UseGrpcConnectivityStateInfo_ReturnOkIdle()
        {
            // Arrange
            // Act
            var info = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.IDLE);

            // Assert
            Assert.Equal(GrpcConnectivityState.IDLE, info.State);
            Assert.Equal(StatusCode.OK, info.Status.StatusCode);
        }

        [Fact]
        public void ForNonErrorInvalid_UseGrpcConnectivityStateInfo_Throw()
        {
            // Arrange
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = GrpcConnectivityStateInfo.ForNonError(GrpcConnectivityState.TRANSIENT_FAILURE);
            });
        }

        [Fact]
        public void ForTransientFailure_UseGrpcConnectivityStateInfo_ReturnUnavailableTransientError()
        {
            // Arrange
            // Act
            var info = GrpcConnectivityStateInfo.ForTransientFailure(new Status(StatusCode.Unavailable, string.Empty));

            // Assert
            Assert.Equal(GrpcConnectivityState.TRANSIENT_FAILURE, info.State);
            Assert.Equal(StatusCode.Unavailable, info.Status.StatusCode);
        }

        [Fact]
        public void ForTransientFailureInvalid_UseGrpcConnectivityStateInfo_Throw()
        {
            // Arrange
            // Act
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = GrpcConnectivityStateInfo.ForTransientFailure(Status.DefaultSuccess);
            });
        }
    }
}
