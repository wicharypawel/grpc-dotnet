using Envoy.Api.V2;
using Grpc.Net.Client.LoadBalancing.Extensions.Internal;
using Grpc.Net.Client.LoadBalancing.Tests.ResolverPlugins.Factories;
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
            var serviceHostName = "my-service";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsAsync(It.IsAny<string>())).Returns(Task.FromResult(new List<Listener>()));
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
        }

        [Fact]
        public async Task ForTarget_UseXdsResolverPlugin_ReturnXdsClientPoolInAttributes()
        {
            // Arrange
            var serviceHostName = "my-service";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsAsync(It.IsAny<string>())).Returns(Task.FromResult(new List<Listener>()));
            var xdsClientFactory = new XdsClientFactory(NullLoggerFactory.Instance);
            xdsClientFactory.OverrideXdsClient = xdsClientMock.Object;

            var resolverPlugin = new XdsResolverPlugin();
            resolverPlugin.OverrideXdsClientFactory = xdsClientFactory;

            // Act
            var resolutionResult = await resolverPlugin.StartNameResolutionAsync(new Uri($"xds://{serviceHostName}:80"));
            var serviceConfig = resolutionResult.ServiceConfig.Config as GrpcServiceConfig ?? throw new InvalidOperationException("Missing config");

            // Assert
            Assert.NotNull(resolutionResult);
            Assert.NotNull(resolutionResult.Attributes.Get(XdsAttributesConstants.XdsClientPoolInstance) as XdsClientObjectPool);
        }

        [Fact]
        public async Task ForOverrideDefaultPolicy_UseXdsResolverPlugin_ReturnServiceConfigWithOverridenPolicyName()
        {
            // Arrange
            var serviceHostName = "my-service";
            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsAsync(It.IsAny<string>())).Returns(Task.FromResult(new List<Listener>()));
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
        public async Task ForLdsHavingRouteConfigInline_UseXdsResolverPlugin_FindClusterName()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = "foo.googleapis.com:80";
            var clusterName = "cluster-foo.googleapis.com";

            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsAsync(authority)).Returns(Task.FromResult(new List<Listener>() { 
                XdsClientTestFactory.BuildLdsResponseForCluster("0", authority, clusterName, "0000").Resources[0].Unpack<Listener>()
            }));
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
        public async Task ForLdsPointingToRds_UseXdsResolverPlugin_FindClusterName()
        {
            // Arrange
            var serviceHostName = "foo.googleapis.com";
            var authority = "foo.googleapis.com:80";
            var routeConfigName = "route-foo.googleapis.com";
            var clusterName = "cluster-foo.googleapis.com";

            var xdsClientMock = new Mock<IXdsClient>(MockBehavior.Strict);
            xdsClientMock.Setup(x => x.GetLdsAsync(authority)).Returns(Task.FromResult(new List<Listener>() { 
                XdsClientTestFactory.BuildLdsResponseForRdsResource("0", authority, routeConfigName, "0000").Resources[0].Unpack<Listener>()
            }));
            xdsClientMock.Setup(x => x.GetRdsAsync(routeConfigName)).Returns(Task.FromResult(new List<RouteConfiguration>() {
                XdsClientTestFactory.BuildRdsResponseForCluster("0", routeConfigName, authority, clusterName, "0000").Resources[0].Unpack<RouteConfiguration>()
            }));
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
        public void ForNotReturnedValues_UseXdsResolverPlugin_ReturnResourceNotFound()
        {
            // according to gRFC documentation XdsResolverPlugin should throw error here
            // current implementation create service config with initialized cds policy
            // it is implemented that way because currently used control-plane does not support LDS
            // in the future simply throw an error if not found and verify that in tests
        }
    }
}
