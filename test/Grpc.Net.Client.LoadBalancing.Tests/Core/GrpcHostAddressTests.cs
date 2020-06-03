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

using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcHostAddressTests
    {
        [Fact]
        public void ForNewInstance_UseGrpcHostAddress_VerifyValues()
        {
            // Arrange
            var hostAddress = new GrpcHostAddress("102.1.2.5", 443);

            // Act
            // Assert
            Assert.Equal("102.1.2.5", hostAddress.Host);
            Assert.Equal(443, hostAddress.Port);
        }

        [Fact]
        public void ForNewInstanceWithEmptyPort_UseGrpcHostAddress_VerifyEmptyPort()
        {
            // Arrange
            var hostAddress = new GrpcHostAddress("102.1.2.5", null);

            // Act
            // Assert
            Assert.Equal("102.1.2.5", hostAddress.Host);
            Assert.Null(hostAddress.Port);
        }
    }
}
