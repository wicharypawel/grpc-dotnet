using Grpc.Net.Client.LoadBalancing.Internal;
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

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri("https://sample.host.com"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

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

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri("https://sample.host.com"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.Equal("round_robin", serviceConfig.RequestedLoadBalancingPolicies[0]);
        }

        [Fact]
        public async Task ForTargetWithDnsScheme_UseNoOpResolverPlugin_ThrowArgumentException()
        {
            // Arrange
            var resolverPlugin = new NoOpResolverPlugin();

            // Act
            // Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("dns://sample.host.com"));
            });
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("xds://sample.host.com"));
            });
        }
    }
}
