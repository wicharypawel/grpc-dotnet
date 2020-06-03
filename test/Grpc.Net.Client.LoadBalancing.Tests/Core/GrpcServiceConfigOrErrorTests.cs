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
    public sealed class GrpcServiceConfigOrErrorTests
    {
        [Fact]
        public void ForNewInstanceFromConfig_UseGrpcServiceConfigOrError_VerifyValues()
        {
            // Arrange
            var config = new object();

            // Act
            var configOrError = GrpcServiceConfigOrError.FromConfig(config);

            // Assert
            Assert.Equal(config, configOrError.Config);
            Assert.Null(configOrError.Status);
        }

        [Fact]
        public void ForNewInstanceFromError_UseGrpcServiceConfigOrError_VerifyValues()
        {
            // Arrange
            var status = new Status(StatusCode.Internal, "test bug");

            // Act
            var configOrError = GrpcServiceConfigOrError.FromError(status);

            // Assert
            Assert.Null(configOrError.Config);
            Assert.Equal(status, configOrError.Status);
        }

        [Fact]
        public void ForNewInstanceFromErrorForOkStatus_UseGrpcServiceConfigOrError_ThrowArgumentException()
        {
            // Arrange
            var status = new Status(StatusCode.OK, string.Empty);

            // Act
            // Assert
            var error = Assert.Throws<ArgumentException>(() => 
            {
                var _ = GrpcServiceConfigOrError.FromError(status);
            });
            Assert.Equal("Can not use OK status.", error.Message);
        }
    }
}
