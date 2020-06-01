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
using Grpc.Net.Client.LoadBalancing.Tests.Core.Fakes;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class NoOpResolverPluginTests
    {
        [Fact]
        public async Task ForTarget_UseNoOpResolverPlugin_ReturnResolutionResultWithTheSameValue()
        {
            // Arrange
            var executor = new ExecutorFake();
            var resolverPlugin = new NoOpResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri("https://sample.host.com"), nameResolutionObserver);
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Single(resolutionResult.HostsAddresses);
            Assert.Equal("sample.host.com", resolutionResult.HostsAddresses[0].Host);
            Assert.Equal(443, resolutionResult.HostsAddresses[0].Port);
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.Equal("pick_first", serviceConfig.RequestedLoadBalancingPolicies[0]);
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseNoOpResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            var executor = new ExecutorFake();
            var attributes = GrpcAttributes.Builder.NewBuilder()
                .Add(AttributesForResolverFactory.GetAttributes())
                .Add(GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin").Build();
            var resolverPlugin = new NoOpResolverPlugin(attributes, executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri("https://sample.host.com"), nameResolutionObserver);
            executor.DrainSingleAction();
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.Equal("round_robin", serviceConfig.RequestedLoadBalancingPolicies[0]);
        }

        [Theory]
        [InlineData("dns")]
        [InlineData("xds")]
        [InlineData("xds-experimental")]
        public async Task ForTargetWithWellKnownScheme_UseNoOpResolverPlugin_ThrowArgumentException(string scheme)
        {
            // Arrange
            var executor = new ExecutorFake();
            var resolverPlugin = new NoOpResolverPlugin(AttributesForResolverFactory.GetAttributes(), executor);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"{scheme}://sample.host.com"), nameResolutionObserver);
            executor.DrainSingleAction();
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();
            Assert.NotNull(error);
            Assert.Contains("require non-default name resolver", error.Value.Detail);
        }
    }
}
