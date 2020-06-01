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
    public sealed class GrpcResolverPluginRegistryTests
    {
        [Fact]
        public void ForRegisteredProvidersSearchDnsProvider_UseGrpcResolverPluginRegistry_ReturnDnsProvider()
        {
            // Arrange
            var registry = GrpcResolverPluginRegistry.CreateEmptyRegistry();
            var dnsProvider = GrpcResolverPluginProviderFactory.GetProvider("dns", 5, true);

            // Act
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("http", 5, true));
            registry.Register(dnsProvider);
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("xds", 5, true));
            var provider = registry.GetProvider("dns");

            // Assert
            Assert.Equal(dnsProvider, provider);
        }

        [Fact]
        public void ForRegisteredProvidersSearchDnsProviderWithHighestPriority_UseGrpcResolverPluginRegistry_ReturnDnsProvider()
        {
            // Arrange
            var registry = GrpcResolverPluginRegistry.CreateEmptyRegistry();
            var dnsProviderWithHighestPriority = GrpcResolverPluginProviderFactory.GetProvider("dns", 6, true);

            // Act
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("http", 5, true));
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("xds", 4, true));
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("dns", 5, true));
            registry.Register(dnsProviderWithHighestPriority);
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("dns", 3, true));
            var provider = registry.GetProvider("dns");

            // Assert
            Assert.Equal(dnsProviderWithHighestPriority, provider);
        }

        [Fact]
        public void ForRegisteredProvidersSearchMultipleProvidersWithHighestPriority_UseGrpcResolverPluginRegistry_ReturnProviders()
        {
            // Arrange
            var registry = GrpcResolverPluginRegistry.CreateEmptyRegistry();
            var dnsProviderWithHighestPriority = GrpcResolverPluginProviderFactory.GetProvider("dns", 6, true);
            var httpProviderWithHighestPriority = GrpcResolverPluginProviderFactory.GetProvider("http", 6, true);
            
            // Act
            registry.Register(httpProviderWithHighestPriority);
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("http", 5, true));
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("xds", 4, true));
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("dns", 7, false)); // not available provider
            registry.Register(dnsProviderWithHighestPriority);
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("dns", 3, true));
            var providerForDns = registry.GetProvider("dns");
            var providerForHttp = registry.GetProvider("http");

            // Assert
            Assert.Equal(dnsProviderWithHighestPriority, providerForDns);
            Assert.Equal(httpProviderWithHighestPriority, providerForHttp);
        }

        [Fact]
        public void ForNotAvailableProvidersSearchDnsProvider_UseGrpcResolverPluginRegistry_ReturnNull()
        {
            // Arrange
            var registry = GrpcResolverPluginRegistry.CreateEmptyRegistry();

            // Act
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("http", 5, true));
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("dns", 5, false));
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("dns", 7, false));
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("xds", 5, true));
            var provider = registry.GetProvider("dns");

            // Assert
            Assert.Null(provider);
        }

        [Fact]
        public void ForDeregisteredProviderSearchThisProvider_UseGrpcResolverPluginRegistry_ReturnNull()
        {
            // Arrange
            var registry = GrpcResolverPluginRegistry.CreateEmptyRegistry();
            var dnsProvider = GrpcResolverPluginProviderFactory.GetProvider("dns", 5, true);

            // Act
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("http", 5, true));
            registry.Register(dnsProvider);
            registry.Register(GrpcResolverPluginProviderFactory.GetProvider("xds", 5, true));
            registry.Deregister(dnsProvider);
            var provider = registry.GetProvider("dns");

            // Assert
            Assert.Null(provider);
        }

        [Fact]
        public void ForDeregisteringNotExistingProvider_UseGrpcResolverPluginRegistry_NotThrowError()
        {
            // Arrange
            var registry = GrpcResolverPluginRegistry.CreateEmptyRegistry();

            // Act
            registry.Deregister(GrpcResolverPluginProviderFactory.GetProvider("http", 5, true));

            // Assert
            // This should not throw any exception
        }
    }
}
