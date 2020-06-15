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
using System;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Providers
{
    public sealed class DnsResolverPluginProviderTests
    {
        [Fact]
        public void ForNewProvider_UseDnsResolverPluginProvider_VerifyBasicInformations()
        {
            // Arrange
            var provider = new DnsResolverPluginProvider();

            // Act
            // Assert
            Assert.Equal("dns", provider.Scheme);
            Assert.Equal(5, provider.Priority);
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public void ForCreateResolverPlugin_UseDnsResolverPluginProvider_VerifyNewPolicyType()
        {
            // Arrange
            var provider = new DnsResolverPluginProvider();
            var uri = new UriBuilder("dns://service.googleapis.com").Uri;
            var synchronizationContext = new GrpcSynchronizationContext((ex) => throw ex);
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(GrpcAttributesConstants.ChannelSynchronizationContext, synchronizationContext)
                .Build();

            // Act
            using var plugin = provider.CreateResolverPlugin(uri, attributes);

            // Assert
            Assert.NotNull(plugin);
            Assert.Equal(typeof(DnsResolverPlugin), plugin.GetType());
        }

        [Fact]
        public void ForNotMatchingScheme_UseDnsResolverPluginProvider_ThrowArgumentException()
        {
            // Arrange
            var provider = new DnsResolverPluginProvider();
            var uri = new UriBuilder("notdns://service.googleapis.com").Uri;
            var synchronizationContext = new GrpcSynchronizationContext((ex) => throw ex);
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(GrpcAttributesConstants.ChannelSynchronizationContext, synchronizationContext)
                .Build();

            // Act
            // Assert
            var error = Assert.Throws<ArgumentException>(() =>
            {
                var _ = provider.CreateResolverPlugin(uri, attributes);
            });
            Assert.Equal("target", error.Message);
        }
    }
}
