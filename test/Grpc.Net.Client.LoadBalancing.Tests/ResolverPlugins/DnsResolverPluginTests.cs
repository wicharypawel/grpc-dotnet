using Grpc.Net.Client.LoadBalancing.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class DnsResolverPluginTests
    {
        [Fact]
        public async Task ForTargetWithNonDnsScheme_UseDnsResolverPluginTests_ThrowArgumentException()
        {
            // Arrange
            var resolverPlugin = new DnsResolverPlugin();

            // Act
            // Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("http://sample.host.com"));
            });
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("https://sample.host.com"));
            });
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("unknown://sample.host.com"));
            });
        }

        [Fact]
        public async Task ForTargetAndEmptyDnsResults_UseDnsResolverPlugin_ReturnNoFinidings()
        {
            // Arrange
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin();
            resolverPlugin.OverrideDnsResults = Task.FromResult(Array.Empty<IPAddress>());

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"dns://{serviceHostName}:80"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Empty(resolutionResult.HostsAddresses);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.Count == 1);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForTargetAndARecordsDnsResults_UseDnsResolverPlugin_ReturnServers()
        {
            // Arrange
            var serviceHostName = "my-service";
            var resolverPlugin = new DnsResolverPlugin();
            resolverPlugin.OverrideDnsResults = Task.FromResult(new IPAddress[] { IPAddress.Parse("10.1.5.211"), 
                IPAddress.Parse("10.1.5.212"), IPAddress.Parse("10.1.5.213") });

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"dns://{serviceHostName}:80"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Equal(3, resolutionResult.HostsAddresses.Count);
            Assert.Empty(resolutionResult.HostsAddresses.Where(x => x.IsLoadBalancer));
            Assert.Equal(3, resolutionResult.HostsAddresses.Where(x => !x.IsLoadBalancer).Count());
            Assert.All(resolutionResult.HostsAddresses.Where(x => !x.IsLoadBalancer), x => Assert.Equal(80, x.Port));
            Assert.All(resolutionResult.HostsAddresses.Where(x => !x.IsLoadBalancer), x => Assert.StartsWith("10.1.5.", x.Host));
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.Count == 1);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "pick_first");
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseDnsResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            var serviceHostName = "my-service";
            var attributes = new GrpcAttributes(new Dictionary<string, object>() { { GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin" } });
            var resolverPlugin = new DnsResolverPlugin(attributes);
            resolverPlugin.OverrideDnsResults = Task.FromResult(new IPAddress[] { IPAddress.Parse("10.1.5.211"),
                IPAddress.Parse("10.1.5.212"), IPAddress.Parse("10.1.5.213") });

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"dns://{serviceHostName}:443"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Single(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.Equal("round_robin", serviceConfig.RequestedLoadBalancingPolicies[0]);
        }
    }
}
