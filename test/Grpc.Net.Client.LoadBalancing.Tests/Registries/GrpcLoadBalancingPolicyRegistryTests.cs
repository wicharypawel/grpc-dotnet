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

using Grpc.Net.Client.LoadBalancing.Tests.Registries.Factories;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class GrpcLoadBalancingPolicyRegistryTests
    {
        [Fact]
        public void ForRegisteredProvidersSearchPickFirstProvider_UseGrpcLoadBalancingPolicyRegistry_ReturnProvider()
        {
            // Arrange
            var registry = GrpcLoadBalancingPolicyRegistry.CreateEmptyRegistry();
            var pickFirstProvider = GrpcLoadBalancingPolicyProviderFactory.GetProvider("pick_first", 5, true);

            // Act
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("round_robin", 5, true));
            registry.Register(pickFirstProvider);
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("xds", 5, true));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("grpclb", 5, true));
            var provider = registry.GetProvider("pick_first");

            // Assert
            Assert.Equal(pickFirstProvider, provider);
        }

        [Fact]
        public void ForRegisteredProvidersSearchProviderWithHighestPriority_UseGrpcLoadBalancingPolicyRegistry_ReturnProviderWithHighestPriority()
        {
            // Arrange
            var registry = GrpcLoadBalancingPolicyRegistry.CreateEmptyRegistry();
            var xdsProviderWithHighestPriority = GrpcLoadBalancingPolicyProviderFactory.GetProvider("xds", 6, true);

            // Act
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("pick_first", 5, true));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("round_robin", 5, true));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("xds", 4, true));
            registry.Register(xdsProviderWithHighestPriority);
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("grpclb", 5, true));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("xds", 5, true));
            var provider = registry.GetProvider("xds");

            // Assert
            Assert.Equal(xdsProviderWithHighestPriority, provider);
        }

        [Fact]
        public void ForNotAvailableProvidersSearchXdsProvider_UseGrpcLoadBalancingPolicyRegistry_ReturnNull()
        {
            // Arrange
            var registry = GrpcLoadBalancingPolicyRegistry.CreateEmptyRegistry();

            // Act
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("pick_first", 5, true));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("round_robin", 5, true));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("xds", 4, false));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("xds", 5, false));
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("grpclb", 5, true));
            var provider = registry.GetProvider("xds");

            // Assert
            Assert.Null(provider);
        }

        [Fact]
        public void ForDeregisteredProviderSearchThisProvider_UseGrpcLoadBalancingPolicyRegistry_ReturnNull()
        {
            // Arrange
            var registry = GrpcLoadBalancingPolicyRegistry.CreateEmptyRegistry();
            var xdsProvider = GrpcLoadBalancingPolicyProviderFactory.GetProvider("xds", 5, true);

            // Act
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("pick_first", 5, true));
            registry.Register(xdsProvider);
            registry.Register(GrpcLoadBalancingPolicyProviderFactory.GetProvider("round_robin", 5, true));
            registry.Deregister(xdsProvider);
            var provider = registry.GetProvider("xds");

            // Assert
            Assert.Null(provider);
        }

        [Fact]
        public void ForDeregisteringNotExistingProvider_UseGrpcLoadBalancingPolicyRegistry_NotThrowError()
        {
            // Arrange
            var registry = GrpcLoadBalancingPolicyRegistry.CreateEmptyRegistry();

            // Act
            registry.Deregister(GrpcLoadBalancingPolicyProviderFactory.GetProvider("round_robin", 5, true));

            // Assert
            // This should not throw any exception
        }
    }
}
