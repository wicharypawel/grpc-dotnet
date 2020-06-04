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

using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Fakes;
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.Policies
{
    public sealed class RoundRobinPolicyTests
    {
        [Fact]
        public void ForEmptyResolutionPassed_UseRoundRobinPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new RoundRobinPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(0);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(resolvedAddresses);
            });
            Assert.Equal("resolvedAddresses must contain at least one address.", exception.Message);
        }

        [Fact]
        public void ForCanHandleEmptyAddressList_UseRoundRobinPolicy_VerifyFalse()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new RoundRobinPolicy(helper);

            // Act
            // Assert
            Assert.False(policy.CanHandleEmptyAddressListFromNameResolution());
        }

        [Fact]
        public void ForResolutionResults_UseRoundRobinPolicy_CreateAmmountSubChannels()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new RoundRobinPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(4);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            policy.HandleResolvedAddresses(resolvedAddresses);
            var subChannels = policy.SubChannels.Values;

            // Assert
            Assert.Equal(4, subChannels.Count);
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
        }
    }
}
