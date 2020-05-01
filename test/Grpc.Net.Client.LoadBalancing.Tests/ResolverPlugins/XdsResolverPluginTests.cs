using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
        public async Task ForTarget_UseXdsResolverPlugin_ReturnNoHostsAddresses()
        {
            // Arrange
            var serviceHostName = "my-service.googleapis.com";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsRdsAsync($"{serviceHostName}:80")).Returns(Task.FromResult(GetSampleConfigUpdate()));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            var resolverPlugin = new XdsResolverPlugin();
            resolverPlugin.OverrideXdsClientFactory = xdsClientFactory;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:80"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.Empty(resolutionResult.HostsAddresses);
            xdsClientMock.Verify(x => x.GetLdsRdsAsync($"{serviceHostName}:80"));
        }

        [Fact]
        public async Task ForTarget_UseXdsResolverPlugin_ReturnXdsClientPoolInAttributes()
        {
            // Arrange
            var serviceHostName = "my-service.googleapis.com";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsRdsAsync($"{serviceHostName}:443")).Returns(Task.FromResult(GetSampleConfigUpdate()));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            var resolverPlugin = new XdsResolverPlugin();
            resolverPlugin.OverrideXdsClientFactory = xdsClientFactory;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:443"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.NotNull(resolutionResult.Attributes.Get(XdsAttributesConstants.XdsClientPoolInstance) as XdsClientObjectPool);
            xdsClientMock.Verify(x => x.GetLdsRdsAsync($"{serviceHostName}:443"));
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseXdsResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            var serviceHostName = "my-service.googleapis.com";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsRdsAsync($"{serviceHostName}:80")).Returns(Task.FromResult(GetSampleConfigUpdate()));
            var attributes = new GrpcAttributes(new Dictionary<string, object>() { { GrpcAttributesConstants.DefaultLoadBalancingPolicy, "round_robin" } });
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            var resolverPlugin = new XdsResolverPlugin(attributes);
            resolverPlugin.OverrideXdsClientFactory = xdsClientFactory;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:80"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.Contains("round_robin", serviceConfig.RequestedLoadBalancingPolicies.Last());
        }

        [Fact]
        public async Task ForConfigUpdateReturnedByXdsClient_UseXdsResolverPlugin_GetClusterNameAndOptForCds()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = $"{serviceHostName}:80"; // authority is hostname:port
            var clusterName = "cluster-foo.googleapis.com";

            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsRdsAsync(authority)).Returns(Task.FromResult(GetSampleConfigUpdate()));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            var resolverPlugin = new XdsResolverPlugin(GrpcAttributes.Empty);
            resolverPlugin.OverrideXdsClientFactory = xdsClientFactory;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:80"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.Empty(resolutionResult.HostsAddresses);
            Assert.NotNull(resolutionResult.ServiceConfig.Config);
            Assert.Equal(clusterName, resolutionResult.Attributes.Get(XdsAttributesConstants.CdsClusterName) as string);
            Assert.Contains("cds_experimental", serviceConfig.RequestedLoadBalancingPolicies);
        }

        [Fact]
        public async Task ForWrongConfigUpdateReturnedByXdsClient_UseXdsResolverPlugin_Throw()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = $"{serviceHostName}:80"; // authority is hostname:port

            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsRdsAsync(authority)).Returns(Task.FromResult(GetWrongConfigUpdate()));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            var resolverPlugin = new XdsResolverPlugin(GrpcAttributes.Empty);
            resolverPlugin.OverrideXdsClientFactory = xdsClientFactory;

            // Act
            // Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:80"));
            });
            Assert.StartsWith("Cluster name can not be specified.", exception.Message);
        }

        [Fact]
        public void ForNotReturnedValues_UseXdsResolverPlugin_ReturnResourceNotFound()
        {
            // according to gRFC documentation XdsResolverPlugin should throw error here
            // current implementation create service config with initialized cds policy
            // it is implemented that way because currently used control-plane does not support LDS
            // in the future simply throw an error if not found and verify that in tests
        }

        private static ConfigUpdate GetSampleConfigUpdate()
        {
            var routes = new List<EnvoyProtoData.Route>()
            {
                new EnvoyProtoData.Route(new EnvoyProtoData.RouteMatch("", "sample-path", true, true), new EnvoyProtoData.RouteAction("cluster-foo.googleapis.com", "", new List<EnvoyProtoData.ClusterWeight>()))
            };
            return new ConfigUpdate(routes);
        }

        private static ConfigUpdate GetWrongConfigUpdate()
        {
            return new ConfigUpdate(new List<EnvoyProtoData.Route>());
        }
    }
}
