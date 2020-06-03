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

using System.Collections.Generic;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Core
{
    public sealed class GrpcNameResolutionResultTests
    {
        [Fact]
        public void ForNewInstance_UseGrpcNameResolutionResult_VerifyValues()
        {
            // Arrange
            var hostsAddresses = new List<GrpcHostAddress>();
            var serviceConfig = GrpcServiceConfigOrError.FromConfig(new object());
            var attributes = GrpcAttributes.Empty;

            // Act
            var resolutionResult = new GrpcNameResolutionResult(hostsAddresses, serviceConfig, attributes);

            // Assert
            Assert.Equal(hostsAddresses, resolutionResult.HostsAddresses);
            Assert.Equal(serviceConfig, resolutionResult.ServiceConfig);
            Assert.Equal(attributes, resolutionResult.Attributes);
        }
    }
}
