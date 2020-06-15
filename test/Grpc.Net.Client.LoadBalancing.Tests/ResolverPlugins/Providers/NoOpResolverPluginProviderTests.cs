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
    public sealed class NoOpResolverPluginProviderTests
    {
        [Fact]
        public void ForNewProvider_UseNoOpResolverPluginProvider_VerifyBasicInformations()
        {
            // Arrange
            var provider = new NoOpResolverPluginProvider();

            // Act
            // Assert
            Assert.Equal(string.Empty, provider.Scheme);
            Assert.Equal(5, provider.Priority);
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public void ForCreateResolverPlugin_UseNoOpResolverPluginProvider_VerifyNewPolicyType()
        {
            // Arrange
            var provider = new NoOpResolverPluginProvider();
            var uri = new UriBuilder("localhost:80").Uri;
            var synchronizationContext = new GrpcSynchronizationContext((ex) => throw ex);
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(GrpcAttributesConstants.ChannelSynchronizationContext, synchronizationContext)
                .Build();

            // Act
            using var plugin = provider.CreateResolverPlugin(uri, attributes);

            // Assert
            Assert.NotNull(plugin);
            Assert.Equal(typeof(NoOpResolverPlugin), plugin.GetType());
        }
    }

    public sealed class HttpResolverPluginProviderTests
    {
        [Fact]
        public void ForNewProvider_UseHttpResolverPluginProvider_VerifyBasicInformations()
        {
            // Arrange
            var provider = new HttpResolverPluginProvider();

            // Act
            // Assert
            Assert.Equal("http", provider.Scheme);
            Assert.Equal(5, provider.Priority);
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public void ForCreateResolverPlugin_UseHttpResolverPluginProvider_VerifyNewPolicyType()
        {
            // Arrange
            var provider = new HttpResolverPluginProvider();
            var uri = new UriBuilder("http://service.googleapis.com").Uri;
            var synchronizationContext = new GrpcSynchronizationContext((ex) => throw ex);
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(GrpcAttributesConstants.ChannelSynchronizationContext, synchronizationContext)
                .Build();

            // Act
            using var plugin = provider.CreateResolverPlugin(uri, attributes);

            // Assert
            Assert.NotNull(plugin);
            Assert.Equal(typeof(NoOpResolverPlugin), plugin.GetType());
        }

        [Fact]
        public void ForNotMatchingScheme_UseHttpResolverPluginProvider_ThrowArgumentException()
        {
            // Arrange
            var provider = new HttpResolverPluginProvider();
            var uri = new UriBuilder("nothttp://service.googleapis.com").Uri;
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

    public sealed class HttpsResolverPluginProviderTests
    {
        [Fact]
        public void ForNewProvider_UseHttpsResolverPluginProvider_VerifyBasicInformations()
        {
            // Arrange
            var provider = new HttpsResolverPluginProvider();

            // Act
            // Assert
            Assert.Equal("https", provider.Scheme);
            Assert.Equal(5, provider.Priority);
            Assert.True(provider.IsAvailable);
        }

        [Fact]
        public void ForCreateResolverPlugin_UseHttpsResolverPluginProvider_VerifyNewPolicyType()
        {
            // Arrange
            var provider = new HttpsResolverPluginProvider();
            var uri = new UriBuilder("https://service.googleapis.com").Uri;
            var synchronizationContext = new GrpcSynchronizationContext((ex) => throw ex);
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(GrpcAttributesConstants.ChannelSynchronizationContext, synchronizationContext)
                .Build();

            // Act
            using var plugin = provider.CreateResolverPlugin(uri, attributes);

            // Assert
            Assert.NotNull(plugin);
            Assert.Equal(typeof(NoOpResolverPlugin), plugin.GetType());
        }

        [Fact]
        public void ForNotMatchingScheme_UseHttpsResolverPluginProvider_ThrowArgumentException()
        {
            // Arrange
            var provider = new HttpsResolverPluginProvider();
            var uri = new UriBuilder("nothttps://service.googleapis.com").Uri;
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
