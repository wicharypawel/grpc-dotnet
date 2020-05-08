using Grpc.Net.Client.LoadBalancing.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Fakes;
using System;
using System.Collections.Generic;
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
            var resolverPlugin = new NoOpResolverPlugin();
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri("https://sample.host.com"), nameResolutionObserver);
            var resolutionResult = await nameResolutionObserver.GetFirstValueOrDefaultAsync();
            Assert.NotNull(resolutionResult);
            var serviceConfig = resolutionResult!.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Single(resolutionResult.HostsAddresses);
            Assert.Equal("sample.host.com", resolutionResult.HostsAddresses[0].Host);
            Assert.Equal(443, resolutionResult.HostsAddresses[0].Port);
            Assert.False(resolutionResult.HostsAddresses[0].IsLoadBalancer);
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.Equal("pick_first", serviceConfig.RequestedLoadBalancingPolicies[0]);
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseNoOpResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            var attributes = new GrpcAttributes(new Dictionary<string, object>() { { GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin" } });
            var resolverPlugin = new NoOpResolverPlugin(attributes);
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            resolverPlugin.Subscribe(new Uri("https://sample.host.com"), nameResolutionObserver);
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
            var resolverPlugin = new NoOpResolverPlugin();
            var nameResolutionObserver = new GrpcNameResolutionObserverFake();

            // Act
            // Assert
            resolverPlugin.Subscribe(new Uri($"{scheme}://sample.host.com"), nameResolutionObserver);
            var error = await nameResolutionObserver.GetFirstErrorOrDefaultAsync();
            Assert.NotNull(error);
            Assert.Contains("require non-default name resolver", error.Value.Detail);
        }
    }
}
