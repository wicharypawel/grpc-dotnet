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
        public void ForEmptyServiceName_UseRoundRobinPolicy_ThrowArgumentException()
        {
            // Arrange
            var helper = new HelperFake();
            using var policy = new RoundRobinPolicy(helper);
            var hostsAddresses = GrpcHostAddressFactory.GetNameResolution(2);
            var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("pick_first"));
            var resolvedAddresses = new GrpcResolvedAddresses(hostsAddresses, config, GrpcAttributes.Empty);

            // Act
            // Assert
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(resolvedAddresses, "", false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
            exception = Assert.Throws<ArgumentException>(() =>
            {
                policy.HandleResolvedAddresses(resolvedAddresses, string.Empty, false);
            });
            Assert.Equal("serviceName not defined.", exception.Message);
        }

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
                policy.HandleResolvedAddresses(resolvedAddresses, "sample-service.contoso.com", false);
            });
            Assert.Equal("resolvedAddresses must contain at least one address.", exception.Message);
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
            policy.HandleResolvedAddresses(resolvedAddresses, "sample-service.contoso.com", false);
            var subChannels = policy.SubChannels.Values;

            // Assert
            Assert.Equal(4, subChannels.Count);
            Assert.All(subChannels, subChannel => Assert.Equal("http", subChannel.Address.Scheme));
            Assert.All(subChannels, subChannel => Assert.Equal(80, subChannel.Address.Port));
            Assert.All(subChannels, subChannel => Assert.StartsWith("10.1.5.", subChannel.Address.Host));
        }

        [Fact]
        public void ForGrpcSubChannels_UseRoundRobinPolicySelectChannels_SelectChannelsInRoundRobin()
        {
            // Arrange
            var subChannels = GrpcSubChannelFactory.GetSubChannelsWithoutLoadBalanceTokens();
            var picker = new RoundRobinPolicy.ReadyPicker(subChannels);

            // Act
            // Assert
            for (int i = 0; i < 30; i++)
            {
                var pickResult = picker.GetNextSubChannel(GrpcPickSubchannelArgs.Empty);
                Assert.NotNull(pickResult);
                Assert.NotNull(pickResult!.SubChannel);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Host, pickResult!.SubChannel!.Address.Host);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Port, pickResult.SubChannel.Address.Port);
                Assert.Equal(subChannels[i % subChannels.Count].Address.Scheme, pickResult.SubChannel.Address.Scheme);
                Assert.Equal(Status.DefaultSuccess, pickResult.Status);
            }
        }
    }
}
