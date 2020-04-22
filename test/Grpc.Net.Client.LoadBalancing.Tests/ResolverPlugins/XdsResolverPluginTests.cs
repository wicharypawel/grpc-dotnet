using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.Policies.Factories;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins
{
    public sealed class XdsResolverPluginTests
    {
        [Fact]
        public async Task ForTargetWithNonXdsScheme_UseXdsResolverPlugin_ThrowArgumentException()
        {
            // Arrange
            var resolverPlugin = new XdsResolverPlugin();

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("http://sample.host.com"));
            });
            Assert.Contains("require xds://", exception.Message, StringComparison.Ordinal);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("https://sample.host.com"));
            });
            Assert.Contains("require xds://", exception.Message, StringComparison.Ordinal);
            exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri("unknown://sample.host.com"));
            });
            Assert.Contains("require xds://", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ForTarget_UseXdsResolverPlugin_ReturnNoFinidingsAndServiceConfigWithXdsPolicy()
        {
            // Arrange
            XdsBootstrapFileFactory.SetBootstrapFileEnv("XdsBootstrapFile.json");
            var serviceHostName = "my-service";

            var resolverPlugin = new XdsResolverPlugin();

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:80"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.Empty(resolutionResult.HostsAddresses);
            Assert.NotEmpty(serviceConfig.RequestedLoadBalancingPolicies);
            Assert.True(serviceConfig.RequestedLoadBalancingPolicies.First() == "xds");
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseXdsResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            XdsBootstrapFileFactory.SetBootstrapFileEnv("XdsBootstrapFile.json");
            var serviceHostName = "my-service";
            var attributes = new GrpcAttributes(new Dictionary<string, object>() { { GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin" } });
            var resolverPlugin = new XdsResolverPlugin(attributes);

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:443"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Equal(2, serviceConfig.RequestedLoadBalancingPolicies.Count);
            Assert.Contains("round_robin", serviceConfig.RequestedLoadBalancingPolicies[1]);
        }

        [Fact]
        public void Test()
        {
            var authority = "foo.googleapis.com:80";
            var clusterName = "cluster-foo.googleapis.com";
            // Simulate receiving an LDS response that contains cluster resolution directly in-line.
            var ldsResponse = XdsClientTestFactory.BuildLdsResponseForCluster("0", authority, clusterName, "0000");
            // Simulate receiving another LDS response that tells client to do RDS.
            var routeConfigName = "route-foo.googleapis.com";
            var ldsResponseForRds = XdsClientTestFactory.BuildLdsResponseForRdsResource("1", authority, routeConfigName, "0001");
            // Simulate receiving an RDS response that contains the resource "route-foo.googleapis.com"
            var rdsResponse = XdsClientTestFactory.BuildRdsResponseForCluster("0", routeConfigName, authority, "cluster-blade.googleapis.com", "0000");
        }
    }
}
